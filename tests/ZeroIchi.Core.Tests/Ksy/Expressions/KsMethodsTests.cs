using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy.Expressions;

public sealed class KsMethodsTests
{
    static KsMethodsTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static KsValue Eval(string source, TestScope? scope = null) =>
        KsEvaluator.Evaluate(KsExpressionParser.Parse(source), scope ?? new TestScope());

    private static KsExpressionException EvalError(string source, TestScope? scope = null) =>
        Assert.Throws<KsExpressionException>(() => Eval(source, scope));

    [Fact]
    public void Call_NumberAndBoolConversions()
    {
        Assert.Equal("123", Eval("123.to_s").AsString);
        Assert.Equal("-5", Eval("(-5).to_s").AsString);
        Assert.Equal((Int128)1, Eval("1.9.to_i").AsInt);
        Assert.Equal((Int128)(-1), Eval("(-1.9).to_i").AsInt);
        Assert.Equal((Int128)1, Eval("true.to_i").AsInt);
        Assert.Equal((Int128)0, Eval("false.to_i").AsInt);
    }

    [Fact]
    public void Call_FloatToI_AtInt128Bounds()
    {
        Assert.Equal((Int128)1.701e38, Eval("1.701e38.to_i").AsInt);
        Assert.Equal(Int128.MinValue, Eval("(-1.7014118346046923e38).to_i").AsInt);
        Assert.Contains("cannot be converted to an integer", EvalError("1.7014118346046923e38.to_i").Message);
        Assert.Contains("cannot be converted to an integer", EvalError("1.8e38.to_i").Message);
        Assert.Contains("cannot be converted to an integer", EvalError("(-1.8e38).to_i").Message);
    }

    [Fact]
    public void Call_StringToI_AtInt128Bounds()
    {
        Assert.Equal(Int128.MaxValue, Eval("'170141183460469231731687303715884105727'.to_i").AsInt);
        Assert.Equal(Int128.MinValue, Eval("'-170141183460469231731687303715884105728'.to_i").AsInt);
        Assert.Contains("out of integer range", EvalError("'170141183460469231731687303715884105728'.to_i").Message);
        Assert.Contains("out of integer range", EvalError("'-170141183460469231731687303715884105729'.to_i").Message);
    }

    [Fact]
    public void Call_EnumToI()
    {
        var scope = new TestScope { Enums = { ["media"] = new FakeEnum("media", (7, "dvd")) } };

        Assert.Equal((Int128)7, Eval("media::dvd.to_i", scope).AsInt);
    }

    [Fact]
    public void Call_StringMethods()
    {
        Assert.Equal("cba", Eval("'abc'.reverse").AsString);
        Assert.Equal((Int128)123, Eval("'123'.to_i").AsInt);
        Assert.Equal((Int128)(-12), Eval("'-12'.to_i").AsInt);
        Assert.Equal((Int128)255, Eval("'ff'.to_i(16)").AsInt);
        Assert.Equal((Int128)5, Eval("'101'.to_i(2)").AsInt);
        Assert.Equal("el", Eval("'hello'.substring(1, 3)").AsString);
        Assert.Equal("", Eval("'hello'.substring(2, 2)").AsString);
        Assert.Equal(new byte[] { 0x41, 0x42 }, Eval("'AB'.to_b('ASCII')").AsBytes.ToArray());
    }

    [Fact]
    public void Call_StringMethods_CountRunes()
    {
        Assert.Equal((Int128)3, Eval("'あいう'.length").AsInt);
        Assert.Equal((Int128)1, Eval("'😀'.length").AsInt);
        Assert.Equal("😀", Eval("'a😀b'.substring(1, 2)").AsString);
        Assert.Equal("b😀a", Eval("'a😀b'.reverse").AsString);
    }

