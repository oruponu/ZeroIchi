using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentDisposeTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentDisposeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void Dispose_LeavesEmptyReadableBuffer()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));

        doc.Dispose();

        Assert.Equal(0, doc.Buffer.Length);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        doc.Dispose();

        Assert.Null(Record.Exception(doc.Dispose));
    }

    [Fact]
    public void Dispose_ReleasesFileForDeletion()
    {
        var path = CreateTestFile([1, 2, 3]);
        var doc = BinaryDocument.Open(path);

        doc.Dispose();
        File.Delete(path);

        Assert.False(File.Exists(path));
    }
}
