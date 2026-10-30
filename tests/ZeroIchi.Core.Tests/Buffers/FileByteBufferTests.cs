using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Tests.Buffers;

public sealed class FileByteBufferTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public FileByteBufferTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void Length_IsTheFileLength()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([1, 2, 3]));

        Assert.Equal(3, buffer.Length);
    }

    [Fact]
    public void ReadByte_ReturnsTheByteAtTheIndex()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([1, 2, 3]));

        Assert.Equal(3, buffer.ReadByte(2));
    }

    [Fact]
    public void ReadBytes_CopiesRangeToBufferOffset()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([1, 2, 3, 4, 5]));

        var dest = new byte[5];
        buffer.ReadBytes(1, dest, 2, 3);

        Assert.Equal([0, 0, 2, 3, 4], dest);
    }

    [Fact]
    public void ReadBytes_PastEnd_CopiesOnlyAvailable()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([1, 2, 3]));

        var dest = new byte[4];
        buffer.ReadBytes(1, dest, 0, 4);

        Assert.Equal([2, 3, 0, 0], dest);
    }

    [Fact]
    public void ReadBytes_AtTheEnd_CopiesNothing()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([1, 2, 3]));

        var dest = new byte[2];
        buffer.ReadBytes(3, dest, 0, 2);

        Assert.Equal([0, 0], dest);
    }

    [Fact]
    public void ReadByte_PastEnd_Throws()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([1, 2, 3]));

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.ReadByte(3));
    }

    [Fact]
    public void Length_OfAnEmptyFile_IsZero()
    {
        using var buffer = new FileByteBuffer(CreateTestFile([]));

        Assert.Equal(0, buffer.Length);
    }

    [Fact]
    public void Open_WhileTheFileIsMapped_ReadsTheSameBytes()
    {
        var path = CreateTestFile([9, 8, 7, 6]);
        using var mapped = new MappedByteBuffer(path);
        using var buffer = new FileByteBuffer(path);

        Assert.Equal(mapped.SliceToArray(0, 4), buffer.SliceToArray(0, 4));
    }

    [Fact]
    public void Dispose_ReleasesTheFile()
    {
        var path = CreateTestFile([1, 2, 3]);
        var buffer = new FileByteBuffer(path);

        buffer.Dispose();
        File.Delete(path);

        Assert.False(File.Exists(path));
    }
}
