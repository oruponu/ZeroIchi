using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentSaveTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public BinaryDocumentSaveTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] MakeSequential(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
            data[i] = (byte)(i % 256);
        return data;
    }

    [Fact]
    public async Task Save_WriteByte_OnlyModifiedBytesChange()
    {
        var original = MakeSequential(256);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(0, 0xFF);
        doc.WriteByte(100, 0xAA);
        doc.WriteByte(255, 0xBB);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(256, saved.Length);
        Assert.Equal(0xFF, saved[0]);
        Assert.Equal(0xAA, saved[100]);
        Assert.Equal(0xBB, saved[255]);

        for (var i = 1; i < 100; i++)
            Assert.Equal(original[i], saved[i]);
    }

    [Fact]
    public async Task Save_InsertBytes_FileGrowsCorrectly()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.InsertBytes(50, [0xDE, 0xAD, 0xBE, 0xEF]);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(104, saved.Length);

        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i], saved[i]);

        Assert.Equal([0xDE, 0xAD, 0xBE, 0xEF], saved[50..54]);

        // The second half is shifted 4 bytes toward the end
        for (var i = 50; i < 100; i++)
            Assert.Equal(original[i], saved[i + 4]);
    }

    [Fact]
    public async Task Save_InsertAtBeginning()
    {
        var original = MakeSequential(50);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.InsertBytes(0, [0xAA, 0xBB]);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(52, saved.Length);
        Assert.Equal(0xAA, saved[0]);
        Assert.Equal(0xBB, saved[1]);
        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i], saved[i + 2]);
    }

    [Fact]
    public async Task Save_AppendByte()
    {
        var original = MakeSequential(10);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.AppendByte(0xFF);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(11, saved.Length);
        for (var i = 0; i < 10; i++)
            Assert.Equal(original[i], saved[i]);
        Assert.Equal(0xFF, saved[10]);
    }

    [Fact]
    public async Task Save_DeleteBytes_FileShrinks()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.DeleteBytes(20, 10);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(90, saved.Length);

        for (var i = 0; i < 20; i++)
            Assert.Equal(original[i], saved[i]);
        for (var i = 30; i < 100; i++)
            Assert.Equal(original[i], saved[i - 10]);
    }

    [Fact]
    public async Task Save_DeleteFromEnd()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.DeleteBytes(90, 10);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(90, saved.Length);
        for (var i = 0; i < 90; i++)
            Assert.Equal(original[i], saved[i]);
    }

    [Fact]
    public async Task Save_DeleteOnly_IsModifiedTrue()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.DeleteBytes(0, 50);

        Assert.True(doc.IsModified);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(50, saved.Length);
        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i + 50], saved[i]);
    }

    [Fact]
    public async Task Save_InsertAndDelete_MixedShift()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.InsertBytes(20, [0xAA, 0xBB, 0xCC]);
        doc.DeleteBytes(60, 10);
        doc.Save();
        doc.Dispose();

        // 100 + 3 - 10 = 93
        var expected = new byte[93];
        Array.Copy(original, 0, expected, 0, 20);
        expected[20] = 0xAA;
        expected[21] = 0xBB;
        expected[22] = 0xCC;
        Array.Copy(original, 20, expected, 23, 37);
        Array.Copy(original, 67, expected, 60, 33);

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(expected, saved);
    }

    [Fact]
    public async Task Save_DeleteThenInsert_MixedShift()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.DeleteBytes(10, 20);
        doc.InsertBytes(50, [0x11, 0x22, 0x33, 0x44, 0x55]);
        doc.Save();
        doc.Dispose();

        var expected = new byte[100 - 20 + 5];
        Array.Copy(original, 0, expected, 0, 10);
        Array.Copy(original, 30, expected, 10, 40);
        expected[50] = 0x11;
        expected[51] = 0x22;
        expected[52] = 0x33;
        expected[53] = 0x44;
        expected[54] = 0x55;
        Array.Copy(original, 70, expected, 55, 30);

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(expected, saved);
    }

    [Fact]
    public async Task Save_WriteByteOnly_SkipsJournal()
    {
        var path = CreateTestFile(MakeSequential(50));
        var journalPath = path + ".journal";

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(0, 0xFF);
        doc.Save();
        doc.Dispose();

        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task Save_TempFileBlocked_KeepsDocumentAndFileIntact()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);
        Directory.CreateDirectory(path + ".zitmp");

        var doc = BinaryDocument.Open(path);
        doc.InsertBytes(50, [0xAA]);

        Assert.Throws<UnauthorizedAccessException>(() => doc.Save());

        var view = new byte[101];
        doc.Buffer.ReadBytes(0, view, 0, 101);
        Assert.Equal(0xAA, view[50]);

        doc.Dispose();
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Save_ReplaceBlocked_RestoresMappingAndKeepsFileIntact()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.InsertBytes(50, [0xAA]);

        using (var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.Throws<IOException>(() => doc.Save());
        }

        var view = new byte[101];
        doc.Buffer.ReadBytes(0, view, 0, 101);
        Assert.Equal(0xAA, view[50]);
        Assert.False(File.Exists(path + ".zitmp"));

        doc.Dispose();
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Save_InPlaceBlocked_RestoresMappingAndKeepsFileIntact()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(50, 0xAA);

        using (var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.Throws<IOException>(() => doc.Save());
        }

        Assert.Equal(original[10], doc.Buffer.ReadByte(10));
        Assert.Equal(0xAA, doc.Buffer.ReadByte(50));

        doc.Dispose();
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Open_CompletesInterruptedReplace()
    {
        var path = Path.Combine(_tempDir, "main.bin");
        var newContent = MakeSequential(80);
        File.WriteAllBytes(path + ".zitmp", newContent);

        var doc = BinaryDocument.Open(path);
        doc.Dispose();

        Assert.False(File.Exists(path + ".zitmp"));
        Assert.Equal(newContent, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public void Open_DeletesStaleTempFile()
    {
        var path = CreateTestFile(MakeSequential(10));
        File.WriteAllBytes(path + ".zitmp", [1, 2, 3]);

        var doc = BinaryDocument.Open(path);
        doc.Dispose();

        Assert.False(File.Exists(path + ".zitmp"));
    }

    [Fact]
    public void Save_WithShift_LeavesNoTempOrJournalFile()
    {
        var path = CreateTestFile(MakeSequential(50));

        var doc = BinaryDocument.Open(path);
        doc.InsertBytes(10, [0xAA]);
        doc.Save();
        doc.Dispose();

        Assert.False(File.Exists(path + ".zitmp"));
        Assert.False(File.Exists(path + ".journal"));
    }

    [Fact]
    public async Task Save_NoChangeNoJournal()
    {
        var path = CreateTestFile(MakeSequential(50));
        var journalPath = path + ".journal";

        var doc = BinaryDocument.Open(path);
        doc.Save();
        doc.Dispose();

        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task Save_LargeFile_WriteByte()
    {
        const int size = 1_000_000;
        var original = MakeSequential(size);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(0, 0xFF);
        doc.WriteByte(size / 2, 0xAA);
        doc.WriteByte(size - 1, 0xBB);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(size, saved.Length);
        Assert.Equal(0xFF, saved[0]);
        Assert.Equal(0xAA, saved[size / 2]);
        Assert.Equal(0xBB, saved[size - 1]);
    }

    [Fact]
    public async Task Save_LargeFile_InsertAndDelete()
    {
        const int size = 500_000;
        var original = MakeSequential(size);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        var insertData = new byte[1000];
        Array.Fill(insertData, (byte)0xCC);
        doc.InsertBytes(100_000, insertData);
        doc.DeleteBytes(300_000, 2000);
        doc.Save();
        doc.Dispose();

        var expected = new byte[size + 1000 - 2000];
        Array.Copy(original, 0, expected, 0, 100_000);
        expected.AsSpan(100_000, 1000).Fill(0xCC);
        Array.Copy(original, 100_000, expected, 101_000, 199_000);
        Array.Copy(original, 301_000, expected, 300_000, 199_000);

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(expected, saved);
    }

    [Fact]
    public async Task Save_MultipleSavesInSequence()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(0, 0x11);
        doc.Save();
        doc.Dispose();

        doc = BinaryDocument.Open(path);
        doc.WriteByte(1, 0x22);
        doc.Save();
        doc.Dispose();

        doc = BinaryDocument.Open(path);
        doc.InsertBytes(50, [0xAA, 0xBB]);
        doc.Save();
        doc.Dispose();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(102, saved.Length);
        Assert.Equal(0x11, saved[0]);
        Assert.Equal(0x22, saved[1]);
        Assert.Equal(0xAA, saved[50]);
        Assert.Equal(0xBB, saved[51]);
    }

    [Fact]
    public async Task SaveAs_OverwriteExistingFile_ReplacesContentAndLeavesNoTemp()
    {
        var original = MakeSequential(100);
        var sourcePath = CreateTestFile(original);
        var targetPath = CreateTestFile([9, 9, 9]);

        var doc = BinaryDocument.Open(sourcePath);
        doc.InsertBytes(50, [0xAA]);
        await doc.SaveAsAsync(targetPath);

        Assert.Equal(targetPath, doc.FilePath);
        Assert.False(File.Exists(targetPath + ".zitmp"));
        doc.Dispose();

        byte[] expected = [.. original[..50], 0xAA, .. original[50..]];
        Assert.Equal(expected, await File.ReadAllBytesAsync(targetPath));
        Assert.Equal(original, await File.ReadAllBytesAsync(sourcePath));
    }

    [Fact]
    public async Task SaveAs_SameFileWithDifferentCase_SavesInPlace()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(10, 0xAA);
        await doc.SaveAsAsync(path.ToUpperInvariant());

        Assert.False(doc.IsModified);
        doc.Dispose();

        var expected = (byte[])original.Clone();
        expected[10] = 0xAA;
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SaveAs_SameFileUnmodified_DoesNothing()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = BinaryDocument.Open(path);
        await doc.SaveAsAsync(path);

        Assert.Equal(path, doc.FilePath);
        doc.Dispose();
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SaveAs_TempFileBlocked_KeepsDocumentAndTargetIntact()
    {
        var original = MakeSequential(100);
        var sourcePath = CreateTestFile(original);
        byte[] targetContent = [9, 9, 9];
        var targetPath = CreateTestFile(targetContent);
        Directory.CreateDirectory(targetPath + ".zitmp");

        var doc = BinaryDocument.Open(sourcePath);
        doc.InsertBytes(50, [0xAA]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => doc.SaveAsAsync(targetPath));

        Assert.Equal(sourcePath, doc.FilePath);
        Assert.Equal(0xAA, doc.Buffer.ReadByte(50));
        Assert.Equal(original[99], doc.Buffer.ReadByte(100));
        doc.Dispose();
        Assert.Equal(targetContent, await File.ReadAllBytesAsync(targetPath));
    }

    [Fact]
    public async Task SaveAs_ReplaceBlocked_KeepsDocumentAndTargetIntact()
    {
        var original = MakeSequential(100);
        var sourcePath = CreateTestFile(original);
        byte[] targetContent = [9, 9, 9];
        var targetPath = CreateTestFile(targetContent);

        var doc = BinaryDocument.Open(sourcePath);
        doc.InsertBytes(50, [0xAA]);

        using (new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            await Assert.ThrowsAsync<IOException>(() => doc.SaveAsAsync(targetPath));
        }

        Assert.Equal(sourcePath, doc.FilePath);
        Assert.Equal(0xAA, doc.Buffer.ReadByte(50));
        Assert.False(File.Exists(targetPath + ".zitmp"));
        doc.Dispose();
        Assert.Equal(targetContent, await File.ReadAllBytesAsync(targetPath));
    }

    [Fact]
    public async Task SaveAs_TargetDirectoryMissing_ThrowsAndKeepsDocument()
    {
        var path = CreateTestFile(MakeSequential(10));
        var targetPath = Path.Combine(_tempDir, "missing", "target.bin");

        var doc = BinaryDocument.Open(path);
        doc.WriteByte(0, 0xAA);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => doc.SaveAsAsync(targetPath));

        Assert.Equal(path, doc.FilePath);
        Assert.Equal(0xAA, doc.Buffer.ReadByte(0));
        doc.Dispose();
    }
}
