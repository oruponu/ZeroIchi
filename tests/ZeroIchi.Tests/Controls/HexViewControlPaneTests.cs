using System.Text;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlPaneTests : IDisposable
{
    private const double RowY = 60;

    static HexViewControlPaneTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlPaneTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x1000]);

        var vm = CreateViewModel();
        await vm.OpenFileAsync(path);
        var window = new MainWindow { DataContext = vm, Width = 1600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (vm, window);
    }

    private static async Task CloseAsync(MainWindowViewModel vm, MainWindow window)
    {
        await vm.DisposeDocumentAsync();
        window.Close();
    }

    private static Point ToWindow(MainWindow window, double x)
        => window.HexView.TranslatePoint(new Point(x, RowY), window)!.Value;

    private static void Click(MainWindow window, double x)
    {
        var point = ToWindow(window, x);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Shift)]
    public Task Tab_KeepsFocusOnHexView(RawInputModifiers modifiers) => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            window.HexView.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(window.HexView, window.FocusManager!.GetFocusedElement());
            Assert.Equal(HexViewPane.Ascii, window.HexView.ActivePane);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task ClickInAsciiColumn_SelectsAsciiPane() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            Click(window, window.HexView.AsciiStartX + 2);
            Assert.Equal(HexViewPane.Ascii, window.HexView.ActivePane);

            Click(window, 120);
            Assert.Equal(HexViewPane.Hex, window.HexView.ActivePane);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task DragFromAsciiIntoHex_KeepsAsciiPane() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            window.MouseDown(ToWindow(window, window.HexView.AsciiStartX + 2), MouseButton.Left);
            window.MouseMove(ToWindow(window, 120), RawInputModifiers.LeftMouseButton);
            window.MouseUp(ToWindow(window, 120), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(HexViewPane.Ascii, window.HexView.ActivePane);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ClickInOtherPane_AfterScrolling_KeepsScrollOffset(bool clickAscii) => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            var scrollable = (IScrollable)window.HexView;
            scrollable.Offset = new Vector(0, 3000);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3000, scrollable.Offset.Y);

            var x = clickAscii ? window.HexView.AsciiStartX + 2 : 120;
            var point = window.HexView.TranslatePoint(new Point(x, 200), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new Vector(0, 3000), scrollable.Offset);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });
}
