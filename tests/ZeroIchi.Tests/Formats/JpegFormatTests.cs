using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class JpegFormatTests
{
    private static byte[] Segment(byte marker, params byte[] data) =>
        [0xFF, marker, (byte)((data.Length + 2) >> 8), (byte)(data.Length + 2), .. data];

    private static readonly byte[] Header =
    [
        0xFF, 0xD8,
        .. Segment(0xC0, 8, 0, 1, 0, 1, 1, 1, 0x11, 0),
        .. Segment(0xDA, 1, 1, 0, 0, 0x3F, 0),
    ];

    [Fact]
    public void Parse_ReadsTheScanUpToTheNextMarker()
    {
        var root = Parse("image/jpeg", [.. Header, 0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD9]);

        Assert.Equal("", Errors(root));
        Assert.Equal("segments[0],segments[1],segments[2],segments[3]", Names(Node(root, "segments")));
        var scan = Node(root, "segments/segments[2]/image_data");
        Assert.Equal((25L, 4L, "12 FF 00 34"), (scan.Offset, scan.Length, scan.Description));
        Assert.Equal("217 (eoi)", Node(root, "segments/segments[3]/marker").Description);
    }

    [Fact]
    public void Parse_RestartMarkerStartsASegmentWithItsOwnScanData()
    {
        var root = Parse("image/jpeg", [.. Header, 0x12, 0xFF, 0xD0, 0x34, 0x56, 0xFF, 0xD9]);

        Assert.Equal("", Errors(root));
        var restart = Node(root, "segments/segments[3]");
        Assert.Equal("magic,marker,image_data", Names(restart));
        Assert.Equal("208 (rst0)", Node(restart, "marker").Description);
        Assert.Equal((28L, 2L), (Node(restart, "image_data").Offset, Node(restart, "image_data").Length));
    }

    [Fact]
    public void Parse_FillBytesBeforeTheNextMarkerStayInTheScanData()
    {
        var root = Parse("image/jpeg", [.. Header, 0x12, 0xFF, 0xFF, 0xFF, 0xD9]);

        Assert.Equal("", Errors(root));
        Assert.Equal("12 FF FF", Node(root, "segments/segments[2]/image_data").Description);
        Assert.Equal((28L, 2L), (Node(root, "segments/segments[3]").Offset, Node(root, "segments/segments[3]").Length));
    }

    [Fact]
    public void Parse_ScanWithoutAnEndMarkerRunsToTheEndOfTheFile()
    {
        var root = Parse("image/jpeg", [.. Header, 0x12, 0x34, 0x56]);

        Assert.Equal("", Errors(root));
        Assert.Equal((25L, 3L), (Node(root, "segments/segments[2]/image_data").Offset,
            Node(root, "segments/segments[2]/image_data").Length));
    }
}
