using System.Text;
using Avalonia.Threading;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowSaveTests : IDisposable
{
    static MainWindowSaveTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowSaveTests() => Directory.CreateDirectory(_tempDir);

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

    [Fact]
    public Task MainWindow_HexViewIsReadOnlyWhileSaving() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, window) = await ShowAsync();
        GatedByteBuffer? gate = null;
        try
        {
            vm.OnBytesDeleted(0, 1, cursorPositionAfter: 0);
            vm.Document!.SaveSourceWrapper = source => gate = new GatedByteBuffer(source);
            Assert.False(window.HexView.IsReadOnly);

            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await HeadlessTestApp.WaitUntilAsync(() => gate is not null);

            Assert.True(window.HexView.IsReadOnly);

            gate!.OpenAll();
            await save;
            await vm.SaveCompletion;

            Assert.False(window.HexView.IsReadOnly);
        }
        finally
        {
            gate?.OpenAll();
            await vm.SaveCompletion;
            await CloseAsync(vm, window);
        }
    });
}
