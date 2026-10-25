using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

internal static class KsMethods
{
    public static KsValue Call(KsExpr node, KsValue target, string name, KsValue[] args)
    {
        if (target.Kind == KsValueKind.Null)
            throw KsEvaluator.Error(node, "Value is null");

        return (target.Kind, name, args.Length) switch
        {
            (KsValueKind.Int, "to_s", 0) => KsValue.FromString(target.AsInt.ToString(CultureInfo.InvariantCulture)),
            (KsValueKind.Float, "to_i", 0) => KsValue.FromInt(FloatToInt(node, target.AsFloat)),
            (KsValueKind.Bool, "to_i", 0) => KsValue.FromInt(target.AsBool ? 1 : 0),
            (KsValueKind.Enum, "to_i", 0) => KsValue.FromInt(target.EnumValue),

            (KsValueKind.String, "length", 0) => KsValue.FromInt(Runes(target.AsString).Length),
            (KsValueKind.String, "reverse", 0) => KsValue.FromString(JoinRunes(Enumerable.Reverse(Runes(target.AsString)))),
            (KsValueKind.String, "to_i", 0) => KsValue.FromInt(ParseInteger(node, target.AsString, 10)),
            (KsValueKind.String, "to_i", 1) => KsValue.FromInt(ParseInteger(node, target.AsString, Radix(node, args[0]))),
            (KsValueKind.String, "to_b", 1) => KsValue.FromBytes(GetEncoding(node, args[0]).GetBytes(target.AsString)),
            (KsValueKind.String, "substring", 2) => KsValue.FromString(Substring(node, target.AsString, args[0], args[1])),

            (KsValueKind.Bytes, "length" or "size", 0) => KsValue.FromInt(target.AsBytes.Length),
            (KsValueKind.Bytes, "first", 0) => KsValue.FromInt(NonEmpty(node, target.AsBytes.ToArray())[0]),
            (KsValueKind.Bytes, "last", 0) => KsValue.FromInt(NonEmpty(node, target.AsBytes.ToArray())[^1]),
            (KsValueKind.Bytes, "min", 0) => KsValue.FromInt(NonEmpty(node, target.AsBytes.ToArray()).Min()),
            (KsValueKind.Bytes, "max", 0) => KsValue.FromInt(NonEmpty(node, target.AsBytes.ToArray()).Max()),
            (KsValueKind.Bytes, "to_s", 1) => KsValue.FromString(GetEncoding(node, args[0]).GetString(target.AsBytes.ToArray())),
            (KsValueKind.Bytes, "index_of", 1) => KsValue.FromInt(IndexOf(node, target.AsBytes.ToArray(), args[0])),

            (KsValueKind.Array, "first", 0) => NonEmpty(node, target.AsArray)[0],
            (KsValueKind.Array, "last", 0) => Last(node, target.AsArray),
            (KsValueKind.Array, "size", 0) => KsValue.FromInt(target.AsArray.Count),
            (KsValueKind.Array, "min", 0) => Extreme(node, target.AsArray, -1),
            (KsValueKind.Array, "max", 0) => Extreme(node, target.AsArray, 1),

            (KsValueKind.Stream, "eof", 0) => KsValue.FromBool(target.AsStream.IsEof),
            (KsValueKind.Stream, "size", 0) => KsValue.FromInt(target.AsStream.Size),
            (KsValueKind.Stream, "pos", 0) => KsValue.FromInt(target.AsStream.Position),

            _ => throw KsEvaluator.Error(node,
                $"{KsValue.KindName(target.Kind)} has no method '{name}' taking {args.Length} argument(s)"),
        };
    }

    private static readonly double Int128UpperExclusive = Math.Pow(2, 127);

    private static Int128 FloatToInt(KsExpr node, double value)
    {
        var truncated = Math.Truncate(value);
        if (double.IsNaN(truncated) || truncated < -Int128UpperExclusive || truncated >= Int128UpperExclusive)
            throw KsEvaluator.Error(node, $"{value.ToString(CultureInfo.InvariantCulture)} cannot be converted to an integer");
        return (Int128)truncated;
    }

    private static Rune[] Runes(string s) => s.EnumerateRunes().ToArray();

    private static string JoinRunes(IEnumerable<Rune> runes)
    {
        var sb = new StringBuilder();
        foreach (var rune in runes)
            sb.Append(rune.ToString());
        return sb.ToString();
    }

