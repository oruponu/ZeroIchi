using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Tests.Ksy.Yaml;

public sealed class YamlScalarValueTests
{
    private static YamlScalarValue Plain(string text) => YamlScalarValue.Resolve(text, YamlScalarStyle.Plain);

    [Theory]
    [InlineData("")]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    public void Resolve_Null(string text)
    {
        Assert.Equal(YamlScalarKind.Null, Plain(text).Kind);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    [InlineData("off", false)]
    [InlineData("OFF", false)]
    public void Resolve_Bool(string text, bool expected)
    {
        var value = Plain(text);

        Assert.Equal(YamlScalarKind.Bool, value.Kind);
        Assert.Equal(expected, value.Bool);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("n")]
    [InlineData("tRUE")]
    [InlineData("yES")]
    [InlineData("08")]
    [InlineData("0x")]
    [InlineData("u4le")]
    [InlineData("1 + 2")]
    public void Resolve_OtherPlainText_IsString(string text)
    {
        var value = Plain(text);

        Assert.Equal(YamlScalarKind.String, value.Kind);
        Assert.Equal(text, value.Text);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("42", "42")]
    [InlineData("-42", "-42")]
    [InlineData("+7", "7")]
    [InlineData("1_000", "1000")]
    [InlineData("0x10", "16")]
    [InlineData("0xFF_FF", "65535")]
    [InlineData("0b101", "5")]
    [InlineData("017", "15")]
    [InlineData("-0x10", "-16")]
    [InlineData("1:30", "90")]
    [InlineData("0xFFFFFFFFFFFFFFFF", "18446744073709551615")]
    public void Resolve_Int(string text, string expected)
    {
        var value = Plain(text);

        Assert.Equal(YamlScalarKind.Int, value.Kind);
        Assert.Equal(Int128.Parse(expected), value.Int);
        Assert.Equal(expected, value.ToExpressionSource());
    }

    [Fact]
    public void Resolve_IntBeyondInt128_IsString()
    {
        var value = Plain("0x1_0000_0000_0000_0000_0000_0000_0000_0000");

        Assert.Equal(YamlScalarKind.String, value.Kind);
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("-2.", -2.0)]
    [InlineData(".5", 0.5)]
    [InlineData("1e3", 1000.0)]
    [InlineData("1_0.2_5", 10.25)]
    public void Resolve_Float(string text, double expected)
    {
        var value = Plain(text);

        Assert.Equal(YamlScalarKind.Float, value.Kind);
        Assert.Equal(expected, value.Float);
    }

    [Fact]
    public void Resolve_SpecialFloats()
    {
        Assert.Equal(double.PositiveInfinity, Plain(".inf").Float);
        Assert.Equal(double.NegativeInfinity, Plain("-.Inf").Float);
        Assert.True(double.IsNaN(Plain(".NaN").Float));
    }

    [Theory]
    [InlineData(YamlScalarStyle.SingleQuoted)]
    [InlineData(YamlScalarStyle.DoubleQuoted)]
    [InlineData(YamlScalarStyle.Literal)]
    [InlineData(YamlScalarStyle.Folded)]
    public void Resolve_NonPlain_IsAlwaysString(YamlScalarStyle style)
    {
        var value = YamlScalarValue.Resolve("0x10", style);

        Assert.Equal(YamlScalarKind.String, value.Kind);
        Assert.Equal("0x10", value.ToExpressionSource());
    }

    [Theory]
    [InlineData("yes", "true")]
    [InlineData("off", "false")]
    [InlineData("1.0", "1.0")]
    [InlineData("1e3", "1000.0")]
    [InlineData("2.5", "2.5")]
    [InlineData(".inf", "Infinity")]
    [InlineData("_parent.len + 2", "_parent.len + 2")]
    public void ToExpressionSource_NormalizesPlainScalars(string text, string expected)
    {
        Assert.Equal(expected, Plain(text).ToExpressionSource());
    }
}
