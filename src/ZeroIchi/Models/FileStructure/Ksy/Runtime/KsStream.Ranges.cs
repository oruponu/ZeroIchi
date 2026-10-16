using System;
using System.Buffers;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

public readonly record struct KsRange(long Start, long Length);

public sealed partial class KsStream
{
    private const int ScanChunk = 4096;

    public KsRange ReadRange(long count)
    {
        AlignToByte();
        Require(count);
        var start = Position;
        Position += count;
        return new KsRange(start, count);
    }

    public KsRange ReadToEnd()
    {
        AlignToByte();
        var start = Position;
        Position = Size;
        return new KsRange(start, Size - start);
    }

    public KsRange ReadTerminated(byte[] terminator, bool include, bool consume, bool eosError)
    {
        AlignToByte();
        var start = Position;
        var found = IndexOf(terminator, start, Size);
        if (found < 0)
        {
            if (eosError)
            {
                throw new KsyParseException(FileStructureError.MissingTerminator(KsyDescriptions.Hex(terminator),
                    FileOffset(start), !IsFileBacked,
                    $"End of stream reached before terminator {Convert.ToHexString(terminator)} (from position {start})"));
            }
            Position = Size;
            return new KsRange(start, Size - start);
        }

        var end = found + terminator.Length;
        Position = consume ? end : found;
        return new KsRange(start, (include ? end : found) - start);
    }

    public KsRange Truncate(KsRange range, byte? padRight, byte[]? terminator, bool include)
    {
        var length = range.Length;
        if (padRight is { } pad && terminator?[^1] != pad) length = TrimEnd(range.Start, length, pad);
        if (terminator is not null)
        {
            var found = IndexOf(terminator, range.Start, range.Start + length);
            if (found >= 0) length = (include ? found + terminator.Length : found) - range.Start;
        }
        return range with { Length = length };
    }

    public byte[] ReadArray(KsRange range)
    {
        var data = new byte[range.Length];
        CopyTo(range.Start, data, 0, data.Length);
        return data;
    }

    private long IndexOf(byte[] terminator, long from, long limit)
    {
        var unit = terminator.Length;
        var chunk = ArrayPool<byte>.Shared.Rent(ScanChunk);
        try
        {
            var capacity = chunk.Length / unit * unit;
            for (var position = from; limit - position >= unit;)
            {
                var count = (int)Math.Min(capacity, (limit - position) / unit * unit);
                CopyTo(position, chunk, 0, count);
                for (var i = 0; i < count; i += unit)
                {
                    if (chunk.AsSpan(i, unit).SequenceEqual(terminator)) return position + i;
                }
                position += count;
            }
            return -1;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private long TrimEnd(long start, long length, byte pad)
    {
        var chunk = ArrayPool<byte>.Shared.Rent(ScanChunk);
        try
        {
            while (length > 0)
            {
                var count = (int)Math.Min(chunk.Length, length);
                CopyTo(start + length - count, chunk, 0, count);
                var kept = chunk.AsSpan(0, count).TrimEnd(pad).Length;
                length -= count - kept;
                if (kept > 0) break;
            }
            return length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }
}
