using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Models.FileStructure.Ksy.Natives;

internal sealed class ZipDataWithDescriptor : IKsyNativeType
{
    private const uint LocalFileSignature = 0x04034B50;
    private const uint CentralDirectorySignature = 0x02014B50;
    private const uint EndOfCentralDirectorySignature = 0x06054B50;
    private const uint Zip64EndOfCentralDirectorySignature = 0x06064B50;
    private const uint Zip64LocatorSignature = 0x07064B50;
    private const uint DescriptorSignature = 0x08074B50;
    private const uint Saturated = 0xFFFFFFFF;

    private readonly ConditionalWeakTable<KsStream, Dictionary<long, long>> _centralDirectories = new();

    public string Name => "zip_data_with_descriptor";

    public int ParameterCount => 1;

    public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments)
    {
        var reader = new KsStreamReader(stream);
        var start = stream.Position;
        var width = HasZip64Extra(reader, start, (long)arguments[0].AsInt) ? 8 : 4;

        var bodies = _centralDirectories.GetValue(stream, _ => ReadCentralDirectory(reader));
        var fromCentral = bodies.TryGetValue(start, out var known) ? known : (long?)null;
        if ((fromCentral ?? FromDescriptors(reader, start, width)) is not { } length)
            return new KsyNativeResult(0, []) { Error = "could not find the end of the data" };

        var body = new KsyNativeNode("body", start, length, reader.Preview(start, length), ValueKind.Bytes);
        var descriptor = start + length;
        var signed = HasSignature(reader, descriptor, length, width);
        var descriptorLength = (signed ? 4 : 0) + 4 + 2 * width;
        if (descriptorLength > reader.Size - descriptor)
            return new KsyNativeResult(length, [body]) { Error = "the data descriptor runs past the end of the file" };

        var fields = new List<KsyNativeNode>();
        var position = descriptor;
        if (signed)
        {
            fields.Add(new KsyNativeNode("magic", position, 4, reader.Preview(position, 4), ValueKind.Bytes));
            position += 4;
        }
        fields.Add(Number("crc32", position, 4, reader.UInt(position, 4)));
        fields.Add(Number("len_body_compressed", position + 4, width, reader.UInt(position + 4, width)));
        fields.Add(Number("len_body_uncompressed", position + 4 + width, width,
            reader.UInt(position + 4 + width, width)));
        return new KsyNativeResult(length + descriptorLength,
        [
            body,
            new KsyNativeNode("data_descriptor", descriptor, descriptorLength, "", ValueKind.None) { Children = fields },
        ]);
    }

    private static bool HasZip64Extra(KsStreamReader reader, long start, long lengthOfExtra)
    {
        var position = start - lengthOfExtra;
        if (lengthOfExtra <= 0 || position < 0) return false;
        while (start - position >= 4)
        {
            if (reader.UInt(position, 2) == 0x0001) return true;
            position += 4 + (long)reader.UInt(position + 2, 2);
        }
        return false;
    }

    private static Dictionary<long, long> ReadCentralDirectory(KsStreamReader reader)
    {
        var bodies = new Dictionary<long, long>();
        if (FindEndOfCentralDirectory(reader) is not { } end) return bodies;

        var count = reader.UInt(end + 10, 2);
        var position = (long)reader.UInt(end + 16, 4);
        if (end >= 20 && reader.UInt(end - 20, 4) == Zip64LocatorSignature)
        {
            var zip64End = (long)Math.Min(reader.UInt(end - 12, 8), long.MaxValue);
            if (Has(reader, zip64End, 56) && reader.UInt(zip64End, 4) == Zip64EndOfCentralDirectorySignature)
            {
                count = reader.UInt(zip64End + 32, 8);
                position = (long)Math.Min(reader.UInt(zip64End + 48, 8), long.MaxValue);
            }
        }

        for (ulong i = 0; i < count && Has(reader, position, 46); i++)
        {
            if (reader.UInt(position, 4) != CentralDirectorySignature) break;

            var uncompressed = reader.UInt(position + 24, 4);
            var compressed = reader.UInt(position + 20, 4);
            var lengthOfName = (long)reader.UInt(position + 28, 2);
            var lengthOfExtra = (long)reader.UInt(position + 30, 2);
            var lengthOfComment = (long)reader.UInt(position + 32, 2);
            var local = reader.UInt(position + 42, 4);
            var extra = position + 46 + lengthOfName;
            if (compressed == Saturated || local == Saturated)
                ReadZip64Sizes(reader, extra, lengthOfExtra, uncompressed == Saturated, ref compressed, ref local);

            if (BodyStart(reader, local) is { } body && compressed <= (ulong)(reader.Size - body))
                bodies.TryAdd(body, (long)compressed);
            position = extra + lengthOfExtra + lengthOfComment;
        }
        return bodies;
    }

    private static long? FindEndOfCentralDirectory(KsStreamReader reader)
    {
        var lowest = Math.Max(0, reader.Size - 22 - 0xFFFF);
        for (var position = reader.Size - 22; position >= lowest; position--)
        {
            if (reader.UInt(position, 4) == EndOfCentralDirectorySignature
                && position + 22 + (long)reader.UInt(position + 20, 2) <= reader.Size)
            {
                return position;
            }
        }
        return null;
    }

    private static void ReadZip64Sizes(KsStreamReader reader, long position, long length, bool hasUncompressed,
        ref ulong compressed, ref ulong local)
    {
        var end = Math.Min(position + length, reader.Size);
        while (end - position >= 4)
        {
            var id = reader.UInt(position, 2);
            var size = (long)reader.UInt(position + 2, 2);
            if (id == 0x0001)
            {
                var field = position + 4;
                var limit = Math.Min(field + size, end);
                if (hasUncompressed) field += 8;
                if (compressed == Saturated && field + 8 <= limit)
                {
                    compressed = reader.UInt(field, 8);
                    field += 8;
                }
                if (local == Saturated && field + 8 <= limit) local = reader.UInt(field, 8);
                return;
            }
            position += 4 + size;
        }
    }

    private static long? BodyStart(KsStreamReader reader, ulong local)
    {
        if (local > (ulong)reader.Size) return null;
        var position = (long)local;
        if (!Has(reader, position, 30) || reader.UInt(position, 4) != LocalFileSignature) return null;
        return position + 30 + (long)reader.UInt(position + 26, 2) + (long)reader.UInt(position + 28, 2);
    }

    private static long? FromDescriptors(KsStreamReader reader, long start, int width)
    {
        for (var position = start; reader.Size - position >= 4 + 2 * width; position++)
        {
            var length = (ulong)(position - start);
            if (Matches(reader, position, length, width, signed: true) || Matches(reader, position, length, width, signed: false))
                return position - start;
        }
        return null;
    }

    private static bool HasSignature(KsStreamReader reader, long position, long length, int width)
    {
        var signed = Fits(reader, position, (ulong)length, width, signed: true);
        var unsigned = Fits(reader, position, (ulong)length, width, signed: false);
        if (signed != unsigned) return signed;
        if (signed) return Matches(reader, position, (ulong)length, width, signed: true)
            || !Matches(reader, position, (ulong)length, width, signed: false);
        return Has(reader, position, 4) && reader.UInt(position, 4) == DescriptorSignature;
    }

    private static bool Matches(KsStreamReader reader, long position, ulong length, int width, bool signed)
    {
        if (!Fits(reader, position, length, width, signed)) return false;
        var after = position + (signed ? 4 : 0) + 4 + 2 * width;
        return after == reader.Size || IsRecord(reader, after);
    }

    private static bool Fits(KsStreamReader reader, long position, ulong length, int width, bool signed)
    {
        var offset = signed ? 4 : 0;
        if (!Has(reader, position, offset + 4 + 2 * width)) return false;
        if (signed && reader.UInt(position, 4) != DescriptorSignature) return false;
        return reader.UInt(position + offset + 4, width) == length;
    }

    private static bool IsRecord(KsStreamReader reader, long position) =>
        Has(reader, position, 4) && reader.UInt(position, 4) is LocalFileSignature or CentralDirectorySignature
            or EndOfCentralDirectorySignature or Zip64EndOfCentralDirectorySignature;

    private static bool Has(KsStreamReader reader, long position, long count) =>
        position >= 0 && count <= reader.Size - position;

    private static KsyNativeNode Number(string name, long position, long length, ulong value) =>
        new(name, position, length, value.ToString(CultureInfo.InvariantCulture), ValueKind.Numeric);
}
