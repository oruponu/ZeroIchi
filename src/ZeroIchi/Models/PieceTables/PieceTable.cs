using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.PieceTables;

public sealed class PieceTable
{
    private readonly ByteBuffer _original;
    private readonly AddBuffer _addBuffer = new();
    private readonly List<Piece> _pieces = [];
    private readonly List<long> _starts = [];

    private long _nextStateId = 1;
    private long _stateId;

    public PieceTable(ByteBuffer original)
    {
        _original = original;
        Length = original.Length;
        if (original.Length > 0)
        {
            _pieces.Add(new Piece(PieceSource.Original, 0, original.Length));
            _starts.Add(0);
        }
    }

    public long Length { get; private set; }

    public bool HasAddPieces => _pieces.Exists(p => p.Source == PieceSource.Add);

    public IReadOnlyList<Piece> Pieces => _pieces;

    public int AddBufferLength => _addBuffer.Length;

    public void ReadAddBuffer(long offset, byte[] buffer, int bufferOffset, int count) =>
        _addBuffer.CopyTo(offset, buffer.AsSpan(bufferOffset, count));

    public byte[] ReadPieces(IReadOnlyList<Piece> pieces)
    {
        long total = 0;
        foreach (var piece in pieces)
            total += piece.Length;

        var result = new byte[total];
        var destOffset = 0;
        foreach (var piece in pieces)
        {
            var length = (int)piece.Length;
            if (piece.Source == PieceSource.Original)
            {
                _original.ReadBytes(piece.Offset, result, destOffset, length);
            }
            else
            {
                _addBuffer.CopyTo(piece.Offset, result.AsSpan(destOffset, length));
            }
            destOffset += length;
        }
        return result;
    }

    public byte ReadByte(long index)
    {
        var (pieceIdx, offsetInPiece) = FindPiece(index);
        var piece = _pieces[pieceIdx];
        return piece.Source == PieceSource.Original
            ? _original.ReadByte(piece.Offset + offsetInPiece)
            : _addBuffer[piece.Offset + offsetInPiece];
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
                _addBuffer.CopyTo(piece.Offset + offsetInPiece, buffer.AsSpan(destOffset, availableInPiece));
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

        var addOffset = _addBuffer.Length;
        _addBuffer.Append(value);
        var newPiece = new Piece(PieceSource.Add, addOffset, 1);

        Piece[] oldPieces = [piece];
        Piece[] newPieces;

        if (piece.Length == 1)
        {
            newPieces = [newPiece];
        }
        else if (offsetInPiece == 0)
        {
            var right = piece with { Offset = piece.Offset + 1, Length = piece.Length - 1 };
            newPieces = [newPiece, right];
        }
        else if (offsetInPiece == piece.Length - 1)
        {
            var left = piece with { Length = piece.Length - 1 };
            newPieces = [left, newPiece];
        }
        else
        {
            var left = piece with { Length = offsetInPiece };
            var right = piece with { Offset = piece.Offset + offsetInPiece + 1, Length = piece.Length - offsetInPiece - 1 };
            newPieces = [left, newPiece, right];
        }

        ReplacePieces(pieceIdx, oldPieces.Length, newPieces, 0);
        return CreateEdit(pieceIdx, oldPieces, newPieces, 0);
    }

    public PieceTableEdit InsertBytes(long index, byte[] bytes)
    {
        var addOffset = _addBuffer.Length;
        _addBuffer.Append(bytes);
        var insertPiece = new Piece(PieceSource.Add, addOffset, bytes.Length);

        int pieceIdx;
        Piece[] oldPieces;
        Piece[] newPieces;

        if (index == Length)
        {
            pieceIdx = _pieces.Count;
            oldPieces = [];
            newPieces = [insertPiece];
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
            }
            else
            {
                var left = piece with { Length = offsetInPiece };
                var right = piece with { Offset = piece.Offset + offsetInPiece, Length = piece.Length - offsetInPiece };
                oldPieces = [piece];
                newPieces = [left, insertPiece, right];
            }
        }

        ReplacePieces(pieceIdx, oldPieces.Length, newPieces, bytes.Length);
        return CreateEdit(pieceIdx, oldPieces, newPieces, bytes.Length);
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

        var removedPieces = new Piece[oldPieceCount];
        for (var i = 0; i < oldPieceCount; i++)
        {
            var piece = oldPieces[i];
            var start = i == 0 ? startOffset : 0;
            var end = i == oldPieceCount - 1 ? endOffsetInPiece : piece.Length;
            removedPieces[i] = piece with { Offset = piece.Offset + start, Length = end - start };
        }

