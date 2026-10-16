using System.Text;
using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class WavFormatTests
{
    private static readonly byte[] Fmt = [1, 0, 1, 0, 0x40, 0x1F, 0, 0, 0x40, 0x1F, 0, 0, 1, 0, 8, 0];

    private static byte[] Chunk(string id, byte[] data, bool pad = true) =>
    [
        .. Encoding.ASCII.GetBytes(id), (byte)data.Length, (byte)(data.Length >> 8), 0, 0, .. data,
        .. pad && data.Length % 2 != 0 ? [(byte)0] : Array.Empty<byte>(),
    ];

    private static byte[] Wav(params byte[][] chunks)
    {
        byte[] body = [.. "WAVE"u8, .. chunks.SelectMany(c => c)];
        return [.. "RIFF"u8, (byte)body.Length, (byte)(body.Length >> 8), 0, 0, .. body];
    }

    [Fact]
    public void Parse_ReadsTheFormatChunk()
    {
        var root = Parse("media/wav", Wav(Chunk("fmt ", Fmt), Chunk("data", [1, 2])));

        Assert.Equal("", Errors(root));
        Assert.Equal("magic,len_data,form_type,chunks", Names(root));
        Assert.Equal("format_tag,num_channels,sample_rate,avg_bytes_per_sec,block_align,bits_per_sample",
            Names(Node(root, "chunks/chunks[0]/data")));
        Assert.Equal("1 (pcm)", Node(root, "chunks/chunks[0]/data/format_tag").Description);
        Assert.Equal("8000", Node(root, "chunks/chunks[0]/data/sample_rate").Description);
    }

    [Fact]
    public void Parse_SkipsThePaddingAfterAnOddSizedChunk()
    {
        var root = Parse("media/wav", Wav(Chunk("fmt ", Fmt), Chunk("data", [1, 2, 3]), Chunk("junk", [9, 9])));

        Assert.Equal("", Errors(root));
        Assert.Equal((47L, 1L), (Node(root, "chunks/chunks[1]/padding").Offset, Node(root, "chunks/chunks[1]/padding").Length));
        var next = Node(root, "chunks/chunks[2]");
        Assert.Equal((48L, 10L, "junk"), (next.Offset, next.Length, Node(next, "id").Description));
    }

    [Fact]
    public void Parse_LastOddSizedChunkMayEndWithoutPadding()
    {
        var root = Parse("media/wav", Wav(Chunk("fmt ", Fmt), Chunk("data", [1, 2, 3], pad: false)));

        Assert.Equal("", Errors(root));
        Assert.Equal("id,len_data,data", Names(Node(root, "chunks/chunks[1]")));
    }

    [Fact]
    public void Parse_ReadsInfoListItemsAsText()
    {
        byte[] info = [.. "INFO"u8, .. Chunk("INAM", "abc\0"u8.ToArray()), .. Chunk("IART", "xyz"u8.ToArray())];

        var root = Parse("media/wav", Wav(Chunk("LIST", info)));

        Assert.Equal("", Errors(root));
        var items = Node(root, "chunks/chunks[0]/data/items");
        Assert.Equal("items[0],items[1]", Names(items));
        Assert.Equal("abc", Node(items, "items[0]/value").Description);
        Assert.Equal("xyz", Node(items, "items[1]/value").Description);
    }

    [Fact]
    public void Parse_ChunkCutOffByTheEndOfTheFileIsADataError()
    {
        byte[] wav = [.. Wav(Chunk("fmt ", Fmt)), .. "data"u8, 100, 0, 0, 0, 1, 2, 3];

        var root = Parse("media/wav", wav);

        Assert.Equal("EndOfData(2C, 100, 3)", Summary(Node(root, "chunks/chunks[1]/data").Error));
    }
}
