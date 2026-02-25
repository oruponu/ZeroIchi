using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime;
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

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (_clipboard is null || Document is null || SelectionLength <= 0) return;

        var start = SelectionStart;
        var length = Math.Min(SelectionLength, (int)Document.Buffer.Length - start);
        if (length <= 0) return;

        var hex = string.Join(" ", Document.Buffer.SliceToArray(start, length).Select(b => b.ToString("X2")));
        await _clipboard.SetTextAsync(hex);
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
        if (_clipboard is null || Document is null) return;

        var text = await _clipboard.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        var bytes = ParseHexString(text);
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

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoRedoManager.CanUndo;
        CanRedo = _undoRedoManager.CanRedo;
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
