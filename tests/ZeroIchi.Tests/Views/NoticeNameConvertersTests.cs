using System.Globalization;
using Avalonia.Data.Converters;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

public sealed class NoticeNameConvertersTests
{
    private static IEnumerable<string>? Convert(string value) =>
        (IEnumerable<string>?)((IValueConverter)NoticeNameConverters.SplitIntoWords)
            .Convert(value, typeof(IEnumerable<string>), null, CultureInfo.InvariantCulture);

    [Fact]
    public void SplitIntoWords_SplitsAfterEveryDot()
    {
        Assert.Equal(["Microsoft.", "Extensions.", "DependencyInjection"], Convert("Microsoft.Extensions.DependencyInjection"));
    }

    [Fact]
    public void SplitIntoWords_SplitsAfterEverySpace()
    {
        Assert.Equal(["Apache ", "Tika"], Convert("Apache Tika"));
    }

    [Fact]
    public void SplitIntoWords_KeepsTheWholeNameInOrder()
    {
        const string name = "SkiaSharp and HarfBuzzSharp: native assets";

        Assert.Equal(name, string.Concat(Convert(name)!));
    }
}
