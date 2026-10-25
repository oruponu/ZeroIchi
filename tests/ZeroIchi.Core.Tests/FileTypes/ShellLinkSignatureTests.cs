using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class ShellLinkSignatureTests
{
    private static byte[] ShellLink(byte firstClsidByte = 0x01)
    {
        var data = new byte[0x4C];
        data[0] = 0x4C;
        byte[] clsid =
            [firstClsidByte, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];
        clsid.CopyTo(data, 4);
        return data;
    }

    [Fact]
    public void ShellLink_IsLnk()
    {
        var detected = FileTypeDetector.Default.Detect(new ArrayByteBuffer(ShellLink()));

        Assert.Equal("application/x-ms-shortcut", detected?.MimeType);
        Assert.Equal("lnk", detected?.Extension);
    }

    [Fact]
    public void WrongClsid_ReturnsNull()
    {
        Assert.Null(ShellLinkSignature.Detect(new ArrayByteBuffer(ShellLink(0x02))));
    }
}
