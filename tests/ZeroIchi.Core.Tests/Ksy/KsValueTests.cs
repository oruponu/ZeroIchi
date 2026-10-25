using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsValueTests
{
    [Fact]
    public void Default_IsNull()
    {
        Assert.Equal(KsValueKind.Null, default(KsValue).Kind);
        Assert.Equal(KsValueKind.Null, KsValue.Null.Kind);
    }

    [Fact]
    public void FromBool_RoundTrips()
    {
        Assert.True(KsValue.FromBool(true).AsBool);
        Assert.False(KsValue.FromBool(false).AsBool);
    }

    [Fact]
    public void FromBytes_WrapsArray()
    {
        var value = KsValue.FromBytes([1, 2, 3]);

        Assert.Equal(KsValueKind.Bytes, value.Kind);
        Assert.Equal(3, value.AsBytes.Length);
        Assert.Equal(new byte[] { 1, 2, 3 }, value.AsBytes.ToArray());
    }

    [Fact]
    public void Accessor_OfWrongKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => KsValue.FromInt(1).AsString);
    }

    [Fact]
    public void ToString_FormatsByKind()
    {
        Assert.Equal("-5", KsValue.FromInt(-5).ToString());
        Assert.Equal("1.5", KsValue.FromFloat(1.5).ToString());
        Assert.Equal("true", KsValue.FromBool(true).ToString());
        Assert.Equal("null", KsValue.Null.ToString());
    }
}
