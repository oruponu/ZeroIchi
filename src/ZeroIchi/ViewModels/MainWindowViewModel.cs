using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Runtime;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileTypes;
using ZeroIchi.Services;

namespace ZeroIchi.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const int ClipboardTextMaxBytes = 16 * 1024 * 1024;

    private readonly IDialogService _dialog;
    private readonly IClipboardService _clipboard;
    private readonly IWindowService _window;

    private bool _isSyncingSelection;
    private int _replacingDocumentCount;

    public MainWindowViewModel(IDialogService dialog, IClipboardService clipboard, IWindowService window)
    {
        _dialog = dialog;
        _clipboard = clipboard;
        _window = window;
        Document = BinaryDocument.CreateNew();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    public partial BinaryDocument? Document { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
    public partial bool IsSaving { get; private set; }

    internal Task SaveCompletion { get; private set; } = Task.CompletedTask;

    internal TimeSpan SaveBusyDelay { get; init; } = TimeSpan.FromMilliseconds(300);

    internal TimeSpan SaveProgressInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    [ObservableProperty]
    public partial string SaveProgressText { get; private set; } = "";

    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Untitled - ZeroIchi";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    public partial int DataVersion { get; set; }

    [ObservableProperty]
    public partial long CursorPosition { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    public partial long SelectionStart { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    public partial long SelectionLength { get; set; }

    [ObservableProperty]
    public partial string StatusBarPositionText { get; set; } = "00000000";

    [ObservableProperty]
    public partial string StatusBarFileTypeText { get; set; } = "";

    [ObservableProperty]
    public partial string StatusBarSizeText { get; set; } = "0 B";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseOverlayCommand))]
    public partial bool IsGoToOffsetVisible { get; set; }

    [ObservableProperty]
    public partial string GoToOffsetText { get; set; } = "";

    [ObservableProperty]
    public partial string GoToOffsetError { get; set; } = "";

    [ObservableProperty]
    public partial bool IsHexGoToOffset { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseOverlayCommand))]
    public partial bool IsSearchVisible { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsHexSearch { get; set; } = true;

    [ObservableProperty]
    public partial long[] SearchMatchOffsets { get; set; } = [];

    [ObservableProperty]
    public partial int SearchMatchLength { get; set; }

    [ObservableProperty]
    public partial int CurrentSearchMatchIndex { get; set; } = -1;

    [ObservableProperty]
    public partial long CurrentSearchMatchOffset { get; set; } = -1;

    [ObservableProperty]
    public partial string SearchStatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsStructureTreeVisible { get; set; } = true;

    public ObservableCollection<StructureTreeItem> StructureTreeItems { get; } = [];

    [ObservableProperty]
    public partial StructureColorMap? StructureColors { get; set; }

    [ObservableProperty]
    public partial bool IsStructureParsing { get; set; }

    [ObservableProperty]
    public partial StructureTreeItem? SelectedStructureItem { get; set; }

    [ObservableProperty]
    public partial bool IsInspectorVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsInspectorBigEndian { get; set; }

    public ObservableCollection<InspectorRow> InspectorRows { get; } = [];

    [RelayCommand]
    private void ToggleStructureTree()
    {
        IsStructureTreeVisible = !IsStructureTreeVisible;
    }

    [RelayCommand]
    private void ToggleInspector()
    {
        IsInspectorVisible = !IsInspectorVisible;
    }

    [RelayCommand]
    private Task ShowAboutAsync() => _dialog.ShowAboutAsync();

    partial void OnCursorPositionChanged(long value)
    {
        UpdateStatusBar();
        UpdateInspector();
        if (!_isSyncingSelection)
            SyncStructureTreeSelection(value);
    }

    partial void OnIsSavingChanged(bool value) => UpdateUndoRedoState();

    partial void OnSelectionStartChanged(long value) => UpdateStatusBar();
    partial void OnSelectionLengthChanged(long value) => UpdateStatusBar();

    partial void OnDocumentChanged(BinaryDocument? oldValue, BinaryDocument? newValue)
    {
        if (oldValue is not null)
            oldValue.Changed -= OnDocumentContentChanged;
        if (newValue is not null)
            newValue.Changed += OnDocumentContentChanged;

        if (newValue?.Buffer is not { Length: > 0 })
            StatusBarFileTypeText = "";
        ClearStructure();
        ScheduleStructureUpdate(immediate: true);
        UpdateStatusBar();
        UpdateInspector();
    }

    private void OnDocumentContentChanged(object? sender, EventArgs e)
    {
        ClearSearchResults();
        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    partial void OnDataVersionChanged(int value)
    {
        ScheduleStructureUpdate(immediate: false);
        UpdateStatusBar();
        UpdateInspector();
    }

    private void UpdateStatusBar()
    {
        var buffer = Document?.Buffer;
        if (buffer is null)
        {
            StatusBarPositionText = "";
            StatusBarFileTypeText = "";
            StatusBarSizeText = "";
            return;
        }

        if (SelectionLength > 1)
        {
            var selEnd = SelectionStart + SelectionLength - 1;
            StatusBarPositionText = $"{SelectionStart:X8} - {selEnd:X8} ({SelectionLength} バイト)";
        }
        else
        {
            StatusBarPositionText = $"{CursorPosition:X8}";
        }

        StatusBarSizeText = FileSizeText.Format(buffer.Length);
    }

    private void UpdateFileType(ByteBuffer buffer) =>
        StatusBarFileTypeText = FileTypeDetector.Default.Detect(buffer)?.Extension ?? "";

    internal static void WarmUpFileTypeDetection() => FileTypeDetector.Default.Detect(new ArrayByteBuffer(new byte[1024]));

    internal async Task PauseBackgroundWorkAsync()
    {
        await Task.WhenAll(PauseStructureUpdatesAsync(), PauseSearchAsync());
    }

    internal void ResumeBackgroundWork()
    {
        ResumeStructureUpdates();
        ResumeSearch();
    }

    // Avalonia bindings cache the old Document, so release Data explicitly
    private async Task ReplaceDocumentAsync(BinaryDocument newDocument)
    {
        _replacingDocumentCount++;
        try
        {
            await PauseBackgroundWorkAsync();
            try
            {
                var oldDocument = Document;
                Document = newDocument;
                oldDocument?.Dispose();

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

                CursorPosition = 0;
                SelectionStart = 0;
                SelectionLength = 0;
                UpdateUndoRedoState();
                UpdateTitle();
                ClearSearchResults();
            }
            finally
            {
                ResumeBackgroundWork();
            }
        }
        finally
        {
            _replacingDocumentCount--;
        }
    }
}
