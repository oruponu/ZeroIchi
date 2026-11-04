using System;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class ReplaceBytesCommand(
    BinaryDocument document, long index, long count, byte[] bytes, long cursorPositionBefore, long cursorPositionAfter)
    : EditCommand(document, cursorPositionBefore, cursorPositionAfter)
{
    private byte[]? _retainedBytes;

    public override long RetainedLength => count;

    public override byte[]? CaptureRetainedBytes()
    {
        if (_retainedBytes is not null) return null;

        var edit = ForwardEdit ?? throw new InvalidOperationException();
        return Document.ReadPieces(edit.RemovedPieces);
    }

    protected override void StoreRetainedBytes(byte[] retainedBytes) => _retainedBytes = retainedBytes;

    protected override PieceTableEdit ApplyForward() => Document.ReplaceBytes(index, count, bytes);

    protected override PieceTableEdit ApplyInverse() => Document.ReplaceBytes(index, bytes.Length, _retainedBytes!);
}
