using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

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

    [RelayCommand]
    private async Task NewFileAsync()
    {
        if (Document is { IsModified: true } && _confirmDiscardChanges is not null)
        {
            if (!await _confirmDiscardChanges())
                return;
        }

        ReplaceDocument(BinaryDocument.CreateNew());
        CursorPosition = 0;
        SelectionStart = 0;
        SelectionLength = 0;
        _undoRedoManager.Clear();
        UpdateUndoRedoState();
        UpdateTitle();
        ClearSearchResults();
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (_storageProvider is null)
            return;

        var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "ファイルを開く",
        });

        if (files.Count == 0)
            return;

        if (files[0].TryGetLocalPath() is not { } path)
            return;

        await OpenFileAsync(path);
    }

    public async Task OpenFileAsync(string path)
    {
        ReplaceDocument(await BinaryDocument.OpenAsync(path));
        UpdateTitle();
        CursorPosition = 0;
        SelectionStart = 0;
        SelectionLength = 0;
        _undoRedoManager.Clear();
        UpdateUndoRedoState();
        ClearSearchResults();
    }

    public void OnByteModified(int index, byte value)
    {
        if (Document is null) return;

        IEditCommand command;
        if (index == (int)Document.Buffer.Length)
        {
            command = new AppendByteCommand(Document, value, CursorPosition);
        }
        else
        {
            command = new WriteByteCommand(Document, index, value, CursorPosition);
        }

        _undoRedoManager.ExecuteCommand(command);
        _lastExecutedCommand = command;
        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    public void CaptureCursorAfterEdit()
    {
        _lastExecutedCommand?.CursorPositionAfter = CursorPosition;
        _lastExecutedCommand = null;
    }

    public void OnBytesDeleted(int index, int count)
    {
        if (Document is null) return;

        var command = new DeleteBytesCommand(Document, index, count, CursorPosition);
        _undoRedoManager.ExecuteCommand(command);
        command.CursorPositionAfter = CursorPosition;
        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private void Undo()
    {
        if (Document is null) return;

        var command = _undoRedoManager.Undo();
        if (command is null) return;

        DataVersion++;
        CursorPosition = command.CursorPositionBefore;
        SelectionLength = 0;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private void Redo()
    {
        if (Document is null) return;

        var command = _undoRedoManager.Redo();
        if (command is null) return;

        DataVersion++;
        CursorPosition = command.CursorPositionAfter;
        SelectionLength = 0;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoRedoManager.CanUndo;
        CanRedo = _undoRedoManager.CanRedo;
    }

    [RelayCommand]
    private Task CopyAsync()
    {
        if (Document is null || SelectionLength <= 0) return Task.CompletedTask;

        var start = SelectionStart;
        var length = Math.Min(SelectionLength, (int)Document.Buffer.Length - start);
        if (length <= 0) return Task.CompletedTask;

        var bytes = new byte[length];
        Document.Buffer.ReadBytes(start, bytes, 0, length);
        _copiedBytes = bytes;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CutAsync()
    {
        if (Document is null || SelectionLength <= 0) return;

        await CopyAsync();

        var start = SelectionStart;
        var length = Math.Min(SelectionLength, (int)Document.Buffer.Length - start);
        if (length <= 0) return;

        var command = new DeleteBytesCommand(Document, start, length, CursorPosition);
        _undoRedoManager.ExecuteCommand(command);

        CursorPosition = start;
        SelectionStart = start;
        SelectionLength = start < (int)Document.Buffer.Length ? 1 : 0;
        command.CursorPositionAfter = CursorPosition;

        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (Document is null) return;

        byte[]? bytes = null;
        if (_clipboard is not null)
        {
            var text = await _clipboard.TryGetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
                bytes = ParseHexString(text);
        }

        bytes ??= _copiedBytes;
        if (bytes is null || bytes.Length == 0) return;

        var insertIndex = CursorPosition;
        var insertCommand = new InsertBytesCommand(Document, insertIndex, bytes, CursorPosition);
        _undoRedoManager.ExecuteCommand(insertCommand);

        CursorPosition = insertIndex + bytes.Length;
        SelectionStart = CursorPosition;
        SelectionLength = CursorPosition < (int)Document.Buffer.Length ? 1 : 0;
        insertCommand.CursorPositionAfter = CursorPosition;

        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (Document?.Buffer is not { Length: > 0 } buffer) return;

        SelectionStart = 0;
        SelectionLength = (int)buffer.Length;
        CursorPosition = 0;
    }

    private static byte[]? ParseHexString(string text)
    {
        var tokens = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!byte.TryParse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return null;
            result[i] = b;
        }
        return result;
    }

    [RelayCommand]
    private void OpenSearch()
    {
        IsSearchVisible = true;
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchVisible = false;
        ClearSearchResults();
    }

    [RelayCommand]
    private void FindNext()
    {
        if (SearchMatchOffsets.Length == 0)
        {
            ExecuteSearch();
            return;
        }

        CurrentSearchMatchIndex = (CurrentSearchMatchIndex + 1) % SearchMatchOffsets.Length;
        NavigateToCurrentMatch();
    }

    [RelayCommand]
    private void FindPrevious()
    {
        if (SearchMatchOffsets.Length == 0)
        {
            ExecuteSearch();
            return;
        }

        CurrentSearchMatchIndex = (CurrentSearchMatchIndex - 1 + SearchMatchOffsets.Length) % SearchMatchOffsets.Length;
        NavigateToCurrentMatch();
    }

    partial void OnSearchTextChanged(string value) => ExecuteSearch();
    partial void OnIsHexSearchChanged(bool value) => ExecuteSearch();

    private void ClearSearchResults()
    {
        SearchMatchOffsets = [];
        SearchMatchLength = 0;
        CurrentSearchMatchIndex = -1;
        SearchStatusText = "";
    }

    private void ExecuteSearch()
    {
        var buffer = Document?.Buffer;
        if (buffer is null || string.IsNullOrEmpty(SearchText))
        {
            ClearSearchResults();
            return;
        }

        var pattern = IsHexSearch ? ParseHexString(SearchText) : Encoding.UTF8.GetBytes(SearchText);
        if (pattern is null || pattern.Length == 0)
        {
            ClearSearchResults();
            if (IsHexSearch) SearchStatusText = "無効な16進数";
            return;
        }

        SearchMatchLength = pattern.Length;
        var dataLength = (int)buffer.Length;
        var matches = new List<int>();

        const int chunkSize = 65536;
        var overlap = pattern.Length - 1;
        var chunk = new byte[chunkSize + overlap];

        for (var offset = 0; offset < dataLength;)
        {
            var readStart = offset == 0 ? 0 : offset - overlap;
            var readLength = Math.Min(chunk.Length, dataLength - readStart);
            buffer.ReadBytes(readStart, chunk, 0, readLength);

            var searchStart = offset == 0 ? 0 : overlap;
            var span = chunk.AsSpan(searchStart, readLength - searchStart);
            var pos = 0;
            while (pos <= span.Length - pattern.Length)
            {
                var idx = span[pos..].IndexOf(pattern);
                if (idx < 0) break;
                matches.Add(readStart + searchStart + pos + idx);
                pos += idx + 1;
            }

            offset += chunkSize;
        }

        SearchMatchOffsets = [.. matches];

        if (matches.Count == 0)
        {
            CurrentSearchMatchIndex = -1;
            SearchStatusText = "0/0";
            return;
        }

        var closestIndex = FindClosestMatch(matches, CursorPosition);
        CurrentSearchMatchIndex = closestIndex;
        NavigateToCurrentMatch();
    }

    private void NavigateToCurrentMatch()
    {
        if (CurrentSearchMatchIndex < 0 || CurrentSearchMatchIndex >= SearchMatchOffsets.Length)
            return;

        var matchOffset = SearchMatchOffsets[CurrentSearchMatchIndex];
        CursorPosition = matchOffset;
        SelectionStart = matchOffset;
        SelectionLength = SearchMatchLength;
        SearchStatusText = $"{CurrentSearchMatchIndex + 1}/{SearchMatchOffsets.Length}";
    }

    private static int FindClosestMatch(List<int> matches, int position)
    {
        var lo = 0;
        var hi = matches.Count - 1;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (matches[mid] < position)
                lo = mid + 1;
            else
                hi = mid;
        }
        if (lo >= matches.Count) return matches.Count - 1;
        if (lo == 0) return 0;

        var distBefore = position - matches[lo - 1];
        var distAfter = matches[lo] - position;
        return distBefore <= distAfter ? lo - 1 : lo;
    }

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (Document is null || !Document.IsModified) return;

        if (Document.IsNew)
        {
            await SaveAsFileAsync();
            return;
        }

        await Document.SaveAsync();
        UpdateTitle();
    }

    [RelayCommand]
    private async Task SaveAsFileAsync()
    {
        if (_storageProvider is null || Document is null) return;

        var file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "名前を付けて保存",
            SuggestedFileName = Document.FileName,
        });

        if (file?.TryGetLocalPath() is not { } path) return;

        await Document.SaveAsAsync(path);
        UpdateTitle();
    }

    [RelayCommand]
    private void Exit()
    {
        _closeAction?.Invoke();
    }

    private void UpdateTitle()
    {
        if (Document is null)
        {
            Title = "ZeroIchi";
            return;
        }

        var modified = Document.IsModified ? "*" : "";
        Title = $"{modified}{Document.FileName} - ZeroIchi";
    }

    partial void OnCursorPositionChanged(int value) => UpdateStatusBar();
    partial void OnSelectionStartChanged(int value) => UpdateStatusBar();
    partial void OnSelectionLengthChanged(int value) => UpdateStatusBar();

    partial void OnDocumentChanged(BinaryDocument? value)
    {
        if (value?.Buffer is { Length: > 0 } buffer)
        {
            var length = (int)Math.Min(buffer.Length, 1024);
            var header = buffer.SliceToArray(0, length);
            StatusBarFileTypeText = DetectFileType(header);
        }
        else
        {
            StatusBarFileTypeText = "";
        }
        UpdateStatusBar();
    }

    partial void OnDataVersionChanged(int value) => UpdateStatusBar();

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

    private static string DetectFileType(byte[] headerBytes)
    {
        if (headerBytes.Length == 0) return "";

        var results = Inspector.Inspect(new ReadOnlySpan<byte>(headerBytes, 0, headerBytes.Length));
        var match = results.ByFileExtension();

        return match.Length > 0 ? match[0].Extension : "";
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
