using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class AppendByteCommand(
    BinaryDocument document, byte value, long cursorPositionBefore, long cursorPositionAfter) : IEditCommand
{
    private PieceTableEdit? _edit;

    public BinaryDocument Document => document;

    public long CursorPositionBefore { get; } = cursorPositionBefore;
    public long CursorPositionAfter { get; } = cursorPositionAfter;

    public void Execute() => _edit = document.AppendByte(value);

    public void Undo() => document.UndoEdit(_edit!);
}
