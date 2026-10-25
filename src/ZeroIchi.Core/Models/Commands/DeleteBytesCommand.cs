using System;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public sealed class DeleteBytesCommand(
    BinaryDocument document, long index, long count, long cursorPositionBefore, long cursorPositionAfter)
    : EditCommand(document, cursorPositionBefore, cursorPositionAfter)
{
    private byte[]? _retainedBytes;

    public override long RetainedLength => count;

    internal bool HasRetainedBytes => _retainedBytes is not null;

    public override byte[]? CaptureRetainedBytes()
    {
        if (_retainedBytes is not null) return null;

        var edit = ForwardEdit ?? throw new InvalidOperationException();
        return Document.ReadPieces(edit.RemovedPieces);
    }

    protected override void StoreRetainedBytes(byte[] bytes) => _retainedBytes = bytes;

    protected override PieceTableEdit ApplyForward() => Document.DeleteBytes(index, count);

    protected override PieceTableEdit ApplyInverse() => Document.InsertBytes(index, _retainedBytes!);
}
