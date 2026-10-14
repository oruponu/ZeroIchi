using System.IO.Compression;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsStreamRangeTests
{
    private static KsStream Stream(params byte[] data) => KsStream.FromBuffer(new ArrayByteBuffer(data));

    [Fact]
    public void ReadRange_ConsumesAndReturnsTheRange()
    {
        var stream = Stream(1, 2, 3, 4);
        stream.ReadInteger(1, false, true);

        Assert.Equal(new KsRange(1, 2), stream.ReadRange(2));
        Assert.Equal(3, stream.Position);
        Assert.Throws<KsyParseException>(() => stream.ReadRange(2));
    }

    [Fact]
    public void ReadToEnd_TakesTheRest()
    {
        var stream = Stream(1, 2, 3);
        stream.ReadInteger(1, false, true);

        Assert.Equal(new KsRange(1, 2), stream.ReadToEnd());
        Assert.True(stream.IsEof);
    }

    [Theory]
    [InlineData(false, true, 2, 3)]
    [InlineData(true, true, 3, 3)]
    [InlineData(false, false, 2, 2)]
    [InlineData(true, false, 3, 2)]
    public void ReadTerminated_FollowsIncludeAndConsume(bool include, bool consume, long length, long position)
    {
        var stream = Stream((byte)'a', (byte)'b', 0, (byte)'c');

        var range = stream.ReadTerminated([0], include, consume, true);

        Assert.Equal(new KsRange(0, length), range);
        Assert.Equal(position, stream.Position);
    }

    [Fact]
    public void ReadTerminated_WithoutTerminator_FailsOrTakesTheRest()
    {
        Assert.Throws<KsyParseException>(() => Stream(1, 2).ReadTerminated([0], false, true, true));

        var stream = Stream(1, 2);
        Assert.Equal(new KsRange(0, 2), stream.ReadTerminated([0], false, true, false));
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void ReadTerminated_MultiByteTerminatorIsMatchedOnUnitBoundaries()
    {
        var stream = Stream((byte)'a', 0, 0, (byte)'b', 0, 0, 1, 1);

        var range = stream.ReadTerminated([0, 0], false, true, true);

        Assert.Equal(new KsRange(0, 4), range);
        Assert.Equal(6, stream.Position);
    }

    [Fact]
    public void ReadTerminated_FindsATerminatorBeyondTheFirstChunk()
    {
        var data = new byte[10_000];
        Array.Fill(data, (byte)'x');
        data[9_000] = 0;

        var range = Stream(data).ReadTerminated([0], false, true, true);

        Assert.Equal(9_000, range.Length);
    }

    [Fact]
    public void Truncate_StripsPaddingThenCutsAtTheTerminator()
    {
        var stream = Stream((byte)'a', 0, (byte)'b', 0x20, 0x20);

        Assert.Equal(new KsRange(0, 3), stream.Truncate(new KsRange(0, 5), 0x20, null, false));
        Assert.Equal(new KsRange(0, 1), stream.Truncate(new KsRange(0, 5), 0x20, [0], false));
        Assert.Equal(new KsRange(0, 2), stream.Truncate(new KsRange(0, 5), null, [0], true));
        Assert.Equal(new KsRange(3, 0), stream.Truncate(new KsRange(3, 2), 0x20, null, false));
    }

    [Fact]
    public void Truncate_PaddingLongerThanOneChunk()
    {
        var data = new byte[10_000];
        data[0] = 1;

        Assert.Equal(new KsRange(0, 1), Stream(data).Truncate(new KsRange(0, 10_000), 0, null, false));
    }

    [Fact]
    public void ReadArray_CopiesTheRange()
    {
        var stream = Stream(1, 2, 3, 4);

        Assert.Equal(new byte[] { 2, 3 }, stream.ReadArray(new KsRange(1, 2)));
        Assert.Equal(new byte[] { 3, 4 }, stream.Substream(2, 2).ReadArray(new KsRange(0, 2)));
    }

    [Fact]
    public void StreamBytes_AnswersTheLengthWithoutReading()
    {
        var buffer = new SparseByteBuffer(3L << 30, (0, [0xAB, 0xCD]));
        var bytes = new KsStreamBytes(KsStream.FromBuffer(buffer), new KsRange(0, 3L << 30), 16);

        Assert.Equal(3L << 30, bytes.Length);
        Assert.Equal(new byte[] { 0xAB, 0xCD, 0 }, bytes.Prefix(3));
        var error = Assert.Throws<KsExpressionException>(() => bytes.ToArray());
        Assert.Contains("too large to load", error.Message);
    }

    [Fact]
    public void StreamBytes_LoadsOnceWithinTheLimit()
    {
        var bytes = new KsStreamBytes(Stream(1, 2, 3), new KsRange(1, 2), 16);

        var first = bytes.ToArray();

        Assert.Equal(new byte[] { 2, 3 }, first);
        Assert.Same(first, bytes.ToArray());
        Assert.Equal(new byte[] { 2 }, bytes.Prefix(1));
    }

    [Fact]
    public void Xor_WithAByteAndWithAKey()
    {
        Assert.Equal(new byte[] { 0xFE, 0xFD }, KsProcess.Xor([1, 2], KsValue.FromInt(0xFF)));
        Assert.Equal(new byte[] { 0x00, 0x00, 0x01 }, KsProcess.Xor([1, 2, 0], KsValue.FromBytes([1, 2])));
        Assert.Throws<KsExpressionException>(() => KsProcess.Xor([1], KsValue.FromInt(256)));
        Assert.Throws<KsExpressionException>(() => KsProcess.Xor([1], KsValue.FromBytes([])));
    }

    [Fact]
    public void Rotate_LeftAndRight()
    {
        Assert.Equal(new byte[] { 0b0000_0011 }, KsProcess.Rotate([0b1000_0001], KsValue.FromInt(1), true));
        Assert.Equal(new byte[] { 0b1100_0000 }, KsProcess.Rotate([0b1000_0001], KsValue.FromInt(1), false));
        Assert.Equal(new byte[] { 0b1000_0001 }, KsProcess.Rotate([0b1000_0001], KsValue.FromInt(8), true));
    }

    [Fact]
    public void Zlib_InflatesWithinTheLimit()
    {
        var plain = new byte[1000];
        Array.Fill(plain, (byte)7);
        var compressed = Compress(plain);

        Assert.Equal(plain, KsProcess.Zlib(compressed, 1000));
        var error = Assert.Throws<KsyParseException>(() => KsProcess.Zlib(compressed, 999));
        Assert.Contains("exceeds the limit", error.Message);
    }

    [Fact]
    public void Zlib_InvalidDataIsAParseError()
    {
        var error = Assert.Throws<KsyParseException>(() => KsProcess.Zlib([1, 2, 3, 4], 100));

        Assert.Contains("Invalid zlib data", error.Message);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(data);
        return output.ToArray();
    }
}
