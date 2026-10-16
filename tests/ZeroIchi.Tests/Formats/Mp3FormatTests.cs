using System.Text;
using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class Mp3FormatTests
{
    private static readonly byte[] Audio = [0xFF, 0xFB, 0x90, 0x00];

    private static byte[] Synchsafe(int value) =>
        [(byte)((value >> 21) & 0x7F), (byte)((value >> 14) & 0x7F), (byte)((value >> 7) & 0x7F), (byte)(value & 0x7F)];

    private static byte[] Tag(int version, byte flags, params byte[][] parts)
    {
        byte[] body = [.. parts.SelectMany(p => p)];
        return [.. "ID3"u8, (byte)version, 0, flags, .. Synchsafe(body.Length), .. body];
    }

    private static byte[] FrameV22(string id, byte[] data) =>
        [.. Encoding.ASCII.GetBytes(id), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length, .. data];

    private static byte[] FrameV23(string id, byte[] data) =>
        [.. Encoding.ASCII.GetBytes(id), 0, 0, (byte)(data.Length >> 8), (byte)data.Length, 0, 0, .. data];

    private static byte[] FrameV24(string id, byte[] data) => [.. Encoding.ASCII.GetBytes(id), .. Synchsafe(data.Length), 0, 0, .. data];

    [Fact]
    public void Default_DetectsMp3ByTheId3Tag()
    {
        Assert.Equal("mp3", Detect([.. Tag(3, 0, FrameV23("TIT2", [0, 0x61])), .. Audio]));
    }

    [Fact]
    public void Parse_ReadsAVersion22Tag()
    {
        var root = Parse("media/mp3",
            [.. Tag(2, 0, FrameV22("TT2", [0, 0x61, 0x62]), FrameV22("TP1", [0, 0x63]), new byte[5]), .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal("header,frames,padding", Names(Node(root, "tag")));
        Assert.Equal("TP1", Node(root, "tag/frames/frames[1]/id").Description);
        Assert.Equal((27L, 5L), (Node(root, "tag/padding").Offset, Node(root, "tag/padding").Length));
        Assert.Equal((32L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_Version23KeepsReadingFramesAfterALargeFirstFrame()
    {
        var root = Parse("media/mp3",
            [.. Tag(3, 0, FrameV23("TIT2", [0, .. new byte[49]]), FrameV23("TALB", [0, 0x62]), new byte[12]), .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal("frames[0],frames[1],frames[2]", Names(Node(root, "tag/frames")));
        Assert.Equal("TALB", Node(root, "tag/frames/frames[1]/id").Description);
        Assert.Equal("true", Node(root, "tag/frames/frames[2]/is_invalid").Description);
        Assert.Equal((92L, 2L), (Node(root, "tag/padding").Offset, Node(root, "tag/padding").Length));
        Assert.Equal((94L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_Version24UsesSynchsafeFrameSizesAndTheExtendedHeader()
    {
        byte[] extended = [.. Synchsafe(6), 1, 0];

        var root = Parse("media/mp3",
            [.. Tag(4, 0x40, extended, FrameV24("TIT2", [0, .. new byte[200]]), FrameV24("TPE1", [0, 0x79]), new byte[7]), .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal((10L, 6L), (Node(root, "tag/header_ex").Offset, Node(root, "tag/header_ex").Length));
        Assert.Equal("201", Node(root, "tag/frames/frames[0]/size/value").Description);
        Assert.Equal((227L, 12L), (Node(root, "tag/frames/frames[1]").Offset, Node(root, "tag/frames/frames[1]").Length));
        Assert.Equal((239L, 7L), (Node(root, "tag/padding").Offset, Node(root, "tag/padding").Length));
        Assert.Equal((246L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_Version24FooterIsPartOfTheTag()
    {
        var frame = FrameV24("TIT2", [0, 0x61, 0x62]);
        byte[] footer = [.. "3DI"u8, 4, 0, 0x10, .. Synchsafe(frame.Length)];

        var root = Parse("media/mp3", [.. Tag(4, 0x10, frame), .. footer, .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal("header,frames,footer", Names(Node(root, "tag")));
        Assert.Equal((33L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Parse_EmptyTagHasNoFrames(int version)
    {
        var root = Parse("media/mp3", [.. Tag(version, 0), .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal("header,padding", Names(Node(root, "tag")));
        Assert.Equal((10L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_PaddingShorterThanAFrameHeaderIsReadAsPadding()
    {
        var root = Parse("media/mp3", [.. Tag(3, 0, new byte[5]), .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal((10L, 5L), (Node(root, "tag/padding").Offset, Node(root, "tag/padding").Length));
        Assert.Equal((15L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_Version24FooterWithoutFramesIsNotReadAsAFrame()
    {
        byte[] footer = [.. "3DI"u8, 4, 0, 0x10, .. Synchsafe(0)];

        var root = Parse("media/mp3", [.. Tag(4, 0x10), .. footer, .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal("header,footer", Names(Node(root, "tag")));
        Assert.Equal((20L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_UnknownVersionKeepsTheTagAsBytes()
    {
        var root = Parse("media/mp3", [.. Tag(5, 0, new byte[4]), .. Audio]);

        Assert.Equal("", Errors(root));
        Assert.Equal((0L, 14L), (Node(root, "tag").Offset, Node(root, "tag").Length));
        Assert.Equal((14L, 4L), (Node(root, "audio_data").Offset, Node(root, "audio_data").Length));
    }

    [Fact]
    public void Parse_TagLongerThanTheFileIsADataError()
    {
        byte[] data = [.. "ID3"u8, 3, 0, 0, .. Synchsafe(100), .. FrameV23("TIT2", [0, 0x61])];

        var root = Parse("media/mp3", data);

        Assert.Equal("EndOfData(0, 110, 22)", Summary(Node(root, "tag").Error));
    }
}
