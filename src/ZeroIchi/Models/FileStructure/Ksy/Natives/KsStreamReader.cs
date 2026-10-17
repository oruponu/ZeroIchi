using System;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Models.FileStructure.Ksy.Natives;

internal sealed class KsStreamReader(KsStream stream)
{
    private const int WindowSize = 64 * 1024;

    private readonly byte[] _window = new byte[WindowSize];
    private long _windowStart;
    private int _windowLength;

    public long Size => stream.Size;

    public long FileOffset(long position) => stream.FileOffset(position);

    public byte this[long position]
    {
        get
        {
            if (position < _windowStart || position >= _windowStart + _windowLength) Fill(position);
            return _window[position - _windowStart];
        }
    }

    public string Preview(long position, long length)
    {
        var bytes = new byte[Math.Min(length, KsyDescriptions.BytesLimit)];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = this[position + i];
        var text = KsyDescriptions.Hex(bytes);
        return length > KsyDescriptions.BytesLimit ? $"{text} ..." : text;
    }

    private void Fill(long position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, stream.Size);
        _windowStart = position;
        _windowLength = (int)Math.Min(WindowSize, stream.Size - position);
        stream.CopyTo(position, _window, 0, _windowLength);
    }
}
