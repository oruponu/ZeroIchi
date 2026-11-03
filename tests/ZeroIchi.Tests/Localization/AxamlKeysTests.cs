using System.Globalization;
using System.Text.RegularExpressions;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

public sealed partial class AxamlKeysTests
{
    [GeneratedRegex(@"\{l:Localize\s+(\w+)\s*\}")]
    internal static partial Regex KeyPattern();

    [Theory]
    [InlineData("{l:Localize MenuFile}")]
    [InlineData("{l:Localize MenuFile }")]
    public void KeyPattern_MatchesTheKey(string text)
    {
        Assert.Equal("MenuFile", KeyPattern().Match(text).Groups[1].Value);
    }

    [Fact]
    public void AllAxamlKeys_ExistInTheResources()
    {
        var keys = Directory.EnumerateFiles(Path.Combine(RepositoryRoot.FullName, "src", "ZeroIchi"), "*.axaml",
                SearchOption.AllDirectories)
            .SelectMany(path => KeyPattern().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.NotNull(Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture)));
    }
}
