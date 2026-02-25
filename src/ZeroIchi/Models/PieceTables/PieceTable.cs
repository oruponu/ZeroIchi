using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.PieceTables;

public class PieceTable
{
    private readonly ByteBuffer _original;
    private readonly List<byte> _addBuffer = [];
    private readonly List<Piece> _pieces = [];

    private int _cachedPieceIndex;
    private long _cachedLogicalOffset;

    public PieceTable(ByteBuffer original)
    {
        _original = original;
        Length = original.Length;
        if (original.Length > 0)
            _pieces.Add(new Piece(PieceSource.Original, 0, original.Length));
    }

    public long Length { get; private set; }

    public bool HasAddPieces => _pieces.Any(p => p.Source == PieceSource.Add);

    public byte ReadByte(long index)
    {
        var (pieceIdx, offsetInPiece) = FindPiece(index);
        var piece = _pieces[pieceIdx];
        return piece.Source == PieceSource.Original
            ? _original.ReadByte(piece.Offset + offsetInPiece)
            : _addBuffer[(int)(piece.Offset + offsetInPiece)];
    }

    public void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        if (count <= 0) return;
        var remaining = count;
        var destOffset = bufferOffset;
        var (pieceIdx, offsetInPiece) = FindPiece(offset);

        while (remaining > 0 && pieceIdx < _pieces.Count)
        {
            var piece = _pieces[pieceIdx];
            var availableInPiece = (int)Math.Min(piece.Length - offsetInPiece, remaining);

            if (piece.Source == PieceSource.Original)
            {
                _original.ReadBytes(piece.Offset + offsetInPiece, buffer, destOffset, availableInPiece);
            }
            else
            {
                var srcOffset = (int)(piece.Offset + offsetInPiece);
                CollectionsMarshal.AsSpan(_addBuffer)
                    .Slice(srcOffset, availableInPiece)
                    .CopyTo(buffer.AsSpan(destOffset, availableInPiece));
            }

            remaining -= availableInPiece;
            destOffset += availableInPiece;
            pieceIdx++;
            offsetInPiece = 0;
        }
    }

    public PieceTableEdit WriteByte(long index, byte value)
    {
        var (pieceIdx, offsetInPiece) = FindPiece(index);
        var piece = _pieces[pieceIdx];

        var addOffset = _addBuffer.Count;
        _addBuffer.Add(value);
        var newPiece = new Piece(PieceSource.Add, addOffset, 1);

        Piece[] oldPieces = [piece];
        Piece[] newPieces;

        if (piece.Length == 1)
        {
            newPieces = [newPiece];
            _pieces[pieceIdx] = newPiece;
        }
        else if (offsetInPiece == 0)
        {
            var right = new Piece(piece.Source, piece.Offset + 1, piece.Length - 1);
            newPieces = [newPiece, right];
            _pieces[pieceIdx] = newPiece;
            _pieces.Insert(pieceIdx + 1, right);
        }
        else if (offsetInPiece == piece.Length - 1)
        {
            var left = new Piece(piece.Source, piece.Offset, piece.Length - 1);
            newPieces = [left, newPiece];
            _pieces[pieceIdx] = left;
            _pieces.Insert(pieceIdx + 1, newPiece);
        }
        else
        {
            var left = new Piece(piece.Source, piece.Offset, offsetInPiece);
            var right = new Piece(piece.Source, piece.Offset + offsetInPiece + 1, piece.Length - offsetInPiece - 1);
            newPieces = [left, newPiece, right];
            _pieces[pieceIdx] = left;
            _pieces.Insert(pieceIdx + 1, newPiece);
            _pieces.Insert(pieceIdx + 2, right);
        }

        InvalidateCache();
        return new PieceTableEdit
        {
            PieceIndex = pieceIdx,
            OldPieces = oldPieces,
            NewPieces = newPieces,
            LengthDelta = 0,
        };
    }

    public PieceTableEdit InsertBytes(long index, byte[] bytes)
    {
        var addOffset = _addBuffer.Count;
        _addBuffer.AddRange(bytes);
        var insertPiece = new Piece(PieceSource.Add, addOffset, bytes.Length);

        int pieceIdx;
        Piece[] oldPieces;
        Piece[] newPieces;

        if (index == Length)
        {
            pieceIdx = _pieces.Count;
            oldPieces = [];
            newPieces = [insertPiece];
            _pieces.Add(insertPiece);
        }
        else
        {
            var (pi, offsetInPiece) = FindPiece(index);
            pieceIdx = pi;
            var piece = _pieces[pi];

            if (offsetInPiece == 0)
            {
                oldPieces = [];
                newPieces = [insertPiece];
                _pieces.Insert(pieceIdx, insertPiece);
            }
            else
            {
                var left = new Piece(piece.Source, piece.Offset, offsetInPiece);
                var right = new Piece(piece.Source, piece.Offset + offsetInPiece, piece.Length - offsetInPiece);
                oldPieces = [piece];
                newPieces = [left, insertPiece, right];
                _pieces[pieceIdx] = left;
                _pieces.Insert(pieceIdx + 1, insertPiece);
                _pieces.Insert(pieceIdx + 2, right);
            }
        }

        Length += bytes.Length;
        InvalidateCache();
        return new PieceTableEdit
        {
            PieceIndex = pieceIdx,
            OldPieces = oldPieces,
            NewPieces = newPieces,
            LengthDelta = bytes.Length,
        };
    }

    public PieceTableEdit DeleteBytes(long index, long count)
    {
        var (startPi, startOffset) = FindPiece(index);
        var endLogical = index + count;

        var endPi = startPi;
        var runningOffset = index - startOffset;
        while (endPi < _pieces.Count && runningOffset + _pieces[endPi].Length < endLogical)
        {
            runningOffset += _pieces[endPi].Length;
            endPi++;
        }

        var endOffsetInPiece = endLogical - runningOffset;
        var oldPieceCount = endPi - startPi + 1;
        var oldPieces = new Piece[oldPieceCount];
        for (var i = 0; i < oldPieceCount; i++)
            oldPieces[i] = _pieces[startPi + i];

        var hasLeft = startOffset > 0;
        var hasRight = endPi < _pieces.Count && endOffsetInPiece < _pieces[endPi].Length;
        var newPieces = new Piece[(hasLeft ? 1 : 0) + (hasRight ? 1 : 0)];
        var idx = 0;

        if (hasLeft)
        {
            var first = _pieces[startPi];
            newPieces[idx++] = new Piece(first.Source, first.Offset, startOffset);
        }

        if (hasRight)
        {
            var last = _pieces[endPi];
            newPieces[idx] = new Piece(last.Source, last.Offset + endOffsetInPiece, last.Length - endOffsetInPiece);
        }

        _pieces.RemoveRange(startPi, oldPieceCount);
        for (var i = 0; i < newPieces.Length; i++)
            _pieces.Insert(startPi + i, newPieces[i]);

        Length -= count;
        InvalidateCache();
        return new PieceTableEdit
        {
            PieceIndex = startPi,
            OldPieces = oldPieces,
            NewPieces = newPieces,
            LengthDelta = -count,
        };
    }

    public PieceTableEdit AppendByte(byte value)
    {
        var addOffset = _addBuffer.Count;
        _addBuffer.Add(value);

        int pieceIdx;
        Piece[] oldPieces;
        Piece[] newPieces;

        if (_pieces.Count > 0 && _pieces[^1] is var last
            && last.Source == PieceSource.Add && last.Offset + last.Length == addOffset)
        {
            pieceIdx = _pieces.Count - 1;
            oldPieces = [last];
            var extended = new Piece(PieceSource.Add, last.Offset, last.Length + 1);
            newPieces = [extended];
            _pieces[^1] = extended;
        }
        else
        {
            pieceIdx = _pieces.Count;
            oldPieces = [];
            var np = new Piece(PieceSource.Add, addOffset, 1);
            newPieces = [np];
            _pieces.Add(np);
        }

        Length++;
        InvalidateCache();
        return new PieceTableEdit
        {
            PieceIndex = pieceIdx,
            OldPieces = oldPieces,
            NewPieces = newPieces,
            LengthDelta = 1,
        };
    }

    public void UndoEdit(PieceTableEdit edit)
    {
        _pieces.RemoveRange(edit.PieceIndex, edit.NewPieces.Length);
        for (var i = 0; i < edit.OldPieces.Length; i++)
            _pieces.Insert(edit.PieceIndex + i, edit.OldPieces[i]);
        Length -= edit.LengthDelta;
        InvalidateCache();
    }

    public bool IsModified(long index)
    {
        if (index < 0 || index >= Length) return false;
        var (pieceIdx, _) = FindPiece(index);
        return _pieces[pieceIdx].Source == PieceSource.Add;
    }

    public void WriteTo(Stream stream)
    {
        var buffer = new byte[81920];
        foreach (var piece in _pieces)
        {
            var remaining = piece.Length;
            var srcOffset = piece.Offset;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(remaining, buffer.Length);
                if (piece.Source == PieceSource.Original)
                {
                    _original.ReadBytes(srcOffset, buffer, 0, chunk);
                }
                else
                {
                    CollectionsMarshal.AsSpan(_addBuffer)
                        .Slice((int)srcOffset, chunk)
                        .CopyTo(buffer.AsSpan(0, chunk));
                }
                stream.Write(buffer, 0, chunk);
                srcOffset += chunk;
                remaining -= chunk;
            }
        }
    }

    private (int pieceIndex, long offsetInPiece) FindPiece(long logicalIndex)
    {
        if (_cachedPieceIndex < _pieces.Count)
        {
            var cachedStart = _cachedLogicalOffset;
            var cachedPiece = _pieces[_cachedPieceIndex];
            if (logicalIndex >= cachedStart && logicalIndex < cachedStart + cachedPiece.Length)
                return (_cachedPieceIndex, logicalIndex - cachedStart);

            var nextIdx = _cachedPieceIndex + 1;
            if (nextIdx < _pieces.Count)
            {
                var nextStart = cachedStart + cachedPiece.Length;
                var nextPiece = _pieces[nextIdx];
                if (logicalIndex >= nextStart && logicalIndex < nextStart + nextPiece.Length)
                {
                    _cachedPieceIndex = nextIdx;
                    _cachedLogicalOffset = nextStart;
                    return (nextIdx, logicalIndex - nextStart);
                }
            }
        }

        long offset = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            if (logicalIndex < offset + piece.Length)
            {
                _cachedPieceIndex = i;
                _cachedLogicalOffset = offset;
                return (i, logicalIndex - offset);
            }
            offset += piece.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(logicalIndex));
    }

    private void InvalidateCache()
    {
        _cachedPieceIndex = 0;
        _cachedLogicalOffset = 0;
    }
}
