using ZeroIchi.Models;
using ZeroIchi.Models.Commands;

namespace ZeroIchi.Tests.Commands;

public sealed class UndoRedoManagerTests
{
    private sealed class FakeCommand : IEditCommand
    {
        public BinaryDocument Document => null!;
        public long CursorPositionBefore => 0;
        public long CursorPositionAfter => 0;
        public long RetainedLength => 0;
        public void Execute() { }
        public void Undo() { }
        public byte[]? CaptureRetainedBytes() => null;
        public void CommitSave(byte[]? retainedBytes) { }
    }

    [Fact]
    public void UndoCommandsNewestFirst_EnumeratesFromLatest()
    {
        var manager = new UndoRedoManager();
        var first = new FakeCommand();
        var second = new FakeCommand();
        var third = new FakeCommand();
        manager.ExecuteCommand(first);
        manager.ExecuteCommand(second);
        manager.ExecuteCommand(third);

        Assert.Equal(new IEditCommand[] { third, second, first }, manager.UndoCommandsNewestFirst);
    }

    [Fact]
    public void DiscardUndoThrough_RemovesCommandAndOlder()
    {
        var manager = new UndoRedoManager();
        var first = new FakeCommand();
        var second = new FakeCommand();
        var third = new FakeCommand();
        manager.ExecuteCommand(first);
        manager.ExecuteCommand(second);
        manager.ExecuteCommand(third);

        manager.DiscardUndoThrough(second);

        Assert.Same(third, Assert.Single(manager.UndoCommandsNewestFirst));
        Assert.Same(third, manager.Undo());
        Assert.False(manager.CanUndo);
    }

    [Fact]
    public void DiscardUndoThrough_KeepsRedoSide()
    {
        var manager = new UndoRedoManager();
        var first = new FakeCommand();
        var second = new FakeCommand();
        manager.ExecuteCommand(first);
        manager.ExecuteCommand(second);
        manager.Undo();

        manager.DiscardUndoThrough(first);

        Assert.False(manager.CanUndo);
        Assert.Same(second, manager.Redo());
    }

    [Fact]
    public void DiscardUndoThrough_CommandNotInUndoSide_Throws()
    {
        var manager = new UndoRedoManager();
        manager.ExecuteCommand(new FakeCommand());

        Assert.Throws<ArgumentException>(() => manager.DiscardUndoThrough(new FakeCommand()));
    }

    [Fact]
    public void AllCommands_IncludesUndoAndRedoSides()
    {
        var manager = new UndoRedoManager();
        var first = new FakeCommand();
        var second = new FakeCommand();
        manager.ExecuteCommand(first);
        manager.ExecuteCommand(second);
        manager.Undo();

        Assert.Equal(2, manager.AllCommands.Count());
        Assert.Contains(first, manager.AllCommands);
        Assert.Contains(second, manager.AllCommands);
    }
}
