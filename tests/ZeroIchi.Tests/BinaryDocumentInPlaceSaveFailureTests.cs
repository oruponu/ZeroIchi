using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentInPlaceSaveFailureTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentInPlaceSaveFailureTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static void WriteByte(BinaryDocument doc, long index, byte value) =>
        doc.ExecuteCommand(new WriteByteCommand(doc, index, value, cursorPositionBefore: 0, cursorPositionAfter: 0));

    private static byte[] ReadAll(BinaryDocument doc)
    {
        var bytes = new byte[doc.FileSize];
        doc.Buffer.ReadBytes(0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public async Task Save_WhenWriteFailsMidway_RestoresFileAndUndoRestoresOriginal()
    {
        byte[] original = [1, 2, 3, 4, 5, 6];
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        WriteByte(doc, 1, 0xAA);
        WriteByte(doc, 4, 0xBB);
        doc.InPlaceStreamWrapper = s => new FailingStream(s, failingWrite: 2, keepFailing: false);

        Assert.Throws<IOException>(() => doc.Save());

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.True(doc.IsModified);
        Assert.Equal([1, 0xAA, 3, 4, 0xBB, 6], ReadAll(doc));

        doc.Undo();
        doc.Undo();

        Assert.False(doc.IsModified);
        Assert.Equal(original, ReadAll(doc));
    }

    [Fact]
    public async Task Save_WhenWriteFailsAfterExtending_RestoresFileLength()
    {
        byte[] original = [1, 2, 3, 4];
        var path = CreateTestFile(original);
        using var doc = BinaryDocument.Open(path);
        WriteByte(doc, 0, 0xAA);
        doc.ExecuteCommand(new AppendByteCommand(doc, 0xCC, cursorPositionBefore: 4, cursorPositionAfter: 5));
        doc.InPlaceStreamWrapper = s => new FailingStream(s, failingWrite: 2, keepFailing: false);

        Assert.Throws<IOException>(() => doc.Save());

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.True(doc.CanUndo);
    }

    [Fact]
    public void Save_WhenRestoreAlsoFails_ClearsHistoryAndStaysModified()
    {
        var path = CreateTestFile([1, 2, 3, 4, 5, 6]);
        using var doc = BinaryDocument.Open(path);
        WriteByte(doc, 1, 0xAA);
        WriteByte(doc, 4, 0xBB);
        doc.InPlaceStreamWrapper = s => new FailingStream(s, failingWrite: 2, keepFailing: true);
        var changedCount = 0;
        doc.Changed += (_, _) => changedCount++;

        Assert.Throws<IOException>(() => doc.Save());

        Assert.False(doc.CanUndo);
        Assert.False(doc.CanRedo);
        Assert.True(doc.IsModified);
        Assert.Equal([1, 0xAA, 3, 4, 0xBB, 6], ReadAll(doc));
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void Save_WhenFileCannotBeOpened_KeepsHistory()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var doc = BinaryDocument.Open(path);
        WriteByte(doc, 1, 0xAA);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.Throws<IOException>(() => doc.Save());
        }

        Assert.True(doc.CanUndo);
        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));
    }

    private sealed class FailingStream(Stream inner, int failingWrite, bool keepFailing) : Stream
    {
        private int _writeCount;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writeCount++;
            if (_writeCount == failingWrite || (keepFailing && _writeCount > failingWrite))
                throw new IOException("Injected write failure");
            inner.Write(buffer, offset, count);
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
    }
}
