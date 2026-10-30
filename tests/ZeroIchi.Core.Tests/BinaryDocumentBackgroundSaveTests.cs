using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentBackgroundSaveTests : IDisposable
{
    private const int FileLength = 262144;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentBackgroundSaveTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private sealed class RecordingProgress : IProgress<long>
    {
        private readonly object _lock = new();
        private readonly List<long> _values = [];

        public void Report(long value)
        {
            lock (_lock)
                _values.Add(value);
        }

        public long[] Values
        {
            get
            {
                lock (_lock)
                    return [.. _values];
            }
        }
    }

    private static byte[] MakeSequential(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
            data[i] = (byte)(i % 256);
        return data;
    }

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] WithLeadingByte(byte[] original) => [0xAA, .. original];

    private static void AssertStrictlyIncreasingTo(long[] values, long last)
    {
        Assert.NotEmpty(values);
        Assert.True(values[0] > 0);
        for (var i = 1; i < values.Length; i++)
            Assert.True(values[i] > values[i - 1]);
        Assert.Equal(last, values[^1]);
    }

    [Fact]
    public async Task SaveAsync_WhileWritingTheTempFile_KeepsTheBufferReadable()
    {
        var original = MakeSequential(FileLength);
        var expected = WithLeadingByte(original);
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        GatedByteBuffer? gate = null;
        doc.SaveSourceWrapper = s => gate = new GatedByteBuffer(s);

        var save = doc.SaveAsync();

        Assert.False(save.IsCompleted);
        Assert.Equal(expected, doc.Buffer.SliceToArray(0, (int)doc.Buffer.Length));
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        gate!.OpenAll();
        await save;
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
        Assert.False(doc.IsModified);
    }

    [Fact]
    public async Task SaveAsAsync_WhileWritingTheTempFile_KeepsTheBufferReadable()
    {
        var original = MakeSequential(FileLength);
        var expected = WithLeadingByte(original);
        var path = CreateTestFile(original);
        var target = Path.Combine(_tempDir, "target.bin");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        GatedByteBuffer? gate = null;
        doc.SaveSourceWrapper = s => gate = new GatedByteBuffer(s);

        var save = doc.SaveAsAsync(target);

        Assert.False(save.IsCompleted);
        Assert.False(File.Exists(target));
        Assert.Equal(expected, doc.Buffer.SliceToArray(0, (int)doc.Buffer.Length));
        gate!.OpenAll();
        await save;
        Assert.Equal(expected, await File.ReadAllBytesAsync(target));
        Assert.Equal(target, doc.FilePath);
    }

    [Fact]
    public async Task SaveAsAsync_ToTheSamePath_WritesInTheBackground()
    {
        var original = MakeSequential(FileLength);
        var expected = WithLeadingByte(original);
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        GatedByteBuffer? gate = null;
        doc.SaveSourceWrapper = s => gate = new GatedByteBuffer(s);

        var save = doc.SaveAsAsync(path);

        Assert.False(save.IsCompleted);
        Assert.Equal(expected, doc.Buffer.SliceToArray(0, (int)doc.Buffer.Length));
        gate!.OpenAll();
        await save;
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SaveAsync_ReportsCumulativeBytesUpToTheFileSize()
    {
        var path = CreateTestFile(MakeSequential(FileLength));
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        var progress = new RecordingProgress();

        await doc.SaveAsync(progress);

        AssertStrictlyIncreasingTo(progress.Values, FileLength + 1);
    }

    [Fact]
    public async Task SaveAsAsync_ReportsCumulativeBytesUpToTheFileSize()
    {
        var path = CreateTestFile(MakeSequential(FileLength));
        var target = Path.Combine(_tempDir, "target.bin");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        var progress = new RecordingProgress();

        await doc.SaveAsAsync(target, progress);

        AssertStrictlyIncreasingTo(progress.Values, FileLength + 1);
    }

    [Fact]
    public async Task SaveAsync_InPlace_CompletesSynchronously()
    {
        var path = CreateTestFile(MakeSequential(FileLength));
        using var doc = BinaryDocument.Open(path);
        doc.WriteByte(0, 0xFF);
        var progress = new RecordingProgress();

        var save = doc.SaveAsync(progress);

        Assert.True(save.IsCompletedSuccessfully);
        Assert.Empty(progress.Values);
        await save;
    }

    [Fact]
    public async Task SaveAsync_WhenTheTempFileCannotBeCreated_KeepsTheOriginalAndTheDocumentReadable()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);
        Directory.CreateDirectory(path + ".zitmp");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(50, [0xAA]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => doc.SaveAsync());

        var view = doc.Buffer.SliceToArray(0, 101);
        Assert.Equal(0xAA, view[50]);
        Assert.True(doc.IsModified);
        doc.Dispose();
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SaveAsAsync_EmptyDocument_CreatesAnEmptyFile()
    {
        var target = Path.Combine(_tempDir, "empty.bin");
        using var doc = BinaryDocument.CreateNew();

        await doc.SaveAsAsync(target);

        Assert.Equal(0, new FileInfo(target).Length);
    }
}
