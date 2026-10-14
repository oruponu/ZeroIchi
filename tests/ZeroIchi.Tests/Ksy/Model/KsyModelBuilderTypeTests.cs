using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using static ZeroIchi.Tests.Ksy.Model.KsyTestLoader;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyModelBuilderTypeTests
{
    static KsyModelBuilderTypeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Theory]
    [InlineData("meta:\n  id: x\nfoo: 1\n", "/foo", 3, 1)]
    [InlineData("meta:\n  id: x\n  author: me\n", "/meta/author", 3, 3)]
    public void Parse_UnknownKey_IsErrorAtKey(string yaml, string path, int line, int column)
    {
        var error = SingleError(yaml);

        Assert.Contains("Unknown key", error.Message);
        Assert.Equal((path, line, column), (error.Path, error.Line, error.Column));
    }

    [Fact]
    public void Parse_MissingMetaId_IsError()
    {
        var error = SingleError("seq:\n  - id: a\n    type: u1\n");

        Assert.Contains("meta/id is required", error.Message);
        Assert.Equal((1, 1), (error.Line, error.Column));
    }

    [Theory]
    [InlineData("cp437", "IBM437")]
    [InlineData("SJIS", "Shift_JIS")]
    [InlineData("cp1251", "windows-1251")]
    [InlineData("utf-16be", "UTF-16BE")]
    public void Parse_EncodingAliases_AreCanonicalized(string alias, string canonical)
    {
        var result = KsyModelBuilder.Parse($"meta:\n  id: x\n  encoding: {alias}\n");

        Assert.Equal(canonical, result.Format!.Root.Encoding);
    }

    [Fact]
    public void Parse_UnknownEncoding_IsError()
    {
        Assert.Contains("Unknown encoding", SingleError("meta:\n  id: x\n  encoding: klingon\n").Message);
    }

    [Fact]
    public void Parse_Enums()
    {
        var format = Load("""
            meta:
              id: x
            enums:
              color:
                0: black
                0x10: red
                2: on
                3:
                  id: blue
                  doc: The color blue
            """);

        var color = format.Root.Enums["color"];
        Assert.Equal("red", color.GetName(16));
        Assert.Equal("true", color.GetName(2));
        Assert.True(color.TryGetValue("blue", out var blue));
        Assert.Equal((Int128)3, blue);
    }

    [Theory]
    [InlineData("    a: one\n", "Enum keys must be integers")]
    [InlineData("    1: one\n    2: one\n", "Duplicate enum value name")]
    [InlineData("    1: One\n", "Invalid enum value name")]
    [InlineData("    1:\n      doc: x\n", "id is required")]
    [InlineData("    1: 5\n", "Enum values must be")]
    public void Parse_InvalidEnums_AreErrors(string body, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nenums:\n  e:\n" + body).Message);
    }

    [Theory]
    [InlineData("u4", false)]
    [InlineData("bytes", false)]
    [InlineData("b1", false)]
    [InlineData("struct", false)]
    [InlineData("io", false)]
    [InlineData("any", false)]
    [InlineData("u1[]", true)]
    public void Parse_ParamTypes(string type, bool isArray)
    {
        var format = Load($"meta:\n  id: x\nparams:\n  - id: p\n    type: {type}\n");

        var param = Assert.Single(format.Root.Params);
        Assert.Equal(isArray, param.IsArray);
        Assert.Null(param.UserType);
    }

    [Fact]
    public void Parse_ParamUserTypeAndEnum()
    {
        var format = Load("""
            meta:
              id: x
            params:
              - id: p
                type: t
              - id: q
                type: u1
                enum: e
            types:
              t:
                seq: []
            enums:
              e:
                1: one
            """);

        Assert.Same(format.Root.Types["t"], format.Root.Params[0].UserType);
        Assert.Same(format.Root.Enums["e"], format.Root.Params[1].Enum);
    }

    [Theory]
    [InlineData("    type: nope\n", "Type 'nope' not found")]
    [InlineData("    enum: nope\n", "Enum 'nope' not found")]
    [InlineData("    bogus: 1\n", "Unknown key")]
    public void Parse_InvalidParams_AreErrors(string line, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nparams:\n  - id: p\n" + line).Message);
    }

    [Fact]
    public void Parse_SwitchEndian()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: hdr
                type: hdr
            types:
              hdr:
                meta:
                  endian:
                    switch-on: _root.order
                    cases:
                      '"II"': le
                      '"MM"': be
                seq:
                  - id: v
                    type: u4
                types:
                  child:
                    seq:
                      - id: w
                        type: u2
            instances:
              order:
                pos: 0
                size: 2
                type: str
                encoding: ASCII
            """);

        var hdr = format.Root.Types["hdr"];
        var endian = Assert.IsType<KsySwitchEndian>(hdr.Endian);
        Assert.Equal(new[] { KsyByteOrder.Le, KsyByteOrder.Be }, endian.Cases.Select(c => c.Order));
        Assert.Same(endian, hdr.Types["child"].EffectiveEndian);
        Assert.Null(format.Root.Endian);
    }

    [Theory]
    [InlineData("  endian: little\n", "endian must be")]
    [InlineData("  endian:\n    cases: {}\n", "switch-on is required")]
    [InlineData("  bit-endian: big\n", "bit-endian must be")]
    public void Parse_InvalidEndian_IsError(string lines, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\n" + lines).Message);
    }

    [Fact]
    public void Parse_NestedMetaInheritsEncodingAndBitEndian()
    {
        var format = Load("""
            meta:
              id: x
              encoding: ASCII
              bit-endian: le
            types:
              t:
                meta:
                  encoding: UTF-8
                types:
                  u:
                    seq: []
            """);

        var t = format.Root.Types["t"];
        Assert.Equal("UTF-8", t.Types["u"].EffectiveEncoding);
        Assert.Equal(KsyByteOrder.Le, t.Types["u"].EffectiveBitEndian);
    }

    [Fact]
    public void Parse_YamlErrors_BecomeDiagnostics()
    {
        var result = KsyModelBuilder.Parse("a: [1\n");

        Assert.Null(result.Format);
        Assert.Equal("main", Assert.Single(result.Diagnostics).File);
    }

    [Fact]
    public void Parse_NestedTypes()
    {
        var format = Load("""
            meta:
              id: x
              endian: be
            types:
              header:
                types:
                  inner:
                    meta:
                      bit-endian: le
                    doc: Inner type
              trailer: {}
            """);

        var header = format.Root.Types["header"];
        var inner = header.Types["inner"];
        Assert.Equal(new[] { "header", "trailer" }, format.Root.Types.Keys.Order());
        Assert.Same(format.Root, header.Outer);
        Assert.Same(format, inner.Format);
        Assert.Equal("x::header::inner", inner.FullName);
        Assert.Equal("/types/header/types/inner", inner.Path);
        Assert.Equal("Inner type", inner.Doc);
        Assert.Equal(new KsyFixedEndian(KsyByteOrder.Be), inner.EffectiveEndian);
        Assert.Equal(KsyByteOrder.Le, inner.EffectiveBitEndian);
        Assert.Null(header.Endian);
    }

    [Fact]
    public void FindType_SearchesNestedThenOuterScopesThenTopLevelId()
    {
        var format = Load("""
            meta:
              id: x
            types:
              a:
                types:
                  b: {}
              c:
                types:
                  d: {}
            """);

        var a = format.Root.Types["a"];
        var b = a.Types["b"];
        var d = format.Root.Types["c"].Types["d"];
        Assert.Same(b, a.FindType(["b"]));
        Assert.Same(d, b.FindType(["c", "d"]));
        Assert.Same(d, b.FindType(["x", "c", "d"]));
        Assert.Same(format.Root, b.FindType(["x"]));
        Assert.Null(b.FindType(["c", "nope"]));
        Assert.Null(b.FindType(["nope"]));
    }

    [Fact]
    public void FindEnum_SearchesOuterScopesAndTypePaths()
    {
        var format = Load("""
            meta:
              id: x
            types:
              a:
                enums:
                  inner:
                    1: one
                types:
                  b: {}
            enums:
              outer:
                1: one
            """);

        var b = format.Root.Types["a"].Types["b"];
        Assert.Same(format.Root.Enums["outer"], b.FindEnum(["outer"]));
        Assert.Same(format.Root.Types["a"].Enums["inner"], b.FindEnum(["inner"]));
        Assert.Same(format.Root.Types["a"].Enums["inner"], format.Root.FindEnum(["a", "inner"]));
        Assert.Null(format.Root.FindEnum(["inner"]));
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("1abc")]
    [InlineData("a-b")]
    public void Parse_InvalidTypeName_IsError(string name)
    {
        var error = SingleError($"meta:\n  id: x\ntypes:\n  {name}: {{}}\n");

        Assert.Contains("Invalid type name", error.Message);
        Assert.Equal((4, 3), (error.Line, error.Column));
    }

    [Fact]
    public void Parse_EncodingAlias_IsWarning()
    {
        var result = KsyModelBuilder.Parse("meta:\n  id: x\n  encoding: utf8\n");

        Assert.NotNull(result.Format);
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal(KsySeverity.Warning, warning.Severity);
        Assert.Equal("/meta/encoding", warning.Path);
        Assert.Equal("UTF-8", result.Format!.Root.Encoding);
    }

    [Fact]
    public void Parse_TopLevelMustBeMapping()
    {
        Assert.Contains("must be a mapping", SingleError("- a\n- b\n").Message);
    }
}
