using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelCloseTests : IDisposable
{
    static MainWindowViewModelCloseTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelCloseTests() => Directory.CreateDirectory(_tempDir);

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

    private string WriteFile()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x100]);
        return path;
    }

    private async Task<(MainWindowViewModel Vm, string Path)> OpenDeletedAsync(CountingDialogService dialog)
    {
        var vm = ViewModelTestFactory.CreateViewModel(dialog);
        var path = WriteFile();
        await vm.OpenFileAsync(path);
        vm.OnBytesDeleted(0, 1, cursorPositionAfter: 0);
        return (vm, path);
    }

    private static async Task CleanUpAsync(MainWindowViewModel vm, GatedByteBuffer? gate, FailingByteBuffer? failing)
    {
        gate?.OpenAll();
        failing?.Open();
        await vm.SaveCompletion;
        await vm.DisposeDocumentAsync();
    }

    [Fact]
    public Task PrepareClose_WhileSaving_WaitsAndClosesWithoutAsking() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, _) = await OpenDeletedAsync(dialog);
        GatedByteBuffer? gate = null;
        vm.Document!.SaveSourceWrapper = source => gate = new GatedByteBuffer(source);
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await HeadlessTestApp.WaitUntilAsync(() => gate is not null);

            var close = vm.PrepareCloseAsync();
            Assert.False(close.IsCompleted);

            gate!.OpenAll();
            Assert.True(await close);
            await save;

            Assert.False(vm.Document!.IsModified);
            Assert.Equal(0, dialog.SaveChangesDialogCount);
        }
        finally { await CleanUpAsync(vm, gate, null); }
    });

    [Fact]
    public Task PrepareClose_DuringAFailingSave_AsksOnceAfterTheError() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, _) = await OpenDeletedAsync(dialog);
        var pathB = WriteFile();
        FailingByteBuffer? failing = null;
        vm.Document!.SaveSourceWrapper = source => failing = new FailingByteBuffer(source);
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await HeadlessTestApp.WaitUntilAsync(() => failing is not null);

            var close = vm.PrepareCloseAsync();
            dialog.SaveChangesAnswer = SaveChangesResult.Cancel;
            failing!.Open();

            Assert.False(await close);
            await save;
            Assert.Equal(1, dialog.ErrorCount);
            Assert.Equal(1, dialog.SaveChangesDialogCount);

            dialog.SaveChangesAnswer = SaveChangesResult.Discard;
            await vm.OpenFileAsync(pathB);

            Assert.Equal(pathB, vm.Document!.FilePath);
        }
        finally { await CleanUpAsync(vm, null, failing); }
    });

    [Fact]
    public Task OpenWithSave_ThenCloseDuringTheSave_ClosesWithoutOpening() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService { SaveChangesAnswer = SaveChangesResult.Save };
        var (vm, pathA) = await OpenDeletedAsync(dialog);
        var pathB = WriteFile();
        GatedByteBuffer? gate = null;
        vm.Document!.SaveSourceWrapper = source => gate = new GatedByteBuffer(source);
        try
        {
            var open = vm.OpenFileAsync(pathB);
            await HeadlessTestApp.WaitUntilAsync(() => vm.IsSaving);

            var close = vm.PrepareCloseAsync();
            Assert.False(close.IsCompleted);

            await HeadlessTestApp.WaitUntilAsync(() => gate is not null);
            gate!.OpenAll();
            Assert.True(await close);
            await open;

            Assert.Equal(pathA, vm.Document!.FilePath);
            Assert.False(vm.Document.IsModified);
            Assert.Equal(1, dialog.SaveChangesDialogCount);
        }
        finally { await CleanUpAsync(vm, gate, null); }
    });

    [Fact]
    public Task OpenWithSave_ThenCloseDuringAFailingSave_KeepsTheDocumentAndAsksOnce() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService { SaveChangesAnswer = SaveChangesResult.Save };
        var (vm, pathA) = await OpenDeletedAsync(dialog);
        var pathB = WriteFile();
        FailingByteBuffer? failing = null;
        vm.Document!.SaveSourceWrapper = source => failing = new FailingByteBuffer(source);
        try
        {
            var open = vm.OpenFileAsync(pathB);
            await HeadlessTestApp.WaitUntilAsync(() => failing is not null);

            var close = vm.PrepareCloseAsync();
            dialog.SaveChangesAnswer = SaveChangesResult.Cancel;
            failing!.Open();

            Assert.False(await close);
            await open;

            Assert.Equal(pathA, vm.Document!.FilePath);
            Assert.True(vm.Document.IsModified);
            Assert.Equal(2, dialog.SaveChangesDialogCount);
            Assert.Equal(1, dialog.ErrorCount);
        }
        finally { await CleanUpAsync(vm, null, failing); }
    });
}
