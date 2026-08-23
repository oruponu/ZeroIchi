using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class WriteByteCommand(
    BinaryDocument document, int index, byte newValue, int cursorPositionBefore, int cursorPositionAfter)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public BinaryDocument Document => document;

    public int CursorPositionBefore { get; } = cursorPositionBefore;
    public int CursorPositionAfter { get; } = cursorPositionAfter;

    public void Execute() => _edit = document.WriteByte(index, newValue);

    public void Undo() => document.UndoEdit(_edit!);
}
