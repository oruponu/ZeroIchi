using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy.Expressions;

public sealed class KsPrimitiveTypesTests
{
    private static KsTypeRef Type(string name) => new(name.Split("::"));

    [Theory]
    [InlineData("u1", 8L)]
    [InlineData("s2", 16L)]
    [InlineData("u4le", 32L)]
    [InlineData("s8be", 64L)]
    [InlineData("f4", 32L)]
    [InlineData("f8le", 64L)]
    [InlineData("b1", 1L)]
    [InlineData("b12be", 12L)]
    [InlineData("b64", 64L)]
    [InlineData("b65", null)]
    [InlineData("b0", null)]
    [InlineData("str", null)]
    [InlineData("table", null)]
    [InlineData("a::u4", null)]
    public void SizeInBits(string name, long? expected)
    {
        Assert.Equal(expected, KsPrimitiveTypes.SizeInBits(Type(name)));
    }

    [Theory]
    [InlineData("u2", KsValueKind.Int)]
    [InlineData("s4le", KsValueKind.Int)]
    [InlineData("b3", KsValueKind.Int)]
    [InlineData("b1", KsValueKind.Bool)]
    [InlineData("f8", KsValueKind.Float)]
    [InlineData("str", KsValueKind.String)]
    [InlineData("strz", KsValueKind.String)]
    [InlineData("bytes", KsValueKind.Bytes)]
    [InlineData("bool", KsValueKind.Bool)]
    [InlineData("header", null)]
    public void KindOf(string name, KsValueKind? expected)
    {
        Assert.Equal(expected, KsPrimitiveTypes.KindOf(Type(name)));
    }
}
