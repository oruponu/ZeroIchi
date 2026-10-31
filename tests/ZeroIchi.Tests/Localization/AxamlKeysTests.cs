using System.Globalization;
using System.Text.RegularExpressions;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

public sealed partial class AxamlKeysTests
{
    [GeneratedRegex(@"\{l:Localize\s+(\w+)\}")]
    private static partial Regex KeyPattern();

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
