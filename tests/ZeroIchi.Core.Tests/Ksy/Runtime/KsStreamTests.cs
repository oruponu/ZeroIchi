using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsStreamTests
{
    private static KsStream Stream(params byte[] data) => KsStream.FromBuffer(new ArrayByteBuffer(data));

    [Fact]
    public void ReadInteger_ReadsEachWidthAndByteOrder()
    {
        var stream = Stream(0x01, 0x02, 0x01, 0x02, 0x01, 0x02, 0x03, 0x04, 0xFF);

        Assert.Equal(0x0102, stream.ReadInteger(2, false, true));
        Assert.Equal(0x0201, stream.ReadInteger(2, false, false));
        Assert.Equal(0x01020304, stream.ReadInteger(4, false, true));
        Assert.Equal(255, stream.ReadInteger(1, false, true));
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public void ReadInteger_SignedValuesAreSignExtended()
    {
        var stream = Stream(0xFF, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        Assert.Equal(-1, stream.ReadInteger(1, true, true));
        Assert.Equal(-2, stream.ReadInteger(1, true, true));
        Assert.Equal(-1, stream.ReadInteger(8, true, false));
    }

    [Fact]
    public void ReadInteger_UnsignedEightBytesKeepsTheHighBit()
    {
        var stream = Stream(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        Assert.Equal((Int128)ulong.MaxValue, stream.ReadInteger(8, false, true));
    }

    [Fact]
    public void ReadFloat_ReadsSingleAndDouble()
    {
        var stream = Stream(0x3F, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40);

        Assert.Equal(1.5, stream.ReadFloat(4, true));
        Assert.Equal(2.5, stream.ReadFloat(8, false));
    }

    [Fact]
    public void ReadBits_BigEndianReadsFromTheHighBit()
    {
        var stream = Stream(0b1010_0110, 0b1100_0000);

        Assert.Equal(0b101UL, stream.ReadBits(3, true));
        Assert.Equal(0b00110_11UL, stream.ReadBits(7, true));
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void ReadBits_LittleEndianReadsFromTheLowBit()
    {
        var stream = Stream(0b1010_0110, 0b0000_0011);

        Assert.Equal(0b110UL, stream.ReadBits(3, false));
        Assert.Equal(0b11_10100UL, stream.ReadBits(7, false));
    }

    [Fact]
    public void ReadBits_SixtyFourBitsAfterAPartialByte()
    {
        var stream = Stream(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        Assert.Equal(1UL, stream.ReadBits(1, true));
        Assert.Equal(ulong.MaxValue, stream.ReadBits(64, true));
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public void ReadInteger_AfterBits_AlignsToTheNextByte()
    {
        var stream = Stream(0xF0, 0x12);

        stream.ReadBits(4, true);
        Assert.Equal(0x12, stream.ReadInteger(1, false, true));
    }

    [Fact]
    public void IsEof_IsFalseWhileBitsRemain()
    {
        var stream = Stream(0xFF);

        stream.ReadBits(4, true);
        Assert.False(stream.IsEof);
        stream.ReadBits(4, true);
        Assert.True(stream.IsEof);
    }

    [Fact]
    public void ReadBeyondTheEnd_ThrowsParseException()
    {
        var stream = Stream(0x01, 0x02, 0x03);
        stream.ReadInteger(2, false, true);

        var error = Assert.Throws<KsyParseException>(() => stream.ReadInteger(4, false, true));

        Assert.Contains("needed 4 byte(s) at position 2, but only 1 remain", error.Message);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void Substream_IsAWindowWithItsOwnPosition()
    {
        var parent = Stream(0x00, 0x11, 0x22, 0x33, 0x44);

        var sub = parent.Substream(1, 3);

        Assert.Equal(3, sub.Size);
        Assert.Equal(0x1122, sub.ReadInteger(2, false, true));
        Assert.Equal(3, sub.FileOffset(sub.Position));
        Assert.Throws<KsyParseException>(() => sub.ReadInteger(2, false, true));
        Assert.Equal(0, parent.Position);
    }

    [Fact]
    public void FromBytes_IsNotFileBacked()
    {
        var stream = KsStream.FromBytes([0x01]);

        Assert.False(stream.IsFileBacked);
        Assert.False(stream.Substream(0, 1).IsFileBacked);
        Assert.True(Stream(0x01).IsFileBacked);
    }

    [Fact]
    public void Seek_MovesAndDropsTheBitState()
    {
        var stream = Stream(0xFF, 0x0F, 0xAB);
        stream.ReadBits(4, true);

        stream.Seek(2);

        Assert.Equal(0xAB, stream.ReadInteger(1, false, true));
        Assert.Throws<KsyParseException>(() => stream.Seek(4));
        Assert.Throws<KsyParseException>(() => stream.Seek(-1));
    }

    [Fact]
    public void RestoreState_ReturnsToTheSavedPositionAndBits()
    {
        var stream = Stream(0b1010_0000, 0x00, 0x00);
        stream.ReadBits(1, true);
        var saved = stream.SaveState();

        stream.Seek(2);
        stream.RestoreState(saved);

        Assert.Equal(0UL, stream.ReadBits(1, true));
        Assert.Equal(1UL, stream.ReadBits(1, true));
    }
}
