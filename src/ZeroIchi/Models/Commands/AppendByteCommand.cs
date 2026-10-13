using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class AppendByteCommand(
    BinaryDocument document, byte value, long cursorPositionBefore, long cursorPositionAfter)
    : EditCommand(document, cursorPositionBefore, cursorPositionAfter)
{
    private long? _index;

    protected override PieceTableEdit ApplyForward()
    {
        _index ??= Document.FileSize;
        return Document.AppendByte(value);
    }

    protected override PieceTableEdit ApplyInverse() => Document.DeleteBytes(_index!.Value, 1);
}
