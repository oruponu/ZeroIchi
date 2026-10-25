using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Natives;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Tests.Ksy.Natives;

public sealed class KsStreamReaderTests
{
    private sealed class CountingBuffer(byte[] data) : ByteBuffer
    {
        public int Reads { get; private set; }

        public override long Length => data.Length;

        public override byte ReadByte(long index)
        {
            Reads++;
            return data[index];
        }

        public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
        {
            Reads++;
            Array.Copy(data, offset, buffer, bufferOffset, count);
        }
    }

    [Fact]
    public void Indexer_ReadingBackwardDoesNotRefillForEveryByte()
    {
        var data = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();
        var buffer = new CountingBuffer(data);
        var reader = new KsStreamReader(KsStream.FromBuffer(buffer));

        for (var position = data.Length - 1; position >= data.Length - 100_000; position--)
            Assert.Equal(data[position], reader[position]);

        Assert.InRange(buffer.Reads, 1, 5);
    }

    [Fact]
    public void UInt_AcrossTheWindowEdgeDoesNotRefillForEveryRead()
    {
        var data = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();
        var buffer = new CountingBuffer(data);
        var reader = new KsStreamReader(KsStream.FromBuffer(buffer));

        for (var position = 65_000L; position < 67_000; position++) reader.UInt(position, 4);

        Assert.InRange(buffer.Reads, 1, 5);
    }
}
