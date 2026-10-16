using System;
using System.IO;
using System.IO.Compression;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal static class KsProcess
{
    public static byte[] Xor(byte[] data, KsValue key)
    {
        var result = new byte[data.Length];
        switch (key.Kind)
        {
            case KsValueKind.Int when key.AsInt >= 0 && key.AsInt <= 255:
            {
                var k = (byte)key.AsInt;
                for (var i = 0; i < data.Length; i++) result[i] = (byte)(data[i] ^ k);
                return result;
            }
            case KsValueKind.Bytes:
            {
                var k = key.AsBytes.ToArray();
                if (k.Length == 0) throw new KsExpressionException("xor key is empty", -1, 0);
                for (var i = 0; i < data.Length; i++) result[i] = (byte)(data[i] ^ k[i % k.Length]);
                return result;
            }
            default:
                throw new KsExpressionException(
                    $"xor key must be a byte or a byte array (got {KsValue.KindName(key.Kind)} {key})", -1, 0);
        }
    }

    public static byte[] Rotate(byte[] data, KsValue amount, bool left)
    {
        if (amount.Kind != KsValueKind.Int)
        {
            throw new KsExpressionException(
                $"Rotation amount must be an integer (got {KsValue.KindName(amount.Kind)})", -1, 0);
        }

        var shift = (int)KsIntMath.FloorMod(left ? amount.AsInt : -amount.AsInt, 8);
        var result = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] << shift | data[i] >> (8 - shift));
        return result;
    }

    public static byte[] Zlib(byte[] data, long limit)
    {
        try
        {
            using var input = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > limit)
                {
                    throw new KsyParseException(FileStructureError.Limit(FileStructureErrorKind.DecompressLimit, limit,
                        $"Decompressed data exceeds the limit ({limit} bytes)"));
                }
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        catch (InvalidDataException e)
        {
            throw new KsyParseException(
                new FileStructureError(FileStructureErrorKind.Zlib, [e.Message], $"Invalid zlib data: {e.Message}"));
        }
    }
}
