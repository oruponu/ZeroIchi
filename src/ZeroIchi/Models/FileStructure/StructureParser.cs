using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileStructure;

public static class StructureParser
{
    private const int DefaultNodeBudget = 100_000;

    public static FileStructureNode Parse(FormatDefinition definition, ByteBuffer buffer) =>
        Parse(definition, buffer, DefaultNodeBudget);

    internal static FileStructureNode Parse(FormatDefinition definition, ByteBuffer buffer, int nodeBudget)
    {
        var context = new ParseContext(buffer, nodeBudget);
        var offset = 0L;
        var children = ParseFields(definition.Fields, context, ref offset, definition.IsBigEndian);

        if (context.IsTruncated)
            children.Add(CreateTruncationNode(offset, buffer.Length, nodeBudget));

        return new FileStructureNode
        {
            Name = definition.Name,
            Offset = 0,
            Length = buffer.Length,
            Children = children,
        };
    }

    private sealed class ParseContext(ByteBuffer buffer, int nodeBudget)
    {
        private int _remainingNodes = nodeBudget;

        public ByteBuffer Buffer { get; } = buffer;
        public bool IsTruncated { get; private set; }

        public void Add(List<FileStructureNode> nodes, FileStructureNode node)
        {
            nodes.Add(node);
            _remainingNodes--;
        }

        public bool StopIfExhausted()
        {
            if (_remainingNodes > 0) return false;
            IsTruncated = true;
            return true;
        }
    }

    private static FileStructureNode CreateTruncationNode(long offset, long bufferLength, int nodeBudget) => new()
    {
        Name = "",
        Offset = offset,
        Length = bufferLength - offset,
        IsUnparsed = true,
        Error = FileStructureError.Limit(FileStructureErrorKind.NodeBudget, nodeBudget, "Node budget exceeded"),
    };

    private static List<FileStructureNode> ParseFields(
        FieldDefinition[] fields, ParseContext context, ref long offset, bool bigEndian,
        Dictionary<string, long>? persistentValues = null, long limit = long.MaxValue)
    {
        var buffer = context.Buffer;
        var nodes = new List<FileStructureNode>();
        var siblingValues = new Dictionary<string, long>();
        var end = Math.Min(limit, buffer.Length);

        foreach (var field in fields)
        {
            if (offset >= end && field.Type is not "match") break;
            if (context.StopIfExhausted()) break;

            if (offset < end && field.PeekMin is { } peekMin)
            {
                var peekByte = buffer.ReadByte(offset);
                if (peekByte < peekMin)
                {
                    if (persistentValues is not null && persistentValues.TryGetValue(field.Id, out var pv))
                        siblingValues[field.Id] = pv;
                    continue;
                }
            }

            var fieldBigEndian = field.Endian is not null ? field.Endian == "big" : bigEndian;

            switch (field.Type)
            {
                case "group":
                    context.Add(nodes, ParseGroup(field, context, ref offset, fieldBigEndian, siblingValues, end));
                    break;
                case "repeat":
                    context.Add(nodes, ParseRepeat(field, context, ref offset, fieldBigEndian, siblingValues, end));
                    break;
                case "match":
                    if (ParseMatch(field, context, ref offset, fieldBigEndian, siblingValues, end) is { } matchNode)
                        context.Add(nodes, matchNode);
                    break;
                case "scan":
                    var scanNode = ParseScanField(field, buffer, ref offset, end);
                    if (scanNode.Length > 0)
                        context.Add(nodes, scanNode);
                    break;
                case "vlq":
                    var vlqNode = ParseVlqField(field, buffer, ref offset, siblingValues, end);
                    if (vlqNode.Length > 0)
                        context.Add(nodes, vlqNode);
                    break;
                case "synchsafe":
                    var synchsafeNode = ParseSynchsafeField(field, buffer, ref offset, siblingValues, end);
                    if (synchsafeNode.Length > 0)
                        context.Add(nodes, synchsafeNode);
                    break;
                default:
                    var leaf = ParseLeafField(field, buffer, ref offset, fieldBigEndian, siblingValues, end);
                    if (leaf.Length > 0)
                        context.Add(nodes, leaf);
                    break;
            }

            if (field.PeekMin is not null && persistentValues is not null
                && siblingValues.TryGetValue(field.Id, out var newValue))
            {
                persistentValues[field.Id] = newValue;
            }
        }

        return nodes;
    }

