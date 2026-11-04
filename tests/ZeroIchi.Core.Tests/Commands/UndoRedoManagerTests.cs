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

    private sealed class RecordingCommand(bool throwOnExecute = false) : IEditCommand
    {
        public bool IsApplied { get; private set; }
        public BinaryDocument Document => null!;
        public long CursorPositionBefore => 0;
        public long CursorPositionAfter => 0;
        public long RetainedLength => 0;

        public void Execute()
        {
            if (throwOnExecute) throw new InvalidOperationException("Execute failed.");
            IsApplied = true;
        }

        public void Undo() => IsApplied = false;
        public byte[]? CaptureRetainedBytes() => null;
        public void CommitSave(byte[]? retainedBytes) { }
    }

    [Fact]
    public void ReplaceLast_SwapsTopCommand_LeavingOneUndoStep()
    {
        var manager = new UndoRedoManager();
        var expected = new RecordingCommand();
        var replacement = new RecordingCommand();
        manager.ExecuteCommand(expected);

        manager.ReplaceLast(expected, replacement);

        Assert.False(expected.IsApplied);
        Assert.True(replacement.IsApplied);
        Assert.Same(replacement, manager.Undo());
        Assert.False(manager.CanUndo);
        Assert.Same(replacement, manager.Redo());
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void ReplaceLast_Throws_WhenExpectedIsNotLast()
    {
        var manager = new UndoRedoManager();
        var first = new RecordingCommand();
        var second = new RecordingCommand();
        manager.ExecuteCommand(first);
        manager.ExecuteCommand(second);

        Assert.Throws<InvalidOperationException>(() => manager.ReplaceLast(first, new RecordingCommand()));

        Assert.True(first.IsApplied);
        Assert.True(second.IsApplied);
        Assert.Equal(new IEditCommand[] { second, first }, manager.UndoCommandsNewestFirst);
    }

    [Fact]
    public void ReplaceLast_Throws_WhenUndoListIsEmpty()
    {
        var manager = new UndoRedoManager();
        var command = new RecordingCommand();

        Assert.Throws<InvalidOperationException>(() => manager.ReplaceLast(command, new RecordingCommand()));
        Assert.False(manager.CanUndo);
    }

    [Fact]
    public void ReplaceLast_WhenReplacementThrows_RestoresExpectedAndHistory()
    {
        var manager = new UndoRedoManager();
        var expected = new RecordingCommand();
        manager.ExecuteCommand(expected);

        var exception = Assert.Throws<InvalidOperationException>(
            () => manager.ReplaceLast(expected, new RecordingCommand(throwOnExecute: true)));

        Assert.Equal("Execute failed.", exception.Message);
        Assert.True(expected.IsApplied);
        Assert.Same(expected, manager.Undo());
        Assert.False(manager.CanUndo);
    }
}
