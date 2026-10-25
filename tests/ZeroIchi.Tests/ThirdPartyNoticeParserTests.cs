using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class ThirdPartyNoticeParserTests
{
    private const string Fenced = "# T\n\nintro\n\n## A\n\n- Package: P\n- License: MIT\n\n```\nCopyright X\n```\n";

    [Fact]
    public void Load_ParsesTheNoticesFileOfTheRepository()
    {
        var expected = ThirdPartyNotices.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot.FullName, "THIRD-PARTY-NOTICES.md")));

        var loaded = ThirdPartyNotices.Load();

        Assert.Equal(expected.Preamble, loaded.Preamble);
        Assert.Equal(expected.Notices, loaded.Notices);
    }

    [Fact]
    public void Parse_ReadsThePreambleWithoutTheTitle()
    {
        Assert.Equal("intro", ThirdPartyNotices.Parse(Fenced).Preamble);
    }

    [Fact]
    public void Parse_KeepsTheBulletsAndDropsTheFences()
    {
        Assert.Equal("- Package: P\n- License: MIT\n\nCopyright X", Assert.Single(ThirdPartyNotices.Parse(Fenced).Notices).Body);
    }

    [Theory]
    [InlineData("- License: MIT", "MIT")]
    [InlineData("- License: CC0-1.0 (https://creativecommons.org/publicdomain/zero/1.0/)", "CC0-1.0")]
    [InlineData("- License: SIL Open Font License, Version 1.1 (text below)", "SIL Open Font License, Version 1.1")]
    public void Parse_ReadsTheLicenseWithoutTheTrailingParenthetical(string line, string license)
    {
        Assert.Equal(license, Assert.Single(ThirdPartyNotices.Parse($"## A\n\n- Source: S\n{line}\n").Notices).License);
    }

    [Fact]
    public void Parse_LeavesTheLicenseEmptyWithoutALicenseLine()
    {
        Assert.Null(Assert.Single(ThirdPartyNotices.Parse("## A\n\n- Source: S\n\ntext\n").Notices).License);
    }

    [Fact]
    public void Parse_IgnoresHeadingsInsideCodeBlocks()
    {
        var notices = ThirdPartyNotices.Parse("## A\n\n```\n## not a heading\n```\n\n## B\n\ntext\n").Notices;

        Assert.Equal(["A", "B"], notices.Select(n => n.Name));
        Assert.Equal("## not a heading", notices[0].Body);
    }

    [Fact]
    public void Parse_TreatsCrlfLikeLf()
    {
        var lf = ThirdPartyNotices.Parse(Fenced);
        var crlf = ThirdPartyNotices.Parse(Fenced.Replace("\n", "\r\n"));

        Assert.Equal(lf.Preamble, crlf.Preamble);
        Assert.Equal(lf.Notices, crlf.Notices);
    }

    [Fact]
    public void Parse_ReadsLicenseOnlyOutsideCodeBlocks()
    {
        Assert.Null(Assert.Single(ThirdPartyNotices.Parse("## A\n\n```\n- License: Fake\n```\n").Notices).License);
    }
}
