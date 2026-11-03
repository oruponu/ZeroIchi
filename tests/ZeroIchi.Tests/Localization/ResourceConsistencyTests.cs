using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

public sealed partial class ResourceConsistencyTests
{
    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex PlaceholderPattern();

    private static Dictionary<string, string> ReadStrings(CultureInfo culture)
    {
        var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    private static Dictionary<string, string> English => ReadStrings(CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Japanese => ReadStrings(CultureInfo.GetCultureInfo("ja"));

    [Fact]
    public void JapaneseAndEnglish_HaveTheSameKeys()
    {
        Assert.Equal(English.Keys.Order(), Japanese.Keys.Order());
    }

    [Fact]
    public void JapaneseAndEnglish_HaveTheSamePlaceholders()
    {
        var japanese = Japanese;
        Assert.All(English, entry => Assert.Equal(Placeholders(entry.Value), Placeholders(japanese[entry.Key])));
    }

    [Fact]
    public void MenuKeys_HaveOneAccessKeyInEachLanguage()
    {
        var menuValues = English.Concat(Japanese).Where(entry => entry.Key.StartsWith("Menu", StringComparison.Ordinal));

        Assert.All(menuValues, entry => Assert.Equal(1, entry.Value.Count(c => c == '_')));
    }

    [Fact]
    public void Japanese_IsLoadedFromTheSatelliteAssembly()
    {
        Assert.Equal("ファイルを開く", Japanese["FileOpenTitle"]);
        Assert.Equal("Open File", English["FileOpenTitle"]);
    }

    private static string[] Placeholders(string value) =>
        PlaceholderPattern().Matches(value).Select(match => match.Value).Distinct().Order().ToArray();
}
