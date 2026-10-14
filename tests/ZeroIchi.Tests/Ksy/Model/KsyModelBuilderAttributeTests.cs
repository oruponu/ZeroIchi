using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using static ZeroIchi.Tests.Ksy.Model.KsyTestLoader;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyModelBuilderAttributeTests
{
    static KsyModelBuilderAttributeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Theory]
    [InlineData("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n    size: 4\n", "/seq/0/size", 6, 5)]
    [InlineData("meta:\n  id: x\nseq:\n  - id: a\n    size: 4\n    encoding: ASCII\n", "/seq/0/encoding", 6, 5)]
    [InlineData("meta:\n  id: x\nseq:\n  - id: a\n    type: f4le\n    enum: e\n", "/seq/0/enum", 6, 5)]
    public void Parse_UnknownKey_IsErrorAtKey(string yaml, string path, int line, int column)
    {
        var error = SingleError(yaml);

        Assert.Contains("Unknown key", error.Message);
        Assert.Equal((path, line, column), (error.Path, error.Line, error.Column));
    }

    [Fact]
    public void Parse_MinimalDefinition()
    {
        var format = Load("""
            meta:
              id: sample
              file-extension: [smp, SMP2]
            seq:
              - id: magic
                contents: [0x89, 'PNG']
              - id: version
                type: u1
            """);

        Assert.Equal("sample", format.Id);
        Assert.Equal("sample", format.Root.Name);
        Assert.Equal(new[] { "smp", "SMP2" }, format.FileExtensions);
        Assert.Equal(new[] { "magic", "version" }, format.Root.Seq.Select(a => a.Id));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, Attribute(format, "magic").Contents);
        Assert.Equal(new KsyIntType(1, false, null), Attribute(format, "version").DataType);
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("1abc")]
    [InlineData("a-b")]
    public void Parse_InvalidIdentifier_IsError(string id)
    {
        var error = SingleError($"meta:\n  id: x\nseq:\n  - id: {id}\n    type: u1\n");

        Assert.Contains("Invalid attribute name", error.Message);
        Assert.Equal("/seq/0/id", error.Path);
        Assert.Equal((4, 9), (error.Line, error.Column));
    }

    [Fact]
    public void Parse_DashKeys_AreIgnored()
    {
        var format = Load("""
            meta:
              id: x
              -zeroichi-note: anything
            -webide-representation: '{a}'
            seq:
              - id: a
                type: u1
                -orig-id: A
            """);

        Assert.Equal("{a}", format.Root.WebideRepresentation);
    }

    [Theory]
    [InlineData("u2", 2, false, null)]
    [InlineData("s4le", 4, true, KsyByteOrder.Le)]
    [InlineData("u8be", 8, false, KsyByteOrder.Be)]
    public void Parse_IntegerTypes(string type, int size, bool signed, KsyByteOrder? order)
    {
        var format = Load($"meta:\n  id: x\n  endian: le\nseq:\n  - id: a\n    type: {type}\n");

        Assert.Equal(new KsyIntType(size, signed, order), Attribute(format, "a").DataType);
        Assert.Equal(new KsyFixedEndian(KsyByteOrder.Le), format.Root.Endian);
    }

    [Fact]
    public void Parse_MultiByteTypeWithoutEndian_IsError()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: u4\n");

        Assert.Contains("needs an endianness", error.Message);
        Assert.Equal("/seq/0/type", error.Path);
    }

    [Fact]
    public void Parse_FloatType()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: f8be\n");

        Assert.Equal(new KsyFloatType(8, KsyByteOrder.Be), Attribute(format, "a").DataType);
    }

    [Fact]
    public void Parse_BitTypes()
    {
        var format = Load("""
            meta:
              id: x
              bit-endian: le
            seq:
              - id: flag
                type: b1
              - id: one_bit_enum
                type: b1
                enum: e
              - id: wide
                type: b12be
            enums:
              e:
                0: off_value
                1: on_value
            """);

        Assert.Equal(new KsyBitType(1, KsyByteOrder.Le, true), Attribute(format, "flag").DataType);
        Assert.Equal(new KsyBitType(1, KsyByteOrder.Le, false), Attribute(format, "one_bit_enum").DataType);
        Assert.Equal(new KsyBitType(12, KsyByteOrder.Be, false), Attribute(format, "wide").DataType);
    }

    [Fact]
    public void Parse_BitEndianDefaultsToBigEndian()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: b3\n");

        Assert.Equal(new KsyBitType(3, KsyByteOrder.Be, false), Attribute(format, "a").DataType);
    }

    [Theory]
    [InlineData("b0")]
    [InlineData("b65")]
    public void Parse_BitTypeOutOfRange_IsError(string type)
    {
        Assert.Contains("1 to 64 bits", SingleError($"meta:\n  id: x\nseq:\n  - id: a\n    type: {type}\n").Message);
    }

    [Fact]
    public void Parse_Strings()
    {
        var format = Load("""
            meta:
              id: x
              encoding: UTF-8
            seq:
              - id: name
                type: str
                size: 4
              - id: wide
                type: strz
                encoding: UTF-16LE
              - id: ascii
                type: strz
                encoding: ASCII
                terminator: 0x0a
            """);

        var name = Attribute(format, "name");
        Assert.Equal("UTF-8", ((KsyStrType)name.DataType!).EncodingName);
        Assert.False(((KsyStrType)name.DataType!).IsZeroTerminated);
        Assert.Equal(new byte[] { 0, 0 }, Attribute(format, "wide").Terminator);
        Assert.Equal(new byte[] { 0x0a }, Attribute(format, "ascii").Terminator);
    }

    [Fact]
    public void Parse_StringWithoutEncoding_IsError()
    {
        Assert.Contains("needs an encoding", SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: str\n    size: 2\n").Message);
    }

    [Fact]
    public void Parse_BytesNeedSize()
    {
        Assert.Contains("size, size-eos or terminator is required", SingleError("meta:\n  id: x\nseq:\n  - id: a\n").Message);
    }

    [Fact]
    public void Parse_SizeAndSizeEos_IsError()
    {
        Assert.Contains("cannot be used together", SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size: 2\n    size-eos: true\n").Message);
    }

    [Fact]
    public void Parse_SizeKeys()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: a
                size: 0x10
                pad-right: 0x20
              - id: b
                size-eos: true
              - id: c
                terminator: 0
                consume: false
                include: true
                eos-error: false
            """);

        Assert.NotNull(Attribute(format, "a").Size);
        Assert.Equal((byte)0x20, Attribute(format, "a").PadRight);
        Assert.True(Attribute(format, "b").SizeEos);
        var c = Attribute(format, "c");
        Assert.Equal(new byte[] { 0 }, c.Terminator);
        Assert.False(c.Consume);
        Assert.True(c.Include);
        Assert.False(c.EosError);
    }

    [Theory]
    [InlineData("    size-eos: 'true'\n", "true or false")]
    [InlineData("    terminator: 256\n", "0 to 255")]
    [InlineData("    terminator: '\\n'\n", "0 to 255")]
    public void Parse_ValueKinds_AreChecked(string line, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nseq:\n  - id: a\n" + line).Message);
    }

    [Theory]
    [InlineData("'AB'", new byte[] { 0x41, 0x42 })]
    [InlineData("[0x41, 66, 'CD', '0x10', '-1']", new byte[] { 0x41, 0x42, 0x43, 0x44, 0x10, 0xFF })]
    [InlineData("[-128, 255]", new byte[] { 0x80, 0xFF })]
    [InlineData("\"\\u3042\"", new byte[] { 0xE3, 0x81, 0x82 })]
    public void Parse_Contents(string yaml, byte[] expected)
    {
        var format = Load($"meta:\n  id: x\nseq:\n  - id: a\n    contents: {yaml}\n");

        Assert.Equal(expected, Attribute(format, "a").Contents);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("[256]")]
    [InlineData("[true]")]
    [InlineData("['256']")]
    [InlineData("['9223372036854775808']")]
    [InlineData("['0xFFFFFFFFFFFFFFFF']")]
    [InlineData("['0x100']")]
    public void Parse_InvalidContents_IsError(string yaml)
    {
        SingleError($"meta:\n  id: x\nseq:\n  - id: a\n    contents: {yaml}\n");
    }

    [Fact]
    public void Parse_ContentsWithSize_WarnsThatSizeIsIgnored()
    {
        var result = KsyModelBuilder.Parse("meta:\n  id: x\nseq:\n  - id: a\n    contents: 'AB'\n    size: 4\n");

        Assert.NotNull(result.Format);
        Assert.Equal(KsySeverity.Warning, Assert.Single(result.Diagnostics).Severity);
    }

    [Fact]
    public void Parse_DuplicateAttributeIds_IsError()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n  - id: a\n    type: u1\n");

        Assert.Contains("Duplicate attribute id", error.Message);
        Assert.Equal("/seq/1/id", error.Path);
    }

    [Fact]
    public void Parse_UnnamedAttributes_GetGeneratedIds()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - type: u1\n  - type: u1\n");

        Assert.Equal(new[] { "_unnamed0", "_unnamed1" }, format.Root.Seq.Select(a => a.Id));
    }

    [Fact]
    public void Parse_ExpressionSyntaxError_ReportsColumnInsideScalar()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size: 1 + * 2\n");

        Assert.Equal("/seq/0/size", error.Path);
        Assert.Equal((5, 15), (error.Line, error.Column));
    }

    [Fact]
    public void Parse_ExpressionSyntaxErrorInQuotedScalar_CountsTheQuote()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size: '1 + * 2'\n");

        Assert.Equal((5, 16), (error.Line, error.Column));
    }

    [Fact]
    public void Parse_AllErrorsAreReported()
    {
        var result = KsyModelBuilder.Parse("meta:\n  id: x\nseq:\n  - id: A\n    type: u1\n  - id: b\n    type: u4\n  - id: c\n    type: u1\n    bogus: 1\n");

        Assert.Null(result.Format);
        Assert.Equal(3, result.Diagnostics.Count(d => d.Severity == KsySeverity.Error));
    }

    [Fact]
    public void Parse_NestedTypesAndUserTypeReferences()
    {
        var format = Load("""
            meta:
              id: x
              endian: be
            seq:
              - id: header
                type: header
              - id: body
                type: header::inner
                size: 4
            types:
              header:
                seq:
                  - id: len
                    type: u2
                types:
                  inner:
                    seq:
                      - id: v
                        type: u1
                    instances:
                      outer_ref:
                        value: 1
            """);

        var header = format.Root.Types["header"];
        var inner = header.Types["inner"];
        Assert.Same(format.Root, header.Outer);
        Assert.Equal("x::header::inner", inner.FullName);
        Assert.Equal("/types/header/types/inner", inner.Path);
        Assert.Same(header, ((KsyUserType)Attribute(format, "header").DataType!).Type);
        Assert.Same(inner, ((KsyUserType)Attribute(format, "body").DataType!).Type);
        Assert.Equal(new KsyFixedEndian(KsyByteOrder.Be), inner.EffectiveEndian);
    }

    [Fact]
    public void Parse_TypeLookupSearchesOuterScopes()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: a
                type: a
            types:
              a:
                seq:
                  - id: b
                    type: b
              b:
                seq:
                  - id: v
                    type: u1
            """);

        var a = format.Root.Types["a"];
        Assert.Same(format.Root.Types["b"], ((KsyUserType)a.Seq[0].DataType!).Type);
    }

    [Fact]
    public void Parse_UnknownType_IsError()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: nope\n");

        Assert.Contains("Type 'nope' not found", error.Message);
        Assert.Equal("/seq/0/type", error.Path);
    }

    [Theory]
    [InlineData("::a")]
    [InlineData("a[]")]
    [InlineData("A")]
    [InlineData("a(1")]
    [InlineData("a(1][0)")]
    [InlineData("a(1]+[2)")]
    public void Parse_InvalidTypeReference_IsError(string type)
    {
        SingleError($"meta:\n  id: x\nseq:\n  - id: f\n    type: '{type}'\ntypes:\n  a:\n    seq: []\n");
    }

    [Fact]
    public void Parse_TypeArguments()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: body
                type: block(len, len + 1)
            types:
              block:
                params:
                  - id: size
                    type: u1
                  - id: other
                    type: u2
                seq:
                  - id: data
                    size: size
            """);

        var body = (KsyUserType)Attribute(format, "body").DataType!;
        Assert.Equal(2, body.Arguments.Count);
        Assert.IsType<KsBinary>(body.Arguments[1]);
        Assert.Equal(new[] { "size", "other" }, format.Root.Types["block"].Params.Select(p => p.Id));
    }

    [Fact]
    public void Parse_TypeArgumentCountMismatch_IsError()
    {
        var error = SingleError("""
            meta:
              id: x
            seq:
              - id: body
                type: block(1, 2)
            types:
              block:
                params:
                  - id: size
                    type: u1
            """);

        Assert.Contains("takes 1 argument(s) but 2 were given", error.Message);
    }

    [Fact]
    public void Parse_DuplicateIdsAcrossParamsSeqAndInstances_AreErrors()
    {
        var result = KsyModelBuilder.Parse("""
            meta:
              id: x
            params:
              - id: a
            seq:
              - id: a
                type: u1
            instances:
              a:
                value: 1
            """);

        Assert.Equal(2, result.Diagnostics.Count(d => d.Message.Contains("Duplicate attribute id")));
    }

    [Fact]
    public void Parse_Switch()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: kind
                type: u1
              - id: body
                size: 4
                type:
                  switch-on: kind
                  cases:
                    1: a
                    0x10: u1
                    '"x"': a
                    _: b
            types:
              a:
                seq: []
              b:
                seq: []
            """);

        var body = (KsySwitchType)Attribute(format, "body").DataType!;
        Assert.IsType<KsName>(body.On);
        Assert.Equal(4, body.Cases.Count);
        Assert.Equal((Int128)16, ((KsIntLiteral)body.Cases[1].Match!).Value);
        Assert.IsType<KsStringLiteral>(body.Cases[2].Match);
        Assert.Null(body.Cases[3].Match);
        Assert.Same(format.Root.Types["b"], ((KsyUserType)body.Cases[3].Type).Type);
    }

    [Theory]
    [InlineData("      switch-on: k\n", "switch-on and cases are required")]
    [InlineData("      switch-on: k\n      cases:\n        1: nope\n", "Type 'nope' not found")]
    [InlineData("      switch-on: k\n      cases: {}\n      bogus: 1\n", "Unknown key")]
    public void Parse_InvalidSwitch_IsError(string body, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nseq:\n  - id: k\n    type: u1\n  - id: s\n    type:\n" + body).Message);
    }

    [Fact]
    public void Parse_Instances()
    {
        var format = Load("""
            meta:
              id: x
            seq:
              - id: count
                type: u1
            instances:
              table:
                pos: 0x10
                io: _root._io
                type: u1
                repeat: expr
                repeat-expr: count
              double_count:
                value: count * 2
                if: count > 0
                doc: twice
            """);

        var table = Attribute(format, "table");
        Assert.Equal(KsyAttributeKind.ParseInstance, table.Kind);
        Assert.NotNull(table.Pos);
        Assert.NotNull(table.Io);
        Assert.Equal(KsyRepeatKind.Expr, table.Repeat);
        var doubled = Attribute(format, "double_count");
        Assert.Equal(KsyAttributeKind.ValueInstance, doubled.Kind);
        Assert.IsType<KsBinary>(doubled.Value);
        Assert.NotNull(doubled.If);
        Assert.Equal("twice", doubled.Doc);
    }

    [Theory]
    [InlineData("    value: 1\n    pos: 0\n", "Unknown key 'pos' in a value instance")]
    [InlineData("    value: 1\n    type: u1\n", "Unknown key 'type' in a value instance")]
    [InlineData("    type: u1\n    value2: 1\n", "Unknown key 'value2' in a parse instance")]
    public void Parse_InvalidInstanceKeys_AreErrors(string body, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\ninstances:\n  i:\n" + body).Message);
    }

    [Fact]
    public void Parse_PosAndIoInSeq_AreUnknownKeys()
    {
        Assert.Contains("Unknown key 'pos'", SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n    pos: 0\n").Message);
    }

    [Theory]
    [InlineData("    repeat: eos\n", KsyRepeatKind.Eos)]
    [InlineData("    repeat: expr\n    repeat-expr: 3\n", KsyRepeatKind.Expr)]
    [InlineData("    repeat: until\n    repeat-until: _ == 0\n", KsyRepeatKind.Until)]
    public void Parse_Repeat(string lines, KsyRepeatKind expected)
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n" + lines);

        Assert.Equal(expected, Attribute(format, "a").Repeat);
    }

    [Theory]
    [InlineData("    repeat: expr\n", "repeat-expr is required")]
    [InlineData("    repeat: until\n", "repeat-until is required")]
    [InlineData("    repeat: forever\n", "repeat must be")]
    [InlineData("    repeat-expr: 3\n", "Unknown key 'repeat-expr'")]
    [InlineData("    repeat: eos\n    repeat-until: true\n", "Unknown key 'repeat-until'")]
    public void Parse_InvalidRepeat_IsError(string lines, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n" + lines).Message);
    }

    [Theory]
    [InlineData("    valid: 0x10\n", typeof(KsyValidEq))]
    [InlineData("    valid: yes\n", typeof(KsyValidEq))]
    [InlineData("    valid:\n      eq: 3\n", typeof(KsyValidEq))]
    [InlineData("    valid:\n      min: 1\n      max: 5\n", typeof(KsyValidRange))]
    [InlineData("    valid:\n      max: 5\n", typeof(KsyValidRange))]
    [InlineData("    valid:\n      any-of: [1, 2, 3]\n", typeof(KsyValidAnyOf))]
    [InlineData("    valid:\n      expr: _ % 2 == 0\n", typeof(KsyValidExpr))]
    [InlineData("    enum: e\n    valid:\n      in-enum: true\n", typeof(KsyValidInEnum))]
    public void Parse_Valid(string lines, Type expected)
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n" + lines + "enums:\n  e:\n    1: one\n");

        Assert.IsType(expected, Attribute(format, "a").Valid);
    }

    [Theory]
    [InlineData("    valid: 1.5\n", "valid must be")]
    [InlineData("    valid:\n      eq: 1\n      min: 0\n", "Unknown key 'min' in valid")]
    [InlineData("    valid:\n      in-enum: false\n", "in-enum must be true")]
    [InlineData("    valid:\n      in-enum: true\n", "requires enum")]
    [InlineData("    valid: {}\n", "valid needs")]
    public void Parse_InvalidValid_IsError(string lines, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n" + lines).Message);
    }

    [Fact]
    public void Parse_ContentsWithValid_IsError()
    {
        Assert.Contains("contents and valid", SingleError("meta:\n  id: x\nseq:\n  - id: a\n    contents: [1]\n    valid: 1\n").Message);
    }

    [Theory]
    [InlineData("zlib", KsyProcessKind.Zlib)]
    [InlineData("xor(0xff)", KsyProcessKind.Xor)]
    [InlineData("xor([1, 2])", KsyProcessKind.Xor)]
    [InlineData("rol(3)", KsyProcessKind.Rol)]
    [InlineData("ror(len)", KsyProcessKind.Ror)]
    public void Parse_Process(string process, KsyProcessKind expected)
    {
        var format = Load($"meta:\n  id: x\nseq:\n  - id: len\n    type: u1\n  - id: a\n    size: 4\n    process: {process}\n");

        Assert.Equal(expected, Attribute(format, "a").Process!.Kind);
    }

    [Theory]
    [InlineData("    size: 4\n    process: my_decoder(1)\n", "Unsupported process")]
    [InlineData("    size: 4\n    process: xor\n", "Unsupported process")]
    [InlineData("    type: t\n    process: zlib\n", "process requires size")]
    public void Parse_InvalidProcess_IsError(string lines, string messagePart)
    {
        Assert.Contains(messagePart, SingleError("meta:\n  id: x\nseq:\n  - id: a\n" + lines + "types:\n  t:\n    seq: []\n").Message);
    }

    [Fact]
    public void Parse_ProcessOnString_IsUnknownKey()
    {
        Assert.Contains("Unknown key 'process'", SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: str\n    encoding: ASCII\n    size: 2\n    process: zlib\n").Message);
    }

    [Fact]
    public void Parse_ToStringExpression()
    {
        var format = Load("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\nto-string: a.to_s\n");

        Assert.IsType<KsMember>(format.Root.ToStringExpr);
    }

    [Fact]
    public void Parse_NonScalarExpression_IsError()
    {
        Assert.Contains("Expected an expression", SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size: [1]\n").Message);
    }

    [Fact]
    public void Parse_ExpressionErrorInBlockScalar_ReportsScalarStart()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    size: |\n      1 +\n      * 2\n");

        Assert.Equal((5, 11), (error.Line, error.Column));
    }

    [Fact]
    public void Parse_InvalidAttributeEncoding_DoesNotCascade()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: a\n    type: str\n    encoding: foo\n    size: 4\n");

        Assert.Contains("Unknown encoding 'foo'", error.Message);
    }
}
