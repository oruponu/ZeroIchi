using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroIchi.Models;

public sealed record ThirdPartyNotice(string Name, string? License, string Body);

public sealed record ThirdPartyNoticeDocument(string Preamble, IReadOnlyList<ThirdPartyNotice> Notices);

public static partial class ThirdPartyNotices
{
    internal const string ResourceName = "ZeroIchi.THIRD-PARTY-NOTICES.md";

    private const string HeadingPrefix = "## ";
    private const string LicensePrefix = "- License: ";

    public static ThirdPartyNoticeDocument Load()
    {
        using var stream = typeof(ThirdPartyNotices).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} was not found.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static ThirdPartyNoticeDocument Parse(string text)
    {
        var preamble = new StringBuilder();
        var notices = new List<ThirdPartyNotice>();
        string? name = null;
        string? license = null;
        var body = new StringBuilder();
        var inCodeBlock = false;

        void Flush()
        {
            if (name is not null) notices.Add(new ThirdPartyNotice(name, license, body.ToString().Trim('\n')));
        }

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (!inCodeBlock && line.StartsWith(HeadingPrefix, StringComparison.Ordinal))
            {
                Flush();
                name = line[HeadingPrefix.Length..];
                license = null;
                body.Clear();
                continue;
            }

            if (name is null)
            {
                if (!line.StartsWith("# ", StringComparison.Ordinal)) preamble.Append(line).Append('\n');
                continue;
            }

            if (!inCodeBlock && license is null && line.StartsWith(LicensePrefix, StringComparison.Ordinal))
                license = TrailingParenthetical().Replace(line[LicensePrefix.Length..], "");

            body.Append(line).Append('\n');
        }

        Flush();
        return new ThirdPartyNoticeDocument(preamble.ToString().Trim('\n'), notices);
    }

    [GeneratedRegex(@"\s*\([^)]*\)$")]
    private static partial Regex TrailingParenthetical();
}
