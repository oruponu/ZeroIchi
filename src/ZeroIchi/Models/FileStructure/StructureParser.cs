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
        foreach (var child in children)
        {
            if (child.FieldId is not null && template.Contains($"${{{child.FieldId}}}"))
                template = template.Replace($"${{{child.FieldId}}}", child.Description);
        }
        return template;
    }

    private static FileStructureNode ParseLeafField(
        FieldDefinition field, ByteBuffer buffer, ref long offset, bool bigEndian,
        Dictionary<string, long> siblingValues)
    {
        var size = ResolveSize(field, siblingValues);
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
        };

        offset += actualSize;
        return node;
    }

    private static int ResolveSize(FieldDefinition field, Dictionary<string, long> siblingValues)
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
            return siblingValues.TryGetValue(refId, out var v) ? (int)v : 0;
        }

        return 0;
    }

    private static string ReadNonNumericDescription(string type, ByteBuffer buffer, long offset, int size)
    {
        if (size <= 0) return "";

        switch (type)
        {
            case "ascii":
                return Encoding.ASCII.GetString(buffer.SliceToArray(offset, size));
            case "bytes":
                {
                    var displaySize = Math.Min(size, 16);
                    var bytes = buffer.SliceToArray(offset, displaySize);
                    var hex = new StringBuilder(displaySize * 3);
                    for (var i = 0; i < displaySize; i++)
                    {
                        if (i > 0) hex.Append(' ');
                        hex.Append(bytes[i].ToString("X2"));
                    }
                    if (size > 16) hex.Append(" ...");
                    return hex.ToString();
                }
            default:
                return "";
        }
    }

    private static long? ReadNumericValue(string type, ByteBuffer buffer, long offset, int size, bool bigEndian)
    {
        if (size <= 0) return null;

        switch (type)
        {
            case "uint8":
                return buffer.ReadByte(offset);
            case "int8":
                return (sbyte)buffer.ReadByte(offset);
            case "uint16":
                {
                    var bytes = buffer.SliceToArray(offset, size);
                    return bigEndian
                        ? BinaryPrimitives.ReadUInt16BigEndian(bytes)
                        : BinaryPrimitives.ReadUInt16LittleEndian(bytes);
                }
            case "int16":
                {
                    var bytes = buffer.SliceToArray(offset, size);
                    return bigEndian
                        ? BinaryPrimitives.ReadInt16BigEndian(bytes)
                        : BinaryPrimitives.ReadInt16LittleEndian(bytes);
                }
            case "uint32":
                {
                    var bytes = buffer.SliceToArray(offset, size);
                    return bigEndian
                        ? BinaryPrimitives.ReadUInt32BigEndian(bytes)
                        : BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                }
            case "int32":
                {
                    var bytes = buffer.SliceToArray(offset, size);
                    return bigEndian
                        ? BinaryPrimitives.ReadInt32BigEndian(bytes)
                        : BinaryPrimitives.ReadInt32LittleEndian(bytes);
                }
            case "uint64":
                {
                    var bytes = buffer.SliceToArray(offset, size);
                    return (long)(bigEndian
                        ? BinaryPrimitives.ReadUInt64BigEndian(bytes)
                        : BinaryPrimitives.ReadUInt64LittleEndian(bytes));
                }
            case "int64":
                {
                    var bytes = buffer.SliceToArray(offset, size);
                    return bigEndian
                        ? BinaryPrimitives.ReadInt64BigEndian(bytes)
                        : BinaryPrimitives.ReadInt64LittleEndian(bytes);
                }
            default:
                return null;
        }
    }
}
