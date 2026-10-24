using System;
using System.Text.RegularExpressions;

namespace ZeroIchi.Models.FileTypes;

internal static class MagicRegex
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

    public static Regex Create(string pattern) =>
        new($"^(?:{pattern})", RegexOptions.CultureInvariant, MatchTimeout);
}
