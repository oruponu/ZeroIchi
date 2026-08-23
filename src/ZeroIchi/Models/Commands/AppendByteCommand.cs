using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class AppendByteCommand(
    BinaryDocument document, byte value, int cursorPositionBefore, int cursorPositionAfter) : IEditCommand
{
    private PieceTableEdit? _edit;

    public BinaryDocument Document => document;

    public int CursorPositionBefore { get; } = cursorPositionBefore;
    public int CursorPositionAfter { get; } = cursorPositionAfter;

    public void Execute() => _edit = document.AppendByte(value);

    public void Undo() => document.UndoEdit(_edit!);
}
