using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileStructure;

public static class StructureParser
{
    public static FileStructureNode Parse(FormatDefinition definition, ByteBuffer buffer)
    {
        var offset = 0L;
        var children = ParseFields(definition.Fields, buffer, ref offset, definition.IsBigEndian);

        return new FileStructureNode
        {
            Name = definition.Name,
            Offset = 0,
            Length = (int)Math.Min(buffer.Length, int.MaxValue),
            Children = children,
        };
    }

    private static List<FileStructureNode> ParseFields(
        FieldDefinition[] fields, ByteBuffer buffer, ref long offset, bool bigEndian)
    {
        var nodes = new List<FileStructureNode>();
        var siblingValues = new Dictionary<string, long>();

        foreach (var field in fields)
        {
            if (offset >= buffer.Length) break;

            var fieldBigEndian = field.Endian is not null ? field.Endian == "big" : bigEndian;

            switch (field.Type)
            {
                case "group":
                    nodes.Add(ParseGroup(field, buffer, ref offset, fieldBigEndian));
                    break;
                case "repeat":
                    nodes.Add(ParseRepeat(field, buffer, ref offset, fieldBigEndian));
                    break;
                default:
                    var leaf = ParseLeafField(field, buffer, ref offset, fieldBigEndian, siblingValues);
                    if (leaf.Length > 0)
                        nodes.Add(leaf);
                    break;
            }
        }

        return nodes;
    }

    private static FileStructureNode ParseGroup(
        FieldDefinition field, ByteBuffer buffer, ref long offset, bool bigEndian)
    {
        var startOffset = offset;
        var children = ParseFields(field.Fields ?? [], buffer, ref offset, bigEndian);

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = (int)(offset - startOffset),
            Children = children,
        };
    }

    private static FileStructureNode ParseRepeat(
        FieldDefinition field, ByteBuffer buffer, ref long offset, bool bigEndian)
    {
        var startOffset = offset;
        var children = new List<FileStructureNode>();

        while (offset < buffer.Length)
        {
            var iterationOffset = offset;
            var iterationChildren = ParseFields(field.Fields ?? [], buffer, ref offset, bigEndian);

            if (offset == iterationOffset) break; // 無限ループ防止

            var iterationName = field.NameTemplate is not null
                ? ResolveNameTemplate(field.NameTemplate, iterationChildren)
                : $"{field.Name}[{children.Count}]";

            children.Add(new FileStructureNode
            {
                Name = iterationName,
                FieldId = field.Id,
                Offset = iterationOffset,
                Length = (int)(offset - iterationOffset),
                Children = iterationChildren,
            });
        }

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = (int)(offset - startOffset),
            Children = children,
        };
    }

    private static string ResolveNameTemplate(string template, List<FileStructureNode> children)
    {
        var sb = new StringBuilder(template);
        foreach (var child in children)
        {
            if (child.FieldId is not null)
                sb.Replace($"${{{child.FieldId}}}", child.Description);
        }
        return sb.ToString();
    }

    private static FileStructureNode ParseLeafField(
        FieldDefinition field, ByteBuffer buffer, ref long offset, bool bigEndian,
        Dictionary<string, long> siblingValues)
    {
        var size = ResolveSize(field, siblingValues, buffer.Length, offset);
        if (size < 0) size = 0;

        // 残りのバッファサイズを超えないよう制限
        var available = (int)Math.Min(buffer.Length - offset, int.MaxValue);
        var actualSize = Math.Min(size, available);

        var numericValue = ReadNumericValue(field.Type, buffer, offset, actualSize, bigEndian);
        var description = numericValue.HasValue
            ? numericValue.Value.ToString()
            : ReadNonNumericDescription(field.Type, buffer, offset, actualSize);

        if (numericValue.HasValue)
            siblingValues[field.Id] = numericValue.Value;

        var node = new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = offset,
            Length = actualSize,
            Description = description,
            ValueKind = GetValueKind(field.Type, numericValue.HasValue),
        };

        offset += actualSize;
        return node;
    }

    private static int ResolveSize(
        FieldDefinition field, Dictionary<string, long> siblingValues, long bufferLength, long offset)
    {
        if (field.FixedSize >= 0)
            return field.FixedSize;

        if (field.Size is not { } sizeElement)
            return 0;

        if (sizeElement.ValueKind == JsonValueKind.Number)
            return sizeElement.GetInt32();

        if (sizeElement.ValueKind == JsonValueKind.String)
        {
            var refId = sizeElement.GetString()!;
            if (refId == "rest")
                return (int)Math.Min(bufferLength - offset, int.MaxValue);
            return siblingValues.TryGetValue(refId, out var v) ? (int)v : 0;
        }

        return 0;
    }

    private static string ReadNonNumericDescription(string type, ByteBuffer buffer, long offset, int size)
    {
        if (size <= 0) return "";

        return type switch
        {
            "ascii" => Encoding.ASCII.GetString(buffer.SliceToArray(offset, size)),
            "bytes" => FormatBytesHex(buffer, offset, size),
            _ => "",
        };
    }

    private static string FormatBytesHex(ByteBuffer buffer, long offset, int size)
    {
        var displaySize = Math.Min(size, 16);
        var hex = Convert.ToHexString(buffer.SliceToArray(offset, displaySize));
        var spaced = string.Create(displaySize * 3 - 1, hex, (span, h) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = (i + 1) % 3 == 0 ? ' ' : h[i - i / 3];
        });
        return size > 16 ? $"{spaced} ..." : spaced;
    }

    private static ValueKind GetValueKind(string type, bool isNumeric)
    {
        if (isNumeric) return ValueKind.Numeric;
        return type switch
        {
            "ascii" => ValueKind.Ascii,
            "bytes" => ValueKind.Bytes,
            _ => ValueKind.None,
        };
    }

    private static long? ReadNumericValue(string type, ByteBuffer buffer, long offset, int size, bool bigEndian)
    {
        if (size is <= 0 or > 8) return null;

        var bytes = buffer.SliceToArray(offset, size);
        return type switch
        {
            "uint8" => bytes[0],
            "int8" => (sbyte)bytes[0],
            "uint16" => bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            "int16" => bigEndian ? BinaryPrimitives.ReadInt16BigEndian(bytes) : BinaryPrimitives.ReadInt16LittleEndian(bytes),
            "uint32" => bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            "int32" => bigEndian ? BinaryPrimitives.ReadInt32BigEndian(bytes) : BinaryPrimitives.ReadInt32LittleEndian(bytes),
            "uint64" => (long)(bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes)),
            "int64" => bigEndian ? BinaryPrimitives.ReadInt64BigEndian(bytes) : BinaryPrimitives.ReadInt64LittleEndian(bytes),
            _ => null,
        };
    }
}
