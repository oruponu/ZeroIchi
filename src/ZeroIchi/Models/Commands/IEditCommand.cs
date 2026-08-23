namespace ZeroIchi.Models.Commands;

public interface IEditCommand
{
    BinaryDocument Document { get; }

    void Execute();
    void Undo();

    int CursorPositionBefore { get; }
    int CursorPositionAfter { get; }
}
