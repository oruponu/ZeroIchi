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
    private Func<Task<bool>>? _confirmDiscardChanges;
    private readonly UndoRedoManager _undoRedoManager = new();
    private IEditCommand? _lastExecutedCommand;
    private byte[]? _copiedBytes;
    private bool _isSyncingSelection;

    [ObservableProperty]
    private BinaryDocument? _document = BinaryDocument.CreateNew();

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string _title = "Untitled - ZeroIchi";

    [ObservableProperty]
    private int _dataVersion;

    [ObservableProperty]
    private int _cursorPosition;

    [ObservableProperty]
    private int _selectionStart;

    [ObservableProperty]
    private int _selectionLength;

    [ObservableProperty]
    private string _statusBarPositionText = "00000000";

    [ObservableProperty]
    private string _statusBarFileTypeText = "";

    [ObservableProperty]
    private string _statusBarSizeText = "0 B";

    [ObservableProperty]
    private bool _isGoToOffsetVisible;

    [ObservableProperty]
    private string _goToOffsetText = "";

    [ObservableProperty]
    private string _goToOffsetError = "";

    [ObservableProperty]
    private bool _isSearchVisible;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isHexSearch = true;

    [ObservableProperty]
    private int[] _searchMatchOffsets = [];

    [ObservableProperty]
    private int _searchMatchLength;

    [ObservableProperty]
    private int _currentSearchMatchIndex = -1;

    [ObservableProperty]
    private string _searchStatusText = "";

    [ObservableProperty]
    private bool _isStructureTreeVisible = true;

    public ObservableCollection<StructureTreeItem> StructureTreeItems { get; } = [];

    [ObservableProperty]
    private StructureColorMap? _structureColors;

    [ObservableProperty]
    private StructureTreeItem? _selectedStructureItem;

    [ObservableProperty]
    private bool _isInspectorVisible = true;

    [ObservableProperty]
    private bool _isInspectorBigEndian;

    [ObservableProperty]
    private List<DataInspectorEntry> _inspectorEntries = [];

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

    public void SetConfirmDiscardChanges(Func<Task<bool>> confirmDiscardChanges)
    {
        _confirmDiscardChanges = confirmDiscardChanges;
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
