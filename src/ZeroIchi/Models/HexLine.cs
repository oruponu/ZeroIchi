using System;

namespace ZeroIchi.Models;

public record HexLine(string Offset, string HexBytes, string Ascii)
{
    public const int BytesPerLine = 16;

    public static HexLine Create(byte[] data, int lineIndex)
    {
        var offset = lineIndex * BytesPerLine;
        var lineSpan = data.AsSpan(offset, Math.Min(BytesPerLine, data.Length - offset));

        return new HexLine(offset.ToString("X8"), FormatHex(lineSpan), FormatAscii(lineSpan));
    }

    private static string FormatHex(ReadOnlySpan<byte> lineData)
    {
        Span<char> buf = stackalloc char[BytesPerLine * 3 + 1]; // "XX " * 16 + midpoint space
        var pos = 0;

        for (var i = 0; i < BytesPerLine; i++)
        {
            if (i < lineData.Length)
            {
                lineData[i].TryFormat(buf[pos..], out _, "X2");
                pos += 2;
            }
            else
            {
                buf[pos++] = ' ';
                buf[pos++] = ' ';
            }

            buf[pos++] = ' ';

            if (i == 7)
                buf[pos++] = ' ';
        }

        return new string(buf[..pos]);
    }

    private static string FormatAscii(ReadOnlySpan<byte> lineData)
    {
        Span<char> buf = stackalloc char[lineData.Length];

        for (var i = 0; i < lineData.Length; i++)
        {
            var b = lineData[i];
            buf[i] = b is >= 0x20 and <= 0x7E ? (char)b : '.';
        }

        return new string(buf);
    }
}
