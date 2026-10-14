using System.Collections.Generic;
using System.Text;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

internal sealed class KsSingleByteEncoding : Encoding
{
    private const int UpperHalfStart = 0xA0;

    private readonly string _name;
    private readonly char[] _chars = new char[256];
    private readonly Dictionary<char, byte> _bytes = [];

    public KsSingleByteEncoding(string name, string upperHalf)
    {
        _name = name;
        for (var i = 0; i < UpperHalfStart; i++)
            _chars[i] = (char)i;
        upperHalf.CopyTo(0, _chars, UpperHalfStart, 256 - UpperHalfStart);

        for (var i = 0; i < _chars.Length; i++)
            _bytes.TryAdd(_chars[i], (byte)i);
    }

    public override string EncodingName => _name;

    public override bool IsSingleByte => true;

    public override int GetByteCount(char[] chars, int index, int count) => count;

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        for (var i = 0; i < charCount; i++)
            bytes[byteIndex + i] = _bytes.TryGetValue(chars[charIndex + i], out var b) ? b : (byte)'?';
        return charCount;
    }

    public override int GetCharCount(byte[] bytes, int index, int count) => count;

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        for (var i = 0; i < byteCount; i++)
            chars[charIndex + i] = _chars[bytes[byteIndex + i]];
        return byteCount;
    }

    public override int GetMaxByteCount(int charCount) => charCount;

    public override int GetMaxCharCount(int byteCount) => byteCount;
}
