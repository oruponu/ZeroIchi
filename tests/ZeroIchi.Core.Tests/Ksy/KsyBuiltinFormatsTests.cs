using System.Diagnostics.CodeAnalysis;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsyBuiltinFormatsTests
{
    private sealed class ThrowingSource : IKsyImportSource
    {
        public bool TryRead(string path, [NotNullWhen(true)] out string? text) =>
            throw new InvalidOperationException("broken source");
    }

    private sealed class RecordingSource(IKsyImportSource inner) : IKsyImportSource
    {
        public HashSet<string> Paths { get; } = [];

        public bool TryRead(string path, [NotNullWhen(true)] out string? text)
        {
            Paths.Add(path);
            return inner.TryRead(path, out text);
        }
    }

    private static IEnumerable<string> EmbeddedPaths()
    {
        var prefix = KsyBuiltinFormats.ResourcePrefix;
        foreach (var name in typeof(KsyBuiltinFormats).Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".ksy", StringComparison.Ordinal))
                continue;
            yield return name[prefix.Length..^".ksy".Length].Replace('.', '/');
        }
    }

    private static IKsyImportSource Definitions() => new KsyMemoryImportSource()
        .Add("good", "meta:\n  id: good\nseq:\n  - id: a\n    type: u1\n")
        .Add("warned", "meta:\n  id: warned\nseq:\n  - id: a\n    contents: [1]\n    size: 1\n")
        .Add("broken", "meta:\n  id: broken\nseq:\n  - id: a\n    type: nope\n");

    [Fact]
    public void Load_ReturnsADefinitionWithoutDiagnostics()
    {
        Assert.Equal("good", KsyBuiltinFormats.Load(Definitions, "good", () => new KsyNativeTypeRegistry())?.Id);
    }

    [Theory]
    [InlineData("warned")]
    [InlineData("broken")]
    [InlineData("missing")]
    public void Load_ReturnsNullForADefinitionWithDiagnostics(string path)
    {
        Assert.Null(KsyBuiltinFormats.Load(Definitions, path, () => new KsyNativeTypeRegistry()));
    }

    [Fact]
    public void Load_ReturnsNullWhenLoadingThrows()
    {
        Assert.Null(KsyBuiltinFormats.Load(() => new ThrowingSource(), "any", () => new KsyNativeTypeRegistry()));
    }

    [Fact]
    public void Load_ReturnsNullWhenTheNativeTypesCannotBeCreated()
    {
        Assert.Null(KsyBuiltinFormats.Load(() => new KsyMemoryImportSource(), "any",
            () => throw new InvalidOperationException("duplicate native type")));
    }

    [Fact]
    public void BuiltinDefinitions_LoadWithoutDiagnostics()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var source = KsyBuiltinFormats.CreateSource();
        var natives = KsyBuiltinFormats.CreateNativeTypes();

        var problems = EmbeddedPaths()
            .SelectMany(path => KsyModelBuilder.Load(path, source, natives).Diagnostics.Select(d => d.ToString()))
            .ToList();

        Assert.Empty(problems);
    }

    [Fact]
    public void BuiltinDefinitions_LoadWithCrlfLineEndings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var embedded = KsyBuiltinFormats.CreateSource();
        var source = new KsyMemoryImportSource();
        foreach (var path in EmbeddedPaths())
        {
            Assert.True(embedded.TryRead(path, out var text));
            source.Add(path, text.ReplaceLineEndings("\r\n"));
        }

        var problems = EmbeddedPaths()
            .SelectMany(path => KsyModelBuilder.Load(path, source, KsyBuiltinFormats.CreateNativeTypes()).Diagnostics)
            .Select(d => d.ToString())
            .ToList();

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("media/id3v2_4")]
    [InlineData("common/vlq_base128_be")]
    public void Source_ReadsTheEmbeddedDefinitions(string path)
    {
        Assert.True(KsyBuiltinFormats.CreateSource().TryRead(path, out _));
    }

    [Fact]
    public void Roots_AreEmbeddedDefinitions()
    {
        Assert.All(KsyBuiltinFormats.Roots, root => Assert.Contains(root.Path, EmbeddedPaths()));
    }

    [Fact]
    public void Roots_AreSortedByPath()
    {
        var paths = KsyBuiltinFormats.Roots.Select(root => root.Path).ToList();

        Assert.Equal(paths.Order(StringComparer.Ordinal), paths);
    }

    [Fact]
    public void Roots_MimeTypesAreKnownToTheDetector()
    {
        var known = TikaMimeRules.Create().Concat(ContainerDetector.ExtraTypes).Select(type => type.Name).ToHashSet();

        Assert.All(KsyBuiltinFormats.Roots.SelectMany(root => root.MimeTypes), mime => Assert.Contains(mime, known));
    }

    [Fact]
    public void Roots_MimeTypesAreUnique()
    {
        var mimeTypes = KsyBuiltinFormats.Roots.SelectMany(root => root.MimeTypes).ToList();

        Assert.Equal(mimeTypes.Distinct(), mimeTypes);
    }

    [Fact]
    public void EmbeddedDefinitions_AreRootsOrTheirImports()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var source = new RecordingSource(KsyBuiltinFormats.CreateSource());
        foreach (var root in KsyBuiltinFormats.Roots)
            KsyModelBuilder.Load(root.Path, source, KsyBuiltinFormats.CreateNativeTypes());

        Assert.All(EmbeddedPaths(), path => Assert.Contains(path, source.Paths));
    }
}
