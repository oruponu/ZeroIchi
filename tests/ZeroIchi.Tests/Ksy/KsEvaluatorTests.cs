using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsEvaluatorTests
{
    private static KsValue Eval(string source, TestScope? scope = null) =>
        KsEvaluator.Evaluate(KsExpressionParser.Parse(source), scope ?? new TestScope());

    private static KsExpressionException EvalError(string source, TestScope? scope = null) =>
        Assert.Throws<KsExpressionException>(() => Eval(source, scope));

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("7 / 2", "3")]
    [InlineData("-7 / 2", "-4")]
    [InlineData("7 / -2", "-4")]
    [InlineData("-5 % 3", "1")]
    [InlineData("5 % -3", "-1")]
    [InlineData("0xFFFFFFFFFFFFFFFF + 1", "18446744073709551616")]
    [InlineData("1 << 4", "16")]
    [InlineData("-16 >> 2", "-4")]
    [InlineData("0xF0 & 0x3C", "48")]
    [InlineData("0xF0 | 0x0F", "255")]
    [InlineData("0xFF ^ 0x0F", "240")]
    [InlineData("~0", "-1")]
    [InlineData("+5", "5")]
    public void Evaluate_IntegerArithmetic(string source, string expected)
    {
        var value = Eval(source);

        Assert.Equal(KsValueKind.Int, value.Kind);
        Assert.Equal(Int128.Parse(expected), value.AsInt);
    }

    [Theory]
    [InlineData("170141183460469231731687303715884105727 + 1", "overflow")]
    [InlineData("-170141183460469231731687303715884105727 - 2", "overflow")]
    [InlineData("1 << 127", "overflow")]
    [InlineData("1 << -1", "shift count")]
    [InlineData("1 / 0", "Division by zero")]
    [InlineData("1 % 0", "Division by zero")]
    [InlineData("1.0 / 0", "Division by zero")]
    [InlineData("1 / 0.0", "Division by zero")]
    [InlineData("1.0 % 0", "Division by zero")]
    [InlineData("1.5 % -0.0", "Division by zero")]
    public void Evaluate_IntegerErrors(string source, string messagePart)
    {
        Assert.Contains(messagePart, EvalError(source).Message);
    }

    [Theory]
    [InlineData("1 + 0.5", 1.5)]
    [InlineData("7.0 / 2", 3.5)]
    [InlineData("-1.5 * 2", -3.0)]
    [InlineData("-7.5 % 2", 0.5)]
    public void Evaluate_FloatArithmetic(string source, double expected)
    {
        var value = Eval(source);

        Assert.Equal(KsValueKind.Float, value.Kind);
        Assert.Equal(expected, value.AsFloat);
    }

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 <= 1", false)]
    [InlineData("1 == 1.0", true)]
    [InlineData("1 != 2", true)]
    [InlineData("'abc' < 'abd'", true)]
    [InlineData("'a' == 'a'", true)]
    [InlineData("[0x01, 0xFF] > [0x01, 0x02]", true)]
    [InlineData("[1, 2] == [1, 2]", true)]
    [InlineData("true == true", true)]
    [InlineData("true != false", true)]
    public void Evaluate_Comparison(string source, bool expected)
    {
        Assert.Equal(expected, Eval(source).AsBool);
    }

    [Theory]
    [InlineData("'a' == 1")]
    [InlineData("true < false")]
    [InlineData("'a' + 1")]
    [InlineData("1 & 1.0")]
    [InlineData("not 1")]
    [InlineData("1 and true")]
    [InlineData("1 ? 2 : 3")]
    public void Evaluate_TypeMismatch_IsError(string source)
    {
        EvalError(source);
    }

    [Theory]
    [InlineData("true and false", false)]
    [InlineData("false or true", true)]
    [InlineData("not false", true)]
    [InlineData("false and 1 / 0 == 0", false)]
    [InlineData("true or 1 / 0 == 0", true)]
    public void Evaluate_Logic_ShortCircuits(string source, bool expected)
    {
        Assert.Equal(expected, Eval(source).AsBool);
    }

    [Fact]
    public void Evaluate_Ternary_EvaluatesOnlySelectedBranch()
    {
        Assert.Equal((Int128)1, Eval("true ? 1 : 1 / 0").AsInt);
    }

    [Fact]
    public void Evaluate_StringConcat()
    {
        Assert.Equal("abcd", Eval("'ab' + 'cd'").AsString);
    }

    [Fact]
    public void Evaluate_FString()
    {
        Assert.Equal("n=3, s=x", Eval("f\"n={1 + 2}, s={'x'}\"").AsString);
    }

    [Fact]
    public void Evaluate_FStringWithBraceInQuotedLiteral()
    {
        Assert.Equal("}", Eval("f\"{'}'}\"").AsString);
        Assert.Equal("a\"}", Eval("f\"{\"a\\\"}\"}\"").AsString);
        Assert.Equal("}", Eval("f\"{f\"{\"}\"}\"}\"").AsString);
    }

    [Fact]
    public void Evaluate_FStringWithFloat_IsError()
    {
        Assert.Contains("f-string", EvalError("f\"{1.5}\"").Message);
    }

    [Theory]
    [InlineData("[255]", KsValueKind.Bytes)]
    [InlineData("[]", KsValueKind.Bytes)]
    [InlineData("[0, 1]", KsValueKind.Bytes)]
    [InlineData("[256]", KsValueKind.Array)]
    [InlineData("[1, -1]", KsValueKind.Array)]
    [InlineData("['a', 'b']", KsValueKind.Array)]
    public void Evaluate_ArrayLiteral_ChoosesBytesOrArray(string source, KsValueKind expected)
    {
        Assert.Equal(expected, Eval(source).Kind);
    }

    [Fact]
    public void Evaluate_MixedArray_IsError()
    {
        Assert.Contains("mixed types", EvalError("[1, 'a']").Message);
    }

    [Fact]
    public void Evaluate_Index()
    {
        Assert.Equal((Int128)300, Eval("[1, 2, 300][2]").AsInt);
        Assert.Equal((Int128)2, Eval("[1, 2, 3][1]").AsInt);
        Assert.Contains("out of range", EvalError("[1, 2][5]").Message);
    }

    [Fact]
    public void Evaluate_Name_ResolvesThroughScope()
    {
        var scope = new TestScope { Names = { ["len"] = KsValue.FromInt(4) } };

        Assert.Equal((Int128)8, Eval("len * 2", scope).AsInt);
    }

    [Fact]
    public void Evaluate_UnknownName_ReportsNodePosition()
    {
        var e = EvalError("1 + foo");

        Assert.Equal((4, 3), (e.Position, e.Length));
        Assert.Contains("foo", e.Message);
    }

    [Fact]
    public void Evaluate_Null_PassesThroughButCannotBeUsed()
    {
        var scope = new TestScope { Names = { ["skipped"] = KsValue.Null } };

        Assert.Equal(KsValueKind.Null, Eval("skipped", scope).Kind);
        Assert.Equal((Int128)1, Eval("true ? 1 : skipped", scope).AsInt);
        Assert.Contains("null", EvalError("skipped + 1", scope).Message);
        Assert.Contains("null", EvalError("skipped == 0", scope).Message);
        Assert.Contains("null", EvalError("skipped.foo", scope).Message);
        Assert.Contains("null", EvalError("skipped ? 1 : 2", scope).Message);
    }

    [Fact]
    public void Evaluate_StructMembers()
    {
        var root = new FakeStruct("root");
        var header = new FakeStruct("header", staticSize: 8) { Parent = root, Io = new FakeStream(3, 100) };
        header.Members["size"] = KsValue.FromInt(10);
        var scope = new TestScope { Names = { ["hdr"] = KsValue.FromStruct(header) } };

        Assert.Equal((Int128)10, Eval("hdr.size", scope).AsInt);
        Assert.Same(root, Eval("hdr._root", scope).AsStruct);
        Assert.Same(root, Eval("hdr._parent", scope).AsStruct);
        Assert.Equal(KsValueKind.Stream, Eval("hdr._io", scope).Kind);
        Assert.Equal((Int128)8, Eval("hdr._sizeof", scope).AsInt);
        Assert.Contains("nope", EvalError("hdr.nope", scope).Message);
        Assert.Contains("fixed-size", EvalError("hdr._parent._sizeof", scope).Message);
    }

    [Fact]
    public void Evaluate_ParentOfRoot_IsNull()
    {
        var scope = new TestScope { Names = { ["r"] = KsValue.FromStruct(new FakeStruct("root")) } };

        Assert.Equal(KsValueKind.Null, Eval("r._parent", scope).Kind);
    }

    [Fact]
    public void Evaluate_EnumRef()
    {
        var media = new FakeEnum("media", (1, "cdrom"), (2, "dvd"));
        var other = new FakeEnum("other", (2, "dvd"));
        var scope = new TestScope { Enums = { ["media"] = media, ["other"] = other } };

        var value = Eval("media::dvd", scope);
        Assert.Equal(KsValueKind.Enum, value.Kind);
        Assert.Same(media, value.EnumType);
        Assert.Equal((Int128)2, value.EnumValue);
        Assert.True(Eval("media::dvd == media::dvd", scope).AsBool);
        Assert.False(Eval("media::dvd != media::dvd", scope).AsBool);
        Assert.Contains("nope", EvalError("media::nope", scope).Message);
        Assert.Contains("not found", EvalError("foo::bar", scope).Message);
        Assert.Contains("different enums", EvalError("media::dvd == other::dvd", scope).Message);
        EvalError("media::cdrom < media::dvd", scope);
    }

    [Fact]
    public void Evaluate_SizeOf()
    {
        var scope = new TestScope
        {
            Types = { ["hdr_type"] = new FakeType(64), ["var_type"] = new FakeType(null) },
        };

        Assert.Equal((Int128)4, Eval("sizeof<u4>", scope).AsInt);
        Assert.Equal((Int128)3, Eval("bitsizeof<b3>", scope).AsInt);
        Assert.Equal((Int128)8, Eval("sizeof<hdr_type>", scope).AsInt);
        Assert.Equal((Int128)64, Eval("bitsizeof<hdr_type>", scope).AsInt);
        Assert.Contains("whole number of bytes", EvalError("sizeof<b3>", scope).Message);
        Assert.Contains("fixed-size", EvalError("sizeof<var_type>", scope).Message);
        Assert.Contains("not found", EvalError("sizeof<unknown>", scope).Message);
    }

    [Fact]
    public void Evaluate_Cast()
    {
        var header = new FakeStruct("header");
        var scope = new TestScope
        {
            Names = { ["len"] = KsValue.FromInt(4), ["hdr"] = KsValue.FromStruct(header) },
            Types = { ["header"] = new FakeType(null, v => v.Kind == KsValueKind.Struct && v.AsStruct.TypeName == "header") },
        };

        Assert.Equal((Int128)4, Eval("len.as<u4>", scope).AsInt);
        Assert.Same(header, Eval("hdr.as<header>", scope).AsStruct);
        Assert.Contains("Cannot treat", EvalError("len.as<str>", scope).Message);
        Assert.Contains("Cannot treat", EvalError("len.as<header>", scope).Message);
        Assert.Contains("not found", EvalError("len.as<nope>", scope).Message);
    }

    [Fact]
    public void Evaluate_ErrorWithoutPosition_IsAttributedToEvaluatingNode()
    {
        var scope = new TestScope
        {
            Names = { ["x"] = KsValue.FromInt(0), ["big"] = KsValue.FromBytes(new ThrowingBytes()) },
        };

        var e = EvalError("x == 1 or big == [1]", scope);

        Assert.Equal((10, 10), (e.Position, e.Length));
        Assert.Contains("too large", e.Message);
    }

    [Fact]
    public void EvaluateBool_RequiresBool()
    {
        Assert.True(KsEvaluator.EvaluateBool(KsExpressionParser.Parse("1 < 2"), new TestScope()));
        Assert.Throws<KsExpressionException>(() => KsEvaluator.EvaluateBool(KsExpressionParser.Parse("1"), new TestScope()));
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("0x7FFFFFFFFFFFFFFF", long.MaxValue)]
    public void EvaluateNonNegativeLong_InRange(string source, long expected)
    {
        Assert.Equal(expected, KsEvaluator.EvaluateNonNegativeLong(KsExpressionParser.Parse(source), new TestScope()));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0x8000000000000000")]
    [InlineData("1.0")]
    [InlineData("'1'")]
    public void EvaluateNonNegativeLong_OutOfRange_IsError(string source)
    {
        Assert.Throws<KsExpressionException>(() =>
            KsEvaluator.EvaluateNonNegativeLong(KsExpressionParser.Parse(source), new TestScope()));
    }
}
