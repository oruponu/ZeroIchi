using Avalonia.Controls;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

public sealed class TextFlyoutStringsTests
{
    [Fact]
    public void Apply_SetsTheTextBoxContextMenuStrings()
    {
        var resources = new ResourceDictionary();

        TextFlyoutStrings.Apply(resources);

        Assert.Equal("切り取り(_T)", resources["StringTextFlyoutCutText"]);
        Assert.Equal("コピー(_C)", resources["StringTextFlyoutCopyText"]);
        Assert.Equal("貼り付け(_P)", resources["StringTextFlyoutPasteText"]);
    }
}
