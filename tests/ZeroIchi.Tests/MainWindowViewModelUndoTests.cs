using System.Text;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public class MainWindowViewModelUndoTests : IDisposable
{
    static MainWindowViewModelUndoTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelUndoTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private static MainWindowViewModel CreateViewModel()
        => new(new StubDialogService(), new StubClipboardService(), new StubWindowService());

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }

    [Fact]
    public async Task Undo_AfterSave_DoesNothing()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.OnByteModified(0, 0xFF);
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.False(vm.CanUndo);
            Assert.False(vm.CanRedo);

            vm.UndoCommand.Execute(null);

            Assert.Equal(0xFF, vm.Document!.Buffer.ReadByte(0));
        }
        finally
        {
            vm.Document?.ReleaseData();
        }
    }

    [Fact]
    public async Task Undo_AfterSaveThenEdit_UndoesOnlyNewEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.OnByteModified(0, 0xAA);
            await vm.SaveFileCommand.ExecuteAsync(null);

            vm.OnByteModified(1, 0xBB);
            Assert.True(vm.CanUndo);

            vm.UndoCommand.Execute(null);

            Assert.Equal(0xAA, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(2, vm.Document.Buffer.ReadByte(1));
            Assert.False(vm.CanUndo);
        }
        finally
        {
            vm.Document?.ReleaseData();
        }
    }

    [Fact]
    public async Task Redo_AfterUndo_RestoresEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.OnByteModified(0, 0xFF);
            vm.UndoCommand.Execute(null);
            Assert.Equal(1, vm.Document!.Buffer.ReadByte(0));

            vm.RedoCommand.Execute(null);

            Assert.Equal(0xFF, vm.Document.Buffer.ReadByte(0));
            Assert.True(vm.CanUndo);
            Assert.False(vm.CanRedo);
        }
        finally
        {
            vm.Document?.ReleaseData();
        }
    }
}
