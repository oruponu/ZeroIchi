using System.Text;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelSearchTests : IDisposable
{
    static MainWindowViewModelSearchTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelSearchTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile()
    {
        var bytes = new byte[100];
        bytes[95] = 0xAB;
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static MainWindowViewModel CreateViewModel()
        => new(new StubDialogService(), new StubClipboardService(), new StubWindowService());

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
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
    public async Task Edit_AfterSearch_ClearsSearchResults()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.SearchText = "AB";
            Assert.Equal([95L], vm.SearchMatchOffsets);

            vm.OnBytesDeleted(0, 10, cursorPositionAfter: 0);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal(-1, vm.CurrentSearchMatchIndex);
            Assert.Equal("", vm.SearchStatusText);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task FindNext_AfterShrinkingEdit_MovesToCurrentMatch()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.SearchText = "AB";
            vm.OnBytesDeleted(0, 10, cursorPositionAfter: 0);

            vm.FindNextCommand.Execute(null);

            Assert.Equal(85, vm.CursorPosition);
            Assert.Equal(85, vm.SelectionStart);
            Assert.Equal(1, vm.SelectionLength);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
