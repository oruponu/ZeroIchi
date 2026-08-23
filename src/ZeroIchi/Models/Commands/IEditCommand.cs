namespace ZeroIchi.Models.Commands;

public interface IEditCommand
{
    BinaryDocument Document { get; }

    void Execute();
    void Undo();

    long CursorPositionBefore { get; }
    long CursorPositionAfter { get; }
}
