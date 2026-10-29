using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.Search;

namespace ZeroIchi.Tests.Search;

public sealed class ByteSearcherTests
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
    public void Scan_FindsPatternAtPlantedOffsets(long[] offsets)
    {
        byte[] pattern = [0x58, 0x59];
        var buffer = CreateBuffer(pattern, offsets);

        var matches = Collect(buffer, pattern, 0).After;

        Assert.Equal(offsets, matches);
    }

    [Fact]
    public void Scan_SingleBytePattern_FindsMatchesAdjacentToBoundaries()
    {
        byte[] pattern = [0x58];
        var buffer = CreateBuffer(pattern, ChunkSize - 1, ChunkSize, ChunkSize * 2 - 1, ChunkSize * 2);

        var matches = Collect(buffer, pattern, 0).After;

        long[] expected = [ChunkSize - 1, ChunkSize, ChunkSize * 2 - 1, ChunkSize * 2];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Scan_FindsOverlappingMatchesAcrossBoundary()
    {
        var buffer = CreateBuffer([0x58, 0x58, 0x58, 0x58], ChunkSize * 2 - 2);

        var matches = Collect(buffer, [0x58, 0x58], 0).After;

        long[] expected = [ChunkSize * 2 - 2, ChunkSize * 2 - 1, ChunkSize * 2];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Scan_MultiBytePattern_FindsMatchSpanningBoundary()
    {
        byte[] pattern = [0x57, 0x58, 0x59, 0x5A];
        var buffer = CreateBuffer(pattern, ChunkSize * 2 - 1);

        var matches = Collect(buffer, pattern, 0).After;

        long[] expected = [ChunkSize * 2 - 1];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Scan_ReturnsEmpty_WhenPatternLongerThanBuffer()
    {
        var buffer = new ArrayByteBuffer([0x58]);

        var matches = Collect(buffer, [0x58, 0x59], 0).After;

        Assert.Empty(matches);
    }

    [Fact]
    public void Scan_ReturnsEmpty_WhenBufferEmpty()
    {
        var buffer = new ArrayByteBuffer([]);

        var matches = Collect(buffer, [0x58, 0x59], 0).After;

        Assert.Empty(matches);
    }

    [Fact]
    public void Scan_EmptyPattern_Throws()
    {
        var buffer = new ArrayByteBuffer([0x58]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ByteSearcher.Scan(buffer, [], 0, MaxMatches, _ => { }, CancellationToken.None));
    }

    [Fact]
    public void Scan_PatternBeyondIntRange_ReturnsLongOffset()
    {
        const long plantedOffset = 0x8000_0000L;
        byte[] pattern = [0x58, 0x59];
        var buffer = new SparseByteBuffer(plantedOffset + pattern.Length, (plantedOffset, pattern));

        var matches = Collect(buffer, pattern, 0).After;

        long[] expected = [plantedOffset];
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Scan_StopsAtMaxMatches()
    {
        var data = new byte[1000];
        Array.Fill(data, (byte)0x58);

        var matches = Collect(new ArrayByteBuffer(data), [0x58], 0, maxMatches: 10).After;

        Assert.Equal(10, matches.Count);
    }

    private static (List<long> After, List<long> Before) Collect(ByteBuffer buffer, byte[] pattern, long start,
        int maxMatches = MaxMatches)
    {
        var after = new List<long>();
        var before = new List<long>();
        ByteSearcher.Scan(buffer, pattern, start, maxMatches,
            p => (p.Phase == SearchPhase.AfterStart ? after : before).AddRange(p.Matches), CancellationToken.None);
        return (after, before);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(100L)]
    [InlineData(101L)]
    [InlineData(ChunkSize - 1L)]
    [InlineData(ChunkSize * 2L)]
    [InlineData(BufferSize - 1L)]
    [InlineData((long)BufferSize)]
    public void Scan_FromAnyStart_FindsEachMatchOnceInWrappedOrder(long start)
    {
        byte[] pattern = [0x58, 0x59];
        long[] planted = [100, ChunkSize - 1, ChunkSize * 2 - 1, ChunkSize * 2 + 1, BufferSize - 2];
        var buffer = CreateBuffer(pattern, planted);

        var (after, before) = Collect(buffer, pattern, start);

        Assert.Equal(planted.Where(o => o >= start), after);
        Assert.Equal(planted.Where(o => o < start), before);
    }

    [Fact]
    public void Scan_OverlappingMatchesAroundTheStart_AreSplitByTheirStartOffset()
    {
        var buffer = CreateBuffer([0x58, 0x58, 0x58, 0x58], 1000);

        var (after, before) = Collect(buffer, [0x58, 0x58], 1001);

        Assert.Equal([1001L, 1002L], after);
        Assert.Equal([1000L], before);
    }

    [Fact]
    public void Scan_ReportsOncePerChunkWithOneReadEach()
    {
        var buffer = new CountingByteBuffer(CreateBuffer([0x58], 10));
        var reports = new List<SearchProgress>();

        ByteSearcher.Scan(buffer, [0x58], ChunkSize + 5, MaxMatches, reports.Add, CancellationToken.None);

        (SearchPhase, long)[] expected =
        [
            (SearchPhase.AfterStart, ChunkSize * 2 + 5), (SearchPhase.AfterStart, ChunkSize * 3 + 5),
            (SearchPhase.AfterStart, BufferSize), (SearchPhase.BeforeStart, ChunkSize),
            (SearchPhase.BeforeStart, ChunkSize + 5),
        ];
        Assert.Equal(expected, reports.Select(r => (r.Phase, r.ScannedTo)));
        Assert.Equal([10L], reports.SelectMany(r => r.Matches));
        Assert.Equal(reports.Count, buffer.Reads);
    }

    [Fact]
    public void Scan_StopsWhenCancelled()
    {
        var buffer = CreateBuffer([0x58], 10, ChunkSize * 3);
        using var cts = new CancellationTokenSource();
        var reports = 0;

        Assert.Throws<OperationCanceledException>(() => ByteSearcher.Scan(buffer, [0x58], 0, MaxMatches,
            _ => { if (++reports == 1) cts.Cancel(); }, cts.Token));

        Assert.Equal(1, reports);
    }

    [Fact]
    public void Scan_StopsAtMaxMatchesCountingFromTheStart()
    {
        var buffer = CreateBuffer([0x58], 10, 20, ChunkSize + 10, ChunkSize + 20);

        var (after, before) = Collect(buffer, [0x58], ChunkSize, maxMatches: 3);

        Assert.Equal([ChunkSize + 10L, ChunkSize + 20L], after);
        Assert.Equal([10L], before);
    }

    [Fact]
    public void Scan_PatternLongerThanBuffer_ReportsNoMatches()
    {
        var (after, before) = Collect(new ArrayByteBuffer([0x58]), [0x58, 0x58], 0);

        Assert.Empty(after);
        Assert.Empty(before);
    }

    [Fact]
    public void Scan_EmptyBuffer_ReportsNothing()
    {
        var reports = new List<SearchProgress>();

        ByteSearcher.Scan(new ArrayByteBuffer([]), [0x58], 0, MaxMatches, reports.Add, CancellationToken.None);

        Assert.Empty(reports);
    }

    private sealed class CountingByteBuffer(ByteBuffer inner) : ByteBuffer
    {
        public int Reads { get; private set; }

        public override long Length => inner.Length;

        public override byte ReadByte(long index) => inner.ReadByte(index);

        public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
        {
            Reads++;
            inner.ReadBytes(offset, buffer, bufferOffset, count);
        }
    }
}
