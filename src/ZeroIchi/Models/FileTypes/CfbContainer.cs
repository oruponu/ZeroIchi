using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using ZeroIchi.Models.Buffers;
using static ZeroIchi.Models.FileTypes.ContainerDetector;

namespace ZeroIchi.Models.FileTypes;

internal static class CfbContainer
{
    private const int HeaderLength = 512;
    private const int DifatCount = 109;
    private const int EntryLength = 128;
    private const int MaxSectors = 64;
    private const int MaxEntries = 1024;
    private const uint MaxRegularSector = 0xFFFFFFFA;

    private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static readonly byte[] MsiClsid =
        [0x84, 0x10, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    private sealed record Entry(string Name, byte[] Clsid);

    public static string? Detect(ByteBuffer buffer)
    {
        var header = Read(buffer, 0, HeaderLength);
        if (header is null || !header.AsSpan(0, Signature.Length).SequenceEqual(Signature)) return null;

        var entries = ReadDirectory(buffer, header);
        if (entries is null || entries.Count == 0) return null;

        if (entries[0].Clsid.AsSpan().SequenceEqual(MsiClsid)) return "application/x-ms-installer";
        if (entries.Exists(entry => entry.Name.StartsWith("__substg1.0_", StringComparison.Ordinal)
                || entry.Name == "__properties_version1.0"))
        {
            return "application/vnd.ms-outlook";
        }
        if (entries.Exists(entry => entry.Name == "WordDocument")) return "application/msword";
        if (entries.Exists(entry => entry.Name is "Workbook" or "Book")) return "application/vnd.ms-excel";
        if (entries.Exists(entry => entry.Name == "PowerPoint Document")) return "application/vnd.ms-powerpoint";
        return null;
    }

    private static List<Entry>? ReadDirectory(ByteBuffer buffer, byte[] header)
    {
        var shift = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(0x1E));
        if (shift is not (9 or 12)) return null;
        var sectorLength = 1 << shift;
        var fatCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x2C));

        var fatSectors = new Dictionary<uint, byte[]>();
        var visited = new HashSet<uint>();
        var entries = new List<Entry>();
        var sectorsRead = 0;

        byte[]? ReadSector(uint sector)
        {
            if (++sectorsRead > MaxSectors) return null;
            return Read(buffer, ((long)sector + 1) << shift, sectorLength);
        }

        var sector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x30));
        while (sector < MaxRegularSector && visited.Add(sector) && entries.Count < MaxEntries)
        {
            var directory = ReadSector(sector);
            if (directory is null) return null;
            for (var offset = 0;
                 offset + EntryLength <= sectorLength && entries.Count < MaxEntries;
                 offset += EntryLength)
            {
                var entry = directory.AsSpan(offset, EntryLength);
                if (entry[0x42] == 0) continue;
                var nameLength = Math.Min(BinaryPrimitives.ReadUInt16LittleEndian(entry[0x40..]), (ushort)64);
                var name = Encoding.Unicode.GetString(entry[..Math.Max(nameLength - 2, 0)]);
                entries.Add(new Entry(name, entry.Slice(0x50, 16).ToArray()));
            }

            var perFatSector = (uint)(sectorLength / 4);
            var fatIndex = sector / perFatSector;
            if (fatIndex >= DifatCount || fatIndex >= fatCount) break;
            var fatSector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x4C + (int)fatIndex * 4));
            if (!fatSectors.TryGetValue(fatSector, out var fat))
            {
                fat = ReadSector(fatSector);
                if (fat is null) break;
                fatSectors[fatSector] = fat;
            }
            sector = BinaryPrimitives.ReadUInt32LittleEndian(fat.AsSpan((int)(sector % perFatSector) * 4));
        }
        return entries;
    }
}
