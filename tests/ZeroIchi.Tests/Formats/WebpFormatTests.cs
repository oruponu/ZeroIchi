using System.Text;
using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class WebpFormatTests
{
    private static byte[] Chunk(string name, byte[] data, byte padding = 0) =>
    [
        .. Encoding.ASCII.GetBytes(name), (byte)data.Length, (byte)(data.Length >> 8), 0, 0, .. data,
        .. data.Length % 2 != 0 ? [padding] : Array.Empty<byte>(),
    ];

    private static byte[] Webp(params byte[][] chunks)
    {
        byte[] body = [.. "WEBP"u8, .. chunks.SelectMany(c => c)];
        return [.. "RIFF"u8, (byte)body.Length, (byte)(body.Length >> 8), 0, 0, .. body];
    }

    [Fact]
    public void Parse_AcceptsANonZeroPaddingByte()
    {
        var root = Parse("image/webp", Webp(Chunk("VP8L", [0x2F, 0, 0, 0, 0], padding: 0xAA)));

        Assert.Equal("", Errors(root));
        var chunk = Node(root, "payload/chunks/chunks[0]");
        Assert.Equal((12L, 14L), (chunk.Offset, chunk.Length));
        Assert.Equal((25L, 1L, "AA"), (Node(chunk, "padding").Offset, Node(chunk, "padding").Length,
            Node(chunk, "padding").Description));
    }

    [Fact]
    public void Parse_KeepsReadingAfterAnUnknownChunk()
    {
        var root = Parse("image/webp", Webp(Chunk("ABCD", "xyz"u8.ToArray(), padding: 0x55), Chunk("VP8L", [0x2F, 0, 0, 0, 0])));

        Assert.Equal("", Errors(root));
        Assert.Equal("chunks[0],chunks[1]", Names(Node(root, "payload/chunks")));
        Assert.Equal((12L, 12L), (Node(root, "payload/chunks/chunks[0]").Offset, Node(root, "payload/chunks/chunks[0]").Length));
        Assert.Equal("1278758998 (vp8l)", Node(root, "payload/chunks/chunks[1]/name").Description);
    }
}
