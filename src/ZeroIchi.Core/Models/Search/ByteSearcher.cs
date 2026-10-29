using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.Search;

public static class ByteSearcher
{
    private const int ChunkSize = 65536;

    public static List<long> FindMatches(ByteBuffer buffer, byte[] pattern, int maxMatches)
    {
        var matches = new List<long>();

        var overlap = pattern.Length - 1;
        var chunk = ArrayPool<byte>.Shared.Rent(ChunkSize + overlap);
        try
        {
            foreach (var (offset, readLength) in EnumerateChunks(buffer.Length, ChunkSize, overlap))
            {
                buffer.ReadBytes(offset, chunk, 0, readLength);

                var span = chunk.AsSpan(0, readLength);
                var pos = 0;
                while (pos <= span.Length - pattern.Length)
                {
                    var idx = span[pos..].IndexOf(pattern);
                    if (idx < 0) break;
                    matches.Add(offset + pos + idx);
                    if (matches.Count >= maxMatches) return matches;
                    pos += idx + 1;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return matches;
    }

    public static void Scan(ByteBuffer buffer, byte[] pattern, long start, int maxMatches,
        Action<SearchProgress> report, CancellationToken cancellationToken)
    {
        var length = buffer.Length;
        var overlap = pattern.Length - 1;
        var found = 0;
        var chunk = ArrayPool<byte>.Shared.Rent(ChunkSize + overlap);
        try
        {
            if (!ScanPhase(SearchPhase.AfterStart, start, length)) return;
            ScanPhase(SearchPhase.BeforeStart, 0, start);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        bool ScanPhase(SearchPhase phase, long from, long to)
        {
            for (var offset = from; offset < to; offset += ChunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scannedTo = Math.Min(offset + ChunkSize, to);
                var ownedLength = (int)(scannedTo - offset);
                var readLength = (int)Math.Min(ownedLength + overlap, length - offset);
                buffer.ReadBytes(offset, chunk, 0, readLength);

                var matches = new List<long>();
                var span = chunk.AsSpan(0, readLength);
                var pos = 0;
                while (pos <= span.Length - pattern.Length)
                {
                    var idx = span[pos..].IndexOf(pattern);
                    if (idx < 0 || pos + idx >= ownedLength) break;
                    matches.Add(offset + pos + idx);
                    if (++found >= maxMatches) break;
                    pos += idx + 1;
                }

                report(new SearchProgress(phase, scannedTo, [.. matches]));
                if (found >= maxMatches) return false;
            }

            return true;
        }
    }

    public static int FindClosestMatch(List<long> matches, long position)
    {
        var lo = 0;
        var hi = matches.Count - 1;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (matches[mid] < position)
                lo = mid + 1;
            else
                hi = mid;
        }
        if (lo >= matches.Count) return matches.Count - 1;
        if (lo == 0) return 0;

        var distBefore = position - matches[lo - 1];
        var distAfter = matches[lo] - position;
        return distBefore <= distAfter ? lo - 1 : lo;
    }

    internal static IEnumerable<(long Offset, int ReadLength)> EnumerateChunks(
        long dataLength, int chunkSize, int overlap)
    {
        for (long offset = 0; offset < dataLength; offset += chunkSize)
            yield return (offset, (int)Math.Min(chunkSize + overlap, dataLength - offset));
    }
}
