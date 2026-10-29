using ZeroIchi.Models.Search;

namespace ZeroIchi.Tests.Search;

public sealed class SearchMatchSetTests
{
    private const long Start = 500;
    private const long Length = 1000;

    private static SearchProgress After(long scannedTo, params long[] matches) => new(SearchPhase.AfterStart, scannedTo, matches);
    private static SearchProgress Before(long scannedTo, params long[] matches) => new(SearchPhase.BeforeStart, scannedTo, matches);

    private static SearchTarget At(long offset) => new(true, offset);
    private static readonly SearchTarget Pending = new(false, -1);

    [Fact]
    public void ToArray_PutsMatchesBeforeTheStartFirst()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(Length, 550, 900));
        set.Add(Before(Start, 100, 300));

        Assert.Equal([100L, 300L, 550L, 900L], set.ToArray());
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void FindNext_WaitsUntilTheGapIsScanned()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(600, 550));

        Assert.Equal(At(550), set.FindNext(Start - 1));
        Assert.Equal(Pending, set.FindNext(550));

        set.Add(After(Length, 900));

        Assert.Equal(At(900), set.FindNext(550));
    }

    [Fact]
    public void FindNext_PastTheLastMatch_WrapsOnceTheBeginningIsScanned()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(Length, 550));

        Assert.Equal(Pending, set.FindNext(550));

        set.Add(Before(200));
        Assert.Equal(Pending, set.FindNext(550));

        set.Add(Before(400, 300));
        Assert.Equal(At(300), set.FindNext(550));
    }

    [Fact]
    public void FindPrevious_FromTheStart_WaitsForTheWholeRangeBeforeIt()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(Length, 550));
        set.Add(Before(300, 100));

        Assert.Equal(Pending, set.FindPrevious(Start));

        set.Add(Before(Start));

        Assert.Equal(At(100), set.FindPrevious(Start));
    }

    [Fact]
    public void FindPrevious_WithNoMatchBeforeTheStart_WrapsToTheLastMatch()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(Length, 550, 900));

        Assert.Equal(Pending, set.FindPrevious(Start));

        set.Add(Before(Start));

        Assert.Equal(At(900), set.FindPrevious(Start));
    }

    [Fact]
    public void FindNext_WithTheOnlyMatch_ResolvesToItselfOnlyAfterTheScanCompletes()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(Length, 550));

        Assert.Equal(Pending, set.FindNext(550));

        set.Add(Before(Start));

        Assert.Equal(At(550), set.FindNext(550));
    }

    [Fact]
    public void Complete_ResolvesAmongTheMatchesFound()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(600, 550));

        set.Complete();

        Assert.True(set.IsComplete);
        Assert.Equal(At(550), set.FindNext(550));
        Assert.Equal(At(550), set.FindPrevious(550));
    }

    [Fact]
    public void NoMatches_ResolveToNoneOnlyWhenComplete()
    {
        var set = new SearchMatchSet(Start, Length);
        set.Add(After(Length));

        Assert.Equal(Pending, set.FindNext(Start - 1));

        set.Complete();

        Assert.Equal(At(-1), set.FindNext(Start - 1));
        Assert.Equal(At(-1), set.FindPrevious(Start));
    }

    [Fact]
    public void FindNext_FromBeforeZero_FindsAMatchAtTheStartOfTheFile()
    {
        var set = new SearchMatchSet(0, Length);
        set.Add(After(100, 0));

        Assert.Equal(At(0), set.FindNext(-1));
    }
}
