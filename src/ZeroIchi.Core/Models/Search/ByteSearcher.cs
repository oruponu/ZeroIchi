using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.Search;

public static class ByteSearcher
{
    private const int ChunkSize = 65536;

    public static void Scan(ByteBuffer buffer, byte[] pattern, long start, int maxMatches,
        Action<SearchProgress> report, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(pattern.Length);

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
}
