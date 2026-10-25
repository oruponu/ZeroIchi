using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy.Expressions;

public sealed class KsEncodingsTests
{
    static KsEncodingsTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Theory]
    [InlineData("ASCII")]
    [InlineData("UTF-8")]
    [InlineData("utf-8")]
    [InlineData("UTF-16LE")]
    [InlineData("UTF-16BE")]
    [InlineData("UTF-32LE")]
    [InlineData("UTF-32BE")]
    [InlineData("ISO-8859-1")]
    [InlineData("windows-1252")]
    [InlineData("IBM437")]
    [InlineData("IBM866")]
    [InlineData("Shift_JIS")]
    [InlineData("Big5")]
    [InlineData("EUC-KR")]
    public void TryGet_SupportedName_Succeeds(string name)
    {
        Assert.True(KsEncodings.TryGet(name, out var encoding));
        Assert.NotNull(encoding);
    }

    [Fact]
    public void TryGet_UnknownName_Fails()
    {
        Assert.False(KsEncodings.TryGet("NOPE", out _));
        Assert.False(KsEncodings.TryGet("cp932", out _));
    }

    [Fact]
    public void CanonicalNames_Has35Entries_AndAllAreAvailable()
    {
        Assert.Equal(35, KsEncodings.CanonicalNames.Count);
        foreach (var name in KsEncodings.CanonicalNames)
            Assert.True(KsEncodings.TryGet(name, out _), name);
    }

    [Theory]
    [InlineData("ISO-8859-10", 0xA1, "Ą")]
    [InlineData("ISO-8859-14", 0xA1, "Ḃ")]
    [InlineData("ISO-8859-16", 0xA4, "€")]
    [InlineData("ISO-8859-16", 0x41, "A")]
    public void Iso8859WithoutDotNetProvider_Decodes(string name, byte value, string expected)
    {
        Assert.True(KsEncodings.TryGet(name, out var encoding));
        Assert.Equal(expected, encoding.GetString([value]));
    }

    [Fact]
    public void Iso8859WithoutDotNetProvider_EncodesAndReplacesUnmappable()
    {
        Assert.True(KsEncodings.TryGet("ISO-8859-16", out var encoding));
        Assert.Equal(new byte[] { 0x41, 0xA1, 0xA4, 0x3F }, encoding.GetBytes("AĄ€あ"));
    }

    [Fact]
    public void Utf16Be_DecodesBigEndian()
    {
        Assert.True(KsEncodings.TryGet("UTF-16BE", out var encoding));
        Assert.Equal("A", encoding.GetString([0x00, 0x41]));
    }

    [Fact]
    public void ShiftJis_Decodes()
    {
        Assert.True(KsEncodings.TryGet("Shift_JIS", out var encoding));
        Assert.Equal("あ", encoding.GetString([0x82, 0xA0]));
    }
}
