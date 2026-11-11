using System.Buffers.Binary;
using static ZeroIchi.Tests.Formats.BuiltinFormat;

namespace ZeroIchi.Tests.Formats;

public sealed class BuiltinDetectionTests
{
    private static byte[] Pe(ushort characteristics)
    {
        var data = new byte[0x200];
        "MZ"u8.CopyTo(data);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x3C), 0x80);
        "PE\0\0"u8.CopyTo(data.AsSpan(0x80));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x84), 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x96), characteristics);
        return data;
    }

    [Fact]
    public void Default_UndetectableData_IsNull()
    {
        Assert.Null(Detect(new byte[16]));
    }

    [Theory]
    [InlineData((ushort)0x0102)]
    [InlineData((ushort)0x2102)]
    public void Default_ExeAndDllUseThePeDefinition(ushort characteristics)
    {
        Assert.Equal("microsoft_pe", Detect(Pe(characteristics)));
    }

    [Fact]
    public void Default_Mp4UsesTheQuickTimeDefinition()
    {
        byte[] mp4 = [0, 0, 0, 0x18, .. "ftypmp42"u8, 0, 0, 0, 0, .. "mp42isom"u8, 0, 0, 0, 8, .. "free"u8];

        Assert.Equal("quicktime_mov", Detect(mp4));
    }
}
