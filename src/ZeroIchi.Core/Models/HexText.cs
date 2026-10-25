using System;
using System.Globalization;

namespace ZeroIchi.Models;

public static class HexText
{
    public static string Format(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return "";

        var chars = new char[bytes.Length * 3 - 1];
        for (var i = 0; i < bytes.Length; i++)
        {
            var pos = i * 3;
            if (i > 0) chars[pos - 1] = ' ';
            chars[pos] = ToHexChar(bytes[i] >> 4);
            chars[pos + 1] = ToHexChar(bytes[i] & 0xF);
        }
        return new string(chars);
    }

    public static byte[]? Parse(string text)
    {
        var tokens = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!byte.TryParse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return null;
            result[i] = b;
        }
        return result;
    }

    private static char ToHexChar(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
}
