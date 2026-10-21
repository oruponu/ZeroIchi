using System.Text;
using Avalonia.Threading;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi.Tests;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowOverlayTests : IDisposable
{
    static MainWindowOverlayTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowOverlayTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x100]);

        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService());
        await vm.OpenFileAsync(path);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (vm, window);
    }

    private static async Task CloseAsync(MainWindowViewModel vm, MainWindow window)
    {
        await vm.DisposeDocumentAsync();
        window.Close();
    }

    private static object? FocusedElement(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        return window.FocusManager?.GetFocusedElement();
    }

    [Fact]
    public Task OpenSearch_WhileOpen_FocusesTheSearchBoxAgain() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            vm.OpenSearchCommand.Execute(null);
            Assert.Same(window.SearchTextBox, FocusedElement(window));

            window.HexView.Focus();
            vm.OpenSearchCommand.Execute(null);

            Assert.Same(window.SearchTextBox, FocusedElement(window));
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task OpenGoToOffset_WhileOpen_FocusesTheOffsetBoxAgain() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            vm.OpenGoToOffsetCommand.Execute(null);
            Assert.Same(window.GoToOffsetTextBox, FocusedElement(window));

            window.HexView.Focus();
            vm.OpenGoToOffsetCommand.Execute(null);

            Assert.Same(window.GoToOffsetTextBox, FocusedElement(window));
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task OpenSearch_ClosesTheGoToOffsetBar() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            vm.OpenGoToOffsetCommand.Execute(null);
            vm.OpenSearchCommand.Execute(null);

            Assert.False(vm.IsGoToOffsetVisible);
            Assert.True(vm.IsSearchVisible);
            Assert.Same(window.SearchTextBox, FocusedElement(window));
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task OpenGoToOffset_ClosesTheSearchBar() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            vm.OpenSearchCommand.Execute(null);
            vm.OpenGoToOffsetCommand.Execute(null);

            Assert.False(vm.IsSearchVisible);
            Assert.True(vm.IsGoToOffsetVisible);
            Assert.Same(window.GoToOffsetTextBox, FocusedElement(window));
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

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
}
