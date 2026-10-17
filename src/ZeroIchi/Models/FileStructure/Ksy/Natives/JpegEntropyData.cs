using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Models.FileStructure.Ksy.Natives;

internal sealed class JpegEntropyData : IKsyNativeType
{
    public string Name => "jpeg_entropy_data";

    public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments)
    {
        var reader = new KsStreamReader(stream);
        var start = stream.Position;
        var position = start;
        while (position < stream.Size)
        {
            if (reader[position] != 0xFF)
            {
                position++;
                continue;
            }

            var next = position + 1;
            while (next < stream.Size && reader[next] == 0xFF) next++;
            if (next >= stream.Size) return new KsyNativeResult(stream.Size - start, []);
            if (IsMarker(reader[next])) return new KsyNativeResult(next - 1 - start, []);
            position = next + 1;
        }
        return new KsyNativeResult(stream.Size - start, []);
    }

    private static bool IsMarker(byte code) => code is 0x01 or >= 0xC0 and <= 0xFE;
}
