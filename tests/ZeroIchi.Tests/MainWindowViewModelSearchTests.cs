using ZeroIchi.Models.Buffers;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelSearchTests
{
    private const int ChunkSize = 65536;
    private const int BufferSize = ChunkSize * 4;

    private static ArrayByteBuffer CreateBuffer(byte[] pattern, params int[] offsets)
    {
        var data = new byte[BufferSize];
        foreach (var offset in offsets)
            pattern.CopyTo(data, offset);
        return new ArrayByteBuffer(data);
    }

    [Theory]
    [InlineData(new[] { 100 })]
    [InlineData(new[] { 100, ChunkSize * 2 - 1 })]
    [InlineData(new[] { ChunkSize - 1, ChunkSize * 2 - 1, ChunkSize * 3 - 1 })]
    [InlineData(new[] { ChunkSize * 2 })]
    [InlineData(new[] { BufferSize - 2 })]
    [InlineData(new int[0])]
    public void FindMatches_FindsPatternAtPlantedOffsets(int[] offsets)
    {
        byte[] pattern = [0x58, 0x59];
        var buffer = CreateBuffer(pattern, offsets);

        var matches = MainWindowViewModel.FindMatches(buffer, pattern);

        Assert.Equal(offsets, matches);
    }

    [Fact]
    public void FindMatches_SingleBytePattern_FindsMatchesAdjacentToBoundaries()
    {
        byte[] pattern = [0x58];
        var buffer = CreateBuffer(pattern, ChunkSize - 1, ChunkSize, ChunkSize * 2 - 1, ChunkSize * 2);

        var matches = MainWindowViewModel.FindMatches(buffer, pattern);

        int[] expected = [ChunkSize - 1, ChunkSize, ChunkSize * 2 - 1, ChunkSize * 2];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_FindsOverlappingMatchesAcrossBoundary()
    {
        var buffer = CreateBuffer([0x58, 0x58, 0x58, 0x58], ChunkSize * 2 - 2);

        var matches = MainWindowViewModel.FindMatches(buffer, [0x58, 0x58]);

        int[] expected = [ChunkSize * 2 - 2, ChunkSize * 2 - 1, ChunkSize * 2];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_MultiBytePattern_FindsMatchSpanningBoundary()
    {
        byte[] pattern = [0x57, 0x58, 0x59, 0x5A];
        var buffer = CreateBuffer(pattern, ChunkSize * 2 - 1);

        var matches = MainWindowViewModel.FindMatches(buffer, pattern);

        int[] expected = [ChunkSize * 2 - 1];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FindMatches_ReturnsEmpty_WhenPatternLongerThanBuffer()
    {
        var buffer = new ArrayByteBuffer([0x58]);

        var matches = MainWindowViewModel.FindMatches(buffer, [0x58, 0x59]);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_ReturnsEmpty_WhenBufferEmpty()
    {
        var buffer = new ArrayByteBuffer([]);

        var matches = MainWindowViewModel.FindMatches(buffer, [0x58, 0x59]);

        Assert.Empty(matches);
    }
}
