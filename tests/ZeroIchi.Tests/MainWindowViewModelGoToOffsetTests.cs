using System.Text;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelGoToOffsetTests : IDisposable
{
    static MainWindowViewModelGoToOffsetTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelGoToOffsetTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(int length)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[length]);
        return path;
    }

    private static MainWindowViewModel CreateViewModel()
        => new(new StubDialogService(), new StubClipboardService(), new StubWindowService());

    [Fact]
    public async Task GoToOffset_TreatsInputAsHexByDefault()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(0x200));
        try
        {
            vm.OpenGoToOffsetCommand.Execute(null);
            vm.GoToOffsetText = "100";
            vm.GoToOffsetCommand.Execute(null);

            Assert.Equal(0x100L, vm.CursorPosition);
            Assert.Equal(0x100L, vm.SelectionStart);
            Assert.Equal(1L, vm.SelectionLength);
            Assert.False(vm.IsGoToOffsetVisible);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task GoToOffset_TreatsInputAsDecimal_WhenHexModeIsOff()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(0x200));
        try
        {
            vm.IsHexGoToOffset = false;
            vm.GoToOffsetText = "100";
            vm.GoToOffsetCommand.Execute(null);

            Assert.Equal(100L, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task GoToOffset_ClearsError_WhenRadixChanges()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(0x200));
        try
        {
            vm.IsHexGoToOffset = false;
            vm.GoToOffsetText = "1A";
            vm.GoToOffsetCommand.Execute(null);
            Assert.Equal("無効な値", vm.GoToOffsetError);

            vm.IsHexGoToOffset = true;

            Assert.Equal("", vm.GoToOffsetError);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
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
}
