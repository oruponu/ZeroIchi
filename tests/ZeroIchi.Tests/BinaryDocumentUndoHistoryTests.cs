using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentUndoHistoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentUndoHistoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void ExecuteCommand_ThenUndo_RestoresContentAndState()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        try
        {
            doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0));
            Assert.True(doc.CanUndo);
            Assert.Equal(0xFF, doc.Buffer.ReadByte(1));

            var command = doc.Undo();

            Assert.NotNull(command);
            Assert.Equal(2, doc.Buffer.ReadByte(1));
            Assert.False(doc.CanUndo);
            Assert.True(doc.CanRedo);
        }
        finally
        {
            doc.Dispose();
        }
    }

    [Fact]
    public void Save_KeepsUndoAndRedoHistory()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        try
        {
            doc.ExecuteCommand(new WriteByteCommand(doc, 0, 0xAA, cursorPositionBefore: 0, cursorPositionAfter: 0));
            doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0xBB, cursorPositionBefore: 0, cursorPositionAfter: 0));
            doc.Undo();

            doc.Save();

            Assert.True(doc.CanUndo);
            Assert.True(doc.CanRedo);

            Assert.NotNull(doc.Redo());
            Assert.Equal(0xBB, doc.Buffer.ReadByte(1));

            doc.Undo();
            doc.Undo();
            Assert.Equal(1, doc.Buffer.ReadByte(0));
            Assert.Equal(2, doc.Buffer.ReadByte(1));
        }
        finally
        {
            doc.Dispose();
        }
    }

    [Fact]
    public async Task SaveAs_KeepsUndoHistory()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var savePath = Path.Combine(_tempDir, "saved.bin");
        try
        {
            doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0));
            await doc.SaveAsAsync(savePath);

            Assert.True(doc.CanUndo);
            Assert.NotNull(doc.Undo());
            Assert.Equal(2, doc.Buffer.ReadByte(1));
            Assert.Equal([1, 0xFF, 3], await File.ReadAllBytesAsync(savePath));
        }
        finally
        {
            doc.Dispose();
        }
    }

    [Fact]
    public async Task SaveAs_WhenAllEditsUndone_KeepsRedoHistory()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var savePath = Path.Combine(_tempDir, "saved-unmodified.bin");
        try
        {
            doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0));
            doc.Undo();

            await doc.SaveAsAsync(savePath);

            Assert.False(doc.CanUndo);
            Assert.True(doc.CanRedo);
            Assert.NotNull(doc.Redo());
            Assert.Equal(0xFF, doc.Buffer.ReadByte(1));
            Assert.True(doc.IsModified);
        }
        finally
        {
            doc.Dispose();
        }
    }

    [Fact]
    public void Save_WhenAllEditsUndone_KeepsHistory()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        try
        {
            doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0));
            doc.Undo();

            doc.Save();

            Assert.True(doc.CanRedo);
            Assert.NotNull(doc.Redo());
            Assert.Equal(0xFF, doc.Buffer.ReadByte(1));
        }
        finally
        {
            doc.Dispose();
        }
    }

    [Fact]
    public void UndoEdit_StaleEditAfterSave_Throws()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        try
        {
            var edit = doc.WriteByte(1, 0xFF);
            doc.Save();

            Assert.Throws<InvalidOperationException>(() => doc.UndoEdit(edit));
        }
        finally
        {
            doc.Dispose();
        }
    }

    [Fact]
    public void ExecuteCommand_CommandForAnotherDocument_Throws()
    {
        var docA = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var docB = BinaryDocument.Open(CreateTestFile([4, 5, 6]));
        try
        {
            var command = new WriteByteCommand(docA, 0, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0);

            Assert.Throws<InvalidOperationException>(() => docB.ExecuteCommand(command));
            Assert.False(docA.IsModified);
            Assert.False(docB.CanUndo);
        }
        finally
        {
            docA.Dispose();
            docB.Dispose();
        }
    }

    [Fact]
    public void Dispose_ClearsHistory()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        doc.ExecuteCommand(new WriteByteCommand(doc, 1, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0));

        doc.Dispose();

        Assert.False(doc.CanUndo);
        Assert.Null(doc.Undo());
    }
}
