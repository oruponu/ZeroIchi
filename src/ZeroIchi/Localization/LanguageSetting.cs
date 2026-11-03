using System;
using System.Globalization;

namespace ZeroIchi.Localization;

public static class LanguageSetting
{
    public const string Japanese = "ja";
    public const string English = "en";

    public static string? Normalize(string? language) =>
        string.Equals(language, Japanese, StringComparison.OrdinalIgnoreCase) ? Japanese
        : string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English
        : null;

    public static CultureInfo Resolve(string? language) =>
        Normalize(language) is { } supported ? CultureInfo.GetCultureInfo(supported) : CultureInfo.CurrentUICulture;
}
