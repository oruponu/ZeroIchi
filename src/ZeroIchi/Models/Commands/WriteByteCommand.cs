using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class WriteByteCommand(
    BinaryDocument document, long index, byte newValue, long cursorPositionBefore, long cursorPositionAfter)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public BinaryDocument Document => document;

    public long CursorPositionBefore { get; } = cursorPositionBefore;
    public long CursorPositionAfter { get; } = cursorPositionAfter;

    public void Execute() => _edit = document.WriteByte(index, newValue);

    public void Undo() => document.UndoEdit(_edit!);
}
