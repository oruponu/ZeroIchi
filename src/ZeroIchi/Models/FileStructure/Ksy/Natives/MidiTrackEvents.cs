using System.Collections.Generic;
using System.Globalization;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Models.FileStructure.Ksy.Natives;

internal sealed class MidiTrackEvents : IKsyNativeType
{
    private static readonly Dictionary<int, string> MetaTypes = new()
    {
        [0x00] = "sequence_number",
        [0x01] = "text_event",
        [0x02] = "copyright",
        [0x03] = "sequence_track_name",
        [0x04] = "instrument_name",
        [0x05] = "lyric_text",
        [0x06] = "marker_text",
        [0x07] = "cue_point",
        [0x20] = "midi_channel_prefix_assignment",
        [0x2F] = "end_of_track",
        [0x51] = "tempo",
        [0x54] = "smpte_offset",
        [0x58] = "time_signature",
        [0x59] = "key_signature",
        [0x7F] = "sequencer_specific_event",
    };

    public string Name => "midi_track_events";

    public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments)
    {
        var reader = new KsStreamReader(stream);
        var start = stream.Position;
        var position = start;
        var events = new List<KsyNativeNode>();
        int? running = null;
        while (position < stream.Size)
        {
            var eventStart = position;
            var fields = new List<KsyNativeNode>();
            var error = ReadEvent(reader, ref position, ref running, fields);
            if (error is not null) return new KsyNativeResult(eventStart - start, events) { Error = error };

            events.Add(new KsyNativeNode($"event[{events.Count}]", eventStart, position - eventStart, "", ValueKind.None)
            {
                Children = fields,
            });
        }
        return new KsyNativeResult(position - start, events);
    }

    private static string? ReadEvent(KsStreamReader reader, ref long position, ref int? running,
        List<KsyNativeNode> fields)
    {
        var eventStart = position;
        if (!TryReadVlq(reader, ref position, "v_time", fields)) return PastTheEnd(reader, eventStart);
        if (position >= reader.Size) return PastTheEnd(reader, eventStart);

        int status = reader[position];
        if (status >= 0x80)
        {
            fields.Add(Number("event_header", position, 1, status));
            position++;
            running = status < 0xF0 ? status : null;
        }
        else if (running is { } previous)
        {
            status = previous;
        }
        else
        {
            return $"data byte {status:X2} at 0x{reader.FileOffset(position):X} has no running status";
        }

        if (status is > 0xF0 and not 0xF7 and not 0xFF)
            return $"status {status:X2} at 0x{reader.FileOffset(position - 1):X} is not allowed in a track";

        var read = status switch
        {
            0xFF => TryReadMeta(reader, ref position, fields),
            0xF0 or 0xF7 => TryReadSysex(reader, ref position, fields),
            _ => TryReadChannel(reader, ref position, status, fields),
        };
        return read ? null : PastTheEnd(reader, eventStart);
    }

    private static bool TryReadChannel(KsStreamReader reader, ref long position, int status, List<KsyNativeNode> fields)
    {
        var (type, names) = (status & 0xF0) switch
        {
            0x80 => ("note_off_event", new[] { "note", "velocity" }),
            0x90 => ("note_on_event", ["note", "velocity"]),
            0xA0 => ("polyphonic_pressure_event", ["note", "pressure"]),
            0xB0 => ("controller_event", ["controller", "value"]),
            0xC0 => ("program_change_event", ["program"]),
            0xD0 => ("channel_pressure_event", ["pressure"]),
            _ => ("pitch_bend_event", ["b1", "b2"]),
        };
        if (names.Length > reader.Size - position) return false;

        var body = new List<KsyNativeNode>();
        for (var i = 0; i < names.Length; i++) body.Add(Number(names[i], position + i, 1, reader[position + i]));
        fields.Add(new KsyNativeNode("event_body", position, names.Length, type, ValueKind.None) { Children = body });
        position += names.Length;
        return true;
    }

    private static bool TryReadMeta(KsStreamReader reader, ref long position, List<KsyNativeNode> fields)
    {
        var start = position;
        if (position >= reader.Size) return false;
        int metaType = reader[position];
        var body = new List<KsyNativeNode>
        {
            new("meta_type", position, 1, MetaTypes.TryGetValue(metaType, out var name)
                ? string.Create(CultureInfo.InvariantCulture, $"{metaType} ({name})")
                : metaType.ToString(CultureInfo.InvariantCulture), ValueKind.Numeric),
        };
        position++;
        if (!TryReadLengthAndData(reader, ref position, "body", body)) return false;
        fields.Add(new KsyNativeNode("meta_event_body", start, position - start, "", ValueKind.None) { Children = body });
        return true;
    }

    private static bool TryReadSysex(KsStreamReader reader, ref long position, List<KsyNativeNode> fields)
    {
        var start = position;
        var body = new List<KsyNativeNode>();
        if (!TryReadLengthAndData(reader, ref position, "data", body)) return false;
        fields.Add(new KsyNativeNode("sysex_body", start, position - start, "", ValueKind.None) { Children = body });
        return true;
    }

    private static bool TryReadLengthAndData(KsStreamReader reader, ref long position, string name,
        List<KsyNativeNode> body)
    {
        if (!TryReadVlq(reader, ref position, "len", body)) return false;
        var length = long.Parse(body[^1].Description, CultureInfo.InvariantCulture);
        if (length > reader.Size - position) return false;
        body.Add(new KsyNativeNode(name, position, length, reader.Preview(position, length), ValueKind.Bytes));
        position += length;
        return true;
    }

    private static bool TryReadVlq(KsStreamReader reader, ref long position, string name, List<KsyNativeNode> nodes)
    {
        var start = position;
        long value = 0;
        for (var i = 0; i < 4 && position < reader.Size; i++)
        {
            var b = reader[position++];
            value = (value << 7) | (b & 0x7Fu);
            if ((b & 0x80) != 0) continue;
            nodes.Add(Number(name, start, position - start, value));
            return true;
        }
        return false;
    }

    private static string PastTheEnd(KsStreamReader reader, long position) =>
        $"event at 0x{reader.FileOffset(position):X} runs past the end of the track";

    private static KsyNativeNode Number(string name, long position, long length, long value) =>
        new(name, position, length, value.ToString(CultureInfo.InvariantCulture), ValueKind.Numeric);
}
