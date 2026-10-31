using Avalonia.Controls;
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
}
