using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using ZeroIchi.Models;
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

    internal static long EffectiveCopyLength(long selectionLength, long selectionStart, long bufferLength) =>
        Math.Clamp(selectionLength, 0, Math.Max(0, bufferLength - selectionStart));

    internal static bool CanCopySelection(long selectionLength, long selectionStart, long bufferLength) =>
        EffectiveCopyLength(selectionLength, selectionStart, bufferLength) <= Array.MaxLength;

    internal static string? ToClipboardText(byte[] bytes, int maxBytes) =>
        bytes.Length <= maxBytes ? HexText.Format(bytes) : null;

    private bool CanCopy() =>
        Document is null || CanCopySelection(SelectionLength, SelectionStart, Document.Buffer.Length);

    private byte[]? ReadSelectedBytes()
    {
        if (Document is null) return null;

        var length = EffectiveCopyLength(SelectionLength, SelectionStart, Document.Buffer.Length);
        if (length <= 0) return null;

        var bytes = new byte[length];
        Document.Buffer.ReadBytes(SelectionStart, bytes, 0, (int)length);
        return bytes;
    }

    private Task WriteClipboardAsync(byte[] bytes) =>
        clipboard.SetBytesAsync(bytes, ToClipboardText(bytes, ClipboardTextMaxBytes));

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyAsync()
    {
        if (ReadSelectedBytes() is { } bytes)
            await WriteClipboardAsync(bytes);
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CutAsync()
    {
        if (Document is not { } document || ReadSelectedBytes() is not { } bytes) return;

        var start = SelectionStart;
        var cursorPositionBefore = CursorPosition;
        var version = DataVersion;

        await WriteClipboardAsync(bytes);
        if (Document != document || DataVersion != version) return;

        var command = new DeleteBytesCommand(document, start, bytes.Length, cursorPositionBefore, cursorPositionAfter: start);
        document.ExecuteCommand(command);

        CursorPosition = start;
        SelectionStart = start;
        SelectionLength = start < document.Buffer.Length ? 1 : 0;

        DataVersion++;
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (Document is null) return;

        var bytes = await clipboard.TryGetBytesAsync();
        if (bytes is null && await clipboard.GetTextAsync() is { } text)
            bytes = HexText.Parse(text);

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
}
