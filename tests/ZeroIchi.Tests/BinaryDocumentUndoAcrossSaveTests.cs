using System.Runtime.CompilerServices;
using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentUndoAcrossSaveTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentUndoAcrossSaveTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] ReadAll(BinaryDocument doc)
    {
        var bytes = new byte[doc.FileSize];
        doc.Buffer.ReadBytes(0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] Sequence(int count) => Enumerable.Range(0, count).Select(i => (byte)i).ToArray();

    private static void Write(BinaryDocument doc, long index, byte value) =>
        doc.ExecuteCommand(new WriteByteCommand(doc, index, value, cursorPositionBefore: index, cursorPositionAfter: index + 1));

    private static void Delete(BinaryDocument doc, long index, long count) =>
        doc.ExecuteCommand(new DeleteBytesCommand(doc, index, count, cursorPositionBefore: index, cursorPositionAfter: index));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TrackPieceTable(BinaryDocument doc) => new(doc.CurrentPieceTable);

    [Fact]
    public async Task Save_ThenUndoWrite_RestoresByteAndMarksModified()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var doc = BinaryDocument.Open(path);
        Write(doc, 1, 0xFF);
        doc.Save();

        Assert.True(doc.CanUndo);
        doc.Undo();

        Assert.Equal([1, 2, 3], ReadAll(doc));
        Assert.True(doc.IsModified);
        Assert.True(doc.IsByteModified(1));
        Assert.Equal([1, 0xFF, 3], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public void Save_ThenUndoAppend_RemovesAppendedByte()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        doc.ExecuteCommand(new AppendByteCommand(doc, 0xCC, cursorPositionBefore: 3, cursorPositionAfter: 4));
        doc.Save();

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));

        doc.Redo();
        Assert.Equal([1, 2, 3, 0xCC], ReadAll(doc));
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void Save_ThenUndoInsertInMiddle_RemovesInsertedBytes()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        doc.ExecuteCommand(new InsertBytesCommand(doc, 1, [0xAA, 0xBB], cursorPositionBefore: 1, cursorPositionAfter: 3));
        doc.Save();

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));

        doc.Redo();
        Assert.Equal([1, 0xAA, 0xBB, 2, 3], ReadAll(doc));
        Assert.False(doc.IsModified);
    }

    [Fact]
    public async Task Save_ThenUndoDeleteInMiddle_ReinsertsDeletedBytes()
    {
        var path = CreateTestFile([1, 2, 3, 4, 5, 6]);
        using var doc = BinaryDocument.Open(path);
        Delete(doc, 1, 3);
        doc.Save();
        Assert.Equal([1, 5, 6], await File.ReadAllBytesAsync(path));

        doc.Undo();
        Assert.Equal([1, 2, 3, 4, 5, 6], ReadAll(doc));
        Assert.True(doc.IsModified);

        doc.Redo();
        Assert.Equal([1, 5, 6], ReadAll(doc));
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void Save_ThenUndoDeleteAtEnd_ReinsertsTruncatedBytes()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3, 4, 5, 6]));
        Delete(doc, 4, 2);
        doc.Save();

        doc.Undo();

        Assert.Equal([1, 2, 3, 4, 5, 6], ReadAll(doc));
    }

    [Fact]
    public async Task Save_ThenUndoDeleteOfWholeFile_RestoresContent()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var doc = BinaryDocument.Open(path);
        Delete(doc, 0, 3);
        doc.Save();
        Assert.Empty(await File.ReadAllBytesAsync(path));

        doc.Undo();

        Assert.Equal([1, 2, 3], ReadAll(doc));
    }

    [Fact]
    public void Save_ThenUndoDeleteSpanningEdits_RestoresEditedBytes()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3, 4, 5, 6, 7, 8]));
        Write(doc, 2, 0xAA);
        Write(doc, 5, 0xBB);
        Delete(doc, 1, 6);
        doc.Save();

        doc.Undo();
        Assert.Equal([1, 2, 0xAA, 4, 5, 0xBB, 7, 8], ReadAll(doc));

        doc.Undo();
        doc.Undo();
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], ReadAll(doc));
    }

    [Fact]
    public void Save_ThenUndoRedoRepeatedly_TogglesModified()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        Write(doc, 0, 0xAA);
        doc.Save();

        for (var i = 0; i < 2; i++)
        {
            doc.Undo();
            Assert.True(doc.IsModified);
            Assert.Equal([1, 2, 3], ReadAll(doc));

            doc.Redo();
            Assert.False(doc.IsModified);
            Assert.Equal([0xAA, 2, 3], ReadAll(doc));
        }
    }

    [Fact]
    public void Save_Twice_UndoesAcrossBothSaves()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        Write(doc, 0, 0xAA);
        doc.Save();
        Delete(doc, 2, 1);
        doc.Save();

        doc.Undo();
        Assert.Equal([0xAA, 2, 3], ReadAll(doc));
        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));

        doc.Redo();
        doc.Redo();
        Assert.Equal([0xAA, 2], ReadAll(doc));
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void Save_UndoThenSaveAgain_RedoReappliesEdit()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        Write(doc, 0, 0xAA);
        doc.Save();
        doc.Undo();
        doc.Save();

        Assert.True(doc.CanRedo);
        doc.Redo();
        Assert.Equal([0xAA, 2, 3], ReadAll(doc));
        Assert.True(doc.IsModified);

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void Save_WhenRetainedBytesExceedLimit_DiscardsOverflowingAndOlderHistory()
    {
        using var doc = BinaryDocument.Open(CreateTestFile(Sequence(16)));
        doc.UndoRetentionLimit = 4;
        Write(doc, 0, 0xAA);
        Delete(doc, 10, 6);
        Delete(doc, 2, 2);
        doc.Save();

        doc.Undo();

        Assert.Equal([0xAA, 1, 2, 3, 4, 5, 6, 7, 8, 9], ReadAll(doc));
        Assert.False(doc.CanUndo);
    }

    [Fact]
    public void Save_WhenRetainedBytesEqualLimit_KeepsAllHistory()
    {
        using var doc = BinaryDocument.Open(CreateTestFile(Sequence(16)));
        doc.UndoRetentionLimit = 6;
        Write(doc, 0, 0xAA);
        Delete(doc, 10, 6);
        doc.Save();

        doc.Undo();
        doc.Undo();

        Assert.Equal(Sequence(16), ReadAll(doc));
        Assert.False(doc.CanUndo);
    }

    [Fact]
    public void Save_DoesNotKeepOldPieceTableAlive()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        doc.ExecuteCommand(new InsertBytesCommand(doc, 1, [0xAA, 0xBB], cursorPositionBefore: 1, cursorPositionAfter: 3));
        var oldTable = TrackPieceTable(doc);

        doc.Save();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.True(doc.CanUndo);
        Assert.False(oldTable.IsAlive);
    }
}
