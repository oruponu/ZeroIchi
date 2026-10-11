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

    [Theory]
    [InlineData("80000000", true, 0x80000000L)]
    [InlineData("1A", true, 0x1AL)]
    [InlineData("1a", true, 0x1AL)]
    [InlineData("100", true, 0x100L)]
    [InlineData("0x1A", true, 0x1AL)]
    [InlineData("0X1A", false, 0x1AL)]
    [InlineData("100", false, 100L)]
    [InlineData("80000000", false, 80000000L)]
    public void TryParseOffset_ParsesByRadix(string text, bool isHex, long expected)
    {
        Assert.True(MainWindowViewModel.TryParseOffset(text, isHex, out var offset));
        Assert.Equal(expected, offset);
    }

    [Theory]
    [InlineData("1A", false)]
    [InlineData("G", true)]
    [InlineData("0x", true)]
    [InlineData("0x", false)]
    [InlineData("", true)]
    public void TryParseOffset_RejectsInvalidText(string text, bool isHex)
    {
        Assert.False(MainWindowViewModel.TryParseOffset(text, isHex, out _));
    }

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
            vm.Document?.Dispose();
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
            vm.Document?.Dispose();
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
            vm.Document?.Dispose();
        }
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }
}
