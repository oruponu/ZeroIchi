using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class FileTypeDetectorTests
{
    private static MimeTypeEntry Type(string name, string? parent, int priority, string value) =>
        new(name, parent, [], [new MimeMagic(priority, TikaMagicParser.Parse("string", "0", value, null, null, [], out _))]);

    private static string? Detect(FileTypeDetector detector, byte[] data) =>
        detector.Detect(new ArrayByteBuffer(data))?.MimeType;

    [Fact]
    public void EmptyFile_IsNull()
    {
        Assert.Null(FileTypeDetector.Default.Detect(new ArrayByteBuffer([])));
    }

    [Fact]
    public void UnknownBytes_AreNull()
    {
        Assert.Null(FileTypeDetector.Default.Detect(new ArrayByteBuffer(new byte[16])));
    }

    [Fact]
    public void HigherPriorityTier_Wins()
    {
        var detector = new FileTypeDetector([Type("x/high", null, 60, "AB"), Type("x/low", null, 50, "A")]);

        Assert.Equal("x/high", Detect(detector, "ABC"u8.ToArray()));
    }

    [Fact]
    public void DescendantBeatsItsAncestorInTheSameTier()
    {
        var detector = new FileTypeDetector([Type("x/child", "x/parent", 50, "AB"), Type("x/parent", null, 50, "A")]);

        Assert.Equal("x/child", Detect(detector, "ABC"u8.ToArray()));
    }

    [Fact]
    public void Ties_PickTheOrdinallyGreaterName()
    {
        var detector = new FileTypeDetector([Type("x/b", null, 50, "A"), Type("x/a", null, 50, "AB")]);

        Assert.Equal("x/b", Detect(detector, "ABC"u8.ToArray()));
    }

    [Fact]
    public void Extension_IsTheFirstGlob()
    {
        Assert.Equal("mpga", FileTypeDetector.Default.Extension("audio/mpeg"));
        Assert.Equal("tiff", FileTypeDetector.Default.Extension("image/tiff"));
    }

    [Fact]
    public void Extension_FallsBackToTheParent()
    {
        Assert.Equal("dll", FileTypeDetector.Default.Extension("application/x-msdownload;format=pe32"));
    }

    [Fact]
    public void Extension_IsEmptyWhenNoAncestorHasOne()
    {
        Assert.Equal("", FileTypeDetector.Default.Extension("application/x-sqlite3"));
    }

    public static TheoryData<string, byte[]> RequiredFormats() => new()
    {
        { "image/png", Bytes(0x89, "PNG", 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, "IHDR") },
        { "image/jpeg", Bytes(0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, "JFIF", 0) },
        { "image/bmp", At(34, (0, Bytes("BM")), (26, Bytes(1, 0, 24, 0, 0, 0, 0, 0))) },
        { "image/webp", Bytes("RIFF", 0x24, 0, 0, 0, "WEBPVP8 ") },
        { "image/gif", Bytes("GIF89a", 1, 0, 1, 0) },
        { "application/zip", Bytes("PK", 3, 4, 20, 0, 0, 0, 0, 0) },
        { "audio/vnd.wave", Bytes("RIFF", 0x24, 0, 0, 0, "WAVEfmt ") },
        { "audio/mpeg", Bytes("ID3", 4, 0, 0, 0, 0, 0, 0) },
        { "audio/mpeg", Bytes(0xFF, 0xFB, 0x90, 0x00, 0, 0, 0, 0) },
        { "audio/midi", Bytes("MThd", 0, 0, 0, 6, 0, 1) },
        { "application/pdf", Bytes("%PDF-1.7\n") },
        { "application/x-7z-compressed", Bytes("7z", 0xBC, 0xAF, 0x27, 0x1C, 0, 4) },
        { "application/x-rar-compressed", Bytes("Rar!", 0x1A, 0x07, 0x00) },
        { "application/gzip", Bytes(0x1F, 0x8B, 8, 0, 0, 0, 0, 0) },
        { "application/x-xz", Bytes(0xFD, "7zXZ", 0, 0, 4) },
        { "application/x-tar", At(512, (0, Bytes("file.txt")), (257, Bytes("ustar", 0, "00"))) },
        { "application/vnd.ms-cab-compressed", Bytes("MSCF", 0, 0, 0, 0, 0x10, 0, 0, 0) },
        { "application/x-iso9660-image", At(34816, (32769, Bytes("CD001", 1))) },
        { "application/x-elf", Bytes(0x7F, "ELF", 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0) },
        { "application/x-shockwave-flash", Bytes("FWS", 10, 0, 0, 0, 0) },
        { "application/x-debian-package", Bytes("!<arch>\ndebian-binary   ") },
        { "audio/x-flac", Bytes("fLaC", 0, 0, 0, 0x22) },
        { "audio/vorbis", At(64, (0, Bytes("OggS", 0, 2)), (28, Bytes(1, "vorbis"))) },
        { "audio/mp4", Bytes(0, 0, 0, 0x20, "ftypM4A ", 0, 0, 0, 0) },
        { "audio/x-aiff", Bytes("FORM", 0, 0, 0, 0x2E, "AIFFCOMM") },
        { "video/mp4", Bytes(0, 0, 0, 0x18, "ftypmp42", 0, 0, 0, 0) },
        { "video/quicktime", Bytes(0, 0, 0, 0x14, "ftypqt  ", 0, 0, 0, 0) },
        { "video/x-msvideo", Bytes("RIFF", 0x24, 0, 0, 0, "AVI LIST") },
        { "video/x-flv", Bytes("FLV", 1, 5, 0, 0, 0, 9) },
        { "video/x-ms-wmv", Bytes(0x30, 0x26, 0xB2, 0x75, Encoding.Unicode.GetBytes("wmv2")) },
        { "video/mpeg", Bytes(0, 0, 1, 0xBA, 0x44, 0, 4, 0) },
        { "image/vnd.adobe.photoshop", Bytes("8BPS", 0, 1, 0, 0, 0, 0) },
        { "image/tiff", Bytes("II", 0x2A, 0, 8, 0, 0, 0) },
        { "image/vnd.microsoft.icon", Bytes(0, 0, 1, 0, 1, 0, 16, 16) },
        { "image/heic", Bytes(0, 0, 0, 0x18, "ftypheic", 0, 0, 0, 0) },
        { "image/avif", Bytes(0, 0, 0, 0x1C, "ftypavif", 0, 0, 0, 0) },
        { "application/x-font-ttf", Bytes(0, 1, 0, 0, 0, 0x0E, 0, 0x80) },
        { "application/x-font-otf", Bytes("OTTO", 0, 0x0A, 0, 0x80) },
        { "application/rtf", Bytes("{\\rtf1\\ansi") },
        { "application/java-vm", Bytes(0xCA, 0xFE, 0xBA, 0xBE, 0, 0, 0, 0x34) },
        { "application/x-sqlite3", Bytes("SQLite format 3", 0, 0x10, 0) },
        { "application/x-rpm", Bytes(0xED, 0xAB, 0xEE, 0xDB, 3, 0) },
        { "application/x-bzip2", Bytes("BZh91AY&SY") },
        { "audio/x-aac", At(400, (0, Bytes(0xFF, 0xF1, 0x50, 0x80)), (200, Bytes(0xFF, 0xF1, 0x50, 0x80))) },
    };

    [Theory]
    [MemberData(nameof(RequiredFormats))]
    public void RequiredFormat_IsDetectedFromContent(string expected, byte[] data)
    {
        var detected = Detect(FileTypeDetector.Default, data);

        Assert.NotNull(detected);
        Assert.True(FileTypeDetector.Default.IsA(detected, expected), $"{detected} is not {expected}");
    }

    private static byte[] Bytes(params object[] parts)
    {
        var bytes = new List<byte>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case int value:
                    bytes.Add((byte)value);
                    break;
                case string text:
                    bytes.AddRange(Encoding.Latin1.GetBytes(text));
                    break;
                case byte[] array:
                    bytes.AddRange(array);
                    break;
            }
        }
        return bytes.ToArray();
    }

    private static byte[] At(int length, params (int Offset, byte[] Data)[] regions)
    {
        var bytes = new byte[length];
        foreach (var (offset, data) in regions) data.CopyTo(bytes, offset);
        return bytes;
    }
}
