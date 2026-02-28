using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class DeleteBytesCommand(BinaryDocument document, int index, int count, int cursorPosition)
    : IEditCommand
{
    private PieceTableEdit? _edit;

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => _edit = document.DeleteBytes(index, count);

    public void Undo() => document.UndoEdit(_edit!);
}
