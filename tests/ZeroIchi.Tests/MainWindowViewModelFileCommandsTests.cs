using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelFileCommandsTests : IDisposable
{
    static MainWindowViewModelFileCommandsTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelFileCommandsTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static (MainWindowViewModel Vm, StubDialogService Dialog) CreateViewModel()
    {
        var dialog = new StubDialogService();
        return (new MainWindowViewModel(dialog, new StubClipboardService(), new StubWindowService()), dialog);
    }

    private sealed class StubDialogService : IDialogService
    {
        public SaveChangesResult SaveChangesAnswer { get; set; } = SaveChangesResult.Discard;
        public string? SaveFilePath { get; set; }
        public int SaveChangesDialogCount { get; private set; }
        public int ErrorCount { get; private set; }

        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult(SaveFilePath);

        public Task<SaveChangesResult> ShowSaveChangesDialogAsync()
        {
            SaveChangesDialogCount++;
            return Task.FromResult(SaveChangesAnswer);
        }

        public Task ShowErrorAsync(string message, string detail)
        {
            ErrorCount++;
            return Task.CompletedTask;
        }

        public Task ShowAboutAsync() => Task.CompletedTask;
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public Task SetBytesAsync(byte[] bytes, string? text) => Task.CompletedTask;
        public Task<byte[]?> TryGetBytesAsync() => Task.FromResult<byte[]?>(null);
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }

    [Fact]
    public async Task OpenFile_WhenModifiedAndCancelled_KeepsDocument()
    {
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile([1, 2, 3]));
        var document = vm.Document;
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            dialog.SaveChangesAnswer = SaveChangesResult.Cancel;

            await vm.OpenFileAsync(CreateTestFile([4, 5, 6]));

            Assert.Equal(1, dialog.SaveChangesDialogCount);
            Assert.Same(document, vm.Document);
            Assert.Equal(0xFF, vm.Document!.Buffer.ReadByte(0));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenFile_WhenModifiedAndDiscarded_ReplacesAndDisposesOldDocument()
    {
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile([1, 2, 3]));
        var oldDocument = vm.Document!;
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);

            await vm.OpenFileAsync(CreateTestFile([4, 5, 6]));

            Assert.Equal(1, dialog.SaveChangesDialogCount);
            Assert.NotSame(oldDocument, vm.Document);
            Assert.Equal(4, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(0, oldDocument.Buffer.Length);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenFile_WhenUnmodified_DoesNotAsk()
    {
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile([1, 2, 3]));
        try
        {
            await vm.OpenFileAsync(CreateTestFile([4, 5, 6]));

            Assert.Equal(0, dialog.SaveChangesDialogCount);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenFile_SameFileWhenModifiedAndSaved_SavesThenReopens()
    {
        var path = CreateTestFile([1, 2, 3]);
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(path);
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            dialog.SaveChangesAnswer = SaveChangesResult.Save;

            await vm.OpenFileAsync(path);

            Assert.False(vm.Document!.IsModified);
            Assert.Equal(0xFF, vm.Document.Buffer.ReadByte(0));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
        Assert.Equal([0xFF, 2, 3], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task OpenFile_MissingPath_ShowsErrorAndKeepsDocument()
    {
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile([1, 2, 3]));
        var document = vm.Document;
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);

            await vm.OpenFileAsync(Path.Combine(_tempDir, "missing.bin"));

            Assert.Equal(1, dialog.ErrorCount);
            Assert.Same(document, vm.Document);
            Assert.Equal(0xFF, vm.Document!.Buffer.ReadByte(0));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task SaveFile_InPlaceBlocked_ShowsErrorAndKeepsModified()
    {
        var path = CreateTestFile([1, 2, 3]);
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(path);
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await vm.SaveFileCommand.ExecuteAsync(null);
            }

            Assert.Equal(1, dialog.ErrorCount);
            Assert.True(vm.Document!.IsModified);
            Assert.Equal(0xFF, vm.Document.Buffer.ReadByte(0));
            Assert.True(vm.CanUndo);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task SaveFile_AfterFailure_SucceedsOnceUnblocked()
    {
        var path = CreateTestFile([1, 2, 3]);
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(path);
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await vm.SaveFileCommand.ExecuteAsync(null);
            }
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ErrorCount);
            Assert.False(vm.Document!.IsModified);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
        Assert.Equal([0xFF, 2, 3], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ConfirmDiscardChanges_SaveFails_ReturnsFalse()
    {
        var path = CreateTestFile([1, 2, 3]);
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(path);
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            dialog.SaveChangesAnswer = SaveChangesResult.Save;

            bool result;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                result = await vm.ConfirmDiscardChangesAsync();
            }

            Assert.False(result);
            Assert.Equal(1, dialog.ErrorCount);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task SaveAsFile_TempFileBlocked_ShowsErrorAndKeepsPath()
    {
        var path = CreateTestFile([1, 2, 3]);
        var targetPath = Path.Combine(_tempDir, "target.bin");
        Directory.CreateDirectory(targetPath + ".zitmp");
        var (vm, dialog) = CreateViewModel();
        dialog.SaveFilePath = targetPath;
        await vm.OpenFileAsync(path);
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);

            await vm.SaveAsFileCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ErrorCount);
            Assert.Equal(path, vm.Document!.FilePath);
            Assert.False(File.Exists(targetPath));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task FileOperation_DocumentBecomesUnusable_DetachesToNewDocument()
    {
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile([1, 2, 3]));
        var oldDocument = vm.Document!;
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);

            var result = await vm.TryFileOperationAsync("保存できませんでした", () =>
            {
                ((MappedByteBuffer)oldDocument.Original).ReleaseMapping();
                throw new IOException();
            });

            Assert.False(result);
            Assert.Equal(1, dialog.ErrorCount);
            Assert.NotSame(oldDocument, vm.Document);
            Assert.True(vm.Document!.IsNew);
            Assert.False(vm.Document.IsModified);
            Assert.Equal(0, oldDocument.Buffer.Length);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task ConfirmDiscardChanges_AfterDetach_ReturnsTrueWithoutAsking()
    {
        var (vm, dialog) = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile([1, 2, 3]));
        var oldDocument = vm.Document!;
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            await vm.TryFileOperationAsync("保存できませんでした", () =>
            {
                ((MappedByteBuffer)oldDocument.Original).ReleaseMapping();
                throw new IOException();
            });

            var result = await vm.ConfirmDiscardChangesAsync();

            Assert.True(result);
            Assert.Equal(0, dialog.SaveChangesDialogCount);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
