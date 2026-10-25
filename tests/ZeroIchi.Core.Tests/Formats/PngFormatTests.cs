using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class PngFormatTests
{
    private static byte[] Chunk(string type, params byte[] data) =>
    [
        (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length,
        .. System.Text.Encoding.ASCII.GetBytes(type), .. data, 0, 0, 0, 0,
    ];

    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        .. Chunk("IHDR", 0, 0, 0, 2, 0, 0, 0, 3, 8, 2, 0, 0, 0),
        .. Chunk("IDAT", 0x78, 0x9C, 0x63, 0x00),
        .. Chunk("IEND"),
    ];

    [Fact]
    public void Default_DetectsPngByItsSignature()
    {
        Assert.Equal("png", Detect(Png));
    }

    [Fact]
    public void Parse_ReadsTheHeaderAndTheChunks()
    {
        var root = Parse("image/png", Png);

        Assert.Equal("", Errors(root));
        Assert.Equal("magic,ihdr_len,ihdr_type,ihdr,ihdr_crc,chunks", Names(root));
        var ihdr = Node(root, "ihdr");
        Assert.Equal((16L, 13L), (ihdr.Offset, ihdr.Length));
        Assert.Equal("2", Node(root, "ihdr/width").Description);
        Assert.Equal("chunks[0],chunks[1]", Names(Node(root, "chunks")));
        Assert.Equal((33L, 16L), (Node(root, "chunks/chunks[0]").Offset, Node(root, "chunks/chunks[0]").Length));
    }
}
