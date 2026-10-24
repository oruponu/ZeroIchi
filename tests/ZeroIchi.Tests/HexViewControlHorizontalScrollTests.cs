using System.Text;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi.Tests;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlHorizontalScrollTests : IDisposable
{
    private const double RowY = 60;

    static HexViewControlHorizontalScrollTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlHorizontalScrollTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync(double width)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x10000]);

        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService());
        await vm.OpenFileAsync(path);
        var window = new MainWindow { DataContext = vm, Width = width };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var scrollable = (IScrollable)window.HexView;
        Assert.True(scrollable.Extent.Width > scrollable.Viewport.Width);
        return (vm, window);
    }

    private static async Task CloseAsync(MainWindowViewModel vm, MainWindow window)
    {
        await vm.DisposeDocumentAsync();
        window.Close();
    }

    private static double ScrollHorizontally(MainWindow window, double x)
    {
        // The headless app has no theme, so the ScrollViewer has no presenter to forward its offset
        var scrollable = (IScrollable)window.HexView;
        scrollable.Offset = new Vector(x, 0);
        Dispatcher.UIThread.RunJobs();
        return scrollable.Offset.X;
    }

    private static void Click(MainWindow window, double x)
    {
        var point = window.HexView.TranslatePoint(new Point(x, RowY), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(MainWindow window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Click_WhileScrolledHorizontally_HitsTheCellUnderThePointer() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync(800);
        try
        {
            Click(window, 250);
            var expected = window.HexView.CursorPosition;

            var scrollX = ScrollHorizontally(window, 100);
            Assert.True(scrollX > 0);
            Click(window, 250 - scrollX);

            Assert.Equal(expected, window.HexView.CursorPosition);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task Click_OnTheAddressColumnWhileScrolled_DoesNotSelectAHiddenCell() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync(800);
        try
        {
            Click(window, 250);
            var cursor = window.HexView.CursorPosition;
            Assert.True(ScrollHorizontally(window, 100) > 0);

            Click(window, 10);

            Assert.Equal(cursor, window.HexView.CursorPosition);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task KeyMove_ToAHiddenColumn_ScrollsItIntoView() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync(800);
        try
        {
            Click(window, 150);
            var scrollable = (IScrollable)window.HexView;

            Press(window, Key.End);
            Assert.True(scrollable.Offset.X > 0);

            Press(window, Key.Home);
            Assert.Equal(0, scrollable.Offset.X);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task CursorMove_WhileItsAsciiCellIsVisible_DoesNotScroll() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync(800);
        try
        {
            var scrollable = (IScrollable)window.HexView;
            var scrollX = ScrollHorizontally(window, scrollable.Extent.Width);
            Assert.True(scrollX > 0);

            Click(window, scrollable.Viewport.Width - 3);

            Assert.Equal(15, window.HexView.CursorPosition % 16);
            Assert.Equal(scrollX, scrollable.Offset.X);
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
