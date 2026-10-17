using static ZeroIchi.Tests.Formats.BuiltinFormat;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

public sealed class ZipFormatTests
{
    [Fact]
    public void Parse_ReadsLocalFilesTheCentralDirectoryAndItsEnd()
    {
        var root = Parse("archive/zip", new ZipBuilder().Add("a.txt", [1, 2, 3]).Add("b.txt", [4]).Build());

        Assert.Equal("", Errors(root));
        Assert.Equal("sections[0],sections[1],sections[2],sections[3],sections[4]", Names(Node(root, "sections")));
        Assert.Equal((35L, 3L), (Node(root, "sections/sections[0]/body/body").Offset,
            Node(root, "sections/sections[0]/body/body").Length));
    }

    [Theory]
    [InlineData(true, 16)]
    [InlineData(false, 12)]
    public void Parse_DataDescriptorFollowsTheBody(bool signature, long descriptorLength)
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true, signature: signature).Add("b.txt", [4]);

        var root = Parse("archive/zip", builder.Build());

        Assert.Equal("", Errors(root));
        var data = Node(root, "sections/sections[0]/body/body_with_descriptor");
        Assert.Equal("body,data_descriptor", Names(data));
        Assert.Equal((38L, descriptorLength), (Node(data, "data_descriptor").Offset, Node(data, "data_descriptor").Length));
        Assert.Equal(38 + descriptorLength, Node(root, "sections/sections[1]").Offset);
    }

    [Fact]
    public void Parse_DataDescriptorWithoutACentralDirectory()
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true);

        var root = Parse("archive/zip", builder.Build(centralDirectory: false));

        Assert.Equal("", Errors(root));
        Assert.Equal("sections[0]", Names(Node(root, "sections")));
        Assert.Equal((35L, 3L), (Node(root, "sections/sections[0]/body/body_with_descriptor/body").Offset,
            Node(root, "sections/sections[0]/body/body_with_descriptor/body").Length));
    }

    [Fact]
    public void Parse_Zip64SizesComeFromTheExtraField()
    {
        var root = Parse("archive/zip", new ZipBuilder().Add("a.txt", [1, 2, 3], zip64: true).Build(zip64End: true));

        Assert.Equal("", Errors(root));
        Assert.Equal("4294967295", Node(root, "sections/sections[0]/body/header/len_body_compressed").Description);
        Assert.Equal((55L, 3L), (Node(root, "sections/sections[0]/body/body").Offset,
            Node(root, "sections/sections[0]/body/body").Length));
        Assert.Equal("1542", Node(root, "sections/sections[2]/section_type").Description);
        Assert.Equal("1798", Node(root, "sections/sections[3]/section_type").Description);
        Assert.Equal("1541", Node(root, "sections/sections[4]/section_type").Description);
    }

    [Fact]
    public void Parse_Zip64DataDescriptorUsesEightByteSizes()
    {
        var builder = new ZipBuilder().Add("a.txt", [1, 2, 3], descriptor: true, zip64: true);

        var root = Parse("archive/zip", builder.Build(zip64End: true));

        Assert.Equal("", Errors(root));
        Assert.Equal(24, Node(root, "sections/sections[0]/body/body_with_descriptor/data_descriptor").Length);
    }
}
