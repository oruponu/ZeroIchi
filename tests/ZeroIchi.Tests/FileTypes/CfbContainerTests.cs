using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class CfbContainerTests
{
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FreeSector = 0xFFFFFFFF;
    private const uint FatSector = 0xFFFFFFFD;

    private static readonly byte[] MsiClsid =
        [0x84, 0x10, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    private static byte[] Cfb(string stream, byte[]? rootClsid = null, uint directoryNext = EndOfChain,
        uint directoryStart = 1)
    {
        var data = new byte[512 * 3];
        var header = data.AsSpan(0, 512);
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[0x18..], 0x3E);
        BinaryPrimitives.WriteUInt16LittleEndian(header[0x1A..], 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header[0x1C..], 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(header[0x1E..], 9);
        BinaryPrimitives.WriteUInt16LittleEndian(header[0x20..], 6);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0x2C..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0x30..], directoryStart);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0x38..], 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0x3C..], EndOfChain);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0x44..], EndOfChain);
        for (var i = 0; i < 109; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header[(0x4C + i * 4)..], i == 0 ? 0 : FreeSector);
        }

        var fat = data.AsSpan(512, 512);
        for (var i = 0; i < 128; i++) BinaryPrimitives.WriteUInt32LittleEndian(fat[(i * 4)..], FreeSector);
        BinaryPrimitives.WriteUInt32LittleEndian(fat, FatSector);
        BinaryPrimitives.WriteUInt32LittleEndian(fat[4..], directoryNext);

        var directory = data.AsSpan(1024, 512);
        WriteEntry(directory[..128], "Root Entry", 5, rootClsid);
        WriteEntry(directory[128..256], stream, 2, null);
        return data;
    }

    private static void WriteEntry(Span<byte> entry, string name, byte type, byte[]? clsid)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name);
        nameBytes.CopyTo(entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[0x40..], (ushort)(nameBytes.Length + 2));
        entry[0x42] = type;
        clsid?.CopyTo(entry[0x50..]);
    }

    private static string? Detect(byte[] data) => CfbContainer.Detect(new ArrayByteBuffer(data));

    [Theory]
    [InlineData("WordDocument", "application/msword", "doc")]
    [InlineData("Workbook", "application/vnd.ms-excel", "xls")]
    [InlineData("PowerPoint Document", "application/vnd.ms-powerpoint", "ppt")]
    [InlineData("__substg1.0_0037001F", "application/vnd.ms-outlook", "msg")]
    public void StreamName_DecidesTheType(string stream, string mime, string extension)
    {
        var detected = FileTypeDetector.Default.Detect(new ArrayByteBuffer(Cfb(stream)));

        Assert.Equal(mime, detected?.MimeType);
        Assert.Equal(extension, detected?.Extension);
    }

    [Fact]
    public void MsiClsid_IsInstaller()
    {
        var detected = FileTypeDetector.Default.Detect(new ArrayByteBuffer(Cfb("WordDocument", MsiClsid)));

        Assert.Equal("application/x-ms-installer", detected?.MimeType);
        Assert.Equal("msi", detected?.Extension);
    }

    [Fact]
    public void UnknownStreams_AreGenericOle2()
    {
        var detected = FileTypeDetector.Default.Detect(new ArrayByteBuffer(Cfb("Contents")));

        Assert.Equal("application/x-tika-msoffice", detected?.MimeType);
        Assert.Equal("", detected?.Extension);
    }

    [Fact]
    public void CyclicFatChain_StopsWithinTheLimit()
    {
        var data = Cfb("WordDocument", directoryNext: 1);

        var stopwatch = Stopwatch.StartNew();
        var detected = Detect(data);
        stopwatch.Stop();

        Assert.Equal("application/msword", detected);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"{stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void DirectorySectorOutsideTheFile_ReturnsNull()
    {
        Assert.Null(Detect(Cfb("WordDocument", directoryStart: 1000)));
    }
}
