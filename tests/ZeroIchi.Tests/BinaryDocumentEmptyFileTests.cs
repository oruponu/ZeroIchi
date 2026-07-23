using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Tests;

public class BinaryDocumentEmptyFileTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentEmptyFileTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void MappedByteBuffer_EmptyFile_BehavesAsZeroLengthBuffer()
    {
        var path = CreateFile([]);

        var buffer = new MappedByteBuffer(path);

        Assert.Equal(0, buffer.Length);

        var dest = new byte[] { 0xFF, 0xFF };
        buffer.ReadBytes(0, dest, 0, dest.Length);
        Assert.Equal([0xFF, 0xFF], dest);

        buffer.Dispose();
    }

    [Fact]
    public void Open_EmptyFile_Succeeds()
    {
        var path = CreateFile([]);

        var doc = BinaryDocument.Open(path);

        Assert.Equal(0, doc.FileSize);
        Assert.False(doc.IsModified);
        doc.ReleaseData();
    }

    [Fact]
    public async Task Open_EmptyFile_ThenAppendAndSave()
    {
        var path = CreateFile([]);

        var doc = BinaryDocument.Open(path);
        doc.AppendByte(0xAB);
        doc.Save();
        doc.ReleaseData();

        Assert.Equal([0xAB], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SaveAsAsync_EmptyNewDocument_SavesAndRemainsUsable()
    {
        var path = Path.Combine(_tempDir, "saveas.bin");

        var doc = BinaryDocument.CreateNew();
        await doc.SaveAsAsync(path);

        Assert.Equal(0, new FileInfo(path).Length);
        Assert.Equal(0, doc.FileSize);

        doc.AppendByte(0xCD);
        doc.Save();
        doc.ReleaseData();

        Assert.Equal([0xCD], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Save_DeleteAllBytes_TruncatesToEmptyAndRemainsUsable()
    {
        var path = CreateFile([1, 2, 3, 4]);

        var doc = BinaryDocument.Open(path);
        doc.DeleteBytes(0, 4);
        doc.Save();

        Assert.Equal(0, new FileInfo(path).Length);

        doc.AppendByte(0x99);
        doc.Save();
        doc.ReleaseData();

        Assert.Equal([0x99], await File.ReadAllBytesAsync(path));
    }
}
