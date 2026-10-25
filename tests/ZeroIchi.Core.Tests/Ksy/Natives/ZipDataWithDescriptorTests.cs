using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Natives;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Tests.Ksy.Natives;

public sealed class ZipDataWithDescriptorTests
{
    private static KsyNativeResult Read(byte[] zip, long bodyStart, int lengthOfExtra = 0)
    {
        var stream = KsStream.FromBytes(zip);
        stream.Seek(bodyStart);
        return new ZipDataWithDescriptor().Read(stream, [KsValue.FromInt(lengthOfExtra)]);
    }

    private static string Names(KsyNativeNode node) => string.Join(",", node.Children.Select(c => c.Name));

    [Fact]
    public void Read_FindsTheLengthInTheCentralDirectory()
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true);

        var result = Read(builder.Build(), builder.BodyStarts[0]);

        Assert.Null(result.Error);
        Assert.Equal(3 + 16, result.Consumed);
        Assert.Equal(["body", "data_descriptor"], result.Nodes.Select(n => n.Name));
        Assert.Equal((35L, 3L, "01 02 03"), (result.Nodes[0].Position, result.Nodes[0].Length, result.Nodes[0].Description));
        Assert.Equal("magic,crc32,len_body_compressed,len_body_uncompressed", Names(result.Nodes[1]));
        Assert.Equal("3", result.Nodes[1].Children[2].Description);
    }

    [Fact]
    public void Read_DescriptorWithoutSignature()
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true, signature: false);

        var result = Read(builder.Build(), builder.BodyStarts[0]);

        Assert.Equal(3 + 12, result.Consumed);
        Assert.Equal("crc32,len_body_compressed,len_body_uncompressed", Names(result.Nodes[1]));
    }

    [Fact]
    public void Read_Zip64ExtraFieldWidensTheDescriptorSizes()
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true, zip64: true);

        var result = Read(builder.Build(), builder.BodyStarts[0], lengthOfExtra: 20);

        Assert.Equal(3 + 24, result.Consumed);
        Assert.Equal((8L, "3"), (result.Nodes[1].Children[2].Length, result.Nodes[1].Children[2].Description));
    }

    [Fact]
    public void Read_FindsTheDescriptorWithoutACentralDirectory()
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true).Add("b.txt", [4], descriptor: true);

        var result = Read(builder.Build(centralDirectory: false), builder.BodyStarts[0]);

        Assert.Equal(3 + 16, result.Consumed);
    }

    [Fact]
    public void Read_CentralDirectoryWinsOverAFakeDescriptorInStoredData()
    {
        byte[] body = [9, .. FakeDescriptor(length: 1), 0x50, 0x4B, 0x03, 0x04, 7, 7];
        var builder = new ZipBuilder().Add("a.bin", body, descriptor: true);

        var result = Read(builder.Build(), builder.BodyStarts[0]);

        Assert.Equal(body.Length + 16, result.Consumed);
    }

    [Fact]
    public void Read_FakeDescriptorNotFollowedByARecordIsSkippedWithoutACentralDirectory()
    {
        byte[] body = [9, .. FakeDescriptor(length: 1), 0, 0, 0, 0];
        var builder = new ZipBuilder().Add("a.bin", body, descriptor: true);

        var result = Read(builder.Build(centralDirectory: false), builder.BodyStarts[0]);

        Assert.Equal(body.Length + 16, result.Consumed);
    }

    [Fact]
    public void Read_UnsignedDescriptorWhoseCrcLooksLikeTheSignature()
    {
        var builder = new ZipBuilder().Add("a.bin", [1, 2, 3, 4], descriptor: true, signature: false, crc: 0x08074B50);

        var result = Read(builder.Build(), builder.BodyStarts[0]);

        Assert.Equal(4 + 12, result.Consumed);
        Assert.Equal("crc32,len_body_compressed,len_body_uncompressed", Names(result.Nodes[1]));
    }

    [Fact]
    public void Read_SameInstanceReadsEachFileWithItsOwnCentralDirectory()
    {
        var native = new ZipDataWithDescriptor();
        var first = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true);
        var second = new ZipBuilder().Add("a.txt", [1, 2, 3, 4, 5], descriptor: true);

        var firstStream = KsStream.FromBytes(first.Build());
        firstStream.Seek(first.BodyStarts[0]);
        var secondStream = KsStream.FromBytes(second.Build());
        secondStream.Seek(second.BodyStarts[0]);

        Assert.Equal(3 + 16, native.Read(firstStream, [KsValue.FromInt(0)]).Consumed);
        Assert.Equal(5 + 16, native.Read(secondStream, [KsValue.FromInt(0)]).Consumed);
    }

    [Fact]
    public void Read_DataWithoutAnyDescriptorIsAnError()
    {
        byte[] zip = [.. new ZipBuilder().Add("a.txt", [1, 2, 3]).Build(centralDirectory: false), 5, 6, 7];

        var result = Read(zip, 35);

        Assert.Equal((0L, "could not find the end of the data"), (result.Consumed, result.Error));
        Assert.Empty(result.Nodes);
    }

    private static byte[] FakeDescriptor(int length) =>
        [0x50, 0x4B, 0x07, 0x08, 0, 0, 0, 0, (byte)length, 0, 0, 0, (byte)length, 0, 0, 0];
}