    private static Int128 RequireInt(KsExpr node, KsValue value, string what) =>
        value.Kind == KsValueKind.Int
            ? value.AsInt
            : throw KsEvaluator.Error(node, $"{what} must be an integer (got {KsValue.KindName(value.Kind)})");

    private static int Radix(KsExpr node, KsValue value)
    {
        var radix = RequireInt(node, value, "Radix");
        if (radix < 2 || radix > 36)
            throw KsEvaluator.Error(node, $"Radix {radix} is not supported (2-36)");
        return (int)radix;
    }

    private static Int128 ParseInteger(KsExpr node, string text, int radix)
    {
        var span = text.AsSpan();
        var negative = false;
        if (span.Length > 0 && span[0] is '+' or '-')
        {
            negative = span[0] == '-';
            span = span[1..];
        }

        if (span.IsEmpty)
            throw KsEvaluator.Error(node, $"'{text}' is not an integer");

        // Accumulate as a negative number so that Int128.MinValue is representable.
        Int128 value = 0;
        try
        {
            foreach (var c in span)
            {
                var digit = char.IsAsciiDigit(c) ? c - '0'
                    : char.IsAsciiLetter(c) ? char.ToLowerInvariant(c) - 'a' + 10
                    : -1;
                if (digit < 0 || digit >= radix)
                    throw KsEvaluator.Error(node, $"'{text}' is not a base-{radix} integer");
                value = checked(value * radix - digit);
            }
            return negative ? value : checked(-value);
        }
        catch (OverflowException)
        {
            throw KsEvaluator.Error(node, $"'{text}' is out of integer range");
        }
    }

    private static Encoding GetEncoding(KsExpr node, KsValue value)
    {
        if (value.Kind != KsValueKind.String)
            throw KsEvaluator.Error(node, $"Encoding name must be a string (got {KsValue.KindName(value.Kind)})");
        if (!KsEncodings.TryGet(value.AsString, out var encoding))
            throw KsEvaluator.Error(node, $"Encoding '{value.AsString}' is not available");
        return encoding;
    }

    private static string Substring(KsExpr node, string text, KsValue from, KsValue to)
    {
        var runes = Runes(text);
        var start = RequireInt(node, from, "substring start");
        var end = RequireInt(node, to, "substring end");
        if (start < 0 || end > runes.Length || start > end)
            throw KsEvaluator.Error(node, $"substring({start}, {end}) is out of range (length {runes.Length})");
        return JoinRunes(runes.Skip((int)start).Take((int)(end - start)));
    }

    private static byte[] NonEmpty(KsExpr node, byte[] bytes) =>
        bytes.Length > 0 ? bytes : throw KsEvaluator.Error(node, "Byte array is empty");

    private static IReadOnlyList<KsValue> NonEmpty(KsExpr node, IReadOnlyList<KsValue> items) =>
        items.Count > 0 ? items : throw KsEvaluator.Error(node, "Array is empty");

    private static KsValue Last(KsExpr node, IReadOnlyList<KsValue> items)
    {
        var nonEmpty = NonEmpty(node, items);
        return nonEmpty[nonEmpty.Count - 1];
    }

    private static Int128 IndexOf(KsExpr node, byte[] bytes, KsValue value)
    {
        var target = RequireInt(node, value, "index_of argument");
        if (target < 0 || target > 255) return -1;
        return Array.IndexOf(bytes, (byte)target);
    }

    private static KsValue Extreme(KsExpr node, IReadOnlyList<KsValue> items, int sign)
    {
        var best = NonEmpty(node, items)[0];
        for (var i = 1; i < items.Count; i++)
        {
            if (CompareForExtreme(node, items[i], best) * sign > 0)
                best = items[i];
        }
        return best;
    }

    private static int CompareForExtreme(KsExpr node, KsValue a, KsValue b) => (a.Kind, b.Kind) switch
    {
        (KsValueKind.Int, KsValueKind.Int) => a.AsInt.CompareTo(b.AsInt),
        (KsValueKind.Int or KsValueKind.Float, KsValueKind.Int or KsValueKind.Float) =>
            KsEvaluator.ToDouble(a).CompareTo(KsEvaluator.ToDouble(b)),
        (KsValueKind.String, KsValueKind.String) => string.CompareOrdinal(a.AsString, b.AsString),
        _ => throw KsEvaluator.Error(node, "Cannot compute min/max of this array"),
    };
}
