namespace ZeroIchi.Models;

public class WriteByteCommand(BinaryDocument document, int index, byte newValue, int cursorPosition)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => _edit = document.WriteByte(index, newValue);

    public void Undo() => document.UndoEdit(_edit!);
}
