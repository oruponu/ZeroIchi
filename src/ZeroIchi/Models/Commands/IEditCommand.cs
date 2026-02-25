namespace ZeroIchi.Models.Commands;

public interface IEditCommand
{
    void Execute();
    void Undo();

    int CursorPositionBefore { get; }
    int CursorPositionAfter { get; set; }
}
