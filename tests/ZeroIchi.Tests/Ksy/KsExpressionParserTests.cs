using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsExpressionParserTests
{
    [Theory]
    [InlineData("1 + 2 * 3", "(+ 1 (* 2 3))")]
    [InlineData("a & 1 == 0", "(== (& a 1) 0)")]
    [InlineData("a | b ^ c & d", "(| a (^ b (& c d)))")]
    [InlineData("a << 1 + 2", "(<< a (+ 1 2))")]
    [InlineData("a - b - c", "(- (- a b) c)")]
    [InlineData("a == b and c != d or e", "(or (and (== a b) (!= c d)) e)")]
    [InlineData("not a == b", "(not (== a b))")]
    [InlineData("not not a", "(not (not a))")]
    [InlineData("a ? b : c ? d : e", "(? a b (? c d e))")]
    [InlineData("a or b ? c : d", "(? (or a b) c d)")]
    [InlineData("-a * b", "(* (- a) b)")]
    [InlineData("~a + 1", "(+ (~ a) 1)")]
    [InlineData("-a.b", "(- (. a b))")]
    [InlineData("(a + b) * c", "(* (+ a b) c)")]
    public void Parse_Precedence(string source, string expected)
    {
        Assert.Equal(expected, KsExprPrinter.Print(KsExpressionParser.Parse(source)));
    }

    [Theory]
    [InlineData("a.b.c(1, 2)[3]", "([] (call (. a b) c 1 2) 3)")]
    [InlineData("x.size", "(. x size)")]
    [InlineData("x.substring(0, 2)", "(call x substring 0 2)")]
    [InlineData("x.as<foo::bar>.baz", "(. (as x foo::bar) baz)")]
    [InlineData("x.as<u4>>2", "(> (as x u4) 2)")]
    [InlineData("sizeof<u4> + bitsizeof<b3>", "(+ (sizeof u4) (bitsizeof b3))")]
    [InlineData("media::cdrom", "media::cdrom")]
    [InlineData("a::b::c", "a::b::c")]
    [InlineData("'a' \"b\" 'c'", "\"abc\"")]
    [InlineData("[1, 2, 3]", "[1 2 3]")]
    [InlineData("[]", "[]")]
    [InlineData("true and false", "(and true false)")]
    [InlineData("1.5", "1.5")]
    [InlineData("f\"x={x + 1}!\"", "(f \"x=\" (+ x 1) \"!\")")]
    [InlineData("_parent._io.pos", "(. (. _parent _io) pos)")]
    public void Parse_Atoms_And_Postfix(string source, string expected)
    {
        Assert.Equal(expected, KsExprPrinter.Print(KsExpressionParser.Parse(source)));
    }

    [Fact]
    public void Parse_RecordsSourceSpans()
    {
        var expr = (KsBinary)KsExpressionParser.Parse("ab + cd");

        Assert.Equal((0, 7), (expr.Position, expr.Length));
        Assert.Equal((5, 2), (expr.Right.Position, expr.Right.Length));
    }

    [Fact]
    public void Parse_FStringExpression_HasAbsolutePosition()
    {
        var expr = (KsFString)KsExpressionParser.Parse("f\"x={yy}\"");

        Assert.Equal(5, expr.Parts[1].Position);
    }

    [Theory]
    [InlineData("a < b < c", 6, "chained")]
    [InlineData("a == b != c", 7, "chained")]
    [InlineData("(1 + 2", 6, "')'")]
    [InlineData("1 +", 3, "Unexpected end")]
    [InlineData("1 2", 2, "Unexpected token")]
    [InlineData("a.as<>", 5, "name")]
    [InlineData("a ? b", 5, "':'")]
    [InlineData("f\"{1 +}\"", 6, "Unexpected end")]
    public void Parse_Invalid_ReportsPositionAndMessage(string source, int position, string messagePart)
    {
        var e = Assert.Throws<KsExpressionException>(() => KsExpressionParser.Parse(source));

        Assert.Equal(position, e.Position);
        Assert.Contains(messagePart, e.Message);
    }

    [Theory]
    [InlineData(300, "(", ")")]
    [InlineData(300, "1+", "")]
    [InlineData(300, "-", "")]
    public void Parse_ExcessiveNesting_ReportsDepthError(int count, string prefix, string suffix)
    {
        var source = string.Concat(Enumerable.Repeat(prefix, count)) + "1" + string.Concat(Enumerable.Repeat(suffix, count));

        var e = Assert.Throws<KsExpressionException>(() => KsExpressionParser.Parse(source));

        Assert.Contains("too deeply", e.Message);
    }

    [Fact]
    public void Parse_NestedFStrings_ReportsDepthError()
    {
        var source = "1";
        for (var i = 0; i < 200; i++)
            source = "f\"{" + source + "}\"";

        var e = Assert.Throws<KsExpressionException>(() => KsExpressionParser.Parse(source));

        Assert.Contains("too deeply", e.Message);
    }

    [Fact]
    public void Parse_DeepAstFromParenthesizedPostfixChains_ReportsDepthError()
    {
        var source = "x";
        for (var i = 0; i < 20; i++)
            source = "(" + source + ")" + string.Concat(Enumerable.Repeat(".a", 20));

        var e = Assert.Throws<KsExpressionException>(() => KsExpressionParser.Parse(source));

        Assert.Contains("too deeply", e.Message);
    }

    [Fact]
    public void Parse_NestingWithinLimit_Succeeds()
    {
        var source = new string('(', 100) + "1" + new string(')', 100);

        Assert.Equal("1", KsExprPrinter.Print(KsExpressionParser.Parse(source)));
    }
}
