using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal static partial class KsySummary
{
    private const int MaxDepth = 4;
    private const int MaxItems = 16;
    private const int MaxLength = 256;
    private const string Omitted = "…";

    [GeneratedRegex(@"\{(.*?)\}")]
    private static partial Regex Placeholder();

    private readonly record struct Format(bool Dec, bool Str, string Separator);

    public static string Describe(KsObject obj)
    {
        var text = Summarize(obj, 0, new HashSet<KsObject>(ReferenceEqualityComparer.Instance));
        return text.Length > MaxLength ? $"{text[..MaxLength]} ..." : text;
    }

    private static string Summarize(KsObject obj, int depth, HashSet<KsObject> visiting)
    {
        visiting.Add(obj);
        try
        {
            if (obj.Type.WebideRepresentation is { } template)
                return Placeholder().Replace(template, m => Expand(obj, m.Groups[1].Value, depth, visiting));

            if (obj.Type.ToStringExpr is { } expr)
            {
                try
                {
                    var value = KsEvaluator.Evaluate(expr, obj);
                    return Render(value, new Format(false, false, ", "), depth, visiting);
                }
                catch (Exception e) when (e is KsExpressionException or KsyParseException)
                {
                    return "";
                }
            }
            return "";
        }
        finally
        {
            visiting.Remove(obj);
        }
    }

    private static string Expand(KsObject obj, string placeholder, int depth, HashSet<KsObject> visiting)
    {
        var colon = placeholder.IndexOf(':');
        var path = colon < 0 ? placeholder : placeholder[..colon];
        var format = ParseFormat(colon < 0 ? "" : placeholder[(colon + 1)..]);

        var value = KsValue.FromStruct(obj);
        foreach (var part in path.Split('.'))
        {
            if (value.Kind != KsValueKind.Struct || value.AsStruct is not KsObject current) return "";
            try
            {
                if (!current.TryResolveName(part, out value)) return "";
            }
            catch (Exception e) when (e is KsExpressionException or KsyParseException)
            {
                return "";
            }
        }
        return Render(value, format, depth, visiting);
    }

    private static Format ParseFormat(string text)
    {
        var format = new Format(false, false, ", ");
        foreach (var option in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = option.IndexOf('=');
            var key = equals < 0 ? option : option[..equals];
            format = key switch
            {
                "dec" => format with { Dec = true },
                "str" => format with { Str = true },
                "sep" when equals >= 0 => format with { Separator = option[(equals + 1)..] },
                _ => format,
            };
        }
        return format;
    }

    private static string Render(KsValue value, Format format, int depth, HashSet<KsObject> visiting)
    {
        switch (value.Kind)
        {
            case KsValueKind.Int:
                return format.Dec ? value.AsInt.ToString(CultureInfo.InvariantCulture) : Hex(value.AsInt);
            case KsValueKind.Enum:
                return value.EnumType.GetName(value.EnumValue) ?? Hex(value.EnumValue);
            case KsValueKind.Bytes:
                return format.Str ? Ascii(value.AsBytes) : KsyDescriptions.Bytes(value.AsBytes);
            case KsValueKind.Struct:
                return value.AsStruct is KsObject child && depth < MaxDepth && !visiting.Contains(child)
                    ? Summarize(child, depth + 1, visiting)
                    : Omitted;
            case KsValueKind.Array:
            {
                var items = value.AsArray;
                var sb = new StringBuilder();
                for (var i = 0; i < items.Count && i < MaxItems; i++)
                {
                    if (i > 0) sb.Append(format.Separator);
                    sb.Append(Render(items[i], format, depth, visiting));
                }
                if (items.Count > MaxItems) sb.Append(format.Separator).Append(Omitted);
                return sb.ToString();
            }
            default:
                return KsyDescriptions.Describe(value).Text;
        }
    }

    private static string Hex(Int128 value)
    {
        var magnitude = value < 0 ? (UInt128)(-(value + 1)) + 1 : (UInt128)value;
        var digits = magnitude.ToString("X", CultureInfo.InvariantCulture);
        return value < 0 ? $"-0x{digits}" : $"0x{digits}";
    }

    private static string Ascii(IKsBytes bytes)
    {
        var prefix = bytes is KsStreamBytes stream
            ? stream.Prefix(KsyDescriptions.BytesLimit)
            : bytes.ToArray()[..(int)Math.Min(KsyDescriptions.BytesLimit, bytes.Length)];
        var sb = new StringBuilder("\"");
        foreach (var b in prefix) sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
        sb.Append('"');
        if (bytes.Length > KsyDescriptions.BytesLimit) sb.Append(" ...");
        return sb.ToString();
    }
}
