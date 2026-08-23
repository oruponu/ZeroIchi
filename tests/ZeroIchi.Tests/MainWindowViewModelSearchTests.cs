using ZeroIchi.Models.Buffers;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelSearchTests
{
    private const int ChunkSize = 65536;
    private const int BufferSize = ChunkSize * 4;
    private const int MaxMatches = 100_000;

    private static ArrayByteBuffer CreateBuffer(byte[] pattern, params long[] offsets)
    {
        var data = new byte[BufferSize];
        foreach (var offset in offsets)
            pattern.CopyTo(data, (int)offset);
        return new ArrayByteBuffer(data);
    }

    [Theory]
    [InlineData(new long[] { 100 })]
    [InlineData(new long[] { 100, ChunkSize * 2 - 1 })]
    [InlineData(new long[] { ChunkSize - 1, ChunkSize * 2 - 1, ChunkSize * 3 - 1 })]
    [InlineData(new long[] { ChunkSize * 2 })]
    [InlineData(new long[] { BufferSize - 2 })]
    [InlineData(new long[0])]
    public void FindMatches_FindsPatternAtPlantedOffsets(long[] offsets)
    {
        byte[] pattern = [0x58, 0x59];
        var buffer = CreateBuffer(pattern, offsets);

        var matches = MainWindowViewModel.FindMatches(buffer, pattern, MaxMatches);

        Assert.Equal(offsets, matches);
    }

    [Fact]
    public void FindMatches_SingleBytePattern_FindsMatchesAdjacentToBoundaries()
    {
        byte[] pattern = [0x58];
        var buffer = CreateBuffer(pattern, ChunkSize - 1, ChunkSize, ChunkSize * 2 - 1, ChunkSize * 2);

        var matches = MainWindowViewModel.FindMatches(buffer, pattern, MaxMatches);

        long[] expected = [ChunkSize - 1, ChunkSize, ChunkSize * 2 - 1, ChunkSize * 2];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_FindsOverlappingMatchesAcrossBoundary()
    {
        var buffer = CreateBuffer([0x58, 0x58, 0x58, 0x58], ChunkSize * 2 - 2);

        var matches = MainWindowViewModel.FindMatches(buffer, [0x58, 0x58], MaxMatches);

        long[] expected = [ChunkSize * 2 - 2, ChunkSize * 2 - 1, ChunkSize * 2];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_MultiBytePattern_FindsMatchSpanningBoundary()
    {
        byte[] pattern = [0x57, 0x58, 0x59, 0x5A];
        var buffer = CreateBuffer(pattern, ChunkSize * 2 - 1);

        var matches = MainWindowViewModel.FindMatches(buffer, pattern, MaxMatches);

        long[] expected = [ChunkSize * 2 - 1];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_ReturnsEmpty_WhenPatternLongerThanBuffer()
    {
        var buffer = new ArrayByteBuffer([0x58]);

        var matches = MainWindowViewModel.FindMatches(buffer, [0x58, 0x59], MaxMatches);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_ReturnsEmpty_WhenBufferEmpty()
    {
        var buffer = new ArrayByteBuffer([]);

        var matches = MainWindowViewModel.FindMatches(buffer, [0x58, 0x59], MaxMatches);

        Assert.Empty(matches);
    }

    [Fact]
    public void EnumerateChunks_BeyondIntRange_KeepsOffsetsAndFinalReadLength()
    {
        const long dataLength = 3L * 1024 * 1024 * 1024;

        var chunks = MainWindowViewModel.EnumerateChunks(dataLength, ChunkSize, overlap: 1).ToList();

        Assert.Equal(dataLength / ChunkSize, (long)chunks.Count);
        Assert.Equal(0L, chunks[0].Offset);
        Assert.Equal(dataLength - ChunkSize, chunks[^1].Offset);
        Assert.Equal(ChunkSize, chunks[^1].ReadLength);
        Assert.Contains(chunks, c => c.Offset > int.MaxValue);
    }

    [Theory]
    [InlineData(0x2000L, 0)]
    [InlineData(0x7FFFFFFFL, 1)]
    [InlineData(0x80000000L, 1)]
    [InlineData(0xC0000000L, 2)]
    public void FindClosestMatch_SelectsNearestOffsetBeyondIntRange(long position, int expected)
    {
        List<long> matches = [0x1000L, 0x80000000L, 0xF0000000L];

        Assert.Equal(expected, MainWindowViewModel.FindClosestMatch(matches, position));
    }

    [Fact]
    public void FindMatches_PatternBeyondIntRange_ReturnsLongOffset()
    {
        const long plantedOffset = 0x8000_0000L;
        byte[] pattern = [0x58, 0x59];
        var buffer = new SparseByteBuffer(plantedOffset + pattern.Length, (plantedOffset, pattern));

        var matches = MainWindowViewModel.FindMatches(buffer, pattern, MaxMatches);

        long[] expected = [plantedOffset];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_StopsAtMaxMatches()
    {
        var data = new byte[1000];
        Array.Fill(data, (byte)0x58);

        var matches = MainWindowViewModel.FindMatches(new ArrayByteBuffer(data), [0x58], maxMatches: 10);

        Assert.Equal(10, matches.Count);
    }
}
