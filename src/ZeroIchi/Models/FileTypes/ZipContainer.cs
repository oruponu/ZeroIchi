using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileTypes;

internal static class ZipContainer
{
    private const uint LocalHeaderSignature = 0x04034B50;
    private const uint CentralHeaderSignature = 0x02014B50;
    private const uint EndRecordSignature = 0x06054B50;
    private const int LocalHeaderLength = 30;
    private const int CentralHeaderLength = 46;
    private const int EndRecordLength = 22;
    private const int EndRecordSearchLength = EndRecordLength + ushort.MaxValue;
    private const int MaxCentralDirectoryLength = 1024 * 1024;
    private const int MaxEntries = 4096;
    private const int MaxCompressedLength = 64 * 1024;
    private const int MaxExpandedLength = 256 * 1024;
    private const int MaxMimetypeLength = 128;

    private static readonly (string Marker, string MimeType)[] OoxmlTypes =
    [
        ("application/vnd.ms-excel.sheet.macroEnabled.main+xml", "application/vnd.ms-excel.sheet.macroenabled.12"),
        ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        ("application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        ("application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
    ];

    private sealed record Entry(ushort Method, uint CompressedLength, uint LocalHeaderOffset);

    public static string? Detect(ByteBuffer buffer, Func<string, bool> isKnown)
    {
        var header = Read(buffer, 0, LocalHeaderLength);
        if (header is null || BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature) return null;

        try
        {
            var mimetype = ReadMimetype(buffer, header);
            if (mimetype is not null && isKnown(mimetype)) return mimetype;

            var entries = ReadCentralDirectory(buffer);
            if (entries is null) return null;
            if (entries.ContainsKey("AndroidManifest.xml")) return "application/vnd.android.package-archive";
            if (entries.ContainsKey("AppxManifest.xml")) return "application/vnd.ms-appx";
            if (entries.TryGetValue("[Content_Types].xml", out var contentTypes)) return DetectOoxml(buffer, contentTypes);
            if (entries.ContainsKey("META-INF/MANIFEST.MF")) return "application/java-archive";
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static string? ReadMimetype(ByteBuffer buffer, byte[] header)
    {
        var method = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(18));
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
        if (method != 0 || nameLength != "mimetype".Length) return null;

        var name = Read(buffer, LocalHeaderLength, nameLength);
        if (name is null || !name.AsSpan().SequenceEqual("mimetype"u8)) return null;

        var body = Read(buffer, LocalHeaderLength + nameLength + extraLength, (int)Math.Min(length, MaxMimetypeLength));
        return body is null ? null : Encoding.ASCII.GetString(body).Trim();
    }

    private static Dictionary<string, Entry>? ReadCentralDirectory(ByteBuffer buffer)
    {
        var tailLength = (int)Math.Min(buffer.Length, EndRecordSearchLength);
        var tailStart = buffer.Length - tailLength;
        var tail = buffer.SliceToArray(tailStart, tailLength);
        var end = -1;
        for (var i = tailLength - EndRecordLength; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) != EndRecordSignature) continue;
            end = i;
            break;
        }
        if (end < 0) return null;

        var count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end + 10));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 12));
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 16));
        if (count == ushort.MaxValue || length == uint.MaxValue || offset == uint.MaxValue) return null;
        if (count > MaxEntries || length > MaxCentralDirectoryLength) return null;
        if (offset + (long)length > tailStart + end) return null;

        var directory = Read(buffer, offset, (int)length);
        if (directory is null) return null;

        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var position = 0;
        for (var i = 0; i < count; i++)
        {
            if (position + CentralHeaderLength > directory.Length) return null;
            var record = directory.AsSpan(position);
            if (BinaryPrimitives.ReadUInt32LittleEndian(record) != CentralHeaderSignature) return null;
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(record[28..]);
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(record[30..]);
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(record[32..]);
            if (position + CentralHeaderLength + nameLength > directory.Length) return null;

            var name = Encoding.UTF8.GetString(record.Slice(CentralHeaderLength, nameLength));
            entries.TryAdd(name, new Entry(
                BinaryPrimitives.ReadUInt16LittleEndian(record[10..]),
                BinaryPrimitives.ReadUInt32LittleEndian(record[20..]),
                BinaryPrimitives.ReadUInt32LittleEndian(record[42..])));
            position += CentralHeaderLength + nameLength + extraLength + commentLength;
        }
        return entries;
    }

    private static string? DetectOoxml(ByteBuffer buffer, Entry entry)
    {
        var content = Expand(buffer, entry);
        if (content is null) return null;

        var text = Encoding.Latin1.GetString(content);
        foreach (var (marker, mimeType) in OoxmlTypes)
        {
            if (text.Contains(marker, StringComparison.Ordinal)) return mimeType;
        }
        return "application/x-tika-ooxml";
    }

    private static byte[]? Expand(ByteBuffer buffer, Entry entry)
    {
        if (entry.CompressedLength > MaxCompressedLength) return null;
        if (entry.Method is not (0 or 8)) return null;

        var header = Read(buffer, entry.LocalHeaderOffset, LocalHeaderLength);
        if (header is null || BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature) return null;
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
        var compressed = Read(buffer, entry.LocalHeaderOffset + LocalHeaderLength + nameLength + extraLength,
            (int)entry.CompressedLength);
        if (compressed is null) return null;
        if (entry.Method == 0) return compressed;

        using var deflate = new DeflateStream(new MemoryStream(compressed), CompressionMode.Decompress);
        var expanded = new byte[MaxExpandedLength + 1];
        var total = 0;
        int read;
        while (total < expanded.Length && (read = deflate.Read(expanded, total, expanded.Length - total)) > 0)
        {
            total += read;
        }
        return total > MaxExpandedLength ? null : expanded[..total];
    }

    private static byte[]? Read(ByteBuffer buffer, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > buffer.Length) return null;
        return buffer.SliceToArray(offset, count);
    }
}
