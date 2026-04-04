using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;
using System.Threading.Tasks;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
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
}
