using System;
using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public interface IKsBytes
{
    long Length { get; }
    byte[] ToArray();
}

public sealed class KsByteArray(byte[] bytes) : IKsBytes
{
    public long Length => bytes.Length;
    public byte[] ToArray() => bytes;
}

public interface IKsStream
{
    long Position { get; }
    long Size { get; }
    bool IsEof { get; }
}

public interface IKsStruct
{
    string TypeName { get; }
    IKsStruct? Parent { get; }
    IKsStruct Root { get; }
    IKsStream Io { get; }
    long? StaticSize { get; }
    bool TryGetMember(string name, out KsValue value);

    bool TryGetFieldSize(string name, out long size)
    {
        size = 0;
        return false;
    }
}

public interface IKsEnumType
{
    string Name { get; }
    string? GetName(Int128 value);
    bool TryGetValue(string name, out Int128 value);
}

public interface IKsTypeInfo
{
    long? SizeInBits { get; }
    bool IsInstance(KsValue value);
}

public interface IKsEvalScope
{
    bool TryResolveName(string name, out KsValue value);
    IKsEnumType? ResolveEnum(IReadOnlyList<string> path);
    IKsTypeInfo? ResolveType(IReadOnlyList<string> path);

    bool TryResolveFieldSize(string name, out long size)
    {
        size = 0;
        return false;
    }
}
