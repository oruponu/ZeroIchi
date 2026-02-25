using System.Buffers.Binary;
using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public class BinaryDocumentSaveTests : IDisposable
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

    private static void WriteTestJournal(
        string path, long finalSize, (byte source, long offset, long length)[] pieces, byte[] addBuffer)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Span<byte> buf = stackalloc byte[17];

        fs.Write("ZIJF"u8);

        BinaryPrimitives.WriteInt64LittleEndian(buf, finalSize);
        fs.Write(buf[..8]);

        BinaryPrimitives.WriteInt32LittleEndian(buf, pieces.Length);
        fs.Write(buf[..4]);

        foreach (var (source, offset, length) in pieces)
        {
            buf[0] = source;
            BinaryPrimitives.WriteInt64LittleEndian(buf[1..], offset);
            BinaryPrimitives.WriteInt64LittleEndian(buf[9..], length);
            fs.Write(buf);
        }

        BinaryPrimitives.WriteInt32LittleEndian(buf, addBuffer.Length);
        fs.Write(buf[..4]);

        if (addBuffer.Length > 0)
            fs.Write(addBuffer);
    }

    [Fact]
    public async Task SaveAsync_WriteByte_OnlyModifiedBytesChange()
    {
        var original = MakeSequential(256);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.WriteByte(0, 0xFF);
        doc.WriteByte(100, 0xAA);
        doc.WriteByte(255, 0xBB);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(256, saved.Length);
        Assert.Equal(0xFF, saved[0]);
        Assert.Equal(0xAA, saved[100]);
        Assert.Equal(0xBB, saved[255]);

        for (var i = 1; i < 100; i++)
            Assert.Equal(original[i], saved[i]);
    }

    [Fact]
    public async Task SaveAsync_InsertBytes_FileGrowsCorrectly()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.InsertBytes(50, [0xDE, 0xAD, 0xBE, 0xEF]);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(104, saved.Length);

        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i], saved[i]);

        Assert.Equal([0xDE, 0xAD, 0xBE, 0xEF], saved[50..54]);

        // 後半は4バイト後ろにずれる
        for (var i = 50; i < 100; i++)
            Assert.Equal(original[i], saved[i + 4]);
    }

    [Fact]
    public async Task SaveAsync_InsertAtBeginning()
    {
        var original = MakeSequential(50);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.InsertBytes(0, [0xAA, 0xBB]);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(52, saved.Length);
        Assert.Equal(0xAA, saved[0]);
        Assert.Equal(0xBB, saved[1]);
        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i], saved[i + 2]);
    }

    [Fact]
    public async Task SaveAsync_AppendByte()
    {
        var original = MakeSequential(10);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.AppendByte(0xFF);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(11, saved.Length);
        for (var i = 0; i < 10; i++)
            Assert.Equal(original[i], saved[i]);
        Assert.Equal(0xFF, saved[10]);
    }

    [Fact]
    public async Task SaveAsync_DeleteBytes_FileShrinks()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.DeleteBytes(20, 10);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(90, saved.Length);

        for (var i = 0; i < 20; i++)
            Assert.Equal(original[i], saved[i]);
        for (var i = 30; i < 100; i++)
            Assert.Equal(original[i], saved[i - 10]);
    }

    [Fact]
    public async Task SaveAsync_DeleteFromEnd()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.DeleteBytes(90, 10);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(90, saved.Length);
        for (var i = 0; i < 90; i++)
            Assert.Equal(original[i], saved[i]);
    }

    [Fact]
    public async Task SaveAsync_DeleteOnly_IsModifiedTrue()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.DeleteBytes(0, 50);

        Assert.True(doc.IsModified);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(50, saved.Length);
        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i + 50], saved[i]);
    }

    [Fact]
    public async Task SaveAsync_InsertAndDelete_MixedShift()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.InsertBytes(20, [0xAA, 0xBB, 0xCC]);
        doc.DeleteBytes(60, 10);
        await doc.SaveAsync();
        doc.ReleaseData();

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
    public async Task SaveAsync_DeleteThenInsert_MixedShift()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.DeleteBytes(10, 20);
        doc.InsertBytes(50, [0x11, 0x22, 0x33, 0x44, 0x55]);
        await doc.SaveAsync();
        doc.ReleaseData();

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
    public async Task SaveAsync_WriteByteOnly_SkipsJournal()
    {
        var path = CreateTestFile(MakeSequential(50));
        var journalPath = path + ".journal";

        var doc = await BinaryDocument.OpenAsync(path);
        doc.WriteByte(0, 0xFF);
        await doc.SaveAsync();
        doc.ReleaseData();

        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task SaveAsync_WithShift_CreatesAndDeletesJournal()
    {
        var path = CreateTestFile(MakeSequential(50));
        var journalPath = path + ".journal";

        var journalCreated = new ManualResetEventSlim(false);
        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(journalPath));
        watcher.Created += (_, _) => journalCreated.Set();
        watcher.EnableRaisingEvents = true;

        var doc = await BinaryDocument.OpenAsync(path);
        doc.InsertBytes(10, [0xAA]);
        await doc.SaveAsync();
        doc.ReleaseData();

        Assert.True(journalCreated.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task SaveAsync_NoChangeNoJournal()
    {
        var path = CreateTestFile(MakeSequential(50));
        var journalPath = path + ".journal";

        var doc = await BinaryDocument.OpenAsync(path);
        await doc.SaveAsync();
        doc.ReleaseData();

        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task OpenAsync_RecoverFromJournal_RestoresCorrectData()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);
        var journalPath = path + ".journal";

        // Insert [0xAA, 0xBB] at position 50 を表すジャーナルを作成
        WriteTestJournal(journalPath, 102, [
            (0, 0, 50),
            (1, 0, 2),
            (0, 50, 50),
        ], [0xAA, 0xBB]);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(102, saved.Length);

        for (var i = 0; i < 50; i++)
            Assert.Equal(original[i], saved[i]);

        Assert.Equal([0xAA, 0xBB], saved[50..52]);

        // 後半は2バイト後ろにずれる
        for (var i = 50; i < 100; i++)
            Assert.Equal(original[i], saved[i + 2]);

        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task SaveAsync_LargeFile_WriteByte()
    {
        const int size = 1_000_000;
        var original = MakeSequential(size);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.WriteByte(0, 0xFF);
        doc.WriteByte(size / 2, 0xAA);
        doc.WriteByte(size - 1, 0xBB);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(size, saved.Length);
        Assert.Equal(0xFF, saved[0]);
        Assert.Equal(0xAA, saved[size / 2]);
        Assert.Equal(0xBB, saved[size - 1]);
    }

    [Fact]
    public async Task SaveAsync_LargeFile_InsertAndDelete()
    {
        const int size = 500_000;
        var original = MakeSequential(size);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        var insertData = new byte[1000];
        Array.Fill(insertData, (byte)0xCC);
        doc.InsertBytes(100_000, insertData);
        doc.DeleteBytes(300_000, 2000);
        await doc.SaveAsync();
        doc.ReleaseData();

        var expected = new byte[size + 1000 - 2000];
        Array.Copy(original, 0, expected, 0, 100_000);
        expected.AsSpan(100_000, 1000).Fill(0xCC);
        Array.Copy(original, 100_000, expected, 101_000, 199_000);
        Array.Copy(original, 301_000, expected, 300_000, 199_000);

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(expected, saved);
    }

    [Fact]
    public async Task SaveAsync_MultipleSavesInSequence()
    {
        var original = MakeSequential(100);
        var path = CreateTestFile(original);

        var doc = await BinaryDocument.OpenAsync(path);
        doc.WriteByte(0, 0x11);
        await doc.SaveAsync();
        doc.ReleaseData();

        doc = await BinaryDocument.OpenAsync(path);
        doc.WriteByte(1, 0x22);
        await doc.SaveAsync();
        doc.ReleaseData();

        doc = await BinaryDocument.OpenAsync(path);
        doc.InsertBytes(50, [0xAA, 0xBB]);
        await doc.SaveAsync();
        doc.ReleaseData();

        var saved = await File.ReadAllBytesAsync(path);
        Assert.Equal(102, saved.Length);
        Assert.Equal(0x11, saved[0]);
        Assert.Equal(0x22, saved[1]);
        Assert.Equal(0xAA, saved[50]);
        Assert.Equal(0xBB, saved[51]);
    }
}
