using System.IO.Compression;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterErrorTests
{
    [Fact]
    public void Parse_EndOfDataInConvertedDataIsMarked()
    {
        var compressed = Compress([0x01, 0x02]);
        var data = new[] { (byte)compressed.Length }.Concat(compressed).ToArray();

        var root = Parse("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: body
                type: inner
                size: len
                process: zlib
            types:
              inner:
                seq:
                  - id: value
                    type: u4be
            """, data);

        Assert.Equal("EndOfData(0, 4, 2) converted", Summary(Node(root, "body/value").Error));
    }

    [Fact]
    public void Parse_EndOfDataInASizedTypeInsideConvertedDataUsesTheConvertedPosition()
    {
        var compressed = Compress([0x00, 0x01, 0x02, 0x03]);
        var data = new[] { (byte)compressed.Length }.Concat(compressed).ToArray();

        var root = Parse("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: body
                type: inner
                size: len
                process: zlib
            types:
              inner:
                seq:
                  - id: skip
                    size: 1
                  - id: part
                    type: leaf
                    size: 2
              leaf:
                seq:
                  - id: value
                    type: u4be
            """, data);

        Assert.Equal("EndOfData(1, 4, 2) converted", Summary(Node(root, "body/part/value").Error));
    }

    [Fact]
    public void Parse_FailedInstanceKeepsItsErrorWhenUsedAgain()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
            instances:
              bad:
                pos: 2
                type: u4be
              use:
                value: bad + 1
            """, [1, 2, 3]);

        Assert.Equal("EndOfData(2, 4, 1)", Summary(Node(root, "bad").Error));
        Assert.Equal("EndOfData(2, 4, 1)", Summary(Node(root, "use").Error));
    }

    [Fact]
    public void Parse_MissingTerminatorIsReportedWithItsStart()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
              - id: s
                type: strz
                encoding: ASCII
            """, [1, (byte)'a', (byte)'b']);

        var error = Node(root, "s").Error;
        Assert.Equal("MissingTerminator(00, 1)", Summary(error));
        Assert.StartsWith("End of stream reached before terminator 00", error?.Message);
    }

    [Fact]
    public void Parse_SeekBeyondTheDataIsReportedInFilePositions()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
            instances:
              far:
                pos: 10
                type: u1
            """, [1, 2, 3, 4]);

        var error = Node(root, "far").Error;
        Assert.Equal("Seek(A, 4)", Summary(error));
        Assert.Equal("Cannot seek to position 10 (stream size 4)", error?.Message);
    }

    [Fact]
    public void Parse_InvalidZlibDataKeepsTheDetail()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: body
                size: 2
                process: zlib
            """, [0x00, 0x00]);

        var error = Node(root, "body").Error;
        Assert.Equal(FileStructureErrorKind.Zlib, error?.Kind);
        Assert.Equal($"Invalid zlib data: {error?.Arguments[0]}", error?.Message);
    }

    [Fact]
    public void Parse_DecompressedSizeLimitIsReported()
    {
        var compressed = Compress(new byte[1000]);
        var data = new[] { (byte)compressed.Length }.Concat(compressed).ToArray();

        var root = Parse("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: body
                size: len
                process: zlib
            """, data, new KsyParseOptions { MaxDecompressedSize = 100 });

        Assert.Equal("DecompressLimit(100)", Summary(Node(root, "body").Error));
    }

    [Fact]
    public void Parse_ByteArrayLoadLimitIsReported()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: s
                type: str
                size: 4
                encoding: ASCII
            """, [(byte)'a', (byte)'b', (byte)'c', (byte)'d'], new KsyParseOptions { MaxBytesLoad = 2 });

        Assert.Equal("BytesLoadLimit(4, 2)", Summary(Node(root, "s").Error));
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(data);
        return output.ToArray();
    }
}
