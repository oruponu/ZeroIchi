using System;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed class KsStreamBytes(KsStream stream, KsRange range, long loadLimit, Action<long>? charge = null)
    : IKsBytes
{
    private byte[]? _data;

    public long Length => range.Length;

    public byte[] ToArray()
    {
        if (_data is not null) return _data;
        if (range.Length > loadLimit)
        {
            throw new KsExpressionException(
                $"Byte array of {range.Length} bytes is too large to load (limit {loadLimit})", -1, 0);
        }
        charge?.Invoke(range.Length);
        return _data = stream.ReadArray(range);
    }

    public byte[] Prefix(int count)
    {
        if (_data is not null) return _data[..Math.Min(count, _data.Length)];
        return stream.ReadArray(range with { Length = Math.Min(count, range.Length) });
    }
}
