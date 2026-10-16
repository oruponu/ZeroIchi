using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Model.KsyTestLoader;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyNativeTypeTests
{
    private sealed class TestNativeType : IKsyNativeType
    {
        public string Name => "test_blob";

        public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments) => new(0, []);
    }

    private sealed class PairNativeType : IKsyNativeType
    {
        public string Name => "pair_blob";

        public int ParameterCount => 2;

        public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments) => new(0, []);
    }

    private static KsyNativeTypeRegistry Registry()
    {
        var registry = new KsyNativeTypeRegistry();
        registry.Register(new TestNativeType());
        registry.Register(new PairNativeType());
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
    public void Parse_ArrayMethodsOnRepeatedNativeValues_AreAllowed()
    {
        Load("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blobs\n    type: test_blob\n    size: 4\n    repeat: expr\n    repeat-expr: 2\ninstances:\n  v:\n    value: blobs.size\n", natives: Registry());
    }

    [Fact]
    public void Parse_NativeTypeWithArguments_IsError()
    {
        var error = SingleError("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: test_blob(4)\n", natives: Registry());

        Assert.Contains("Native type 'test_blob' takes 0 argument(s) but 1 were given", error.Message);
        Assert.Equal("/seq/0/type", error.Path);
    }

    [Fact]
    public void Parse_NativeTypeTakesItsDeclaredArguments()
    {
        var format = Load("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: n\n    type: u1\n  - id: blob\n    type: pair_blob(n, n > 1)\n", natives: Registry());

        var native = Assert.IsType<KsyNativeType>(Attribute(format, "blob").DataType);
        Assert.Equal(2, native.Arguments.Count);
    }

    [Fact]
    public void Parse_NativeTypeWithTooFewArguments_IsError()
    {
        var error = SingleError("meta:\n  id: x\n  ks-opaque-types: true\nseq:\n  - id: blob\n    type: pair_blob(1)\n", natives: Registry());

        Assert.Contains("Native type 'pair_blob' takes 2 argument(s) but 1 were given", error.Message);
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
