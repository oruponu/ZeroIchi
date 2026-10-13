using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class WriteByteCommand(
    BinaryDocument document, long index, byte newValue, long cursorPositionBefore, long cursorPositionAfter)
    : EditCommand(document, cursorPositionBefore, cursorPositionAfter)
{
    private byte? _oldValue;

    protected override PieceTableEdit ApplyForward()
    {
        _oldValue ??= Document.Buffer.ReadByte(index);
        return Document.WriteByte(index, newValue);
    }

    protected override PieceTableEdit ApplyInverse() => Document.WriteByte(index, _oldValue!.Value);
}
