using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class BmpFormatTests
{
    private static byte[] U2(int value) => [(byte)value, (byte)(value >> 8)];

    private static byte[] U4(long value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];

    private static byte[] Bmp(byte[] dib, byte[] palette, byte[] bits)
    {
        var offset = 14 + dib.Length + palette.Length;
        return [(byte)'B', (byte)'M', .. U4(offset + bits.Length), 0, 0, 0, 0, .. U4(offset), .. dib, .. palette, .. bits];
    }

    private static byte[] InfoHeader(int size, int bitsPerPixel, int compression, int colorsUsed) =>
    [
        .. U4(size), .. U4(1), .. U4(1), .. U2(1), .. U2(bitsPerPixel), .. U4(compression), .. U4(4),
        .. U4(2835), .. U4(2835), .. U4(colorsUsed), .. U4(0),
    ];

    [Fact]
    public void Default_DetectsBmpByItsSignature()
    {
        Assert.Equal("bmp", Detect(Bmp([.. U4(12), .. U2(1), .. U2(1), .. U2(1), .. U2(1)], [0, 0, 0, 255, 255, 255], [0x80, 0, 0, 0])));
    }

    [Fact]
    public void Parse_CoreHeaderUsesThreeBytePaletteEntries()
    {
        var root = Parse("image/bmp",
            Bmp([.. U4(12), .. U2(1), .. U2(1), .. U2(1), .. U2(1)], [0, 0, 0, 255, 255, 255], [0x80, 0, 0, 0]));

        Assert.Equal("", Errors(root));
        Assert.Equal("file_hdr,dib_info,bitmap", Names(root));
        Assert.Equal((18L, 8L, "true"), (Node(root, "dib_info/header").Offset, Node(root, "dib_info/header").Length,
            Node(root, "dib_info/header/is_core_header").Description));
        Assert.Equal("colors[0],colors[1]", Names(Node(root, "dib_info/color_table/colors")));
        Assert.Equal((29L, 3L), (Node(root, "dib_info/color_table/colors/colors[1]").Offset,
            Node(root, "dib_info/color_table/colors/colors[1]").Length));
        Assert.Equal((32L, 4L), (Node(root, "bitmap").Offset, Node(root, "bitmap").Length));
    }

    [Fact]
    public void Parse_InfoHeaderWithAPaletteReadsTheUsedColors()
    {
        var root = Parse("image/bmp", Bmp(InfoHeader(40, 8, 0, 2), [0, 0, 0, 0, 255, 255, 255, 0], [1, 0, 0, 0]));

        Assert.Equal("", Errors(root));
        Assert.Equal("2", Node(root, "dib_info/header/bitmap_info_ext/num_colors_used").Description);
        Assert.Equal((58L, 4L), (Node(root, "dib_info/color_table/colors/colors[1]").Offset,
            Node(root, "dib_info/color_table/colors/colors[1]").Length));
        Assert.Equal((62L, 4L), (Node(root, "bitmap").Offset, Node(root, "bitmap").Length));
    }

    [Fact]
    public void Parse_V5HeaderReadsTheColorMasksAndTheExtensions()
    {
        byte[] v5 =
        [
            .. InfoHeader(124, 32, 3, 0), .. U4(0x00FF0000), .. U4(0x0000FF00), .. U4(0x000000FF), .. U4(0xFF000000),
            .. U4(0x73524742), .. new byte[48], .. U4(4), .. U4(0), .. U4(0), .. U4(0),
        ];

        var root = Parse("image/bmp", Bmp(v5, [], [1, 2, 3, 4]));

        Assert.Equal("", Errors(root));
        Assert.Equal((18L, 120L), (Node(root, "dib_info/header").Offset, Node(root, "dib_info/header").Length));
        Assert.Equal("1934772034 (s_rgb)", Node(root, "dib_info/header/bitmap_v4_ext/color_space_type").Description);
        Assert.Equal((122L, 16L), (Node(root, "dib_info/header/bitmap_v5_ext").Offset,
            Node(root, "dib_info/header/bitmap_v5_ext").Length));
        Assert.Equal("16711680", Node(root, "dib_info/color_mask_red").Description);
        Assert.Equal((138L, 4L), (Node(root, "bitmap").Offset, Node(root, "bitmap").Length));
    }
}
