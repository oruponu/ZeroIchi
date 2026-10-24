using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ZeroIchi.Tests;

public sealed class ThirdPartyNoticesTests
{
    private static readonly string Root = FindRoot();

    internal static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZeroIchi.slnx"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("ZeroIchi.slnx was not found.");
    }

    private static string Notices() =>
        File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md")).ReplaceLineEndings("\n");

    private static IEnumerable<string> ListedPackages() =>
        Regex.Matches(Notices(), @"^- Packages?: (.+)$", RegexOptions.Multiline)
            .SelectMany(m => m.Groups[1].Value.Split([", ", " and "], StringSplitOptions.None));

    private static string PackageDirectory(string package)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "src", "ZeroIchi", "obj", "project.assets.json")));
        var path = assets.RootElement.GetProperty("libraries").EnumerateObject()
            .Single(library => library.Name.StartsWith(package + "/", StringComparison.Ordinal))
            .Value.GetProperty("path").GetString()!;
        return assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, path))
            .First(Directory.Exists);
    }

    public static TheoryData<string> PackageReferences()
    {
        var project = XDocument.Load(Path.Combine(Root, "src", "ZeroIchi", "ZeroIchi.csproj"));
        var data = new TheoryData<string>();
        foreach (var reference in project.Descendants("PackageReference"))
            data.Add(reference.Attribute("Include")!.Value);
        return data;
    }

    public static TheoryData<string> NativeAssetPackages() =>
        new() { "SkiaSharp.NativeAssets.Win32", "HarfBuzzSharp.NativeAssets.Win32" };

    [Theory]
    [MemberData(nameof(PackageReferences))]
    public void Notices_ListEveryShippedPackageReference(string package)
    {
        Assert.Contains(package, ListedPackages());
    }

    [Fact]
    public void Notices_RecordTheImportedFormatLibraryCommit()
    {
        Assert.Contains("c35300d0049440570230aaed4219b1f794dd3843", Notices());
    }

    [Theory]
    [MemberData(nameof(NativeAssetPackages))]
    public void Notices_ReproduceTheNativeAssetNotices(string package)
    {
        var notices = File.ReadAllText(Path.Combine(PackageDirectory(package), "THIRD-PARTY-NOTICES.txt"))
            .ReplaceLineEndings("\n").Trim('\n');

        Assert.Contains(notices, Notices());
    }

    [Fact]
    public void Notices_StateTheIjgAttribution()
    {
        Assert.Contains("- This software is based in part on the work of the Independent JPEG Group.", Notices());
    }

    private static string TikaFile(string name) =>
        File.ReadAllText(Path.Combine(Root, "tools", "MimeRuleGenerator", "tika", name)).ReplaceLineEndings("\n").Trim('\n');

    private static string Section(string title)
    {
        var notices = Notices();
        var start = notices.IndexOf($"\n## {title}\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"## {title} was not found.");
        var end = notices.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        return end < 0 ? notices[start..] : notices[start..end];
    }

    [Fact]
    public void Notices_IncludeTheApacheLicenseOfTika()
    {
        const string end = "END OF TERMS AND CONDITIONS";
        var license = TikaFile("LICENSE.txt");

        Assert.Contains(license[..(license.IndexOf(end, StringComparison.Ordinal) + end.Length)], Section("Apache Tika"));
    }

    [Fact]
    public void Notices_ReproduceThePronomNotice()
    {
        var notice = TikaFile("NOTICE.txt");
        var pronom = notice[notice.IndexOf("Tika-mimetypes.xml includes mimetype definitions", StringComparison.Ordinal)..];

        var section = Section("Apache Tika");

        Assert.Contains(pronom, section);
        Assert.Contains("Open Government Licence v3.0", section);
    }

    [Fact]
    public void Notices_RecordTheImportedTikaVersion()
    {
        var section = Section("Apache Tika");

        Assert.Contains("4.1.0", section);
        Assert.Contains("c5348700b3f58e5559a50c8e1156d7e2806f42f9", section);
    }
}
