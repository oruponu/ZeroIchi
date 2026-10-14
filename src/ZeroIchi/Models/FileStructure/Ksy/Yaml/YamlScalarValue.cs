using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Yaml;

public enum YamlScalarStyle
{
    Plain,
    SingleQuoted,
    DoubleQuoted,
    Literal,
    Folded,
}

public enum YamlScalarKind
{
    Null,
    Bool,
    Int,
    Float,
    String,
}

public readonly record struct YamlScalarValue(YamlScalarKind Kind, bool Bool, Int128 Int, double Float, string Text)
{
    public static YamlScalarValue Resolve(string text, YamlScalarStyle style)
    {
        if (style != YamlScalarStyle.Plain) return OfString(text);

        if (text.Length == 0 || NullPattern.IsMatch(text))
            return new YamlScalarValue(YamlScalarKind.Null, false, 0, 0, text);
        if (TruePattern.IsMatch(text))
            return new YamlScalarValue(YamlScalarKind.Bool, true, 0, 0, text);
        if (FalsePattern.IsMatch(text))
            return new YamlScalarValue(YamlScalarKind.Bool, false, 0, 0, text);
        if (IntPattern.IsMatch(text))
            return TryParseInt(text, out var value) ? new YamlScalarValue(YamlScalarKind.Int, false, value, 0, text) : OfString(text);
        if (FloatPattern.IsMatch(text) && TryParseFloat(text, out var number))
            return new YamlScalarValue(YamlScalarKind.Float, false, 0, number, text);
        return OfString(text);
    }

    public string ToExpressionSource() => Kind switch
    {
        YamlScalarKind.Bool => Bool ? "true" : "false",
        YamlScalarKind.Int => Int.ToString(CultureInfo.InvariantCulture),
        YamlScalarKind.Float => FloatSource(Float),
        _ => Text,
    };

    private static string FloatSource(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.') || text.Contains('E') ? text : text + ".0";
    }

    private static YamlScalarValue OfString(string text) => new(YamlScalarKind.String, false, 0, 0, text);

    private static readonly Regex NullPattern = new("^(?:~|null|Null|NULL| )$", RegexOptions.CultureInvariant);

    private static readonly Regex TruePattern = new("^(?:yes|Yes|YES|true|True|TRUE|on|On|ON)$", RegexOptions.CultureInvariant);

    private static readonly Regex FalsePattern = new("^(?:no|No|NO|false|False|FALSE|off|Off|OFF)$", RegexOptions.CultureInvariant);

    private static readonly Regex IntPattern = new(
        "^(?:[-+]?0b_*[0-1][0-1_]*|[-+]?0_*[0-7][0-7_]*|[-+]?(?:0|[1-9][0-9_]*)|[-+]?0x_*[0-9a-fA-F][0-9a-fA-F_]*|[-+]?[1-9][0-9_]*(?::[0-5]?[0-9])+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FloatPattern = new(
        "^(?:[-+]?[0-9][0-9_]*\\.[0-9_]*(?:[eE][-+]?[0-9]+)?|[-+]?[0-9][0-9_]*[eE][-+]?[0-9]+|[-+]?\\.[0-9_]+(?:[eE][-+]?[0-9]+)?|[-+]?[0-9][0-9_]*(?::[0-5]?[0-9])+\\.[0-9_]*|[-+]?\\.(?:inf|Inf|INF)|\\.(?:nan|NaN|NAN))$",
        RegexOptions.CultureInvariant);

    private static bool TryParseInt(string text, out Int128 value)
    {
        value = 0;
        var s = text.Replace("_", "");
        var negative = s[0] == '-';
        if (s[0] is '+' or '-') s = s[1..];

        Int128 magnitude = 0;
        try
        {
            if (s.Contains(':'))
            {
                foreach (var part in s.Split(':'))
                    magnitude = checked(magnitude * 60 + Int128.Parse(part, CultureInfo.InvariantCulture));
            }
            else
            {
                var (digits, radix) = s switch
                {
                    _ when s.StartsWith("0b", StringComparison.Ordinal) => (s[2..], 2),
                    _ when s.StartsWith("0x", StringComparison.Ordinal) => (s[2..], 16),
                    _ when s.Length > 1 && s[0] == '0' => (s[1..], 8),
                    _ => (s, 10),
                };
                foreach (var c in digits)
                    magnitude = checked(magnitude * radix + Convert.ToInt32(c.ToString(), 16));
            }

            value = negative ? checked(-magnitude) : magnitude;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryParseFloat(string text, out double value)
    {
        var s = text.Replace("_", "");
        switch (s.TrimStart('+', '-').ToLowerInvariant())
        {
            case ".inf":
                value = s[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            case ".nan":
                value = double.NaN;
                return true;
        }

        if (!s.Contains(':'))
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        var negative = s[0] == '-';
        var parts = s.TrimStart('+', '-').Split(':');
        value = 0;
        foreach (var part in parts)
            value = value * 60 + double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (negative) value = -value;
        return true;
    }
}
