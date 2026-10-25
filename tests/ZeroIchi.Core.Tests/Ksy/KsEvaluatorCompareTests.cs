using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsEvaluatorCompareTests
{
    private static readonly KsExpr Node = new KsName("x", 3, 1);

    [Fact]
    public void ValuesEqual_ComparesLikeTheEqualityOperator()
    {
        var kind = new FakeEnum("kind", (1, "a"));

        Assert.True(KsEvaluator.ValuesEqual(Node, KsValue.FromInt(2), KsValue.FromFloat(2.0)));
        Assert.True(KsEvaluator.ValuesEqual(Node, KsValue.FromString("a"), KsValue.FromString("a")));
        Assert.True(KsEvaluator.ValuesEqual(Node, KsValue.FromEnum(kind, 1), KsValue.FromEnum(kind, 1)));
        Assert.False(KsEvaluator.ValuesEqual(Node, KsValue.FromBytes([1]), KsValue.FromBytes([2])));
    }

    [Fact]
    public void ValuesEqual_MismatchedKindsAndNullAreErrorsAtTheNode()
    {
        var mismatch = Assert.Throws<KsExpressionException>(
            () => KsEvaluator.ValuesEqual(Node, KsValue.FromInt(1), KsValue.FromString("1")));
        var nullValue = Assert.Throws<KsExpressionException>(
            () => KsEvaluator.ValuesEqual(Node, KsValue.Null, KsValue.FromInt(1)));

        Assert.Equal("Operator == cannot be applied to integer and string", mismatch.Message);
        Assert.Equal((3, 1), (mismatch.Position, mismatch.Length));
        Assert.Equal("Value is null", nullValue.Message);
    }

    [Fact]
    public void CompareValues_OrdersNumbersStringsAndBytes()
    {
        Assert.True(KsEvaluator.CompareValues(Node, KsValue.FromInt(1), KsValue.FromFloat(1.5)) < 0);
        Assert.True(KsEvaluator.CompareValues(Node, KsValue.FromString("b"), KsValue.FromString("a")) > 0);
        Assert.True(KsEvaluator.CompareValues(Node, KsValue.FromBytes([0xFF]), KsValue.FromBytes([0x01, 0x00])) > 0);
        Assert.Throws<KsExpressionException>(() => KsEvaluator.CompareValues(Node, KsValue.FromBool(true), KsValue.FromBool(false)));
    }
}
