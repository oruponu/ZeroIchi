using System;

namespace ZeroIchi.Models.Buffers;

public class ArrayByteBuffer(byte[] array) : ByteBuffer
{
    public override long Length => array.Length;

    public override byte ReadByte(long index) => array[index];

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var available = (int)Math.Min(count, array.Length - offset);
        if (available <= 0) return;
        Array.Copy(array, offset, buffer, bufferOffset, available);
    }
}
