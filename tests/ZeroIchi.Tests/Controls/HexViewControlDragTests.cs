using System.Text;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlDragTests : IDisposable
{
    static HexViewControlDragTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlDragTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x10000]);

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

    private static Point ToWindow(MainWindow window, double x, double y)
        => window.HexView.TranslatePoint(new Point(x, y), window)!.Value;

    private static IPointer PressAndCapturePointer(MainWindow window)
    {
        IPointer? pointer = null;
        window.HexView.AddHandler(InputElement.PointerPressedEvent,
            (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        window.MouseDown(ToWindow(window, 120, 60), MouseButton.Left);
        Assert.Same(window.HexView, pointer!.Captured);
        return pointer;
    }

    [Fact]
    public Task CaptureLost_PointerMovedAfterwards_DoesNotExtendSelection() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            var pointer = PressAndCapturePointer(window);
            var cursor = window.HexView.CursorPosition;
            Assert.Equal(1, window.HexView.SelectionLength);

            pointer.Capture(null);
            window.MouseMove(ToWindow(window, 300, 200));

            Assert.Equal(cursor, window.HexView.CursorPosition);
            Assert.Equal(1, window.HexView.SelectionLength);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task CaptureLost_WhileAutoScrolling_StopsExtendingSelection() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            var pointer = PressAndCapturePointer(window);
            window.MouseMove(ToWindow(window, 120, window.HexView.Bounds.Height + 50), RawInputModifiers.LeftMouseButton);

            pointer.Capture(null);
            var selectionLength = window.HexView.SelectionLength;
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(selectionLength, window.HexView.SelectionLength);
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
