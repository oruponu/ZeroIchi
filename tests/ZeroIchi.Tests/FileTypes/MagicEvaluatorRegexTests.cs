using System.Diagnostics;
using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class MagicEvaluatorRegexTests
{
    private const string Catastrophic = "^(a+)+(?<=a)$";

    private static MagicMatch Regex(string offset, string value) =>
        TikaMagicParser.Parse("regex", offset, value, null, null, [], out _);

    private static bool Matches(MagicMatch match, byte[] data) =>
        new MagicEvaluator(new ArrayByteBuffer(data)).Matches(match);

    private static byte[] Repeated(char c, int count, string suffix) =>
        Encoding.Latin1.GetBytes(new string(c, count) + suffix);

    [Fact]
    public void Regex_MatchesFromTheOffset()
    {
        var match = Regex("0", "BZh[1-9]");

        Assert.True(Matches(match, "BZh9"u8.ToArray()));
        Assert.False(Matches(match, " BZh9"u8.ToArray()));
    }

    [Fact]
    public void Regex_RangeTriesEveryStart()
    {
        var match = Regex("2:6", "\\\\xFF[\\\\xF2-\\\\xF7]");
        var atFive = new byte[10];
        atFive[5] = 0xFF;
        atFive[6] = 0xF3;
        var atSeven = new byte[10];
        atSeven[7] = 0xFF;
        atSeven[8] = 0xF3;

        Assert.True(Matches(match, atFive));
        Assert.False(Matches(match, atSeven));
    }

    [Fact]
    public void Regex_SeesBytesAsLatin1()
    {
        Assert.True(Matches(Regex("0", "\\\\xE9"), [0xE9]));
    }

    [Fact]
    public void Regex_InlineFlagsWork()
    {
        var match = Regex("0", "(?s)^\\\\x0D.{3}\\\\x0C");

        Assert.True(Matches(match, [0x0D, 0x0A, (byte)'a', 0x0A, 0x0C]));
    }

    [Fact]
    public void Regex_Timeout_IsNoMatch()
    {
        var match = Regex("0", Catastrophic);

        Assert.True(match.RegexNeedsBacktracking);
        Assert.False(Matches(match, Repeated('a', 40, "b")));
    }

    [Fact]
    public void ManyTimeouts_StayWithinTheBudget()
    {
        var magics = Enumerable.Range(0, 20).Select(_ => new MimeMagic(50, Regex("0", Catastrophic))).ToArray();
        var detector = new FileTypeDetector([new MimeTypeEntry("x/slow", null, [], magics)]);
        var buffer = new ArrayByteBuffer(Repeated('a', 40, "b"));

        var stopwatch = Stopwatch.StartNew();
        var (result, attempts) = detector.DetectWithStatistics(buffer);
        stopwatch.Stop();

        Assert.Null(result);
        Assert.InRange(attempts, 1, 19);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"{stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void RangeScan_StopsWhenTheBudgetRunsOut()
    {
        var evaluator = new MagicEvaluator(new ArrayByteBuffer(Repeated('a', 4100, "b")));

        var stopwatch = Stopwatch.StartNew();
        var matched = evaluator.Matches(Regex("0:4000", Catastrophic));
        stopwatch.Stop();

        Assert.False(matched);
        Assert.InRange(evaluator.RegexAttempts, 1, 9);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"{stopwatch.ElapsedMilliseconds} ms");
    }
}
