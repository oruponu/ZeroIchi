using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy.Expressions;

internal sealed class TestScope : IKsEvalScope
{
    public Dictionary<string, KsValue> Names { get; } = [];
    public Dictionary<string, IKsEnumType> Enums { get; } = [];
    public Dictionary<string, IKsTypeInfo> Types { get; } = [];

    public bool TryResolveName(string name, out KsValue value) => Names.TryGetValue(name, out value);

    public IKsEnumType? ResolveEnum(IReadOnlyList<string> path) => Enums.GetValueOrDefault(string.Join("::", path));

    public IKsTypeInfo? ResolveType(IReadOnlyList<string> path) => Types.GetValueOrDefault(string.Join("::", path));
}

internal sealed class FakeEnum(string name, params (long Value, string Name)[] entries) : IKsEnumType
{
    public string Name => name;

    public string? GetName(Int128 value)
    {
        foreach (var entry in entries)
        {
            if (entry.Value == value) return entry.Name;
        }
        return null;
    }

    public bool TryGetValue(string valueName, out Int128 value)
    {
        foreach (var entry in entries)
        {
            if (entry.Name == valueName)
            {
                value = entry.Value;
                return true;
            }
        }
        value = 0;
        return false;
    }
}

internal sealed class FakeStream(long position, long size) : IKsStream
{
    public long Position => position;
    public long Size => size;
    public bool IsEof => position >= size;
}

internal sealed class FakeStruct(string typeName, long? staticSize = null) : IKsStruct
{
    public Dictionary<string, KsValue> Members { get; } = [];
    public string TypeName => typeName;
    public IKsStruct? Parent { get; init; }
    public IKsStruct Root => Parent?.Root ?? this;
    public IKsStream Io { get; init; } = new FakeStream(0, 0);
    public long? StaticSize => staticSize;

    public bool TryGetMember(string name, out KsValue value) => Members.TryGetValue(name, out value);
}

internal sealed class FakeType(long? sizeInBits, Func<KsValue, bool>? isInstance = null) : IKsTypeInfo
{
    public long? SizeInBits => sizeInBits;
    public bool IsInstance(KsValue value) => isInstance?.Invoke(value) ?? false;
}

internal sealed class ThrowingBytes : IKsBytes
{
    public long Length => 1L << 40;
    public byte[] ToArray() => throw new KsExpressionException("too large", -1, 0);
}
