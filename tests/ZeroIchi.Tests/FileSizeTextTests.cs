using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class FileSizeTextTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1024L * 1024, "1.0 MB")]
    [InlineData(1024L * 1024 * 1024, "1.0 GB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3.0 GB")]
    public void Format_SwitchesUnitAtEach1024(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeText.Format(bytes));
    }
}
