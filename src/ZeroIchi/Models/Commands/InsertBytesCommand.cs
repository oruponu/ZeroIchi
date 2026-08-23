using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class InsertBytesCommand(
    BinaryDocument document, long index, byte[] bytes, long cursorPositionBefore, long cursorPositionAfter)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public BinaryDocument Document => document;

    public long CursorPositionBefore { get; } = cursorPositionBefore;
    public long CursorPositionAfter { get; } = cursorPositionAfter;

    public void Execute() => _edit = document.InsertBytes(index, bytes);

    public void Undo() => document.UndoEdit(_edit!);
}
