using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public static class KsEncodings
{
    private static readonly Dictionary<string, string> DotNetNames = BuildNames();

    public static IReadOnlyList<string> CanonicalNames { get; } = [.. DotNetNames.Keys];

    private static Dictionary<string, string> BuildNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ASCII"] = "us-ascii",
            ["UTF-8"] = "utf-8",
            ["UTF-16LE"] = "utf-16",
            ["UTF-16BE"] = "utf-16BE",
            ["UTF-32LE"] = "utf-32",
            ["UTF-32BE"] = "utf-32BE",
            ["IBM437"] = "IBM437",
            ["IBM866"] = "cp866",
            ["Shift_JIS"] = "shift_jis",
            ["Big5"] = "big5",
            ["EUC-KR"] = "euc-kr",
        };

        foreach (var part in Enumerable.Range(1, 16).Where(p => p != 12))
            names[$"ISO-8859-{part}"] = $"iso-8859-{part}";

        for (var page = 1250; page <= 1258; page++)
            names[$"windows-{page}"] = $"windows-{page}";

        return names;
    }

    public static bool TryGet(string name, [NotNullWhen(true)] out Encoding? encoding)
    {
        encoding = null;
        if (!DotNetNames.TryGetValue(name, out var dotNetName)) return false;

        try
        {
            encoding = Encoding.GetEncoding(dotNetName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
