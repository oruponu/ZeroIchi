using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models;

public sealed record DataInspectorEntry(string Label, string Value);

public static class DataInspector
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Utf16Le = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly Encoding Utf16Be = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly Encoding ShiftJis = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    public static List<DataInspectorEntry> Inspect(ByteBuffer buffer, int offset, bool bigEndian)
    {
        var entries = new List<DataInspectorEntry>(16);
        var remaining = (int)(buffer.Length - offset);
        if (remaining <= 0) return entries;

        var count = Math.Min(remaining, 8);
        var bytes = new byte[8];
        buffer.ReadBytes(offset, bytes, 0, count);
        var span = bytes.AsSpan();

        entries.Add(new DataInspectorEntry("Binary", Convert.ToString(bytes[0], 2).PadLeft(8, '0')));

        entries.Add(new DataInspectorEntry("Int8", ((sbyte)bytes[0]).ToString()));
        entries.Add(new DataInspectorEntry("UInt8", bytes[0].ToString()));

        if (count >= 2)
        {
            entries.Add(new DataInspectorEntry("Int16", (bigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(span)
                : BinaryPrimitives.ReadInt16LittleEndian(span)).ToString()));
            entries.Add(new DataInspectorEntry("UInt16", (bigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span)).ToString()));
        }

        if (count >= 4)
        {
            entries.Add(new DataInspectorEntry("Int32", (bigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(span)
                : BinaryPrimitives.ReadInt32LittleEndian(span)).ToString()));
            entries.Add(new DataInspectorEntry("UInt32", (bigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(span)
                : BinaryPrimitives.ReadUInt32LittleEndian(span)).ToString()));
            entries.Add(new DataInspectorEntry("Float", (bigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(span)
                : BinaryPrimitives.ReadSingleLittleEndian(span)).ToString("G")));
        }

        if (count >= 8)
        {
            entries.Add(new DataInspectorEntry("Int64", (bigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(span)
                : BinaryPrimitives.ReadInt64LittleEndian(span)).ToString()));
            entries.Add(new DataInspectorEntry("UInt64", (bigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(span)
                : BinaryPrimitives.ReadUInt64LittleEndian(span)).ToString()));
            entries.Add(new DataInspectorEntry("Double", (bigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(span)
                : BinaryPrimitives.ReadDoubleLittleEndian(span)).ToString("G")));
        }

        entries.Add(new DataInspectorEntry("ASCII", DecodeAscii(bytes[0])));

        var utf8Len = GetUtf8CharLength(bytes[0]);
        entries.Add(new DataInspectorEntry("UTF-8", DecodeFixed(Utf8, span[..count], utf8Len)));

        if (count >= 2)
        {
            var utf16 = bigEndian ? Utf16Be : Utf16Le;
            var codeUnit = bigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span);
            var utf16Len = char.IsHighSurrogate((char)codeUnit) ? 4 : 2;
            entries.Add(new DataInspectorEntry("UTF-16", DecodeFixed(utf16, span[..count], utf16Len)));
        }

        var sjisLen = GetShiftJisCharLength(bytes[0]);
        entries.Add(new DataInspectorEntry("Shift-JIS", DecodeFixed(ShiftJis, span[..count], sjisLen)));

        return entries;
    }

    private static string DecodeAscii(byte b) => b is >= 0x20 and <= 0x7E ? ((char)b).ToString() : "—";

    private static int GetUtf8CharLength(byte lead) => lead switch
    {
        <= 0x7F => 1,
        >= 0xC2 and <= 0xDF => 2,
        >= 0xE0 and <= 0xEF => 3,
        >= 0xF0 and <= 0xF4 => 4,
        _ => -1,
    };

    private static int GetShiftJisCharLength(byte lead) => lead switch
    {
        <= 0x7F => 1,
        >= 0xA1 and <= 0xDF => 1,
        >= 0x81 and <= 0x9F or >= 0xE0 and <= 0xFC => 2,
        _ => -1,
    };

    private static string DecodeFixed(Encoding encoding, ReadOnlySpan<byte> data, int byteLen)
    {
        if (byteLen < 0 || byteLen > data.Length)
            return "—";
        try
        {
            Span<char> chars = stackalloc char[2];
            var charCount = encoding.GetChars(data[..byteLen], chars);
            if (charCount == 0) return "—";
            var ch = chars[0];
            if (char.IsControl(ch))
                return "—";
            return char.IsHighSurrogate(ch) && charCount >= 2
                ? new string(chars[..2])
                : ch.ToString();
        }
        catch
        {
            return "—";
        }
    }
}
