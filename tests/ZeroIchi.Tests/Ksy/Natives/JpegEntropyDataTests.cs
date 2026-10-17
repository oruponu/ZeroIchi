using ZeroIchi.Models.FileStructure.Ksy.Natives;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Tests.Ksy.Natives;

public sealed class JpegEntropyDataTests
{
    private static long Consumed(byte[] data, long start = 0)
    {
        var stream = KsStream.FromBytes(data);
        stream.Seek(start);
        var result = new JpegEntropyData().Read(stream, []);
        Assert.Empty(result.Nodes);
        Assert.Null(result.Error);
        return result.Consumed;
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 0xFF, 0xD9 }, 2)]
    [InlineData(new byte[] { 1, 0xFF, 0x00, 2, 0xFF, 0xD9 }, 4)]
    [InlineData(new byte[] { 1, 0xFF, 0xFF, 0xFF, 0xD9 }, 3)]
    [InlineData(new byte[] { 1, 0xFF, 0xD0, 2 }, 1)]
    [InlineData(new byte[] { 1, 0xFF, 0x05, 2, 0xFF, 0xC4 }, 4)]
    [InlineData(new byte[] { 1, 2, 3 }, 3)]
    [InlineData(new byte[] { 1, 0xFF }, 2)]
    [InlineData(new byte[] { 1, 0xFF, 0xFF }, 3)]
    [InlineData(new byte[] { 0xFF, 0xD9 }, 0)]
    public void Read_StopsBeforeTheNextMarker(byte[] data, long consumed)
    {
        Assert.Equal(consumed, Consumed(data));
    }

    [Fact]
    public void Read_StartsAtTheCurrentPosition()
    {
        Assert.Equal(2, Consumed([0xFF, 0xDA, 1, 2, 0xFF, 0xD9], start: 2));
    }

    [Fact]
    public void Read_FindsAMarkerPastTheFirstChunk()
    {
        byte[] data = [.. new byte[70_000], 0xFF, 0x00, .. new byte[10], 0xFF, 0xD9];

        Assert.Equal(70_012, Consumed(data));
    }

    [Fact]
    public void Read_FillBytesAcrossAChunkBoundaryStayInTheData()
    {
        byte[] data = [.. new byte[65_535], 0xFF, 0xFF, 0xD9];

        Assert.Equal(65_536, Consumed(data));
    }
}
