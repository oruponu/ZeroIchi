using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class MidiFormatTests
{
    private static byte[] Midi(params byte[][] tracks) =>
    [
        .. "MThd"u8, 0, 0, 0, 6, 0, 1, 0, (byte)tracks.Length, 0, 96,
        .. tracks.SelectMany(t => (byte[])[.. "MTrk"u8, 0, 0, (byte)(t.Length >> 8), (byte)t.Length, .. t]),
    ];

    [Fact]
    public void Default_DetectsMidiByItsSignature()
    {
        Assert.Equal("standard_midi_file", Detect(Midi([0x00, 0xFF, 0x2F, 0x00])));
    }

    [Fact]
    public void Parse_ReadsEventsWithRunningStatus()
    {
        var root = Parse("media/standard_midi_file",
            Midi([0x00, 0x90, 0x3C, 0x40, 0x60, 0x3C, 0x00, 0x00, 0xFF, 0x2F, 0x00]));

        Assert.Equal("", Errors(root));
        var events = Node(root, "tracks/tracks[0]/events");
        Assert.Equal("event[0],event[1],event[2]", Names(events));
        Assert.Equal("v_time,event_body", Names(Node(events, "event[1]")));
        Assert.Equal(("96", "note_on_event"), (Node(events, "event[1]/v_time").Description,
            Node(events, "event[1]/event_body").Description));
        Assert.Equal("47 (end_of_track)", Node(events, "event[2]/meta_event_body/meta_type").Description);
    }

    [Fact]
    public void Parse_EventCutOffInsideTheTrackKeepsTheEventsBefore()
    {
        var root = Parse("media/standard_midi_file", Midi([0x00, 0xC0, 0x05, 0x00, 0x90, 0x3C]));

        var events = Node(root, "tracks/tracks[0]/events");
        Assert.Equal("event[0]", Names(events));
        Assert.Equal("Native(midi_track_events, event at 0x19 runs past the end of the track)", Summary(events.Error));
    }
}
