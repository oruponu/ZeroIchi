using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests.Commands;

public sealed class EditCommandOffsetTests
{
    [Fact]
    public void WriteByteCommand_KeepsCursorPositionsBeyondIntRange()
    {
        const long before = 0x8000_0000L;
        const long after = 0x8000_0001L;
        var document = BinaryDocument.CreateNew();

        var command = new WriteByteCommand(document, before, 0xFF, before, after);

        Assert.Equal(before, command.CursorPositionBefore);
        Assert.Equal(after, command.CursorPositionAfter);
    }

    [Fact]
    public void DeleteBytesCommand_KeepsCursorPositionsBeyondIntRange()
    {
        const long before = 0x1_0000_0000L;
        var document = BinaryDocument.CreateNew();

        var command = new DeleteBytesCommand(document, before, 0x4000_0000L, before, before);

        Assert.Equal(before, command.CursorPositionBefore);
        Assert.Equal(before, command.CursorPositionAfter);
    }
}
