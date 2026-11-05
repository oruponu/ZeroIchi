using System.Text;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowInsertModeTests : IDisposable
{
    static MainWindowInsertModeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowInsertModeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [0x11, 0x22]);

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

    private static void PressInsert(MainWindow window)
    {
        window.KeyPress(Key.Insert, RawInputModifiers.None, PhysicalKey.Insert, null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task InsertKey_TogglesInsertMode() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            window.HexView.Focus();

            PressInsert(window);
            Assert.True(vm.IsInsertMode);

            PressInsert(window);
            Assert.False(vm.IsInsertMode);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task InsertKey_InTextBox_DoesNotToggle() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            vm.OpenSearchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(window.SearchTextBox, window.FocusManager?.GetFocusedElement());

            PressInsert(window);

            Assert.False(vm.IsInsertMode);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });

    [Fact]
    public Task StatusBar_ShowsCurrentMode() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            Assert.True(window.OverwriteModeText.IsVisible);
            Assert.False(window.InsertModeText.IsVisible);

            vm.IsInsertMode = true;
            Dispatcher.UIThread.RunJobs();

            Assert.False(window.OverwriteModeText.IsVisible);
            Assert.True(window.InsertModeText.IsVisible);
        }
        finally
        {
            await CloseAsync(vm, window);
        }
    });
}
