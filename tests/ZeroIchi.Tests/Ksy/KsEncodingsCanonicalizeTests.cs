using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsEncodingsCanonicalizeTests
{
    [Theory]
    [InlineData("UTF-8", "UTF-8")]
    [InlineData("utf-8", "UTF-8")]
    [InlineData("utf8", "UTF-8")]
    [InlineData("UTF_16LE", "UTF-16LE")]
    [InlineData("ascii", "ASCII")]
    [InlineData("US-ASCII", "ASCII")]
    [InlineData("iso8859-1", "ISO-8859-1")]
    [InlineData("Windows-1252", "windows-1252")]
    [InlineData("cp1251", "windows-1251")]
    [InlineData("cp437", "IBM437")]
    [InlineData("437", "IBM437")]
    [InlineData("cp866", "IBM866")]
    [InlineData("shift-jis", "Shift_JIS")]
    [InlineData("SJIS", "Shift_JIS")]
    [InlineData("euckr", "EUC-KR")]
    public void Canonicalize_KnownNamesAndAliases(string name, string expected)
    {
        Assert.Equal(expected, KsEncodings.Canonicalize(name));
    }

    [Theory]
    [InlineData("klingon")]
    [InlineData("cp1259")]
    [InlineData("cp932")]
    [InlineData("")]
    public void Canonicalize_UnknownName_IsNull(string name)
    {
        Assert.Null(KsEncodings.Canonicalize(name));
    }

    [Fact]
    public void Canonicalize_EveryCanonicalNameMapsToItself()
    {
        foreach (var name in KsEncodings.CanonicalNames)
            Assert.Equal(name, KsEncodings.Canonicalize(name));
    }
}
