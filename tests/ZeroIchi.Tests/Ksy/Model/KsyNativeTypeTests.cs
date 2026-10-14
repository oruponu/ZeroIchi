using ZeroIchi.Models.FileStructure.Ksy.Model;
using static ZeroIchi.Tests.Ksy.Model.KsyTestLoader;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyNativeTypeTests
{
    private sealed class TestNativeType : IKsyNativeType
    {
        public string Name => "test_blob";
    }

    private static KsyNativeTypeRegistry Registry()
    {
        var registry = new KsyNativeTypeRegistry();
        registry.Register(new TestNativeType());
        return registry;
    }

    [Fact]
    public void Parse_RegisteredOpaqueType_BecomesNativeType()
    {
        var format = Load("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: test_blob\n    size: 4\n", natives: Registry());

        var native = Assert.IsType<KsyNativeType>(Attribute(format, "blob").DataType);
        Assert.Equal("test_blob", native.Native.Name);
    }

    [Fact]
    public void Parse_UnregisteredOpaqueType_IsNotFound()
    {
        var error = SingleError("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: other_blob\n", natives: Registry());

        Assert.Contains("Type 'other_blob' not found", error.Message);
    }

    [Fact]
    public void Parse_RegisteredTypeWithoutOpaqueTypes_IsNotFound()
    {
        var error = SingleError("meta:\n  id: x\nseq:\n  - id: blob\n    type: test_blob\n", natives: Registry());

        Assert.Contains("Type 'test_blob' not found", error.Message);
    }

    [Fact]
    public void Parse_UserTypeWinsOverNativeType()
    {
        var format = Load("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: test_blob\ntypes:\n  test_blob:\n    seq: []\n", natives: Registry());

        Assert.IsType<KsyUserType>(Attribute(format, "blob").DataType);
    }

    [Fact]
    public void Parse_MemberOfNativeValue_IsError()
    {
        var error = SingleError("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: test_blob\ninstances:\n  v:\n    value: blob.length\n", natives: Registry());

        Assert.Contains("Members of native type 'blob' cannot be referenced", error.Message);
    }

    [Fact]
    public void Parse_NativeTypeIsVariableSize()
    {
        var format = Load("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: test_blob\n", natives: Registry());

        Assert.Null(format.Root.SizeInBits);
    }

    [Fact]
    public void Registry_RejectsDuplicateNames()
    {
        var registry = Registry();

        Assert.Throws<ArgumentException>(() => registry.Register(new TestNativeType()));
    }
}
