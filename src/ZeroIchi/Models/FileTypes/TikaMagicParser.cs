using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroIchi.Models.FileTypes;

internal static class TikaMagicParser
{
    public static MagicMatch Parse(string? type, string? offset, string? value, string? mask,
        string? minShouldMatch, MagicMatch[] children, out string? unsupportedReason)
    {
        unsupportedReason = null;

        if (minShouldMatch is not null)
        {
            var min = int.Parse(minShouldMatch, CultureInfo.InvariantCulture);
            if (min <= 0 || min > children.Length)
                throw new FormatException($"Invalid minShouldMatch: {minShouldMatch} for {children.Length} clauses");
            return new MagicMatch(MagicMatchKind.MinShouldMatch, 0, 0, [], [], null, false, min, children);
        }

        type ??= "string";
        var kind = type switch
        {
            "string" or "unicodeLE" or "big16" or "big32" or "little16" or "little32" or "host16" or "host32"
                => MagicMatchKind.Bytes,
            "stringignorecase" => MagicMatchKind.BytesIgnoreCase,
            "regex" => MagicMatchKind.Regex,
            _ => throw new FormatException($"Unknown match type: {type}"),
        };
        if (value is null)
            throw new FormatException($"Match value is missing for type: {type}");

        var (start, end) = ParseOffset(offset);
        var patternBytes = DecodeValue(value, type);
        var maskBytes = mask is null ? null : DecodeValue(mask, type);

        var length = Math.Max(patternBytes.Length, maskBytes?.Length ?? 0);
        var pattern = new byte[length];
        var fullMask = new byte[length];
        for (var i = 0; i < length; i++)
        {
            fullMask[i] = maskBytes is not null && i < maskBytes.Length ? maskBytes[i] : (byte)0xFF;
            pattern[i] = i < patternBytes.Length ? (byte)(patternBytes[i] & fullMask[i]) : (byte)0;
        }

        if (kind != MagicMatchKind.Regex)
            return new MagicMatch(kind, start, end, pattern, fullMask, null, false, 0, children);

        var regex = Encoding.UTF8.GetString(pattern);
        bool needsBacktracking;
        try
        {
            needsBacktracking = !CanCompile(regex, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
            if (needsBacktracking) _ = new Regex(regex, RegexOptions.CultureInvariant);
        }
        catch (ArgumentException e)
        {
            unsupportedReason = $"regex '{regex}' is rejected by .NET: {e.Message}";
            return new MagicMatch(MagicMatchKind.Unsupported, start, end, pattern, fullMask, regex, false, 0, children);
        }

        return new MagicMatch(kind, start, end, pattern, fullMask, regex, needsBacktracking, 0, children);
    }

    private static bool CanCompile(string regex, RegexOptions options)
    {
        try
        {
            _ = new Regex(regex, options);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static (int Start, int End) ParseOffset(string? offset)
    {
        if (offset is null) return (0, 0);
        var colon = offset.IndexOf(':');
        var start = int.Parse(colon < 0 ? offset : offset.AsSpan(0, colon), CultureInfo.InvariantCulture);
        var end = colon < 0 ? start : int.Parse(offset.AsSpan(colon + 1), CultureInfo.InvariantCulture);
        if (start < 0 || end < start)
            throw new FormatException($"Invalid offset range: {offset}");
        return (start, end);
    }

    private static byte[] DecodeValue(string value, string type)
    {
        var isHex = value.StartsWith("0x", StringComparison.Ordinal);
        var digits = isHex ? value[2..] : value;
        var radix = isHex ? 16 : 8;

        switch (type)
        {
            case "string":
            case "regex":
            case "unicodeLE":
                return DecodeString(value, type);
            case "stringignorecase":
                return DecodeString(value.ToLowerInvariant(), type);
            case "host16":
            case "little16":
            {
                var i = Convert.ToInt32(digits, radix);
                return [(byte)i, (byte)(i >> 8)];
            }
            case "big16":
            {
                var i = Convert.ToInt32(digits, radix);
                return [(byte)(i >> 8), (byte)i];
            }
            case "host32":
            case "little32":
            {
                var i = Convert.ToInt64(digits, radix);
                return [(byte)i, (byte)(i >> 8), (byte)(i >> 16), (byte)(i >> 24)];
            }
            case "big32":
            {
                var i = Convert.ToInt64(digits, radix);
                return [(byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i];
            }
            default:
                throw new FormatException($"Unknown match type: {type}");
        }
    }

    private static byte[] DecodeString(string value, string type)
    {
        if (value.StartsWith("0x", StringComparison.Ordinal))
        {
            var bytes = new byte[(value.Length - 2) / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(value.Substring(2 + i * 2, 2), 16);
            return bytes;
        }

        var decoded = new List<char>(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\')
            {
                decoded.Add(value[i]);
                continue;
            }

            if (i + 1 >= value.Length)
                throw new FormatException($"Trailing backslash in match value: {value}");

            switch (value[i + 1])
            {
                case '\\':
                    decoded.Add('\\');
                    i++;
                    break;
                case 'x':
                    decoded.Add((char)Convert.ToInt32(value.Substring(i + 2, 2), 16));
                    i += 3;
                    break;
                case 'r':
                    decoded.Add('\r');
                    i++;
                    break;
                case 'n':
                    decoded.Add('\n');
                    i++;
                    break;
                default:
                {
                    var j = i + 1;
                    while (j < i + 4 && j < value.Length && char.IsDigit(value[j]))
                        j++;
                    var octal = j > i + 1 ? Convert.ToInt16(value[(i + 1)..j], 8) : 0;
                    decoded.Add((char)(byte)octal);
                    i = j - 1;
                    break;
                }
            }
        }

        if (type == "unicodeLE")
        {
            var bytes = new byte[decoded.Count * 2];
            for (var i = 0; i < decoded.Count; i++)
            {
                bytes[i * 2] = (byte)decoded[i];
                bytes[i * 2 + 1] = (byte)(decoded[i] >> 8);
            }
            return bytes;
        }

        var result = new byte[decoded.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = (byte)decoded[i];
        return result;
    }
}
