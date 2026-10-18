using System.Text;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelUndoTests : IDisposable
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
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
    }

    private sealed class StubClipboardService : IClipboardService
    {
        private byte[]? _bytes;

        public Task SetBytesAsync(byte[] bytes, string? text)
        {
            _bytes = bytes;
            return Task.CompletedTask;
        }

        public Task<byte[]?> TryGetBytesAsync() => Task.FromResult(_bytes);
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }

    [Fact]
    public async Task Undo_AfterSave_RestoresByteAndCursor()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.CursorPosition = 0;
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            vm.CursorPosition = 1;
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.True(vm.CanUndo);
            Assert.False(vm.CanRedo);
            Assert.DoesNotContain("*", vm.Title);

            vm.UndoCommand.Execute(null);

            Assert.Equal(1, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(0, vm.CursorPosition);
            Assert.StartsWith("*", vm.Title);

            vm.RedoCommand.Execute(null);

            Assert.Equal(0xFF, vm.Document.Buffer.ReadByte(0));
            Assert.DoesNotContain("*", vm.Title);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task Undo_AfterSaveThenEdit_UndoesNewEditFirst()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.OnByteModified(0, 0xAA, cursorPositionAfter: 1);
            await vm.SaveFileCommand.ExecuteAsync(null);

            vm.OnByteModified(1, 0xBB, cursorPositionAfter: 2);

            vm.UndoCommand.Execute(null);

            Assert.Equal(0xAA, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(2, vm.Document.Buffer.ReadByte(1));
            Assert.True(vm.CanUndo);

            vm.UndoCommand.Execute(null);

            Assert.Equal(1, vm.Document.Buffer.ReadByte(0));
            Assert.False(vm.CanUndo);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task Redo_AfterUndo_RestoresEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            vm.UndoCommand.Execute(null);
            Assert.Equal(1, vm.Document!.Buffer.ReadByte(0));

            vm.RedoCommand.Execute(null);

            Assert.Equal(0xFF, vm.Document.Buffer.ReadByte(0));
            Assert.True(vm.CanUndo);
            Assert.False(vm.CanRedo);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task Redo_AfterHexInput_RestoresCursorAfterEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.CursorPosition = 0;

            vm.OnByteModified(0, 0xA0, cursorPositionAfter: 0);
            vm.OnByteModified(0, 0xAB, cursorPositionAfter: 1);
            vm.CursorPosition = 1;

            vm.UndoCommand.Execute(null);
            vm.UndoCommand.Execute(null);
            vm.RedoCommand.Execute(null);
            vm.RedoCommand.Execute(null);

            Assert.Equal(0xAB, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task Undo_AfterHexInput_RestoresCursorBeforeEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.CursorPosition = 0;

            vm.OnByteModified(0, 0xA0, cursorPositionAfter: 0);
            vm.OnByteModified(0, 0xAB, cursorPositionAfter: 1);
            vm.CursorPosition = 1;

            vm.UndoCommand.Execute(null);
            vm.UndoCommand.Execute(null);

            Assert.Equal(1, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(0, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task Undo_AfterHexInputAndSave_RestoresOriginalByte()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.CursorPosition = 0;
            vm.OnByteModified(0, 0xA0, cursorPositionAfter: 0);
            vm.OnByteModified(0, 0xAB, cursorPositionAfter: 1);
            vm.CursorPosition = 1;
            await vm.SaveFileCommand.ExecuteAsync(null);

            vm.UndoCommand.Execute(null);
            Assert.Equal(0xA0, vm.Document!.Buffer.ReadByte(0));

            vm.UndoCommand.Execute(null);
            Assert.Equal(1, vm.Document.Buffer.ReadByte(0));
            Assert.Equal(0, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task RedoAndUndo_AfterRangeDelete_RestoreCursorAroundEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.SelectionStart = 1;
            vm.SelectionLength = 2;
            vm.CursorPosition = 2;

            vm.OnBytesDeleted(1, 2, cursorPositionAfter: 1);
            vm.CursorPosition = 1;

            vm.UndoCommand.Execute(null);
            Assert.Equal(2, vm.CursorPosition);

            vm.RedoCommand.Execute(null);
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task RedoAndUndo_AfterCut_RestoreCursorAroundEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.CursorPosition = 2;
            vm.SelectionStart = 1;
            vm.SelectionLength = 2;

            await vm.CutCommand.ExecuteAsync(null);
            Assert.Equal(1, vm.CursorPosition);

            vm.UndoCommand.Execute(null);
            Assert.Equal(2, vm.CursorPosition);

            vm.RedoCommand.Execute(null);
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task RedoAndUndo_AfterPaste_RestoreCursorAroundEdit()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.CursorPosition = 0;
            vm.SelectionStart = 0;
            vm.SelectionLength = 2;
            await vm.CopyCommand.ExecuteAsync(null);

            vm.CursorPosition = 3;
            await vm.PasteCommand.ExecuteAsync(null);
            Assert.Equal(5, vm.CursorPosition);

            vm.UndoCommand.Execute(null);
            Assert.Equal(3, vm.CursorPosition);

            vm.RedoCommand.Execute(null);
            Assert.Equal(5, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
