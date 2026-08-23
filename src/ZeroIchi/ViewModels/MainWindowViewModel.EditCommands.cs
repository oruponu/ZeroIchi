using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;
using System.Threading.Tasks;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    public void OnByteModified(long index, byte value, long cursorPositionAfter)
    {
        if (Document is null) return;

        IEditCommand command;
        if (index == Document.Buffer.Length)
        {
            command = new AppendByteCommand(Document, value, CursorPosition, cursorPositionAfter);
        }
        else
        {
            command = new WriteByteCommand(Document, index, value, CursorPosition, cursorPositionAfter);
        }

        Document.ExecuteCommand(command);
        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    public void OnBytesDeleted(long index, long count, long cursorPositionAfter)
    {
        if (Document is null) return;

        var command = new DeleteBytesCommand(Document, index, count, CursorPosition, cursorPositionAfter);
        Document.ExecuteCommand(command);
        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private void Undo()
    {
        if (Document is null) return;

        var command = Document.Undo();
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

        var command = Document.Redo();
        if (command is null) return;

        DataVersion++;
        CursorPosition = command.CursorPositionAfter;
        SelectionLength = 0;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = Document?.CanUndo ?? false;
        CanRedo = Document?.CanRedo ?? false;
    }

    [RelayCommand]
    private Task CopyAsync()
    {
        if (Document is null || SelectionLength <= 0) return Task.CompletedTask;

        var start = SelectionStart;
        var length = Math.Min(SelectionLength, Document.Buffer.Length - start);
        if (length <= 0) return Task.CompletedTask;

        var bytes = new byte[length];
        Document.Buffer.ReadBytes(start, bytes, 0, (int)length);
        _copiedBytes = bytes;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CutAsync()
    {
        if (Document is null || SelectionLength <= 0) return;

        await CopyAsync();

        var start = SelectionStart;
        var length = Math.Min(SelectionLength, Document.Buffer.Length - start);
        if (length <= 0) return;

        var command = new DeleteBytesCommand(Document, start, length, CursorPosition, cursorPositionAfter: start);
        Document.ExecuteCommand(command);

        CursorPosition = start;
        SelectionStart = start;
        SelectionLength = start < Document.Buffer.Length ? 1 : 0;

        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (Document is null) return;

        byte[]? bytes = null;
        var text = await clipboard.GetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
            bytes = ParseHexString(text);

        bytes ??= _copiedBytes;
        if (bytes is null || bytes.Length == 0) return;

        var insertIndex = CursorPosition;
        var cursorPositionAfter = insertIndex + bytes.Length;
        var insertCommand = new InsertBytesCommand(Document, insertIndex, bytes, CursorPosition, cursorPositionAfter);
        Document.ExecuteCommand(insertCommand);

        CursorPosition = cursorPositionAfter;
        SelectionStart = CursorPosition;
        SelectionLength = CursorPosition < Document.Buffer.Length ? 1 : 0;

        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (Document?.Buffer is not { Length: > 0 } buffer) return;

        SelectionStart = 0;
        SelectionLength = buffer.Length;
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
