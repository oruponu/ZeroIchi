using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    public void OnByteModified(long index, byte value, long cursorPositionAfter)
    {
        if (Document is null || IsSaving) return;

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
    }

    public void OnBytesDeleted(long index, long count, long cursorPositionAfter)
    {
        if (Document is null || IsSaving) return;

        var command = new DeleteBytesCommand(Document, index, count, CursorPosition, cursorPositionAfter);
        Document.ExecuteCommand(command);
    }

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private void Undo()
    {
        if (Document is null) return;

        var command = Document.Undo();
        if (command is null) return;

        CursorPosition = command.CursorPositionBefore;
        SelectByteAtCursor();
    }

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private void Redo()
    {
        if (Document is null) return;

        var command = Document.Redo();
        if (command is null) return;

        CursorPosition = command.CursorPositionAfter;
        SelectByteAtCursor();
    }

    private void SelectByteAtCursor()
    {
        SelectionStart = CursorPosition;
        SelectionLength = Document is { } document && CursorPosition < document.Buffer.Length ? 1 : 0;
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = !IsSaving && (Document?.CanUndo ?? false);
        CanRedo = !IsSaving && (Document?.CanRedo ?? false);
    }

    internal static string? ToClipboardText(byte[] bytes, int maxBytes) =>
        bytes.Length <= maxBytes ? HexText.Format(bytes) : null;

    private bool CanCopy() =>
        Document is null || CopyRange.CanCopy(SelectionLength, SelectionStart, Document.Buffer.Length);

    private bool CanCut() => CanCopy() && !IsSaving;

    private byte[]? ReadSelectedBytes()
    {
        if (Document is null) return null;

        var length = CopyRange.EffectiveLength(SelectionLength, SelectionStart, Document.Buffer.Length);
        if (length <= 0) return null;

        var bytes = new byte[length];
        Document.Buffer.ReadBytes(SelectionStart, bytes, 0, (int)length);
        return bytes;
    }

    private Task WriteClipboardAsync(byte[] bytes) =>
        _clipboard.SetBytesAsync(bytes, ToClipboardText(bytes, ClipboardTextMaxBytes));

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyAsync()
    {
        if (ReadSelectedBytes() is { } bytes)
            await WriteClipboardAsync(bytes);
    }

    [RelayCommand(CanExecute = nameof(CanCut))]
    private async Task CutAsync()
    {
        if (Document is not { } document || ReadSelectedBytes() is not { } bytes) return;

        var start = SelectionStart;
        var cursorPositionBefore = CursorPosition;
        var version = DataVersion;

        await WriteClipboardAsync(bytes);
        if (IsSaving || Document != document || DataVersion != version) return;

        var command = new DeleteBytesCommand(document, start, bytes.Length, cursorPositionBefore, cursorPositionAfter: start);
        document.ExecuteCommand(command);

        CursorPosition = start;
        SelectByteAtCursor();
    }

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private async Task PasteAsync()
    {
        if (Document is null) return;

        var bytes = await _clipboard.TryGetBytesAsync();
        if (bytes is null && await _clipboard.GetTextAsync() is { } text)
            bytes = HexText.Parse(text);

        if (bytes is null || bytes.Length == 0 || IsSaving) return;

        var insertIndex = CursorPosition;
        var cursorPositionAfter = insertIndex + bytes.Length;
        var insertCommand = new InsertBytesCommand(Document, insertIndex, bytes, CursorPosition, cursorPositionAfter);
        Document.ExecuteCommand(insertCommand);

        CursorPosition = cursorPositionAfter;
        SelectByteAtCursor();
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
