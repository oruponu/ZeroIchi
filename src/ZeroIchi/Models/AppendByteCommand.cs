namespace ZeroIchi.Models;

public class AppendByteCommand(BinaryDocument document, byte value, int cursorPosition) : IEditCommand
{
    private PieceTableEdit? _edit;

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => _edit = document.AppendByte(value);

    public void Undo() => document.UndoEdit(_edit!);
}
