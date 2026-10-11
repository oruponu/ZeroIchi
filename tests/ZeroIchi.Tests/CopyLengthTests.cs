using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class CopyLengthTests
{
    private const long Huge = 3L * 1024 * 1024 * 1024;

    [Theory]
    [InlineData(100L, 0L, 1000L, 100L)]
    [InlineData(101L, 900L, 1000L, 100L)]
    [InlineData(10L, 2000L, 1000L, 0L)]
    [InlineData(0L, 0L, 1000L, 0L)]
    [InlineData(Huge, 0L, Huge, Huge)]
    public void EffectiveCopyLength_ClampsToRemainingData(
        long selectionLength, long selectionStart, long bufferLength, long expected)
    {
        Assert.Equal(expected, CopyRange.EffectiveLength(selectionLength, selectionStart, bufferLength));
    }

    [Theory]
    [InlineData(100L, 0L, 1000L, true)]
    [InlineData(0L, 0L, 1000L, true)]
    [InlineData(Huge, 0L, Huge, false)]
    [InlineData(Huge, Huge - 1000, Huge, true)]
    public void CanCopySelection_IsFalse_WhenEffectiveLengthExceedsArrayLimit(
        long selectionLength, long selectionStart, long bufferLength, bool expected)
    {
        Assert.Equal(expected, CopyRange.CanCopy(selectionLength, selectionStart, bufferLength));
    }

    [Fact]
    public void CanCopySelection_AllowsExactlyMaxArrayLength()
    {
        Assert.True(CopyRange.CanCopy(Array.MaxLength, 0, Huge));
        Assert.False(CopyRange.CanCopy(Array.MaxLength + 1L, 0, Huge));
    }
}
