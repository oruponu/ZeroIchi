using static ZeroIchi.Tests.Formats.BuiltinFormat;

namespace ZeroIchi.Tests.Formats;

public sealed class BuiltinDetectionTests
{
    [Fact]
    public void Default_UndetectableData_IsNull()
    {
        Assert.Null(Detect(new byte[16]));
    }
}
