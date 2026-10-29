using System;
using System.Collections.Generic;

namespace ZeroIchi.Models.Search;

public readonly record struct SearchTarget(bool IsResolved, long Offset);

public sealed class SearchMatchSet(long start, long length)
{
    private static readonly SearchTarget Pending = new(false, -1);

    private readonly List<long> _before = [];
    private readonly List<long> _after = [];
    private long _afterScannedTo;
    private long _beforeScannedTo;

    public int Count => _before.Count + _after.Count;

    public bool IsComplete { get; private set; }

    public void Add(SearchProgress progress)
    {
        if (progress.Phase == SearchPhase.AfterStart)
        {
            _after.AddRange(progress.Matches);
            _afterScannedTo = Math.Max(_afterScannedTo, progress.ScannedTo - start);
        }
        else
        {
            _before.AddRange(progress.Matches);
            _beforeScannedTo = Math.Max(_beforeScannedTo, progress.ScannedTo);
        }
    }

    public void Complete() => IsComplete = true;

    public long[] ToArray()
    {
        var result = new long[Count];
        _before.CopyTo(result);
        _after.CopyTo(result, _before.Count);
        return result;
    }

    public SearchTarget FindNext(long position)
    {
        var index = UpperBound(position);
        if (index < Count)
        {
            var match = MatchAt(index);
            return IsScanned(position + 1, match) ? new SearchTarget(true, match) : Pending;
        }

        if (!IsScanned(position + 1, length))
        {
            return Pending;
        }

        if (Count == 0)
        {
            return IsScanned(0, length) ? new SearchTarget(true, -1) : Pending;
        }

        var first = MatchAt(0);
        return IsScanned(0, first) ? new SearchTarget(true, first) : Pending;
    }

    public SearchTarget FindPrevious(long position)
    {
        var index = LowerBound(position) - 1;
        if (index >= 0)
        {
            var match = MatchAt(index);
            return IsScanned(match + 1, position) ? new SearchTarget(true, match) : Pending;
        }

        if (!IsScanned(0, position))
        {
            return Pending;
        }

        if (Count == 0)
        {
            return IsScanned(0, length) ? new SearchTarget(true, -1) : Pending;
        }

        var last = MatchAt(Count - 1);
        return IsScanned(last + 1, length) ? new SearchTarget(true, last) : Pending;
    }

    private long MatchAt(int index) => index < _before.Count ? _before[index] : _after[index - _before.Count];

    private int UpperBound(long value)
    {
        int low = 0, high = Count;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (MatchAt(mid) <= value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private int LowerBound(long value)
    {
        int low = 0, high = Count;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (MatchAt(mid) < value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private bool IsScanned(long from, long to)
    {
        from = Math.Max(from, 0);
        to = Math.Min(to, length);
        if (to <= from || IsComplete)
        {
            return true;
        }

        if (_beforeScannedTo >= start)
        {
            return to <= start + _afterScannedTo;
        }

        return (to <= _beforeScannedTo) || (from >= start && to <= start + _afterScannedTo);
    }
}
