using System.Text;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

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
}
