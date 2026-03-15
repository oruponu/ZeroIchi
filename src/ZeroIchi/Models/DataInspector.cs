using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models;

public sealed record DataInspectorEntry(string Label, string Value);

public static class DataInspector
{
    public static List<DataInspectorEntry> Inspect(ByteBuffer buffer, int offset, bool bigEndian)
    {
        var entries = new List<DataInspectorEntry>(12);
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

        return entries;
    }
}
