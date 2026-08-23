using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class InsertBytesCommand(
    BinaryDocument document, int index, byte[] bytes, int cursorPositionBefore, int cursorPositionAfter)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public BinaryDocument Document => document;

    public int CursorPositionBefore { get; } = cursorPositionBefore;
    public int CursorPositionAfter { get; } = cursorPositionAfter;

    public void Execute() => _edit = document.InsertBytes(index, bytes);

    public void Undo() => document.UndoEdit(_edit!);
}
