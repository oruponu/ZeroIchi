using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Tests.Ksy.Yaml;

public sealed class YamlLoaderTests
{
    private static YamlNode LoadRoot(string text)
    {
        var result = YamlLoader.Load(text);
        Assert.Empty(result.Errors);
        return Assert.IsAssignableFrom<YamlNode>(result.Root);
    }

    private static YamlError SingleError(string text)
    {
        var result = YamlLoader.Load(text);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public void Load_BuildsTreeWithOneBasedPositions()
    {
        var root = (YamlMapping)LoadRoot("meta:\n  id: foo\nseq:\n  - id: a\n    type: u1\n");

        Assert.Equal(new YamlPosition(1, 1), root.Start);
        var seq = (YamlSequence)root.Get("seq")!;
        var first = (YamlMapping)seq.Items[0];
        Assert.Equal(new YamlPosition(4, 5), first.Start);
        var type = (YamlScalar)first.Get("type")!;
        Assert.Equal("u1", type.Text);
        Assert.Equal(new YamlPosition(5, 11), type.Start);
    }

    [Fact]
    public void Load_KeepsEntryOrder()
    {
        var root = (YamlMapping)LoadRoot("b: 1\na: 2\nc: 3\n");

        Assert.Equal(new[] { "b", "a", "c" }, root.Entries.Select(e => e.Key.Text));
    }

    [Theory]
    [InlineData("a: x", YamlScalarStyle.Plain)]
    [InlineData("a: 'x'", YamlScalarStyle.SingleQuoted)]
    [InlineData("a: \"x\"", YamlScalarStyle.DoubleQuoted)]
    [InlineData("a: |\n  x\n", YamlScalarStyle.Literal)]
    [InlineData("a: >\n  x\n", YamlScalarStyle.Folded)]
    public void Load_RecordsScalarStyle(string text, YamlScalarStyle expected)
    {
        var root = (YamlMapping)LoadRoot(text);

        Assert.Equal(expected, ((YamlScalar)root.Get("a")!).Style);
    }

    [Fact]
    public void Load_StringKeyAndIntegerKey_AreDifferent()
    {
        var root = (YamlMapping)LoadRoot("'1': a\n1: b\n");

        Assert.Equal(2, root.Entries.Count);
    }

    [Theory]
    [InlineData("0x10: a\n16: b\n")]
    [InlineData("a: 1\na: 2\n")]
    [InlineData("yes: a\ntrue: b\n")]
    [InlineData("~: a\nnull: b\n")]
    public void Load_DuplicateKeys_AreErrors(string text)
    {
        var result = YamlLoader.Load(text);

        var error = Assert.Single(result.Errors);
        Assert.Contains("Duplicate key", error.Message);
        Assert.Equal(new YamlPosition(2, 1), error.Position);
    }

    [Fact]
    public void Load_DuplicateKeys_AreAllReported()
    {
        var result = YamlLoader.Load("a: 1\na: 2\nb:\n  c: 1\n  c: 2\n");

        Assert.Equal(2, result.Errors.Count);
        Assert.NotNull(result.Root);
    }

    [Fact]
    public void Load_NestingWithinLimit_Succeeds()
    {
        var text = new string('[', YamlLoader.MaxDepth) + new string(']', YamlLoader.MaxDepth);

        Assert.IsType<YamlSequence>(LoadRoot(text));
    }

    [Fact]
    public void Load_TooDeepNesting_IsError()
    {
        var text = new string('[', YamlLoader.MaxDepth + 1) + new string(']', YamlLoader.MaxDepth + 1);

        var error = SingleError(text);

        Assert.Contains("Nesting", error.Message);
        Assert.Equal(new YamlPosition(1, YamlLoader.MaxDepth + 1), error.Position);
    }

    [Fact]
    public void Load_VeryDeepNesting_DoesNotOverflowTheStack()
    {
        var text = new string('[', 100_000) + new string(']', 100_000);

        Assert.Contains("Nesting", SingleError(text).Message);
    }

    [Fact]
    public void Load_Alias_IsError()
    {
        var error = SingleError("a: &x 1\nb: *x\n");

        Assert.Contains("Aliases", error.Message);
        Assert.Equal(new YamlPosition(2, 4), error.Position);
    }

    [Fact]
    public void Load_Tag_IsError()
    {
        Assert.Contains("Tags", SingleError("a: !!str 1\n").Message);
    }

    [Fact]
    public void Load_NonScalarKey_IsError()
    {
        Assert.Contains("keys must be scalars", SingleError("? [a]\n: b\n").Message);
    }

    [Fact]
    public void Load_MultipleDocuments_IsError()
    {
        Assert.Contains("one YAML document", SingleError("a: 1\n---\nb: 2\n").Message);
    }

    [Fact]
    public void Load_EmptyText_IsError()
    {
        Assert.Contains("empty", SingleError("").Message);
    }

    [Fact]
    public void Load_SyntaxError_ReportsPosition()
    {
        var error = SingleError("a: 1\nb: 'x\n");

        Assert.Equal(2, error.Position.Line);
    }

    [Theory]
    [InlineData("a: [1, 2\nb: 3\n")]
    [InlineData("a: 'x")]
    [InlineData("- a\nb: c\n")]
    [InlineData("a:\n\t- x\n")]
    [InlineData("[")]
    [InlineData("{a: 1")]
    [InlineData("a: b: c\n")]
    [InlineData("%YAML 9.9\n---\na: 1\n")]
    public void Load_MalformedYaml_ReportsOneErrorWithoutThrowing(string text)
    {
        var result = YamlLoader.Load(text);

        Assert.Null(result.Root);
        Assert.Single(result.Errors);
    }
}
