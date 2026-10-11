using System.Text;
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
    }

    private sealed class StubClipboardService : IClipboardService
    {
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
            vm.Document?.Dispose();
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
            vm.Document?.Dispose();
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
            vm.Document?.Dispose();
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
            vm.Document?.Dispose();
        }
        Assert.Equal([0xFF, 2, 3], await File.ReadAllBytesAsync(path));
    }
}
