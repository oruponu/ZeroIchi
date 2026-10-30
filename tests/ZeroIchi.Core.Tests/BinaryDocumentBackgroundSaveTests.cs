using System.Collections.Concurrent;
using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.Commands;

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

    private sealed class FreshInstancePumpContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue;

        public FreshInstancePumpContext() : this(new BlockingCollection<(SendOrPostCallback, object?)>())
        {
        }

        private FreshInstancePumpContext(BlockingCollection<(SendOrPostCallback, object?)> queue) => _queue = queue;

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override SynchronizationContext CreateCopy() => new FreshInstancePumpContext(_queue);

        public void Run(Func<Task> action, Action beforeEachCallback)
        {
            var previous = Current;
            try
            {
                SetSynchronizationContext(this);
                var task = action();
                task.ContinueWith(_ => _queue.CompleteAdding(), TaskScheduler.Default);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                {
                    SetSynchronizationContext(new FreshInstancePumpContext(_queue));
                    beforeEachCallback();
                    callback(state);
                }
                task.GetAwaiter().GetResult();
            }
            finally
            {
                SetSynchronizationContext(previous);
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
    public void SaveAsync_DoesNotYieldWhileTheOriginalIsUnmapped()
    {
        var original = MakeSequential(FileLength);
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        GatedByteBuffer? gate = null;
        doc.SaveSourceWrapper = s => gate = new GatedByteBuffer(s);
        var callbacksWhileUnmapped = 0;

        new FreshInstancePumpContext().Run(
            () =>
            {
                var save = doc.SaveAsync();
                gate!.OpenAll();
                return save;
            },
            () =>
            {
                if (!doc.IsUsable)
                    callbacksWhileUnmapped++;
            });

        Assert.Equal(0, callbacksWhileUnmapped);
        Assert.Equal(WithLeadingByte(original), File.ReadAllBytes(path));
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
    public async Task SaveAsync_AfterAFailedWrite_CanSaveAgain()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);
        Directory.CreateDirectory(path + ".zitmp");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => doc.SaveAsync());
        Directory.Delete(path + ".zitmp");

        await doc.SaveAsync();

        Assert.Equal(WithLeadingByte(original), await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public void Save_WritesTheTempFileWithoutReadingTheOriginalMapping()
    {
        var original = MakeSequential(FileLength);
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        doc.SaveSourceWrapper = s =>
        {
            ((MappedByteBuffer)doc.Original).ReleaseMapping();
            return s;
        };

        doc.Save();

        Assert.Equal(WithLeadingByte(original), File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_WhenTheOriginalIsUnmappedAndTheFileShrank_ThrowsWithoutWriting()
    {
        var original = MakeSequential(FileLength);
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        ((MappedByteBuffer)doc.Original).ReleaseMapping();
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write))
            fs.SetLength(10);

        Assert.Throws<ObjectDisposedException>(doc.Save);

        Assert.Equal(original[..10], File.ReadAllBytes(path));
    }

    [Fact]
    public async Task SaveAsAsync_WritesTheTempFileWithoutReadingTheOriginalMapping()
    {
        var original = MakeSequential(FileLength);
        var path = CreateTestFile(original);
        var target = Path.Combine(_tempDir, "copy.bin");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        doc.SaveSourceWrapper = s =>
        {
            ((MappedByteBuffer)doc.Original).ReleaseMapping();
            return s;
        };

        await doc.SaveAsAsync(target);

        Assert.Equal(WithLeadingByte(original), await File.ReadAllBytesAsync(target));
    }

    [Fact]
    public async Task SaveAsAsync_EmptyDocument_CreatesAnEmptyFile()
    {
        var target = Path.Combine(_tempDir, "empty.bin");
        using var doc = BinaryDocument.CreateNew();

        await doc.SaveAsAsync(target);

        Assert.Equal(0, new FileInfo(target).Length);
    }

    [Fact]
    public async Task WhileSaving_RejectsChangesAndAnotherSave()
    {
        var original = MakeSequential(FileLength);
        var expected = WithLeadingByte(original);
        var path = CreateTestFile(original);
        var otherPath = Path.Combine(_tempDir, "other.bin");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA]);
        GatedByteBuffer? gate = null;
        doc.SaveSourceWrapper = s => gate = new GatedByteBuffer(s);

        var save = doc.SaveAsync();

        Assert.True(doc.IsSaving);
        Assert.Throws<InvalidOperationException>(() => doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0x22, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => doc.Undo());
        Assert.Throws<InvalidOperationException>(() => doc.Redo());
        Assert.Throws<InvalidOperationException>(() => doc.Save());
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsAsync(otherPath));
        gate!.OpenAll();
        await save;
        Assert.False(doc.IsSaving);
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task WhileSaving_RejectsLowLevelEdits()
    {
        var original = MakeSequential(FileLength);
        var expected = WithLeadingByte(original);
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        var edit = doc.InsertBytes(0, [0xAA]);
        GatedByteBuffer? gate = null;
        doc.SaveSourceWrapper = s => gate = new GatedByteBuffer(s);

        var save = doc.SaveAsync();

        Assert.Throws<InvalidOperationException>(() => doc.WriteByte(1, 0x22));
        Assert.Throws<InvalidOperationException>(() => doc.AppendByte(0x22));
        Assert.Throws<InvalidOperationException>(() => doc.InsertBytes(1, [0x22]));
        Assert.Throws<InvalidOperationException>(() => doc.DeleteBytes(1, 1));
        Assert.Throws<InvalidOperationException>(() => doc.UndoEdit(edit));
        Assert.Throws<InvalidOperationException>(() => doc.RedoEdit(edit));
        gate!.OpenAll();
        await save;
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task IsSaving_IsClearedWhenTheSaveFails()
    {
        var path = CreateTestFile(MakeSequential(100));
        Directory.CreateDirectory(path + ".zitmp");
        using var doc = BinaryDocument.Open(path);
        doc.InsertBytes(50, [0xAA]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => doc.SaveAsync());

        Assert.False(doc.IsSaving);
        doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0x22, 1, 1));
    }

    [Fact]
    public void IsSaving_IsFalseOutsideASave()
    {
        var path = CreateTestFile(MakeSequential(100));
        using var doc = BinaryDocument.Open(path);
        Assert.False(doc.IsSaving);

        doc.WriteByte(0, 0xFF);
        doc.Save();

        Assert.False(doc.IsSaving);
    }
}
