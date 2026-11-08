using System;
using System.Collections.Generic;
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
        var tokens = text.Split([' ', '\t', '\r', '\n', ',', '-', ':'], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<byte>();
        foreach (var token in tokens)
        {
            if (!TryParseToken(token, result)) return null;
        }
        return result.ToArray();
    }

    private static bool TryParseToken(ReadOnlySpan<char> token, List<byte> result)
    {
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) token = token[2..];

        if (token.Length == 1) return TryAddByte(token, result);
        if (token.Length == 0 || token.Length % 2 != 0) return false;

        for (var i = 0; i < token.Length; i += 2)
        {
            if (!TryAddByte(token.Slice(i, 2), result)) return false;
        }
        return true;
    }

    private static bool TryAddByte(ReadOnlySpan<char> digits, List<byte> result)
    {
        if (!byte.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
            return false;
        result.Add(b);
        return true;
    }

    private static char ToHexChar(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
}
