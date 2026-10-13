using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentChangedTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentChangedTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static void WriteByte(BinaryDocument doc, long index, byte value) =>
        doc.ExecuteCommand(new WriteByteCommand(doc, index, value, cursorPositionBefore: 0, cursorPositionAfter: 0));

    [Fact]
    public void ExecuteCommand_RaisesChangedOnce()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var count = 0;
        doc.Changed += (_, _) => count++;

        WriteByte(doc, 1, 0xFF);

        Assert.Equal(1, count);
    }

    [Fact]
    public void UndoAndRedo_RaiseChangedOnceEach()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        WriteByte(doc, 1, 0xFF);
        var count = 0;
        doc.Changed += (_, _) => count++;

        doc.Undo();
        Assert.Equal(1, count);

        doc.Redo();
        Assert.Equal(2, count);
    }

    [Fact]
    public void UndoAndRedo_WithEmptyHistory_DoNotRaiseChanged()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var count = 0;
        doc.Changed += (_, _) => count++;

        doc.Undo();
        doc.Redo();

        Assert.Equal(0, count);
    }

    [Fact]
    public void Save_RaisesChangedAfterStateIsUpdated()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        WriteByte(doc, 1, 0xFF);
        var count = 0;
        var stateSeen = (CanUndo: false, IsModified: true);
        doc.Changed += (_, _) =>
        {
            count++;
            stateSeen = (doc.CanUndo, doc.IsModified);
        };

        doc.Save();

        Assert.Equal(1, count);
        Assert.Equal((true, false), stateSeen);
    }

    [Fact]
    public void Save_WithoutModification_DoesNotRaiseChanged()
    {
        using var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        var count = 0;
        doc.Changed += (_, _) => count++;

        doc.Save();

        Assert.Equal(0, count);
    }

    [Fact]
    public void Save_WhenWriteFails_DoesNotRaiseChanged()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var doc = BinaryDocument.Open(path);
        WriteByte(doc, 1, 0xFF);
        var count = 0;
        doc.Changed += (_, _) => count++;

        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => doc.Save());
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SaveAsAsync_RaisesChangedAfterFileNameIsUpdated()
    {
        using var doc = BinaryDocument.CreateNew();
        doc.ExecuteCommand(new AppendByteCommand(doc, 0x42, cursorPositionBefore: 0, cursorPositionAfter: 1));
        var count = 0;
        string? fileNameSeen = null;
        doc.Changed += (_, _) =>
        {
            count++;
            fileNameSeen = doc.FileName;
        };

        await doc.SaveAsAsync(Path.Combine(_tempDir, "saved.bin"));

        Assert.Equal(1, count);
        Assert.Equal("saved.bin", fileNameSeen);
    }

    [Fact]
    public async Task SaveAsAsync_ToSamePath_RaisesChangedOnce()
    {
        var path = CreateTestFile([1, 2, 3]);
        using var doc = BinaryDocument.Open(path);
        WriteByte(doc, 1, 0xFF);
        var count = 0;
        doc.Changed += (_, _) => count++;

        await doc.SaveAsAsync(path);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Dispose_DoesNotRaiseChanged()
    {
        var doc = BinaryDocument.Open(CreateTestFile([1, 2, 3]));
        WriteByte(doc, 1, 0xFF);
        var count = 0;
        doc.Changed += (_, _) => count++;

        doc.Dispose();

        Assert.Equal(0, count);
    }
}
