using System;
using System.Globalization;

namespace ZeroIchi.Models;

public static class OffsetText
{
    public static bool TryParse(ReadOnlySpan<char> text, bool isHex, out long offset)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);

        var style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;
        return long.TryParse(text, style, CultureInfo.InvariantCulture, out offset);
    }
}
