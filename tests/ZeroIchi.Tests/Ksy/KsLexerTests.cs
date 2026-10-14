using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsLexerTests
{
    private static KsToken Single(string source)
    {
        var tokens = KsLexer.Tokenize(source);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(KsTokenKind.End, tokens[1].Kind);
        return tokens[0];
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("0x1F", "31")]
    [InlineData("0XfF", "255")]
    [InlineData("0b101", "5")]
    [InlineData("0o17", "15")]
    [InlineData("1_000_000", "1000000")]
    [InlineData("0xFFFF_FFFF_FFFF_FFFF", "18446744073709551615")]
    [InlineData("170141183460469231731687303715884105727", "170141183460469231731687303715884105727")]
    public void Tokenize_Integer(string source, string expected)
    {
        var token = Single(source);

        Assert.Equal(KsTokenKind.Integer, token.Kind);
        Assert.Equal(Int128.Parse(expected), token.IntValue);
        Assert.Equal(source.Length, token.Length);
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("1e3", 1000.0)]
    [InlineData("2.5e-1", 0.25)]
    [InlineData("4.", 4.0)]
    [InlineData(".5", 0.5)]
    [InlineData(".4e+2", 40.0)]
    [InlineData("4. ", 4.0)]
    public void Tokenize_Float(string source, double expected)
    {
        var token = Single(source);

        Assert.Equal(KsTokenKind.Float, token.Kind);
        Assert.Equal(expected, token.FloatValue);
    }

    [Theory]
    [InlineData("4.to_s")]
    [InlineData("4. to_s")]
    public void Tokenize_IntegerFollowedByMethod_IsIntegerThenDot(string source)
    {
        var tokens = KsLexer.Tokenize(source);

        Assert.Equal(
            new[] { KsTokenKind.Integer, KsTokenKind.Dot, KsTokenKind.Identifier, KsTokenKind.End },
            tokens.Select(t => t.Kind));
        Assert.Equal("to_s", tokens[2].Text);
    }

    [Theory]
    [InlineData("'a\\nb'", "a\\nb")]
    [InlineData("\"a\\nb\"", "a\nb")]
    [InlineData("\"\\101\\u3042\"", "Aあ")]
    [InlineData("\"\\e\\\"\\'\\\\\"", "\u001B\"'\\")]
    [InlineData("'it\"s'", "it\"s")]
    public void Tokenize_String(string source, string expected)
    {
        var token = Single(source);

        Assert.Equal(KsTokenKind.String, token.Kind);
        Assert.Equal(expected, token.Text);
    }

    [Fact]
    public void Tokenize_Identifier()
    {
        var token = Single("_parent_2");

        Assert.Equal(KsTokenKind.Identifier, token.Kind);
        Assert.Equal("_parent_2", token.Text);
    }

    [Fact]
    public void Tokenize_Symbols()
    {
        var tokens = KsLexer.Tokenize("<< >> <= >= == != :: < > ( ) [ ] , . ? : + - * / % ~ & | ^");

        Assert.Equal(
            new[]
            {
                KsTokenKind.Shl, KsTokenKind.Shr, KsTokenKind.Le, KsTokenKind.Ge, KsTokenKind.Eq,
                KsTokenKind.Ne, KsTokenKind.DoubleColon, KsTokenKind.Lt, KsTokenKind.Gt,
                KsTokenKind.LParen, KsTokenKind.RParen, KsTokenKind.LBracket, KsTokenKind.RBracket,
                KsTokenKind.Comma, KsTokenKind.Dot, KsTokenKind.Question, KsTokenKind.Colon,
                KsTokenKind.Plus, KsTokenKind.Minus, KsTokenKind.Star, KsTokenKind.Slash,
                KsTokenKind.Percent, KsTokenKind.Tilde, KsTokenKind.Amp, KsTokenKind.Pipe,
                KsTokenKind.Caret, KsTokenKind.End,
            },
            tokens.Select(t => t.Kind));
    }

    [Fact]
    public void Tokenize_FString_SplitsTextAndExpressions()
    {
        var token = Single("f\"a{b}c\"");

        Assert.Equal(KsTokenKind.FString, token.Kind);
        Assert.Equal(
            new[] { new KsFStringPart(false, "a", 2), new KsFStringPart(true, "b", 4), new KsFStringPart(false, "c", 6) },
            token.FStringParts);
    }

    [Fact]
    public void Tokenize_FStringWithBraceInQuotedLiteral_KeepsWholeExpression()
    {
        var token = Single("f\"{'}'}\"");

        Assert.Equal(new[] { new KsFStringPart(true, "'}'", 3) }, token.FStringParts);
    }

    [Fact]
    public void Tokenize_FStringWithEscapedQuoteInDoubleQuotedLiteral_KeepsWholeExpression()
    {
        var token = Single("f\"{\"a\\\"}\"}\"");

        Assert.Equal(new[] { new KsFStringPart(true, "\"a\\\"}\"", 3) }, token.FStringParts);
    }

    [Fact]
    public void Tokenize_NestedFStringWithQuotedBrace_KeepsWholeExpression()
    {
        var token = Single("f\"{f\"{\"}\"}\"}\"");

        Assert.Equal(new[] { new KsFStringPart(true, "f\"{\"}\"}\"", 3) }, token.FStringParts);
    }

    [Fact]
    public void Tokenize_DeeplyNestedFStrings_ReportsDepthError()
    {
        var source = "1";
        for (var i = 0; i < 200; i++)
            source = "f\"{" + source + "}\"";

        var e = Assert.Throws<KsExpressionException>(() => KsLexer.Tokenize(source));

        Assert.Contains("too deeply", e.Message);
    }

    [Fact]
    public void Tokenize_WithOffset_ShiftsPositions()
    {
        var tokens = KsLexer.Tokenize("a + b", 10);

        Assert.Equal(new[] { 10, 12, 14, 15 }, tokens.Select(t => t.Position));
    }

    [Theory]
    [InlineData("'abc", 0)]
    [InlineData("a @ b", 2)]
    [InlineData("\"\\q\"", 1)]
    [InlineData("0x", 0)]
    [InlineData("0x1_0000_0000_0000_0000_0000_0000_0000_0000", 0)]
    [InlineData("f\"{1\"", 4)]
    [InlineData("f\"{1", 2)]
    public void Tokenize_Invalid_ReportsPosition(string source, int position)
    {
        var e = Assert.Throws<KsExpressionException>(() => KsLexer.Tokenize(source));

        Assert.Equal(position, e.Position);
    }

    [Fact]
    public void Tokenize_InvalidWithOffset_ShiftsErrorPosition()
    {
        var e = Assert.Throws<KsExpressionException>(() => KsLexer.Tokenize("a @", 5));

        Assert.Equal(7, e.Position);
    }
}
