using System.Globalization;

namespace ZeroIchi.Tests;

public sealed class AppFontFallbackTests
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja");
    private static readonly CultureInfo JapaneseJapan = CultureInfo.GetCultureInfo("ja-JP");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo EnglishUnitedStates = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void SelectFontFallbackCulture_JapaneseOnAnEnglishSystem_UsesJapanese()
    {
        Assert.Equal("ja", App.SelectFontFallbackCulture(Japanese, EnglishUnitedStates).Name);
    }

    [Fact]
    public void SelectFontFallbackCulture_EnglishOnAJapaneseSystem_UsesTheSystem()
    {
        Assert.Equal("ja-JP", App.SelectFontFallbackCulture(English, JapaneseJapan).Name);
    }

    [Fact]
    public void SelectFontFallbackCulture_EnglishOnAnEnglishSystem_UsesTheSystem()
    {
        Assert.Equal("en-US", App.SelectFontFallbackCulture(English, EnglishUnitedStates).Name);
    }
}
