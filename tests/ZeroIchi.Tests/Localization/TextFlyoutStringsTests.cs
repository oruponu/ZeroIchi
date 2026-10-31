using Avalonia.Controls;
using System.Globalization;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

[Collection(HeadlessTestApp.Collection)]
public sealed class TextFlyoutStringsTests
{
    [Fact]
    public Task Attach_SetsTheTextBoxContextMenuStrings() => HeadlessTestApp.RunAsync(() =>
    {
        var resources = new ResourceDictionary();

        TextFlyoutStrings.Attach(resources);

        Assert.Equal("切り取り(_T)", resources["StringTextFlyoutCutText"]);
        Assert.Equal("コピー(_C)", resources["StringTextFlyoutCopyText"]);
        Assert.Equal("貼り付け(_P)", resources["StringTextFlyoutPasteText"]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Attach_UpdatesTheStringsWhenTheCultureChanges() => HeadlessTestApp.RunAsync(() =>
    {
        using var _ = FakeEnglishStrings.Use();
        var resources = new ResourceDictionary();
        TextFlyoutStrings.Attach(resources);
        try
        {
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            Assert.Equal("en:切り取り(_T)", resources["StringTextFlyoutCutText"]);
            Assert.Equal("en:コピー(_C)", resources["StringTextFlyoutCopyText"]);
            Assert.Equal("en:貼り付け(_P)", resources["StringTextFlyoutPasteText"]);
        }
        finally
        {
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ja"));
        }
        return Task.CompletedTask;
    });
}
