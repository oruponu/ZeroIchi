using System.Globalization;
using System.Runtime.CompilerServices;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests;

internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize() => Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ja"));
}
