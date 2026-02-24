using System;

namespace ZeroIchi.Models;

public abstract class ByteBuffer : IDisposable
{
    public abstract long Length { get; }
    public abstract byte ReadByte(long index);
    public abstract void WriteByte(long index, byte value);
    public abstract void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count);

    public byte[] SliceToArray(long offset, int count)
    {
        if (count <= 0) return [];
        var result = new byte[count];
        ReadBytes(offset, result, 0, count);
        return result;
    }

    public virtual void Dispose() => GC.SuppressFinalize(this);
}
