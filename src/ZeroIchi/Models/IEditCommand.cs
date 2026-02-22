namespace ZeroIchi.Models;

public interface IEditCommand
{
    void Execute();
    void Undo();

    int CursorPositionBefore { get; }
    int CursorPositionAfter { get; set; }
}
