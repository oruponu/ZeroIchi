using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Tests;

internal sealed class SparseByteBuffer(long length, params (long Offset, byte[] Data)[] regions) : ByteBuffer
{
    public override long Length => length;

    public override byte ReadByte(long index)
    {
        foreach (var (offset, data) in regions)
        {
            if (index >= offset && index < offset + data.Length)
                return data[(int)(index - offset)];
        }
        return 0;
    }

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var available = (int)Math.Min(count, length - offset);
        if (available <= 0) return;

        Array.Clear(buffer, bufferOffset, available);

        foreach (var (regionOffset, data) in regions)
        {
            var start = Math.Max(offset, regionOffset);
            var end = Math.Min(offset + available, regionOffset + data.Length);
            if (start >= end) continue;

            Array.Copy(data, (int)(start - regionOffset),
                buffer, bufferOffset + (int)(start - offset), (int)(end - start));
        }
    }
}