    private static FileStructureNode ParseGroup(
        FieldDefinition field, ParseContext context, ref long offset, bool bigEndian,
        Dictionary<string, long> siblingValues, long limit)
    {
        var startOffset = offset;

        var groupLimit = limit;
        if (field.Size is not null)
        {
            var size = Math.Max(ResolveSize(field, siblingValues, limit, offset), 0);
            groupLimit = offset + Math.Min(size, limit - offset);
        }

        var children = ParseFields(field.Fields ?? [], context, ref offset, bigEndian, null, groupLimit);

        if (field.Size is not null && !context.IsTruncated)
            offset = groupLimit;

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = offset - startOffset,
            Children = children,
        };
    }

    private static FileStructureNode ParseRepeat(
        FieldDefinition field, ParseContext context, ref long offset, bool bigEndian,
        Dictionary<string, long> siblingValues, long limit)
    {
        var buffer = context.Buffer;
        var startOffset = offset;
        var children = new List<FileStructureNode>();

        var end = Math.Min(limit, buffer.Length);
        if (field.Size is not null)
        {
            var size = Math.Max(ResolveSize(field, siblingValues, end, offset), 0);
            end = offset + Math.Min(size, end - offset);
        }

        var persistentValues = new Dictionary<string, long>();
        var repeatFields = field.Fields ?? [];

        while (offset < end)
        {
            if (context.StopIfExhausted()) break;

            if (ShouldTerminateRepeat(field.Until, buffer, offset))
            {
                if (field.Size is not null) offset = end; // Consume the rest as implicit padding
                break;
            }

            var iterationOffset = offset;
            var iterationChildren = ParseFields(
                repeatFields, context, ref offset, bigEndian, persistentValues, end);

            if (offset == iterationOffset) break; // Prevent an infinite loop

            var iterationName = ResolveIterationName(field, iterationChildren, children.Count);

            context.Add(children, new FileStructureNode
            {
                Name = iterationName,
                FieldId = field.Id,
                Offset = iterationOffset,
                Length = offset - iterationOffset,
                Children = iterationChildren,
            });
        }

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = offset - startOffset,
            Children = children,
        };
    }

    private static FileStructureNode? ParseMatch(
        FieldDefinition field, ParseContext context, ref long offset, bool bigEndian,
        Dictionary<string, long> siblingValues, long limit)
    {
        if (field is not { On: { } on, Cases: { } cases }
            || !siblingValues.TryGetValue(on, out var matchValue))
        {
            return null;
        }

        foreach (var c in cases)
        {
            if (c.Range is not { Length: >= 2 } || matchValue < c.Range[0] || matchValue > c.Range[1])
                continue;

            var startOffset = offset;
            var children = ParseFields(c.Fields ?? [], context, ref offset, bigEndian, null, limit);

            return new FileStructureNode
            {
                Name = c.Name,
                FieldId = field.Id,
                Offset = startOffset,
                Length = offset - startOffset,
                Description = c.Name,
                Children = children,
            };
        }

        return null;
    }

    private static FileStructureNode ParseScanField(
        FieldDefinition field, ByteBuffer buffer, ref long offset, long limit)
    {
        var startOffset = offset;

        while (offset < limit)
        {
            if (buffer.ReadByte(offset) != 0xFF)
            {
                offset++;
                continue;
            }

            var runEnd = offset;
            while (runEnd < limit && buffer.ReadByte(runEnd) == 0xFF)
                runEnd++;

            if (runEnd >= limit)
            {
                offset = limit;
                break;
            }

            if (!IsMarkerCode(buffer.ReadByte(runEnd)))
            {
                offset = runEnd + 1;
                continue;
            }

            offset = runEnd - 1;
            break;
        }

        var length = offset - startOffset;

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = length,
            Description = length > 0 ? FormatBytesHex(buffer, startOffset, length) : "",
            ValueKind = ValueKind.Bytes,
        };
    }

    private static bool IsMarkerCode(byte code) => code is 0x01 or >= 0xC0 and <= 0xFE;

    private static FileStructureNode ParseVlqField(
        FieldDefinition field, ByteBuffer buffer, ref long offset,
        Dictionary<string, long> siblingValues, long limit)
    {
        var startOffset = offset;
        var value = 0L;

        for (var i = 0; i < 4 && offset < limit; i++)
        {
            var b = buffer.ReadByte(offset++);
            value = (value << 7) | (b & 0x7FL);
            if ((b & 0x80) == 0) break;
        }

        siblingValues[field.Id] = value;

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = offset - startOffset,
            Description = value.ToString(),
            ValueKind = ValueKind.Numeric,
        };
    }

    private static bool ShouldTerminateRepeat(string? until, ByteBuffer buffer, long offset)
    {
        if (until is null || offset >= buffer.Length) return false;
        if (!until.StartsWith("peek:", StringComparison.OrdinalIgnoreCase)) return false;

        var literal = until.AsSpan(5);
        if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) literal = literal[2..];

        if (!byte.TryParse(literal, System.Globalization.NumberStyles.HexNumber, null, out var expected))
            return false;

        return buffer.ReadByte(offset) == expected;
    }

    private static FileStructureNode ParseSynchsafeField(
        FieldDefinition field, ByteBuffer buffer, ref long offset,
        Dictionary<string, long> siblingValues, long limit)
    {
        var startOffset = offset;
        var value = 0L;
        var bytesRead = 0;

        for (var i = 0; i < 4 && offset < limit; i++)
        {
            var b = buffer.ReadByte(offset++);
            value = (value << 7) | (b & 0x7FL);
            bytesRead++;
        }

        siblingValues[field.Id] = value;

        return new FileStructureNode
        {
            Name = field.Name,
            FieldId = field.Id,
            Offset = startOffset,
            Length = bytesRead,
            Description = value.ToString(),
            ValueKind = ValueKind.Numeric,
        };
    }

    private static string ResolveIterationName(
        FieldDefinition field, List<FileStructureNode> children, int index)
    {
        var fallback = $"{field.Name}[{index}]";
        if (field.NameTemplate is null) return fallback;

        var resolved = ResolveNameTemplate(field.NameTemplate, children);
        return resolved.Contains("${") ? fallback : resolved;
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
        Dictionary<string, long> siblingValues, long limit)
    {
        var size = ResolveSize(field, siblingValues, limit, offset);
        if (size < 0) size = 0;

        var available = limit - offset;
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

    private static long ResolveSize(
        FieldDefinition field, Dictionary<string, long> siblingValues, long limit, long offset)
    {
        if (field.FixedSize >= 0)
            return field.FixedSize;

        if (field.Size is not { } sizeElement)
            return 0;

        if (sizeElement.ValueKind == JsonValueKind.Number)
            return sizeElement.GetInt64();

        if (sizeElement.ValueKind == JsonValueKind.String)
        {
            var expression = sizeElement.GetString()!;
            var (refId, adjustment) = SplitSizeExpression(expression);

            long baseValue;
            if (refId == "rest")
                baseValue = limit - offset;
            else if (!siblingValues.TryGetValue(refId, out baseValue))
                return 0;

            var clampedBase = Math.Max(baseValue, 0);
            if (adjustment > long.MaxValue - clampedBase) return long.MaxValue;
            return Math.Max(clampedBase + adjustment, 0);
        }

        return 0;
    }

    private static (string RefId, long Adjustment) SplitSizeExpression(string expression)
    {
        var signIndex = expression.LastIndexOfAny(['+', '-']);
        if (signIndex <= 0 || !long.TryParse(expression.AsSpan(signIndex + 1), out var magnitude))
            return (expression, 0);

        return (expression[..signIndex].TrimEnd(), expression[signIndex] == '-' ? -magnitude : magnitude);
    }

    private static readonly Encoding ShiftJis =
        Encoding.GetEncoding(932);

    private static string ReadNonNumericDescription(string type, ByteBuffer buffer, long offset, long size)
    {
        if (size <= 0) return "";

        return type switch
        {
            "text" => DecodeText(buffer.SliceToArray(offset, (int)Math.Min(size, int.MaxValue))),
            "bytes" => FormatBytesHex(buffer, offset, size),
            _ => "",
        };
    }

    private static string DecodeText(byte[] bytes)
    {
        if (System.Text.Unicode.Utf8.IsValid(bytes))
            return Encoding.UTF8.GetString(bytes);

        return ShiftJis.GetString(bytes);
    }

    private static string FormatBytesHex(ByteBuffer buffer, long offset, long size)
    {
        var displaySize = (int)Math.Min(size, 16);
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
            "text" => ValueKind.Ascii,
            "bytes" => ValueKind.Bytes,
            _ => ValueKind.None,
        };
    }

    private static long? ReadNumericValue(string type, ByteBuffer buffer, long offset, long size, bool bigEndian)
    {
        var required = type switch
        {
            "uint8" or "int8" => 1,
            "uint16" or "int16" => 2,
            "uint32" or "int32" => 4,
            "uint64" or "int64" => 8,
            _ => 0,
        };

        if (required == 0 || size < required) return null;

        var bytes = buffer.SliceToArray(offset, required);
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
