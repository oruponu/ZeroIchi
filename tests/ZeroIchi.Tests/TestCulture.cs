using System.Globalization;
using System.Runtime.CompilerServices;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests;

internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize() => Strings.Culture = CultureInfo.GetCultureInfo("ja");
}
