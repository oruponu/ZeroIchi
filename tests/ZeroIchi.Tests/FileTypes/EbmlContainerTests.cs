using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class EbmlContainerTests
{
    private static byte[] Ebml(params byte[][] elements)
    {
        var body = elements.SelectMany(element => element).ToArray();
        return [0x1A, 0x45, 0xDF, 0xA3, (byte)(0x80 | body.Length), .. body, 0x18, 0x53, 0x80, 0x67, 0x80];
    }

    private static byte[] Element(byte[] id, byte[] value) => [.. id, (byte)(0x80 | value.Length), .. value];

    private static byte[] Version => Element([0x42, 0x86], [1]);

    private static byte[] DocType(string value) => Element([0x42, 0x82], Encoding.ASCII.GetBytes(value));

    [Fact]
    public void WebmDocType_IsWebm()
    {
        var detected = FileTypeDetector.Default.Detect(new ArrayByteBuffer(Ebml(Version, DocType("webm"))));

        Assert.Equal("video/webm", detected?.MimeType);
        Assert.Equal("webm", detected?.Extension);
    }

    [Fact]
    public void MatroskaDocType_IsMkv()
    {
        var detected = FileTypeDetector.Default.Detect(new ArrayByteBuffer(Ebml(Version, DocType("matroska"))));

        Assert.Equal("video/x-matroska", detected?.MimeType);
        Assert.Equal("mkv", detected?.Extension);
    }

    [Fact]
    public void HeaderWithoutDocType_ReturnsNull()
    {
        Assert.Null(EbmlContainer.Detect(new ArrayByteBuffer(Ebml(Version))));
    }
}
