using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Tests;

public sealed class MappedByteBufferTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MappedByteBufferTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static FileStream OpenForWrite(string path) =>
        new(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

    [Fact]
    public void ReadByte_AfterReleaseMapping_ThrowsObjectDisposed()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([1, 2, 3]));
        buffer.ReleaseMapping();

        Assert.Throws<ObjectDisposedException>(() => buffer.ReadByte(0));
    }

    [Fact]
    public void ReadBytes_AfterReleaseMapping_ThrowsObjectDisposed()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([1, 2, 3]));
        buffer.ReleaseMapping();

        var dest = new byte[3];
        Assert.Throws<ObjectDisposedException>(() => buffer.ReadBytes(0, dest, 0, 3));
    }

    [Fact]
    public void RestoreMapping_AfterRelease_ReadsAgain()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([1, 2, 3]));
        buffer.ReleaseMapping();

        buffer.RestoreMapping();

        Assert.Equal(2, buffer.ReadByte(1));
    }

    [Fact]
    public void RestoreMapping_WhileMapped_KeepsReading()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([1, 2, 3]));

        buffer.RestoreMapping();

        Assert.Equal(1, buffer.ReadByte(0));
    }

    [Fact]
    public void EmptyFile_ReleaseAndRestore_ReadsNothing()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([]));
        buffer.ReleaseMapping();
        buffer.RestoreMapping();

        var dest = new byte[4];
        buffer.ReadBytes(0, dest, 0, 4);
        Assert.Equal(0, buffer.Length);
    }

    [Fact]
    public void RestoreMapping_FileShrunk_StaysUnmapped()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var buffer = new MappedByteBuffer(path);
        buffer.ReleaseMapping();
        File.WriteAllBytes(path, [1]);

        buffer.RestoreMapping();

        Assert.Throws<ObjectDisposedException>(() => buffer.ReadByte(0));
    }

    [Fact]
    public void RestoreMapping_FileDeleted_StaysUnmapped()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var buffer = new MappedByteBuffer(path);
        buffer.ReleaseMapping();
        File.Delete(path);

        buffer.RestoreMapping();

        Assert.Throws<ObjectDisposedException>(() => buffer.ReadByte(0));
    }

    [Fact]
    public void IsMapped_TracksReleaseAndRestore()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([1, 2, 3]));
        Assert.True(buffer.IsMapped);

        buffer.ReleaseMapping();
        Assert.False(buffer.IsMapped);

        buffer.RestoreMapping();
        Assert.True(buffer.IsMapped);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3 })]
    public void NewBuffer_RejectsWriteAccessFromOthers(byte[] content)
    {
        var path = CreateTestFile(content);
        using var buffer = new MappedByteBuffer(path);

        Assert.Throws<IOException>(() => OpenForWrite(path).Dispose());
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3 })]
    public void RestoreMapping_RejectsWriteAccessFromOthersAgain(byte[] content)
    {
        var path = CreateTestFile(content);
        using var buffer = new MappedByteBuffer(path);
        buffer.ReleaseMapping();
        OpenForWrite(path).Dispose();

        buffer.RestoreMapping();

        Assert.Throws<IOException>(() => OpenForWrite(path).Dispose());
    }

    [Fact]
    public void IsMapped_EmptyFile_AlwaysTrue()
    {
        using var buffer = new MappedByteBuffer(CreateTestFile([]));

        buffer.ReleaseMapping();

        Assert.True(buffer.IsMapped);
    }
}
