using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ZeroIchi.Views;

public static partial class NoticeNameConverters
{
    public static FuncValueConverter<string?, IReadOnlyList<string>> SplitIntoWords { get; } =
        new(name => string.IsNullOrEmpty(name) ? [] : WordEnd().Split(name));

    [GeneratedRegex("(?<=[. ])(?=.)")]
    private static partial Regex WordEnd();
}
