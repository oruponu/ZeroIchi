using System.Text;
using Avalonia.Threading;
using ZeroIchi.Services;
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

    private sealed class CountingDialogService : IDialogService
    {
        public int SaveChangesDialogCount { get; private set; }

        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);

        public Task<SaveChangesResult> ShowSaveChangesDialogAsync()
        {
            SaveChangesDialogCount++;
            return Task.FromResult(SaveChangesResult.Discard);
        }

        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
        public Task ShowAboutAsync() => Task.CompletedTask;
    }

    private async Task<(MainWindowViewModel Vm, MainWindow Window)> ShowAsync(IDialogService? dialog = null)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x100]);

        var vm = CreateViewModel(dialog);
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

    [Fact]
    public Task Closing_WhileSaving_ClosesAfterTheSave() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, window) = await ShowAsync(dialog);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        GatedByteBuffer? gate = null;
        try
        {
            vm.OnBytesDeleted(0, 1, cursorPositionAfter: 0);
            vm.Document!.SaveSourceWrapper = source => gate = new GatedByteBuffer(source);

            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await HeadlessTestApp.WaitUntilAsync(() => gate is not null);

            window.Close();
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.False(closed);

            gate!.OpenAll();
            await HeadlessTestApp.WaitUntilAsync(() => closed);
            await save;

            Assert.Equal(0, dialog.SaveChangesDialogCount);
        }
        finally
        {
            gate?.OpenAll();
            await vm.SaveCompletion;
            await vm.DisposeDocumentAsync();
            if (!closed) window.Close();
        }
    });
}
