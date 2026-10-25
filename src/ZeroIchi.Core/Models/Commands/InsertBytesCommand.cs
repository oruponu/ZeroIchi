using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class InsertBytesCommand(
    BinaryDocument document, long index, byte[] bytes, long cursorPositionBefore, long cursorPositionAfter)
    : EditCommand(document, cursorPositionBefore, cursorPositionAfter)
{
    protected override PieceTableEdit ApplyForward() => Document.InsertBytes(index, bytes);

    protected override PieceTableEdit ApplyInverse() => Document.DeleteBytes(index, bytes.Length);
}
