using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

public sealed class SaveProgressTests
{
    [Theory]
    [InlineData(0, 200, "保存中 0%")]
    [InlineData(101, 200, "保存中 50%")]
    [InlineData(200, 200, "保存中 100%")]
    [InlineData(0, 0, "保存中 100%")]
    [InlineData(4_294_967_296, 4_294_967_296, "保存中 100%")]
    public void FormatText_ReportsThePercentOfBytesWritten(long written, long total, string expected)
        => Assert.Equal(expected, SaveProgress.FormatText(written, total));
}
