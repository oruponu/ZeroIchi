namespace ZeroIchi.Models.PieceTables;

public record PieceTableEdit
{
    public required int PieceIndex { get; init; }
    public required Piece[] OldPieces { get; init; }
    public required Piece[] NewPieces { get; init; }
    public required long LengthDelta { get; init; }
}
