using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class TikaMimeRulesTests
{
    private static readonly MimeTypeEntry[] Types = TikaMimeRules.Create();

    private static MimeTypeEntry Type(string name) => Types.Single(t => t.Name == name);

    [Fact]
    public void Version_Is410()
    {
        Assert.Equal("4.1.0", TikaMimeRules.Version);
    }

    [Fact]
    public void Create_ContainsEveryMimeType()
    {
        Assert.Equal(1687, Types.Length);
        Assert.Equal(Types.Length, Types.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void DuplicateDefinitions_AreMerged()
    {
        var heic = Type("image/heic");

        Assert.Equal("image/heif", heic.Parent);
        Assert.Equal(["heic"], heic.Extensions);
        Assert.Equal(4, heic.Magics.Length);
    }

    [Fact]
    public void Parents_ResolveToKnownTypes()
    {
        var names = Types.Select(t => t.Name).ToHashSet();

        Assert.All(Types.Where(t => t.Parent is not null), t => Assert.Contains(t.Parent!, names));
    }

    [Fact]
    public void AliasParent_IsResolvedToTheCanonicalName()
    {
        Assert.DoesNotContain(Types, t => t.Parent == "text/x-c");
    }

    [Fact]
    public void Png_HasItsMagicAndExtension()
    {
        var png = Type("image/png");

        Assert.Equal(["png"], png.Extensions);
        var magic = Assert.Single(png.Magics);
        Assert.Equal(50, magic.Priority);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, magic.Clause.Pattern);
    }

    [Fact]
    public void AudioMpeg_KeepsTheGlobOrder()
    {
        var mpeg = Type("audio/mpeg");

        Assert.Equal(["mpga", "mp2", "mp2a", "mp3", "m2a", "m3a"], mpeg.Extensions);
        Assert.Contains(mpeg.Magics, m => m.Priority == 50);
        Assert.Contains(mpeg.Magics, m => m.Priority == 10);
    }

    [Fact]
    public void NonExtensionGlobs_AreDropped()
    {
        Assert.All(Types.SelectMany(t => t.Extensions),
            extension => Assert.DoesNotContain(extension, c => c is '*' or '?' or '[' or '/' or '^'));
    }

    [Fact]
    public void EveryRegex_CanBeCreated()
    {
        static IEnumerable<MagicMatch> Flatten(MagicMatch match) => match.Children.SelectMany(Flatten).Prepend(match);

        var regexes = Types.SelectMany(type => type.Magics).SelectMany(magic => Flatten(magic.Clause))
            .Where(match => match.Kind == MagicMatchKind.Regex).Select(match => match.Regex!).ToArray();

        Assert.NotEmpty(regexes);
        Assert.All(regexes, regex => MagicRegex.Create(regex));
    }

    [Fact]
    public void GeneratedFile_StartsWithTheNotice()
    {
        var path = Path.Combine(RepositoryRoot.FullName, "src", "ZeroIchi.Core", "Models", "FileTypes", "TikaMimeRules.g.cs");
        var header = string.Join("\n", File.ReadLines(path).Take(8));

        Assert.Contains("Apache License, Version 2.0", header);
        Assert.Contains("Apache Tika 4.1.0", header);
        Assert.Contains("Modified", header);
        Assert.Contains("Do not edit", header);
    }
}