    [Theory]
    [InlineData("'12x'.to_i")]
    [InlineData("''.to_i")]
    [InlineData("'1'.to_i(1)")]
    [InlineData("'1'.to_i(37)")]
    [InlineData("'abc'.substring(2, 1)")]
    [InlineData("'abc'.substring(0, 4)")]
    [InlineData("'a'.to_b('NOPE')")]
    [InlineData("'a'.to_b(1)")]
    public void Call_StringMethods_InvalidArguments_AreErrors(string source)
    {
        EvalError(source);
    }

    [Fact]
    public void Call_BytesMethods()
    {
        Assert.Equal((Int128)3, Eval("[1, 2, 3].length").AsInt);
        Assert.Equal((Int128)3, Eval("[1, 2, 3].size").AsInt);
        Assert.Equal((Int128)1, Eval("[1, 2, 3].first").AsInt);
        Assert.Equal((Int128)3, Eval("[1, 2, 3].last").AsInt);
        Assert.Equal((Int128)1, Eval("[5, 1, 9].min").AsInt);
        Assert.Equal((Int128)255, Eval("[5, 255, 9].max").AsInt);
        Assert.Equal("AB", Eval("[0x41, 0x42].to_s('ASCII')").AsString);
        Assert.Equal("あ", Eval("[0x82, 0xA0].to_s('Shift_JIS')").AsString);
        Assert.Equal((Int128)1, Eval("[1, 2, 3].index_of(2)").AsInt);
        Assert.Equal((Int128)(-1), Eval("[1, 2, 3].index_of(9)").AsInt);
        Assert.Equal((Int128)(-1), Eval("[1, 2, 3].index_of(300)").AsInt);
    }

    [Theory]
    [InlineData("[].first")]
    [InlineData("[].last")]
    [InlineData("[].min")]
    [InlineData("[1].to_s('NOPE')")]
    [InlineData("[1].index_of('a')")]
    public void Call_BytesMethods_InvalidUse_IsError(string source)
    {
        EvalError(source);
    }

    [Fact]
    public void Call_ArrayMethods()
    {
        Assert.Equal((Int128)1, Eval("[1, 2, 300].first").AsInt);
        Assert.Equal((Int128)300, Eval("[1, 2, 300].last").AsInt);
        Assert.Equal((Int128)3, Eval("[1, 2, 300].size").AsInt);
        Assert.Equal((Int128)(-1), Eval("[3, -1, 300].min").AsInt);
        Assert.Equal((Int128)300, Eval("[3, -1, 300].max").AsInt);
        Assert.Equal("a", Eval("['b', 'a', 'c'].min").AsString);
        Assert.Equal(2.5, Eval("[1.5, 2.5].max").AsFloat);
    }

    [Fact]
    public void Call_StreamMethods()
    {
        var scope = new TestScope { Names = { ["io"] = KsValue.FromStream(new FakeStream(5, 10)) } };

        Assert.Equal((Int128)5, Eval("io.pos", scope).AsInt);
        Assert.Equal((Int128)10, Eval("io.size", scope).AsInt);
        Assert.False(Eval("io.eof", scope).AsBool);
    }

    [Fact]
    public void Call_StructIoChain()
    {
        var header = new FakeStruct("header") { Io = new FakeStream(10, 10) };
        var scope = new TestScope { Names = { ["hdr"] = KsValue.FromStruct(header) } };

        Assert.True(Eval("hdr._io.eof", scope).AsBool);
        Assert.Equal((Int128)10, Eval("hdr._io.size", scope).AsInt);
    }

    [Fact]
    public void Call_UnknownMethod_IsError()
    {
        var e = EvalError("1.length");

        Assert.Contains("length", e.Message);
        Assert.Equal((0, 8), (e.Position, e.Length));
    }

    [Fact]
    public void Call_OnNull_IsError()
    {
        var scope = new TestScope { Names = { ["skipped"] = KsValue.Null } };

        Assert.Contains("null", EvalError("skipped.to_s", scope).Message);
    }
}
