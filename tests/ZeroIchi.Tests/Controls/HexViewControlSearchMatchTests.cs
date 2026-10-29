using System.Text;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlSearchMatchTests : IDisposable
{
    static HexViewControlSearchMatchTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlSearchMatchTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x10000]);

        var vm = CreateViewModel();
        await vm.OpenFileAsync(path);
        var window = new MainWindow { DataContext = vm, Width = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (vm, window);
    }

    private static async Task CloseAsync(MainWindowViewModel vm, MainWindow window)
    {
        await vm.DisposeDocumentAsync();
        window.Close();
    }

    private static IScrollable ScrollAwayFromTheMatch(MainWindowViewModel vm, MainWindow window)
    {
        vm.CursorPosition = 0x20;
        vm.SearchMatchLength = 1;
        vm.SearchMatchOffsets = [0x20];
        vm.CurrentSearchMatchOffset = 0x20;
        Dispatcher.UIThread.RunJobs();
        var scrollable = (IScrollable)window.HexView;
        scrollable.Offset = new Vector(0, 2000);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2000, scrollable.Offset.Y);
        return scrollable;
    }

    [Fact]
    public Task ShiftingTheMatchIndex_KeepsTheScrollPosition() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            var scrollable = ScrollAwayFromTheMatch(vm, window);

            vm.SearchMatchOffsets = [0x10, 0x20];
            vm.CurrentSearchMatchIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2000, scrollable.Offset.Y);
        }
        finally { await CloseAsync(vm, window); }
    });

    [Fact]
    public Task ChangingTheCurrentMatchOffset_ScrollsToTheCursor() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        try
        {
            var scrollable = ScrollAwayFromTheMatch(vm, window);

            vm.CurrentSearchMatchOffset = -1;
            vm.CurrentSearchMatchOffset = 0x20;
            Dispatcher.UIThread.RunJobs();

            Assert.True(scrollable.Offset.Y < 2000);
        }
        finally { await CloseAsync(vm, window); }
    });
}
