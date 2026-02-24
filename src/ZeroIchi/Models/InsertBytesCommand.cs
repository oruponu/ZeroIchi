namespace ZeroIchi.Models;

public class InsertBytesCommand(BinaryDocument document, int index, byte[] bytes, int cursorPosition)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => _edit = document.InsertBytes(index, bytes);

    public void Undo() => document.UndoEdit(_edit!);
}
