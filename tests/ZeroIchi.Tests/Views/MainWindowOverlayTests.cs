using System.Text;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Views;

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

        var vm = CreateViewModel();
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
    public Task SearchBusyIndicator_DoesNotShiftTheSearchBar() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            vm.OpenSearchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var findNext = window.SearchBar.GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Command == vm.FindNextCommand);
            var indicator = window.SearchBar.GetLogicalDescendants().OfType<TextBlock>()
                .Single(t => t.Text == "検索中");
            var barBounds = window.SearchBar.Bounds;
            var buttonBounds = findNext.Bounds;

            vm.IsSearching = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, indicator.Opacity);
            Assert.True(indicator.Bounds.Width > 0);
            Assert.Equal(barBounds, window.SearchBar.Bounds);
            Assert.Equal(buttonBounds, findNext.Bounds);

            vm.IsSearching = false;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, indicator.Opacity);
            Assert.Equal(barBounds, window.SearchBar.Bounds);
            Assert.Equal(buttonBounds, findNext.Bounds);
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
}
