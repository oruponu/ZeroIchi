using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests;

public sealed class AddBufferTests
{
    [Fact]
    public void Append_PastTheLimit_ThrowsWithoutChangingTheContent()
    {
        var buffer = new AddBuffer(maxLength: 10);
        buffer.Append([1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<OutOfMemoryException>(() => buffer.Append([9, 9, 9]));

        Assert.Equal(8, buffer.Length);
        var content = new byte[8];
        buffer.CopyTo(0, content);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], content);
    }

    [Fact]
    public void AppendByte_AtTheLimit_ThrowsWithoutChangingTheLength()
    {
        var buffer = new AddBuffer(maxLength: 2);
        buffer.Append(1);
        buffer.Append(2);

        Assert.Throws<OutOfMemoryException>(() => buffer.Append(3));

        Assert.Equal(2, buffer.Length);
    }
}
