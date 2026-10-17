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

    public byte this[long position]
    {
        get
        {
            if (position < _windowStart || position >= _windowStart + _windowLength) Fill(position);
            return _window[position - _windowStart];
        }
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
