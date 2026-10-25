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
    private const uint NoStream = 0xFFFFFFFF;

    private static readonly byte[] MsiClsid =
        [0x84, 0x10, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    private sealed record Node(string Name, byte Type, uint Left = NoStream, uint Right = NoStream,
        uint Child = NoStream);

    private static byte[] Cfb(string stream, byte[]? rootClsid = null, uint directoryNext = EndOfChain,
        uint directoryStart = 1) =>
        Cfb([new Node(stream, 2)], rootClsid, directoryNext, directoryStart);

    private static byte[] Cfb(Node[] nodes, byte[]? rootClsid = null, uint directoryNext = EndOfChain,
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
        WriteEntry(directory[..128], new Node("Root Entry", 5, Child: 1), rootClsid);
        for (var i = 0; i < nodes.Length; i++)
        {
            WriteEntry(directory.Slice((i + 1) * 128, 128), nodes[i], null);
        }
        return data;
    }

    private static void WriteEntry(Span<byte> entry, Node node, byte[]? clsid)
    {
        var nameBytes = Encoding.Unicode.GetBytes(node.Name);
        nameBytes.CopyTo(entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[0x40..], (ushort)(nameBytes.Length + 2));
        entry[0x42] = node.Type;
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x44..], node.Left);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x48..], node.Right);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x4C..], node.Child);
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
    public void StreamsInsideAStorage_DoNotDecideTheType()
    {
        var data = Cfb([new Node("Workbook", 2, Right: 2), new Node("MBD0001", 1, Child: 3), new Node("WordDocument", 2)]);

        Assert.Equal("application/vnd.ms-excel", Detect(data));
    }

    [Fact]
    public void DirectoryLongerThanTheSectorLimit_DecidesFromTheEntriesRead()
    {
        const int directorySectors = 80;
        var data = Cfb("WordDocument");
        Array.Resize(ref data, 512 * (2 + directorySectors));
        var fat = data.AsSpan(512, 512);
        for (var sector = 1; sector < directorySectors; sector++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(fat[(sector * 4)..], (uint)sector + 1);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(fat[(directorySectors * 4)..], EndOfChain);

        Assert.Equal("application/msword", Detect(data));
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
