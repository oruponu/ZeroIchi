using Avalonia.Threading;
using System.Globalization;
using ZeroIchi.Localization;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Localization;

[Collection(HeadlessTestApp.Collection)]
public sealed class LocalizeExtensionTests
{
    [Fact]
    public Task Binding_ReReadsTheStringWhenTheCultureChanges() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = new AboutDialog();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal("バージョン情報", dialog.Title);

            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("About ZeroIchi", dialog.Title);
        }
        finally
        {
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ja"));
            await HeadlessTestApp.CloseAsync(dialog);
        }
    });
}
