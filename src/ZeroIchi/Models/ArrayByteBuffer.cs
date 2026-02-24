using System;

namespace ZeroIchi.Models;

public class ArrayByteBuffer(byte[] array) : ByteBuffer
{
    public byte[] Array { get; set; } = array;

    public override long Length => Array.Length;

    public override byte ReadByte(long index) => Array[index];

    public override void WriteByte(long index, byte value) => Array[index] = value;

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var available = (int)Math.Min(count, Array.Length - offset);
        if (available <= 0) return;
        System.Array.Copy(Array, offset, buffer, bufferOffset, available);
    }
}
