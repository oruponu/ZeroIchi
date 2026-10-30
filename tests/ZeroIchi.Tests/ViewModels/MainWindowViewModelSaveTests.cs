using Avalonia.Threading;
using System.Text;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelSaveTests : IDisposable
{
    static MainWindowViewModelSaveTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const int FileSize = 262_144;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));
    private readonly List<GatedByteBuffer> _saveGates = [];
    private readonly List<GatedByteBuffer> _searchGates = [];
    private bool _saveGatesOpen;
    private bool _searchGatesOpen;
    private int _saveSourceCount;

    public MainWindowViewModelSaveTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private sealed class CountingDialogService : IDialogService
    {
        public SaveChangesResult SaveChangesAnswer { get; set; } = SaveChangesResult.Discard;
        public Action? SaveChangesDialogShowing { get; set; }
        public Action? ErrorShowing { get; set; }
        public int SaveChangesDialogCount { get; private set; }
        public int ErrorCount { get; private set; }

        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);

        public Task<SaveChangesResult> ShowSaveChangesDialogAsync()
        {
            SaveChangesDialogCount++;
            SaveChangesDialogShowing?.Invoke();
            return Task.FromResult(SaveChangesAnswer);
        }

        public Task ShowErrorAsync(string message, string detail)
        {
            ErrorCount++;
            ErrorShowing?.Invoke();
            return Task.CompletedTask;
        }

        public Task ShowAboutAsync() => Task.CompletedTask;
    }

    private sealed class PendingClipboardService : IClipboardService
    {
        public TaskCompletionSource<byte[]?> Bytes { get; } = new();

        public Task SetBytesAsync(byte[] bytes, string? text) => Task.CompletedTask;
        public Task<byte[]?> TryGetBytesAsync() => Bytes.Task;
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private static byte[] PatternBytes()
    {
        var bytes = new byte[FileSize];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(i % 251);
        return bytes;
    }

    private string WriteFile(byte[] bytes)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private MainWindowViewModel Create(CountingDialogService? dialog = null, IClipboardService? clipboard = null,
        bool gateSearch = false, TimeSpan? saveBusyDelay = null, TimeSpan? saveProgressInterval = null) =>
        new(dialog ?? new CountingDialogService(), clipboard ?? new StubClipboardService(), new StubWindowService())
        {
            SaveBusyDelay = saveBusyDelay ?? TimeSpan.FromMilliseconds(300),
            SaveProgressInterval = saveProgressInterval ?? TimeSpan.FromMilliseconds(100),
            SearchFlushInterval = TimeSpan.FromMilliseconds(10),
            SearchSource = gateSearch
                ? snapshot =>
                {
                    var gate = new GatedByteBuffer(snapshot);
                    if (_searchGatesOpen) gate.OpenAll();
                    _searchGates.Add(gate);
                    return gate;
                }
                : static snapshot => snapshot,
        };

    private void GateSaves(MainWindowViewModel vm) =>
        vm.Document!.SaveSourceWrapper = source =>
        {
            _saveSourceCount++;
            var gate = new GatedByteBuffer(source);
            if (_saveGatesOpen) gate.OpenAll();
            _saveGates.Add(gate);
            return gate;
        };

    private async Task<(MainWindowViewModel Vm, string Path, byte[] Expected)> OpenDeletedAsync(
        CountingDialogService? dialog = null, IClipboardService? clipboard = null, bool gateSearch = false,
        byte[]? content = null, TimeSpan? saveBusyDelay = null, TimeSpan? saveProgressInterval = null)
    {
        var vm = Create(dialog, clipboard, gateSearch, saveBusyDelay, saveProgressInterval);
        var bytes = content ?? PatternBytes();
        var path = WriteFile(bytes);
        await vm.OpenFileAsync(path);
        vm.OnBytesDeleted(0, 1, cursorPositionAfter: 0);
        GateSaves(vm);
        return (vm, path, bytes[1..]);
    }

    private Task WaitForSaveGateAsync() => HeadlessTestApp.WaitUntilAsync(() => _saveGates.Count > 0);

    private void OpenSaveGates()
    {
        _saveGatesOpen = true;
        foreach (var gate in _saveGates) gate.OpenAll();
    }

    private void OpenSearchGates()
    {
        _searchGatesOpen = true;
        foreach (var gate in _searchGates) gate.OpenAll();
    }

    private async Task CleanUpAsync(MainWindowViewModel vm)
    {
        OpenSaveGates();
        OpenSearchGates();
        await vm.SaveCompletion;
        await vm.SearchUpdate;
        await vm.DisposeDocumentAsync();
    }

    private static List<string> RecordSaveProgressText(MainWindowViewModel vm)
    {
        var shown = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SaveProgressText))
                shown.Add(vm.SaveProgressText);
        };
        return shown;
    }

    [Fact]
    public Task Saving_DisablesEditAndFileCommandsUntilTheSaveEnds() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _, _) = await OpenDeletedAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            Assert.True(vm.IsSaving);
            await WaitForSaveGateAsync();

            Assert.True(vm.IsSaving);
            Assert.False(vm.NewFileCommand.CanExecute(null));
            Assert.False(vm.OpenFileCommand.CanExecute(null));
            Assert.False(vm.SaveFileCommand.CanExecute(null));
            Assert.False(vm.SaveAsFileCommand.CanExecute(null));
            Assert.False(vm.UndoCommand.CanExecute(null));
            Assert.False(vm.RedoCommand.CanExecute(null));
            Assert.False(vm.CutCommand.CanExecute(null));
            Assert.False(vm.PasteCommand.CanExecute(null));
            Assert.False(vm.CanUndo);
            Assert.True(vm.CopyCommand.CanExecute(null));

            OpenSaveGates();
            await save;

            Assert.False(vm.IsSaving);
            Assert.True(vm.SaveAsFileCommand.CanExecute(null));
            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.True(vm.Document!.IsModified);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Saving_IgnoresByteEdits() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, path, expected) = await OpenDeletedAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await WaitForSaveGateAsync();

            vm.OnByteModified(1, 0x77, 1);
            vm.OnBytesDeleted(1, 1, 1);

            OpenSaveGates();
            await save;

            Assert.Equal(expected, File.ReadAllBytes(path));
            Assert.False(vm.Document!.IsModified);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Saving_KeepsCursorAndSelectionChanges() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _, _) = await OpenDeletedAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await WaitForSaveGateAsync();

            vm.CursorPosition = 100;
            vm.SelectionStart = 100;
            vm.SelectionLength = 4;
            Assert.True(vm.CopyCommand.CanExecute(null));

            OpenSaveGates();
            await save;

            Assert.Equal(100L, vm.CursorPosition);
            Assert.Equal(100L, vm.SelectionStart);
            Assert.Equal(4L, vm.SelectionLength);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task SaveRequests_DuringThePauseBeforeSaving_SaveOnce() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, _, _) = await OpenDeletedAsync(dialog, gateSearch: true);
        var document = vm.Document;
        var other = WriteFile(PatternBytes());
        try
        {
            vm.OpenSearchCommand.Execute(null);
            vm.SearchText = "AB";
            Assert.Single(_searchGates);

            var first = vm.SaveFileCommand.ExecuteAsync(null);
            Assert.True(vm.IsSaving);
            var second = vm.SaveFileCommand.ExecuteAsync(null);
            await vm.OpenFileAsync(other);

            OpenSearchGates();
            OpenSaveGates();
            await first;
            await second;
            await vm.SaveCompletion;

            Assert.Equal(1, _saveSourceCount);
            Assert.Same(document, vm.Document);
            Assert.Equal(0, dialog.SaveChangesDialogCount);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Save_WhenTheWriteFails_ClearsIsSavingAndShowsTheError() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, path, _) = await OpenDeletedAsync(dialog);
        Directory.CreateDirectory(path + ".zitmp");
        try
        {
            OpenSaveGates();
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ErrorCount);
            Assert.False(vm.IsSaving);
            Assert.True(vm.SaveFileCommand.CanExecute(null));
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task SlowSave_ShowsTheProgressAndClearsItWhenDone() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _, _) = await OpenDeletedAsync(saveBusyDelay: TimeSpan.FromMilliseconds(20),
            saveProgressInterval: TimeSpan.FromMilliseconds(10));
        try
        {
            _ = vm.SaveFileCommand.ExecuteAsync(null);
            await WaitForSaveGateAsync();

            _saveGates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.SaveProgressText == "保存中 31%");

            OpenSaveGates();
            await vm.SaveCompletion;

            Assert.Equal("", vm.SaveProgressText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task FastSave_DoesNotShowTheProgress() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _, _) = await OpenDeletedAsync(saveBusyDelay: TimeSpan.FromSeconds(1));
        var shown = RecordSaveProgressText(vm);
        try
        {
            OpenSaveGates();
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.False(vm.Document!.IsModified);
            Assert.All(shown, text => Assert.Equal("", text));
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task FailedSave_ClearsTheProgressBeforeTheErrorDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, path, _) = await OpenDeletedAsync(dialog, saveBusyDelay: TimeSpan.Zero);
        Directory.CreateDirectory(path + ".zitmp");
        var shown = RecordSaveProgressText(vm);
        string? textAtError = null;
        dialog.ErrorShowing = () => textAtError = vm.SaveProgressText;
        try
        {
            OpenSaveGates();
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ErrorCount);
            Assert.Contains("保存中 0%", shown);
            Assert.Equal("", textAtError);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task OpenFile_WhileSaving_DoesNothing() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, _, _) = await OpenDeletedAsync(dialog);
        var document = vm.Document;
        var other = WriteFile(PatternBytes());
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await WaitForSaveGateAsync();

            var open = vm.OpenFileAsync(other);
            Assert.True(open.IsCompleted);
            await open;

            Assert.Same(document, vm.Document);
            Assert.Equal(0, dialog.SaveChangesDialogCount);

            OpenSaveGates();
            await save;
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Save_DuringTheReplacementPause_DoesNotStart() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService { SaveChangesAnswer = SaveChangesResult.Discard };
        var (vm, _, _) = await OpenDeletedAsync(dialog, gateSearch: true);
        var other = WriteFile(PatternBytes());
        try
        {
            vm.OpenSearchCommand.Execute(null);
            vm.SearchText = "AB";
            Assert.Single(_searchGates);

            var open = vm.OpenFileAsync(other);
            Assert.False(open.IsCompleted);

            await vm.SaveFileCommand.ExecuteAsync(null);
            Assert.Equal(0, _saveSourceCount);
            Assert.False(vm.IsSaving);

            OpenSearchGates();
            await open;

            Assert.Equal(other, vm.Document!.FilePath);
            Assert.Equal(0, _saveSourceCount);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task OpenFile_WhenASaveStartsDuringTheConfirmation_KeepsTheDocument() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new CountingDialogService();
        var (vm, path, expected) = await OpenDeletedAsync(dialog);
        var document = vm.Document;
        var other = WriteFile(PatternBytes());
        Task? save = null;
        dialog.SaveChangesDialogShowing = () => save = vm.SaveFileCommand.ExecuteAsync(null);
        try
        {
            await vm.OpenFileAsync(other);

            Assert.Equal(1, dialog.SaveChangesDialogCount);
            Assert.Same(document, vm.Document);

            OpenSaveGates();
            await save!;

            Assert.Equal(expected, File.ReadAllBytes(path));
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task SaveFile_RaisesDocumentChangedOnTheUIThread() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _, _) = await OpenDeletedAsync();
        bool? onUiThread = null;
        vm.Document!.Changed += (_, _) => onUiThread = Dispatcher.UIThread.CheckAccess();
        try
        {
            OpenSaveGates();
            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.True(onUiThread);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task SearchTyping_WhileSaving_StartsAfterTheSave() => HeadlessTestApp.RunAsync(async () =>
    {
        var content = new byte[FileSize];
        content[10] = 0xAB;
        content[70000] = 0xAB;
        var (vm, _, _) = await OpenDeletedAsync(content: content);
        try
        {
            _ = vm.SaveFileCommand.ExecuteAsync(null);
            await WaitForSaveGateAsync();

            vm.OpenSearchCommand.Execute(null);
            vm.SearchText = "AB";
            Assert.Empty(vm.SearchMatchOffsets);

            OpenSaveGates();
            await vm.SaveCompletion;
            await vm.SearchUpdate;

            Assert.Equal([9L, 69999L], vm.SearchMatchOffsets);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Paste_WhenASaveStartsWhileReadingTheClipboard_DoesNotEdit() => HeadlessTestApp.RunAsync(async () =>
    {
        var clipboard = new PendingClipboardService();
        var (vm, path, expected) = await OpenDeletedAsync(clipboard: clipboard);
        var length = vm.Document!.Buffer.Length;
        try
        {
            var paste = vm.PasteCommand.ExecuteAsync(null);
            var save = vm.SaveFileCommand.ExecuteAsync(null);

            clipboard.Bytes.SetResult([1, 2]);
            await paste;

            Assert.Equal(length, vm.Document!.Buffer.Length);

            OpenSaveGates();
            await save;

            Assert.Equal(expected, File.ReadAllBytes(path));
        }
        finally { await CleanUpAsync(vm); }
    });
}
