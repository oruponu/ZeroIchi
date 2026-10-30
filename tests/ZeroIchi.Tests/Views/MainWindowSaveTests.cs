using System.Text;
using Avalonia.Threading;
using ZeroIchi.Models.Buffers;
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
        public SaveChangesResult SaveChangesAnswer { get; set; } = SaveChangesResult.Discard;
        public int SaveChangesDialogCount { get; private set; }
        public int ErrorCount { get; private set; }

        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);

        public Task<SaveChangesResult> ShowSaveChangesDialogAsync()
        {
            SaveChangesDialogCount++;
            return Task.FromResult(SaveChangesAnswer);
        }

        public Task ShowErrorAsync(string message, string detail)
        {
            ErrorCount++;
            return Task.CompletedTask;
        }

        public Task ShowAboutAsync() => Task.CompletedTask;
    }

    private sealed class FailingByteBuffer(ByteBuffer inner) : ByteBuffer
    {
        private readonly ManualResetEventSlim _opened = new();

        public override long Length => inner.Length;

        public override byte ReadByte(long index) => inner.ReadByte(index);

        public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
        {
            _opened.Wait();
            throw new IOException("The write failed.");
        }

        public void Open() => _opened.Set();
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

    [Fact]
    public Task Closing_DuringAFailingSave_AsksOnceAndStaysOpen() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, window) = await ShowAsync(dialog);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        FailingByteBuffer? failing = null;
        try
        {
            vm.OnBytesDeleted(0, 1, cursorPositionAfter: 0);
            vm.Document!.SaveSourceWrapper = source => failing = new FailingByteBuffer(source);

            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await HeadlessTestApp.WaitUntilAsync(() => failing is not null);

            window.Close();
            window.Close();
            dialog.SaveChangesAnswer = SaveChangesResult.Cancel;
            failing!.Open();
            await save;
            await HeadlessTestApp.WaitUntilAsync(() => dialog.SaveChangesDialogCount > 0);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, dialog.ErrorCount);
            Assert.Equal(1, dialog.SaveChangesDialogCount);
            Assert.False(closed);
        }
        finally
        {
            failing?.Open();
            await vm.SaveCompletion;
            await vm.DisposeDocumentAsync();
            if (!closed) window.Close();
        }
    });
}
