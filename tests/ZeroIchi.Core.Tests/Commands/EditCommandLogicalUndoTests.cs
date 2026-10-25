using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests.Commands;

public sealed class EditCommandLogicalUndoTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public EditCommandLogicalUndoTests() => Directory.CreateDirectory(_tempDir);

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

    private static void CommitSave(IEditCommand command) => command.CommitSave(command.CaptureRetainedBytes());

    [Fact]
    public void WriteByte_UndoAfterCommitSave_WritesOldValueBack()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var command = new WriteByteCommand(doc, 1, 0xFF, cursorPositionBefore: 1, cursorPositionAfter: 2);
        doc.ExecuteCommand(command);
        CommitSave(command);

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));

        doc.Redo();
        Assert.Equal([1, 0xFF, 3], ReadAll(doc));

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));
    }

    [Fact]
    public void AppendByte_UndoAfterCommitSave_RemovesAppendedByte()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var command = new AppendByteCommand(doc, 0xCC, cursorPositionBefore: 3, cursorPositionAfter: 4);
        doc.ExecuteCommand(command);
        CommitSave(command);

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));

        doc.Redo();
        Assert.Equal([1, 2, 3, 0xCC], ReadAll(doc));
    }

    [Fact]
    public void InsertBytes_UndoAfterCommitSave_RemovesInsertedBytes()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var command = new InsertBytesCommand(doc, 1, [0xAA, 0xBB], cursorPositionBefore: 1, cursorPositionAfter: 3);
        doc.ExecuteCommand(command);
        CommitSave(command);

        doc.Undo();
        Assert.Equal([1, 2, 3], ReadAll(doc));

        doc.Redo();
        Assert.Equal([1, 0xAA, 0xBB, 2, 3], ReadAll(doc));
    }

    [Fact]
    public void DeleteBytes_UndoAfterCommitSave_ReinsertsRetainedBytes()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3, 4, 5, 6]));
        var command = new DeleteBytesCommand(doc, 1, 3, cursorPositionBefore: 1, cursorPositionAfter: 1);
        doc.ExecuteCommand(command);
        Assert.Equal(3, command.RetainedLength);

        CommitSave(command);

        Assert.True(command.HasRetainedBytes);
        Assert.Null(command.CaptureRetainedBytes());

        doc.Undo();
        Assert.Equal([1, 2, 3, 4, 5, 6], ReadAll(doc));

        doc.Redo();
        Assert.Equal([1, 5, 6], ReadAll(doc));
    }

    [Fact]
    public void DeleteBytes_CaptureRetainedBytes_DoesNotStoreBytes()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3, 4, 5, 6]));
        var command = new DeleteBytesCommand(doc, 1, 3, cursorPositionBefore: 1, cursorPositionAfter: 1);
        doc.ExecuteCommand(command);

        Assert.Equal([2, 3, 4], command.CaptureRetainedBytes());
        Assert.False(command.HasRetainedBytes);
    }

    [Fact]
    public void NonDeleteCommands_RetainNothing()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var write = new WriteByteCommand(doc, 0, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 1);
        var insert = new InsertBytesCommand(doc, 0, [0xAA], cursorPositionBefore: 0, cursorPositionAfter: 1);
        doc.ExecuteCommand(write);
        doc.ExecuteCommand(insert);

        Assert.Equal(0, write.RetainedLength);
        Assert.Equal(0, insert.RetainedLength);
        Assert.Null(write.CaptureRetainedBytes());
        Assert.Null(insert.CaptureRetainedBytes());
    }
}
