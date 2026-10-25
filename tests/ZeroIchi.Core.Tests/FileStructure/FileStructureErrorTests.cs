using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Tests.FileStructure;

public sealed class FileStructureErrorTests
{
    [Fact]
    public void Factories_KeepTheEnglishMessage()
    {
        var error = FileStructureError.EndOfData(0x1A, false, 4, 2, "Unexpected end of stream");

        Assert.Equal((FileStructureErrorKind.EndOfData, "Unexpected end of stream"), (error.Kind, error.Message));
        Assert.Equal(["1A", "4", "2"], error.Arguments);
    }
}
