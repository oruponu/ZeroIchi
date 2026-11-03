using System.Globalization;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

public sealed class LanguageSettingTests
{
    [Theory]
    [InlineData("ja", "ja")]
    [InlineData("JA", "ja")]
    [InlineData("en", "en")]
    [InlineData("En", "en")]
    [InlineData(null, null)]
    [InlineData("fr", null)]
    [InlineData("", null)]
    [InlineData("ja-JP", null)]
    public void Normalize_ReturnsTheSupportedLanguageOrNull(string? language, string? expected)
    {
        Assert.Equal(expected, LanguageSetting.Normalize(language));
    }

    [Fact]
    public void Resolve_Japanese_ReturnsJa()
    {
        Assert.Equal("ja", LanguageSetting.Resolve("ja").Name);
    }

    [Fact]
    public void Resolve_English_ReturnsEn()
    {
        Assert.Equal("en", LanguageSetting.Resolve("en").Name);
    }

    [Fact]
    public void Resolve_Unknown_ReturnsTheSystemCulture()
    {
        Assert.Equal(CultureInfo.CurrentUICulture, LanguageSetting.Resolve("fr"));
    }
}
