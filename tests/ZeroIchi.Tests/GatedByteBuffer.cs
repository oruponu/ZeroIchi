using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Tests;

internal sealed class GatedByteBuffer(ByteBuffer inner) : ByteBuffer
{
    private readonly SemaphoreSlim _reads = new(0);
    private volatile bool _isOpen;

    public override long Length => inner.Length;

    public override byte ReadByte(long index) => inner.ReadByte(index);

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        while (!_isOpen && !_reads.Wait(5))
        {
        }

        inner.ReadBytes(offset, buffer, bufferOffset, count);
    }

    public void Allow(int reads) => _reads.Release(reads);

    public void OpenAll() => _isOpen = true;
}
