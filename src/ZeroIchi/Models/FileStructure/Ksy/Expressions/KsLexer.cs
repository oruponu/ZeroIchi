using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public static class KsLexer
{
    private const int MaxFStringDepth = 128;

    public static List<KsToken> Tokenize(string source, int offset = 0)
    {
        List<KsToken> tokens;
        try
        {
            tokens = TokenizeCore(source);
        }
        catch (KsExpressionException e) when (offset != 0)
        {
            throw new KsExpressionException(e.Message, e.Position + offset, e.Length);
        }

        if (offset == 0) return tokens;

        return tokens.ConvertAll(t => t with
        {
            Position = t.Position + offset,
            FStringParts = t.FStringParts.Select(p => p with { Position = p.Position + offset }).ToList(),
        });
    }

    private static List<KsToken> TokenizeCore(string source)
    {
        var tokens = new List<KsToken>();
        var i = 0;
        while (true)
        {
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
            if (i >= source.Length)
            {
                tokens.Add(new KsToken(KsTokenKind.End, source.Length, 0));
                return tokens;
            }

            tokens.Add(ReadToken(source, ref i, 0));
        }
    }

    private static KsToken ReadToken(string s, ref int i, int fStringDepth)
    {
        var c = s[i];
        if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1])))
            return ReadNumber(s, ref i);
        if (c == 'f' && i + 1 < s.Length && s[i + 1] == '"') return ReadFString(s, ref i, fStringDepth + 1);
        if (IsIdentifierStart(c)) return ReadIdentifier(s, ref i);
        if (c is '"' or '\'') return ReadString(s, ref i);
        return ReadSymbol(s, ref i);
    }

    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';

    private static KsToken ReadIdentifier(string s, ref int i)
    {
        var start = i;
        while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_')) i++;
        return new KsToken(KsTokenKind.Identifier, start, i - start) { Text = s[start..i] };
    }

    private static KsToken ReadNumber(string s, ref int i)
    {
        var start = i;
        if (s[i] == '0' && i + 1 < s.Length && char.ToLowerInvariant(s[i + 1]) is 'x' or 'b' or 'o')
        {
            var radix = char.ToLowerInvariant(s[i + 1]) switch { 'x' => 16, 'b' => 2, _ => 8 };
            i += 2;
            var digits = ReadDigits(s, ref i, radix);
            if (digits.Length == 0)
                throw new KsExpressionException("Missing digits in number", start, i - start);
            return new KsToken(KsTokenKind.Integer, start, i - start)
            {
                IntValue = ParseInteger(digits, radix, start, i - start),
            };
        }

        var integerDigits = ReadDigits(s, ref i, 10);
        var isFloat = false;
        if (i < s.Length && s[i] == '.' && IsFractionDot(s, i))
        {
            isFloat = true;
            i++;
            ReadDigits(s, ref i, 10);
        }

        if (i < s.Length && s[i] is 'e' or 'E')
        {
            var beforeExponent = i;
            i++;
            if (i < s.Length && s[i] is '+' or '-') i++;
            if (ReadDigits(s, ref i, 10).Length == 0)
                i = beforeExponent;
            else
                isFloat = true;
        }

        if (isFloat)
        {
            var text = s[start..i].Replace("_", "");
            return new KsToken(KsTokenKind.Float, start, i - start)
            {
                FloatValue = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
            };
        }

        return new KsToken(KsTokenKind.Integer, start, i - start)
        {
            IntValue = ParseInteger(integerDigits, 10, start, i - start),
        };
    }

    // "4.2" and "4." are floats, but "4.to_s" and "4. to_s" are a member access on an integer.
    private static bool IsFractionDot(string s, int dot)
    {
        if (dot + 1 < s.Length && char.IsAsciiDigit(s[dot + 1])) return true;

        var next = dot + 1;
        while (next < s.Length && char.IsWhiteSpace(s[next])) next++;
        return !(next < s.Length && IsIdentifierStart(s[next]));
    }

    private static string ReadDigits(string s, ref int i, int radix)
    {
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            if (s[i] == '_')
            {
                i++;
                continue;
            }

            var d = DigitValue(s[i]);
            if (d < 0 || d >= radix) break;
            sb.Append(s[i]);
            i++;
        }
        return sb.ToString();
    }

    private static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    private static Int128 ParseInteger(string digits, int radix, int position, int length)
    {
        Int128 value = 0;
        try
        {
            foreach (var c in digits)
                value = checked(value * radix + DigitValue(c));
        }
        catch (OverflowException)
        {
            throw new KsExpressionException("Integer literal is too large", position, length);
        }
        return value;
    }

    private static KsToken ReadString(string s, ref int i)
    {
        var start = i;
        var quote = s[i++];
        var sb = new StringBuilder();
        while (true)
        {
            if (i >= s.Length)
                throw new KsExpressionException("Unterminated string literal", start, i - start);

            var c = s[i];
            if (c == quote)
            {
                i++;
                break;
            }

            if (quote == '"' && c == '\\')
            {
                sb.Append(ReadEscape(s, ref i));
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return new KsToken(KsTokenKind.String, start, i - start) { Text = sb.ToString() };
    }

    private static string ReadEscape(string s, ref int i)
    {
        var escapeStart = i;
        i++;
        if (i >= s.Length)
            throw new KsExpressionException("Incomplete escape sequence", escapeStart, 1);

        var c = s[i++];
        switch (c)
        {
            case 'a': return "\a";
            case 'b': return "\b";
            case 't': return "\t";
            case 'n': return "\n";
            case 'v': return "\v";
            case 'f': return "\f";
            case 'r': return "\r";
            case 'e': return "\u001B";
            case '"': return "\"";
            case '\'': return "'";
            case '\\': return "\\";
            case 'u':
                if (i + 4 > s.Length
                    || !int.TryParse(s.AsSpan(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                {
                    throw new KsExpressionException("\\u must be followed by 4 hex digits", escapeStart, 2);
                }
                i += 4;
                return ((char)code).ToString();
            case >= '0' and <= '7':
                var value = c - '0';
                for (var n = 0; n < 2 && i < s.Length && s[i] is >= '0' and <= '7'; n++)
                    value = value * 8 + (s[i++] - '0');
                return ((char)value).ToString();
            default:
                throw new KsExpressionException($"Unknown escape sequence \\{c}", escapeStart, 2);
        }
    }

    private static KsToken ReadFString(string s, ref int i, int depth)
    {
        var start = i;
        if (depth > MaxFStringDepth)
            throw new KsExpressionException("f-strings are nested too deeply", start, 2);

        i += 2;
        var parts = new List<KsFStringPart>();
        var text = new StringBuilder();
        var textStart = i;
        while (true)
        {
            if (i >= s.Length)
                throw new KsExpressionException("Unterminated f-string", start, i - start);

            var c = s[i];
            if (c == '"')
            {
                i++;
                break;
            }

            if (c == '\\')
            {
                text.Append(ReadEscape(s, ref i));
                continue;
            }

            if (c != '{')
            {
                text.Append(c);
                i++;
                continue;
            }

            if (text.Length > 0)
            {
                parts.Add(new KsFStringPart(false, text.ToString(), textStart));
                text.Clear();
            }

            // The expression grammar has no braces, so the first '}' at a token boundary ends the embedding.
            // Reading whole tokens keeps quotes and braces inside string literals and nested f-strings intact.
            var braceStart = i;
            var exprStart = ++i;
            while (true)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i >= s.Length)
                    throw new KsExpressionException("Unclosed '{' in f-string", braceStart, i - braceStart);
                if (s[i] == '}') break;
                ReadToken(s, ref i, depth);
            }

            parts.Add(new KsFStringPart(true, s[exprStart..i], exprStart));
            i++;
            textStart = i;
        }

        if (text.Length > 0)
            parts.Add(new KsFStringPart(false, text.ToString(), textStart));

        return new KsToken(KsTokenKind.FString, start, i - start) { FStringParts = parts };
    }

    private static KsToken ReadSymbol(string s, ref int i)
    {
        var start = i;
        var c = s[i];
        var next = i + 1 < s.Length ? s[i + 1] : '\0';
        var (kind, length) = (c, next) switch
        {
            ('<', '<') => (KsTokenKind.Shl, 2),
            ('>', '>') => (KsTokenKind.Shr, 2),
            ('<', '=') => (KsTokenKind.Le, 2),
            ('>', '=') => (KsTokenKind.Ge, 2),
            ('=', '=') => (KsTokenKind.Eq, 2),
            ('!', '=') => (KsTokenKind.Ne, 2),
            (':', ':') => (KsTokenKind.DoubleColon, 2),
            ('<', _) => (KsTokenKind.Lt, 1),
            ('>', _) => (KsTokenKind.Gt, 1),
            ('(', _) => (KsTokenKind.LParen, 1),
            (')', _) => (KsTokenKind.RParen, 1),
            ('[', _) => (KsTokenKind.LBracket, 1),
            (']', _) => (KsTokenKind.RBracket, 1),
            (',', _) => (KsTokenKind.Comma, 1),
            ('.', _) => (KsTokenKind.Dot, 1),
            ('?', _) => (KsTokenKind.Question, 1),
            (':', _) => (KsTokenKind.Colon, 1),
            ('+', _) => (KsTokenKind.Plus, 1),
            ('-', _) => (KsTokenKind.Minus, 1),
            ('*', _) => (KsTokenKind.Star, 1),
            ('/', _) => (KsTokenKind.Slash, 1),
            ('%', _) => (KsTokenKind.Percent, 1),
            ('~', _) => (KsTokenKind.Tilde, 1),
            ('&', _) => (KsTokenKind.Amp, 1),
            ('|', _) => (KsTokenKind.Pipe, 1),
            ('^', _) => (KsTokenKind.Caret, 1),
            _ => throw new KsExpressionException($"Unexpected character '{c}'", start, 1),
        };
        i += length;
        return new KsToken(kind, start, length);
    }
}
