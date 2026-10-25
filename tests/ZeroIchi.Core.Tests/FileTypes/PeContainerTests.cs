using System.Buffers.Binary;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class PeContainerTests
{
    private const int PeOffset = 0x80;

    private static byte[] Pe(ushort characteristics, int peOffset = PeOffset, bool peSignature = true)
    {
        var data = new byte[0x200];
        data[0] = (byte)'M';
        data[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x3C), peOffset);
        if (peSignature) "PE\0\0"u8.CopyTo(data.AsSpan(PeOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PeOffset + 4), 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PeOffset + 22), characteristics);
        return data;
    }

    private static DetectedFileType? Detect(byte[] data) => FileTypeDetector.Default.Detect(new ArrayByteBuffer(data));

    [Fact]
    public void Executable_IsExe()
    {
        var detected = Detect(Pe(0x0102));

        Assert.Equal("application/x-dosexec", detected?.MimeType);
        Assert.Equal("exe", detected?.Extension);
    }

    [Fact]
    public void Library_IsDll()
    {
        var detected = Detect(Pe(0x2102));

        Assert.Equal("application/x-msdownload", detected?.MimeType);
        Assert.Equal("dll", detected?.Extension);
    }

    [Theory]
    [InlineData(0x7FFFFFFF)]
    [InlineData(-4)]
    public void PeOffsetOutsideTheFile_FallsBackToMagic(int peOffset)
    {
        var data = Pe(0x0102, peOffset);

        Assert.Null(PeContainer.Detect(new ArrayByteBuffer(data)));
        var detected = Detect(data);
        Assert.NotNull(detected);
        Assert.True(FileTypeDetector.Default.IsA(detected.MimeType, "application/x-msdownload"), detected.MimeType);
    }

    [Fact]
    public void MzWithoutPeHeader_FallsBackToMagic()
    {
        var data = Pe(0x0102, peSignature: false);

        Assert.Null(PeContainer.Detect(new ArrayByteBuffer(data)));
        Assert.Equal("dll", Detect(data)?.Extension);
    }
}
