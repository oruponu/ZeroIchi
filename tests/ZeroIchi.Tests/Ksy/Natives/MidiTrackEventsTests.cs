using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Natives;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Tests.Ksy.Natives;

public sealed class MidiTrackEventsTests
{
    private static KsyNativeResult Read(byte[] data, long start = 0)
    {
        var stream = KsStream.FromBytes(data);
        stream.Seek(start);
        return new MidiTrackEvents().Read(stream, []);
    }

    private static string Names(KsyNativeNode node) => string.Join(",", node.Children.Select(c => c.Name));

    private static KsyNativeNode Child(KsyNativeNode node, string name) => node.Children.Single(c => c.Name == name);

    [Fact]
    public void Read_RunningStatusEventHasNoEventHeader()
    {
        var result = Read([0x00, 0x90, 0x3C, 0x40, 0x10, 0x3C, 0x00]);

        Assert.Null(result.Error);
        Assert.Equal(7, result.Consumed);
        Assert.Equal(["event[0]", "event[1]"], result.Nodes.Select(n => n.Name));
        Assert.Equal("v_time,event_header,event_body", Names(result.Nodes[0]));
        Assert.Equal("v_time,event_body", Names(result.Nodes[1]));
        var body = Child(result.Nodes[1], "event_body");
        Assert.Equal((5L, 2L, "note_on_event"), (body.Position, body.Length, body.Description));
        Assert.Equal(("60", "0"), (Child(body, "note").Description, Child(body, "velocity").Description));
    }

    [Fact]
    public void Read_MetaEventHasItsTypeLengthAndBody()
    {
        var result = Read([0x00, 0xFF, 0x03, 0x02, 0x41, 0x42, 0x00, 0xFF, 0x2F, 0x00]);

        Assert.Null(result.Error);
        var meta = Child(result.Nodes[0], "meta_event_body");
        Assert.Equal("meta_type,len,body", Names(meta));
        Assert.Equal(("3 (sequence_track_name)", "2", "41 42"),
            (Child(meta, "meta_type").Description, Child(meta, "len").Description, Child(meta, "body").Description));
        Assert.Equal("47 (end_of_track)", Child(Child(result.Nodes[1], "meta_event_body"), "meta_type").Description);
    }

    [Fact]
    public void Read_SysexEventHasItsLengthAndData()
    {
        var result = Read([0x00, 0xF0, 0x03, 0x7E, 0x7F, 0xF7]);

        Assert.Null(result.Error);
        var sysex = Child(result.Nodes[0], "sysex_body");
        Assert.Equal((2L, 4L, "len,data"), (sysex.Position, sysex.Length, Names(sysex)));
        Assert.Equal("7E 7F F7", Child(sysex, "data").Description);
    }

    [Fact]
    public void Read_DeltaTimeSpanningSeveralBytes()
    {
        var result = Read([0x81, 0x00, 0xC0, 0x05]);

        var vTime = Child(result.Nodes[0], "v_time");
        Assert.Equal((0L, 2L, "128"), (vTime.Position, vTime.Length, vTime.Description));
        Assert.Equal("program_change_event", Child(result.Nodes[0], "event_body").Description);
    }

    [Fact]
    public void Read_MetaEventCancelsTheRunningStatus()
    {
        var result = Read([0x00, 0x90, 0x3C, 0x40, 0x00, 0xFF, 0x01, 0x00, 0x00, 0x3C, 0x00]);

        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(8, result.Consumed);
        Assert.Equal("data byte 3C at 0x9 has no running status", result.Error);
    }

    [Fact]
    public void Read_EventCutOffByTheEndOfTheTrackKeepsTheEventsBefore()
    {
        var result = Read([0x00, 0xC0, 0x05, 0x00, 0x90, 0x3C]);

        Assert.Single(result.Nodes);
        Assert.Equal(3, result.Consumed);
        Assert.Equal("event at 0x3 runs past the end of the track", result.Error);
    }

    [Fact]
    public void Read_SystemCommonStatusIsAnError()
    {
        var result = Read([0x00, 0xF2, 0x00, 0x00]);

        Assert.Empty(result.Nodes);
        Assert.Equal("status F2 at 0x1 is not allowed in a track", result.Error);
    }

    [Fact]
    public void Read_StartsAtTheCurrentPosition()
    {
        var result = Read([0xAA, 0xBB, 0x00, 0xC0, 0x05], start: 2);

        Assert.Equal(3, result.Consumed);
        Assert.Equal((2L, 4L), (result.Nodes[0].Position, Child(result.Nodes[0], "event_body").Position));
    }
}
