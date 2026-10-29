namespace ZeroIchi.Models.Search;

public enum SearchPhase
{
    AfterStart,
    BeforeStart,
}

public readonly record struct SearchProgress(SearchPhase Phase, long ScannedTo, long[] Matches);
