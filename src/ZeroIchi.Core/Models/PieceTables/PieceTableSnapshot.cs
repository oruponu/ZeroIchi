using System;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.PieceTables;

internal sealed class PieceTableSnapshot : ByteBuffer
{
    private readonly ByteBuffer _original;
    private readonly Piece[] _pieces;
    private readonly AddBufferView _added;
    private readonly long[] _starts;
    private readonly bool _ownsOriginal;

    public PieceTableSnapshot(ByteBuffer original, Piece[] pieces, AddBufferView added, bool ownsOriginal = false)
    {
        _original = original;
        _ownsOriginal = ownsOriginal;
        _pieces = pieces;
        _added = added;
        _starts = new long[pieces.Length];
        long offset = 0;
        for (var i = 0; i < pieces.Length; i++)
        {
            _starts[i] = offset;
            offset += pieces[i].Length;
        }
        Length = offset;
    }

    public override long Length { get; }

    public override byte ReadByte(long index)
    {
        var pieceIndex = FindPiece(index);
        var piece = _pieces[pieceIndex];
        var position = piece.Offset + index - _starts[pieceIndex];
        return piece.Source == PieceSource.Original ? _original.ReadByte(position) : _added[position];
    }

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        if (count <= 0) return;
        var pieceIndex = FindPiece(offset);
        var offsetInPiece = offset - _starts[pieceIndex];
        while (count > 0 && pieceIndex < _pieces.Length)
        {
            var piece = _pieces[pieceIndex];
            var available = (int)Math.Min(piece.Length - offsetInPiece, count);
            if (piece.Source == PieceSource.Original)
                _original.ReadBytes(piece.Offset + offsetInPiece, buffer, bufferOffset, available);
            else
                _added.CopyTo(piece.Offset + offsetInPiece, buffer.AsSpan(bufferOffset, available));

            count -= available;
            bufferOffset += available;
            pieceIndex++;
            offsetInPiece = 0;
        }
    }

    public override void Dispose()
    {
        if (_ownsOriginal)
            _original.Dispose();
        base.Dispose();
    }

    private int FindPiece(long index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Length);
        var found = Array.BinarySearch(_starts, index);
        return found >= 0 ? found : ~found - 1;
    }
}
