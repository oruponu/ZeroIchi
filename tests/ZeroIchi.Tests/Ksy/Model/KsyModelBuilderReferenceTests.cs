using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using static ZeroIchi.Tests.Ksy.Model.KsyTestLoader;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyModelBuilderReferenceTests
{
    [Fact]
    public void Parse_NamesInExpressions_ResolveToMembersParamsAndSpecialNames()
    {
        Load("""
            meta:
              id: x
            params:
              - id: base
                type: u1
            seq:
              - id: len
                type: u1
              - id: items
                type: u1
                repeat: until
                repeat-until: _ == 0 or _index >= len
              - id: rest
                size: _io.size - _io.pos + base
            instances:
              total:
                value: len + doubled + _root.len
              doubled:
                value: len * 2
            """);
    }

    [Fact]
    public void Parse_UnknownName_IsErrorAtNamePosition()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size: 1 + lenght\n");

        Assert.Contains("Name 'lenght' not found in type 'x'", error.Message);
        Assert.Equal((5, 15), (error.Line, error.Column));
    }

    [Fact]
    public void Parse_NamesAreCheckedInTheirOwnType()
    {
        var error = SingleError("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: body
                type: body
            types:
              body:
                seq:
                  - id: data
                    size: len
            """);

        Assert.Contains("Name 'len' not found in type 'body'", error.Message);
    }

    [Fact]
    public void Parse_UnknownRootMember_IsError()
    {
        var error = SingleError("""
            meta:
              id: x
            seq:
              - id: body
                type: body
            types:
              body:
                seq:
                  - id: data
                    size: _root.nope
            """);

        Assert.Contains("Name 'nope' not found in type 'x'", error.Message);
    }

    [Fact]
    public void Parse_ParentReferencesAreNotChecked()
    {
        Load("""
            meta:
              id: x
            seq:
              - id: body
                type: body
            types:
              body:
                seq:
                  - id: data
                    size: _parent.whatever
            """);
    }

    [Fact]
    public void Parse_EnumReferences()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: kind
                type: u1
                enum: kinds
              - id: body
                type: t
                if: kind == kinds::big
            types:
              t:
                instances:
                  is_small:
                    value: _parent.kind == kinds::small
                  nested_enum:
                    value: inner::one
                enums:
                  inner:
                    1: one
            enums:
              kinds:
                0: small
                1: big
            """);

        Assert.Same(format.Root.Enums["kinds"], Attribute(format, "kind").Enum);
    }

    [Theory]
    [InlineData("kinds::nope", "Enum 'kinds' has no value 'nope'")]
    [InlineData("nope::small", "Enum 'nope' not found")]
    public void Parse_InvalidEnumReference_IsError(string expr, string messagePart)
    {
        var error = SingleError($"meta:\n  id: x\ninstances:\n  v:\n    value: {expr}\nenums:\n  kinds:\n    0: small\n");

        Assert.Contains(messagePart, error.Message);
    }

    [Fact]
    public void Parse_EnumPathThroughType()
    {
        Load("""
            meta:
              id: x
            instances:
              v:
                value: t::color::red
            types:
              t:
                enums:
                  color:
                    1: red
            """);
    }

    [Theory]
    [InlineData("a.as<u4>")]
    [InlineData("a.as<t>")]
    [InlineData("sizeof<u4> + bitsizeof<b3>")]
    [InlineData("sizeof<t>")]
    public void Parse_TypeReferencesInExpressions(string expr)
    {
        Load($"meta:\n  id: x\nseq:\n  - id: a\n    type: u1\ninstances:\n  v:\n    value: {expr}\ntypes:\n  t:\n    seq:\n      - id: b\n        type: u1\n");
    }

    [Theory]
    [InlineData("a.as<nope>", "Type 'nope' not found")]
    [InlineData("sizeof<nope>", "Type 'nope' not found")]
    [InlineData("sizeof<var>", "sizeof cannot be used on variable-size type 'var'")]
    public void Parse_InvalidTypeReferencesInExpressions_AreErrors(string expr, string messagePart)
    {
        var error = SingleError($"meta:\n  id: x\nseq:\n  - id: a\n    type: u1\ninstances:\n  v:\n    value: {expr}\ntypes:\n  var:\n    seq:\n      - id: b\n        size-eos: true\n");

        Assert.Contains(messagePart, error.Message);
    }

    [Fact]
    public void Parse_SizeofOfCurrentType()
    {
        Load("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\ninstances:\n  v:\n    value: _sizeof\n");
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size-eos: true\ninstances:\n  v:\n    value: _sizeof\n");

        Assert.Contains("_sizeof cannot be used on variable-size type 'x'", error.Message);
    }

    [Fact]
    public void Parse_SizeofOfAttribute()
    {
        var error = SingleError("""
            meta:
              id: x
            seq:
              - id: fixed
                type: fixed
              - id: var
                type: var
            instances:
              a:
                value: fixed._sizeof
              b:
                value: var._sizeof
            types:
              fixed:
                seq:
                  - id: v
                    type: u1
              var:
                seq:
                  - id: v
                    size-eos: true
            """);

        Assert.Contains("_sizeof cannot be used on variable-size type 'var'", error.Message);
        Assert.Equal("/instances/b/value", error.Path);
    }

    [Theory]
    [InlineData("    type: u1\n", 8L)]
    [InlineData("    type: u4le\n", 32L)]
    [InlineData("    type: f8le\n", 64L)]
    [InlineData("    size: 3\n", 24L)]
    [InlineData("    size: 2 * 3\n", 48L)]
    [InlineData("    contents: [1, 2]\n", 16L)]
    [InlineData("    type: b3\n", 3L)]
    [InlineData("    type: u2le\n    repeat: expr\n    repeat-expr: 4\n", 64L)]
    [InlineData("    type: b2\n    repeat: expr\n    repeat-expr: 3\n", 6L)]
    [InlineData("    type: str\n    encoding: ASCII\n    size: 4\n", 32L)]
    [InlineData("    type: fixed\n", 16L)]
    [InlineData("    type: var\n    size: 5\n", 40L)]
    public void Parse_StaticSize_FixedTypes(string attribute, long expectedBits)
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n" + attribute + Helpers);

        Assert.Equal(expectedBits, format.Root.SizeInBits);
    }

    [Theory]
    [InlineData("    size-eos: true\n")]
    [InlineData("    terminator: 0\n")]
    [InlineData("    type: strz\n    encoding: ASCII\n")]
    [InlineData("    type: u1\n    repeat: eos\n")]
    [InlineData("    type: u1\n    repeat: expr\n    repeat-expr: _io.size\n")]
    [InlineData("    type: var\n")]
    [InlineData("    type:\n      switch-on: 1\n      cases:\n        1: fixed\n")]
    public void Parse_StaticSize_VariableTypes(string attribute)
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n" + attribute + Helpers);

        Assert.Null(format.Root.SizeInBits);
    }

    [Fact]
    public void Parse_StaticSize_IgnoresIf()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - size: 0\n    if: false\n  - id: a\n    type: u2le\n    if: true\n");

        Assert.Equal(16L, format.Root.SizeInBits);
    }

    [Fact]
    public void Parse_StaticSize_AlignsBytesAfterBits()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: b4\n  - id: b\n    type: u1\n  - id: c\n    type: b4\n");

        Assert.Equal(20L, format.Root.SizeInBits);
    }

    [Fact]
    public void Parse_StaticSize_RecursiveTypeIsVariable()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: node\ntypes:\n  node:\n    seq:\n      - id: next\n        type: node\n");

        Assert.Null(format.Root.Types["node"].SizeInBits);
    }

    [Fact]
    public void Parse_StaticSize_LongTypeChainOnSmallStack_DoesNotOverflow()
    {
        const int length = 2000;
        var yaml = new System.Text.StringBuilder("meta:\n  id: x\nseq:\n  - id: a\n    type: t0\ninstances:\n  s:\n    value: sizeof<t0>\ntypes:\n");
        for (var i = 0; i < length; i++)
            yaml.Append($"  t{i}:\n    seq:\n      - id: a\n        type: t{i + 1}\n");
        yaml.Append($"  t{length}:\n    seq:\n      - id: v\n        type: u1\n");
        KsyLoadResult? result = null;

        var exception = SmallStack.Run(() => result = KsyModelBuilder.Parse(yaml.ToString()));

        Assert.Null(exception);
        Assert.True(result!.Format is not null, string.Join("\n", result.Diagnostics));
        Assert.Equal(8L, result.Format!.Root.SizeInBits);
        Assert.Equal(8L, result.Format.Root.Types["t0"].SizeInBits);
        Assert.Equal(8L, result.Format.Root.Types[$"t{length}"].SizeInBits);
    }

    [Fact]
    public void Parse_StaticSize_SharedTypesAreComputedOnce()
    {
        const int depth = 22;
        var yaml = new System.Text.StringBuilder("meta:\n  id: x\nseq:\n  - id: a\n    type: t0\ntypes:\n");
        for (var i = 0; i < depth; i++)
            yaml.Append($"  t{i}:\n    seq:\n      - id: a\n        type: t{i + 1}\n      - id: b\n        type: t{i + 1}\n");
        yaml.Append($"  t{depth}:\n    seq:\n      - id: v\n        type: u1\n");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var format = Load(yaml.ToString());

        Assert.Equal(8L << depth, format.Root.SizeInBits);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), stopwatch.Elapsed.ToString());
    }

    [Fact]
    public void KsyType_ImplementsTypeInfoAndEnumType()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: u2le\nenums:\n  e:\n    5: five\n");

        IKsTypeInfo info = format.Root;
        IKsEnumType e = format.Root.Enums["e"];
        Assert.Equal(16L, info.SizeInBits);
        Assert.False(info.IsInstance(KsValue.FromInt(1)));
        Assert.Equal("five", e.GetName(5));
    }

    private const string Helpers = """
        types:
          fixed:
            seq:
              - id: v
                type: u2le
          var:
            seq:
              - id: v
                size-eos: true

        """;
}
