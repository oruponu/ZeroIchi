using System.Diagnostics.CodeAnalysis;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsyBuiltinFormatsTests
{
    private sealed class ThrowingSource : IKsyImportSource
    {
        public bool TryRead(string path, [NotNullWhen(true)] out string? text) =>
            throw new InvalidOperationException("broken source");
    }

    private static IEnumerable<string> EmbeddedPaths()
    {
        var prefix = KsyBuiltinFormats.ResourcePrefix;
        foreach (var name in typeof(KsyBuiltinFormats).Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".ksy", StringComparison.Ordinal))
                continue;
            var relative = name[prefix.Length..^".ksy".Length];
            var dot = relative.IndexOf('.');
            yield return dot < 0 ? relative : relative[..dot] + "/" + relative[(dot + 1)..];
        }
    }

    [Fact]
    public void Load_RegistersOnlyDefinitionsWithoutDiagnostics()
    {
        var source = new KsyMemoryImportSource()
            .Add("good", "meta:\n  id: good\nseq:\n  - id: a\n    type: u1\n")
            .Add("warned", "meta:\n  id: warned\nseq:\n  - id: a\n    contents: [1]\n    size: 1\n")
            .Add("broken", "meta:\n  id: broken\nseq:\n  - id: a\n    type: nope\n");

        var formats = KsyBuiltinFormats.Load(source, ["good", "warned", "broken", "missing"], new KsyNativeTypeRegistry());

        Assert.Equal(["good"], formats.Select(f => f.Id));
    }

    [Fact]
    public void Load_SkipsADefinitionWhoseLoadingThrows()
    {
        var formats = KsyBuiltinFormats.Load(new ThrowingSource(), ["any"], new KsyNativeTypeRegistry());

        Assert.Empty(formats);
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
        Assert.All(KsyBuiltinFormats.Roots, root => Assert.Contains(root, EmbeddedPaths()));
    }
}
