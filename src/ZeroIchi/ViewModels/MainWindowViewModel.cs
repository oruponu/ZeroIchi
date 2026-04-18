using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.Commands;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Services;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly IContentInspector Inspector = new ContentInspectorBuilder
    {
        Definitions = new CondensedBuilder
        {
            UsageType = UsageType.PersonalNonCommercial,
        }.Build(),
    }.Build();

    private IStorageProvider? _storageProvider;
    private IClipboard? _clipboard;
    private Action? _closeAction;
    private Func<Task<SaveChangesResult>>? _showSaveChangesDialog;
    private readonly UndoRedoManager _undoRedoManager = new();
    private IEditCommand? _lastExecutedCommand;
    private byte[]? _copiedBytes;
    private bool _isSyncingSelection;

    [ObservableProperty]
    public partial BinaryDocument? Document { get; set; } = BinaryDocument.CreateNew();

    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Untitled - ZeroIchi";

    [ObservableProperty]
    public partial int DataVersion { get; set; }

    [ObservableProperty]
    public partial int CursorPosition { get; set; }

    [ObservableProperty]
    public partial int SelectionStart { get; set; }

    [ObservableProperty]
    public partial int SelectionLength { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(CloseOverlayCommand))]
    public partial bool IsSearchVisible { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsHexSearch { get; set; } = true;

    [ObservableProperty]
    public partial int[] SearchMatchOffsets { get; set; } = [];

    [ObservableProperty]
    public partial int SearchMatchLength { get; set; }

    [ObservableProperty]
    public partial int CurrentSearchMatchIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string SearchStatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsStructureTreeVisible { get; set; } = true;

    public ObservableCollection<StructureTreeItem> StructureTreeItems { get; } = [];

    [ObservableProperty]
    public partial StructureColorMap? StructureColors { get; set; }

    [ObservableProperty]
    public partial StructureTreeItem? SelectedStructureItem { get; set; }

    [ObservableProperty]
    public partial bool IsInspectorVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsInspectorBigEndian { get; set; }

    [ObservableProperty]
    public partial List<DataInspectorEntry> InspectorEntries { get; set; } = [];

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

    public void SetStorageProvider(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    public void SetClipboard(IClipboard? clipboard)
    {
        _clipboard = clipboard;
    }

    public void SetCloseAction(Action closeAction)
    {
        _closeAction = closeAction;
    }

    public void SetShowSaveChangesDialog(Func<Task<SaveChangesResult>> showSaveChangesDialog)
    {
        _showSaveChangesDialog = showSaveChangesDialog;
    }

    partial void OnCursorPositionChanged(int value)
    {
        UpdateStatusBar();
        UpdateInspector();
        if (!_isSyncingSelection)
            SyncStructureTreeSelection(value);
    }

    partial void OnSelectionStartChanged(int value) => UpdateStatusBar();
    partial void OnSelectionLengthChanged(int value) => UpdateStatusBar();

    partial void OnDocumentChanged(BinaryDocument? value)
    {
        if (value?.Buffer is { Length: > 0 } buffer)
        {
            UpdateFileType(buffer);
            UpdateStructureTree(buffer);
        }
        else
        {
            StatusBarFileTypeText = "";
            StructureTreeItems.Clear();
            StructureColors = null;
            SelectedStructureItem = null;
        }
        UpdateStatusBar();
        UpdateInspector();
    }

    partial void OnDataVersionChanged(int value)
    {
        if (Document?.Buffer is { Length: > 0 } buffer)
        {
            UpdateFileType(buffer);
            UpdateStructureTree(buffer);
        }
        else
        {
            StructureTreeItems.Clear();
            StructureColors = null;
            SelectedStructureItem = null;
        }

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

        StatusBarSizeText = FormatFileSize(buffer.Length);
    }

    private void UpdateFileType(ByteBuffer buffer)
    {
        var length = (int)Math.Min(buffer.Length, 1024);
        var header = buffer.SliceToArray(0, length);
        var results = Inspector.Inspect(header);
        var match = results.ByFileExtension();
        StatusBarFileTypeText = match.Length > 0 ? match[0].Extension : "";
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
        };
    }

    // Avalonia のバインディングが旧 Document をキャッシュするため、Data を明示的に解放する
    private void ReplaceDocument(BinaryDocument newDocument)
    {
        var oldDocument = Document;
        Document = newDocument;
        oldDocument?.ReleaseData();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
