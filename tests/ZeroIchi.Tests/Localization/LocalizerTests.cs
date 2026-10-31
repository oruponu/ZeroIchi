using System.ComponentModel;
using System.Globalization;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

[Collection(HeadlessTestApp.Collection)]
public sealed class LocalizerTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja");

    [Fact]
    public void Indexer_ReturnsTheResourceString()
    {
        Assert.Equal("ファイルを開く", Localizer.Instance["FileOpenTitle"]);
    }

    [Fact]
    public void Indexer_UnknownKey_ReturnsTheKey()
    {
        Assert.Equal("NoSuchKey", Localizer.Instance["NoSuchKey"]);
    }

    [Fact]
    public void SetCulture_SetsTheStringsCultureAndNotifies()
    {
        var names = new List<string?>();
        var cultureChangedCount = 0;
        PropertyChangedEventHandler onPropertyChanged = (_, e) => names.Add(e.PropertyName);
        EventHandler onCultureChanged = (_, _) => cultureChangedCount++;
        Localizer.Instance.PropertyChanged += onPropertyChanged;
        Localizer.Instance.CultureChanged += onCultureChanged;
        try
        {
            Localizer.Instance.SetCulture(English);

            Assert.Equal("en-US", Strings.Culture!.Name);
            Assert.Equal(English, Localizer.Instance.Culture);
            Assert.Contains("Item", names);
            Assert.Equal(1, cultureChangedCount);
        }
        finally
        {
            Localizer.Instance.PropertyChanged -= onPropertyChanged;
            Localizer.Instance.CultureChanged -= onCultureChanged;
            Localizer.Instance.SetCulture(Japanese);
        }
    }

    [Fact]
    public void Strings_UnderAnEnglishCulture_FallsBackToJapanese()
    {
        try
        {
            Localizer.Instance.SetCulture(English);

            Assert.Equal("ファイルを開く", Strings.FileOpenTitle);
        }
        finally
        {
            Localizer.Instance.SetCulture(Japanese);
        }
    }

    [Fact]
    public void FakeEnglishStrings_PrefixesTheValuesUnderEnglish()
    {
        using var _ = FakeEnglishStrings.Use();
        try
        {
            Localizer.Instance.SetCulture(English);

            Assert.Equal("en:ファイルを開く", Strings.FileOpenTitle);
            Assert.Equal("en:ファイルを開く", Localizer.Instance["FileOpenTitle"]);
        }
        finally
        {
            Localizer.Instance.SetCulture(Japanese);
        }
    }
}
