namespace ZeroIchi.Models;

public enum PieceSource : byte { Original, Add }

public readonly record struct Piece(PieceSource Source, long Offset, long Length);