        var hasLeft = startOffset > 0;
        var hasRight = endPi < _pieces.Count && endOffsetInPiece < _pieces[endPi].Length;
        var newPieces = new Piece[(hasLeft ? 1 : 0) + (hasRight ? 1 : 0)];
        var idx = 0;

        if (hasLeft)
        {
            var first = _pieces[startPi];
            newPieces[idx++] = first with { Length = startOffset };
        }

        if (hasRight)
        {
            var last = _pieces[endPi];
            newPieces[idx] = last with { Offset = last.Offset + endOffsetInPiece, Length = last.Length - endOffsetInPiece };
        }

        ReplacePieces(startPi, oldPieceCount, newPieces, -count);
        return CreateEdit(startPi, oldPieces, newPieces, -count, removedPieces);
    }

    public PieceTableEdit AppendByte(byte value)
    {
        var addOffset = _addBuffer.Length;
        _addBuffer.Append(value);

        int pieceIdx;
        Piece[] oldPieces;
        Piece[] newPieces;

        if (_pieces.Count > 0 && _pieces[^1] is { Source: PieceSource.Add } last && last.Offset + last.Length == addOffset)
        {
            pieceIdx = _pieces.Count - 1;
            oldPieces = [last];
            var extended = last with { Length = last.Length + 1 };
            newPieces = [extended];
        }
        else
        {
            pieceIdx = _pieces.Count;
            oldPieces = [];
            var np = new Piece(PieceSource.Add, addOffset, 1);
            newPieces = [np];
        }

        ReplacePieces(pieceIdx, oldPieces.Length, newPieces, 1);
        return CreateEdit(pieceIdx, oldPieces, newPieces, 1);
    }

    private PieceTableEdit CreateEdit(
        int pieceIndex, Piece[] oldPieces, Piece[] newPieces, long lengthDelta, Piece[]? removedPieces = null)
    {
        var stateBefore = _stateId;
        _stateId = _nextStateId++;
        return new PieceTableEdit
        {
            Owner = this,
            StateBefore = stateBefore,
            StateAfter = _stateId,
            PieceIndex = pieceIndex,
            OldPieces = oldPieces,
            NewPieces = newPieces,
            LengthDelta = lengthDelta,
            RemovedPieces = removedPieces ?? [],
        };
    }

    public void UndoEdit(PieceTableEdit edit)
    {
        if (!ReferenceEquals(edit.Owner, this))
            throw new InvalidOperationException();
        if (edit.StateAfter != _stateId)
            throw new InvalidOperationException();

        ReplacePieces(edit.PieceIndex, edit.NewPieces.Length, edit.OldPieces, -edit.LengthDelta);
        _stateId = edit.StateBefore;
    }

    public void RedoEdit(PieceTableEdit edit)
    {
        if (!ReferenceEquals(edit.Owner, this))
            throw new InvalidOperationException();
        if (edit.StateBefore != _stateId)
            throw new InvalidOperationException();

        ReplacePieces(edit.PieceIndex, edit.OldPieces.Length, edit.NewPieces, edit.LengthDelta);
        _stateId = edit.StateAfter;
    }

    public bool IsModified(long index)
    {
        if (index < 0 || index >= Length) return false;
        var (pieceIdx, _) = FindPiece(index);
        return _pieces[pieceIdx].Source == PieceSource.Add;
    }

    public ByteBuffer CreateSnapshot() => new PieceTableSnapshot(_original, _pieces.ToArray(), _addBuffer.View());

    public void WriteTo(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
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
                        _addBuffer.CopyTo(srcOffset, buffer.AsSpan(0, chunk));
                    }
                    stream.Write(buffer, 0, chunk);
                    srcOffset += chunk;
                    remaining -= chunk;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ReplacePieces(int index, int removeCount, Piece[] newPieces, long lengthDelta)
    {
        var start = index < _starts.Count ? _starts[index] : Length;
        _pieces.RemoveRange(index, removeCount);
        _starts.RemoveRange(index, removeCount);
        _pieces.InsertRange(index, newPieces);
        for (var i = 0; i < newPieces.Length; i++)
        {
            _starts.Insert(index + i, start);
            start += newPieces[i].Length;
        }

        if (lengthDelta != 0)
        {
            var starts = CollectionsMarshal.AsSpan(_starts);
            for (var i = index + newPieces.Length; i < starts.Length; i++)
                starts[i] += lengthDelta;
        }
        Length += lengthDelta;
    }

    private (int pieceIndex, long offsetInPiece) FindPiece(long logicalIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(logicalIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(logicalIndex, Length);

        var starts = CollectionsMarshal.AsSpan(_starts);
        var lo = 0;
        var hi = starts.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (starts[mid] <= logicalIndex)
                lo = mid + 1;
            else
                hi = mid;
        }
        return (lo - 1, logicalIndex - starts[lo - 1]);
    }
}
