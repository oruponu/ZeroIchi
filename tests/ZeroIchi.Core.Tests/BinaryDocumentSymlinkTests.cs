using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class BinaryDocumentSymlinkTests : IDisposable
{
    private static readonly byte[] Original = [0, 1, 2, 3, 4, 5, 6, 7];

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));
    private readonly string _targetPath;
    private readonly string _linkPath;

    public BinaryDocumentSymlinkTests()
    {
        Directory.CreateDirectory(_tempDir);
        _targetPath = Path.Combine(_tempDir, "target.bin");
        _linkPath = Path.Combine(_tempDir, "link.bin");
        File.WriteAllBytes(_targetPath, Original);
        File.CreateSymbolicLink(_linkPath, _targetPath);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Open_ViaSymlink_ReadsTargetContent()
    {
        using var doc = BinaryDocument.Open(_linkPath);

        Assert.Equal(Original.Length, doc.Buffer.Length);
        Assert.Equal(Original, doc.Buffer.SliceToArray(0, Original.Length));
    }

    [Fact]
    public async Task Save_AppendViaSymlink_KeepsTargetContent()
    {
        using var doc = BinaryDocument.Open(_linkPath);
        doc.AppendByte(0xFF);
        doc.Save();
        doc.Dispose();

        Assert.Equal([.. Original, 0xFF], await File.ReadAllBytesAsync(_targetPath));
        Assert.NotNull(new FileInfo(_linkPath).LinkTarget);
    }

    [Fact]
    public async Task Save_InsertViaSymlink_UpdatesTargetAndKeepsLink()
    {
        using var doc = BinaryDocument.Open(_linkPath);
        doc.InsertBytes(0, [0xAA]);
        doc.Save();
        doc.Dispose();

        Assert.Equal([0xAA, .. Original], await File.ReadAllBytesAsync(_targetPath));
        Assert.NotNull(new FileInfo(_linkPath).LinkTarget);
    }
}
