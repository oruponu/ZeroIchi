using System;
using System.Globalization;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal static class KsyDescriptions
{
    public const int StringLimit = 64;
    public const int BytesLimit = 16;

    public static (string Text, ValueKind Kind) Describe(KsValue value) => value.Kind switch
    {
        KsValueKind.Int => (value.AsInt.ToString(CultureInfo.InvariantCulture), ValueKind.Numeric),
        KsValueKind.Float => (value.AsFloat.ToString("G", CultureInfo.InvariantCulture), ValueKind.Numeric),
        KsValueKind.Bool => (value.AsBool ? "true" : "false", ValueKind.Numeric),
        KsValueKind.Enum => (EnumText(value), ValueKind.Numeric),
        KsValueKind.String => (Truncate(value.AsString), ValueKind.Ascii),
        KsValueKind.Bytes => (Bytes(value.AsBytes), ValueKind.Bytes),
        _ => ("", ValueKind.None),
    };

    public static string EnumText(KsValue value)
    {
        var number = value.EnumValue.ToString(CultureInfo.InvariantCulture);
        return value.EnumType.GetName(value.EnumValue) is { } name ? $"{number} ({name})" : number;
    }

    public static string Truncate(string text)
    {
        var count = 0;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (count == StringLimit) return $"{text[..index]} ...";
            index += rune.Utf16SequenceLength;
            count++;
        }
        return text;
    }

    public static string Bytes(IKsBytes bytes)
    {
        var prefix = bytes is KsStreamBytes stream
            ? stream.Prefix(BytesLimit)
            : bytes.ToArray()[..(int)Math.Min(BytesLimit, bytes.Length)];
        var text = Hex(prefix);
        return bytes.Length > BytesLimit ? $"{text} ..." : text;
    }

    public static string Hex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
