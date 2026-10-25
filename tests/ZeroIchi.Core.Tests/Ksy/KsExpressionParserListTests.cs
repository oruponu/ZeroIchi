using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsExpressionParserListTests
{
    [Theory]
    [InlineData("[1, 2,]", "[1 2]")]
    [InlineData("[1,]", "[1]")]
    [InlineData("[\n  [1, 2],\n  [3, 4],\n]", "[[1 2] [3 4]]")]
    public void Parse_ArrayLiteralWithTrailingComma(string source, string expected)
    {
        Assert.Equal(expected, KsExprPrinter.Print(KsExpressionParser.Parse(source)));
    }

    [Theory]
    [InlineData("[,]", 1)]
    [InlineData("[1,,]", 3)]
    [InlineData("a.f(1,)", 6)]
    public void Parse_MisplacedComma_IsError(string source, int position)
    {
        var e = Assert.Throws<KsExpressionException>(() => KsExpressionParser.Parse(source));

        Assert.Equal(position, e.Position);
        Assert.Contains("Value expected", e.Message);
    }
}
