namespace ZeroIchi.Models.PieceTables;

public class PieceTableEdit
{
    public int PieceIndex { get; init; }
    public required Piece[] OldPieces { get; init; }
    public required Piece[] NewPieces { get; init; }
    public long LengthDelta { get; init; }
}
