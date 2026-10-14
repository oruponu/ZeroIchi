using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public enum KsyByteOrder
{
    Le,
    Be,
}

public abstract record KsyEndian;

public sealed record KsyFixedEndian(KsyByteOrder Order) : KsyEndian;

public sealed record KsyEndianCase(KsExpr Match, KsyByteOrder Order);

public sealed record KsySwitchEndian(KsExpr On, IReadOnlyList<KsyEndianCase> Cases) : KsyEndian;

public abstract record KsyDataType;

public sealed record KsyIntType(int Size, bool Signed, KsyByteOrder? Order) : KsyDataType;

public sealed record KsyFloatType(int Size, KsyByteOrder? Order) : KsyDataType;

public sealed record KsyBitType(int Width, KsyByteOrder BitOrder, bool IsBool) : KsyDataType;

public sealed record KsyStrType(string EncodingName, Encoding Encoding, bool IsZeroTerminated) : KsyDataType;

public sealed record KsyBytesType : KsyDataType;

public sealed record KsyUserType(KsyType Type, IReadOnlyList<KsExpr> Arguments) : KsyDataType;

public sealed record KsySwitchCase(KsExpr? Match, KsyDataType Type);

public sealed record KsySwitchType(KsExpr On, IReadOnlyList<KsySwitchCase> Cases) : KsyDataType;

public enum KsyRepeatKind
{
    None,
    Eos,
    Expr,
    Until,
}

public enum KsyProcessKind
{
    Zlib,
    Xor,
    Rol,
    Ror,
}

public sealed record KsyProcess(KsyProcessKind Kind, KsExpr? Argument);

public abstract record KsyValidation;

public sealed record KsyValidEq(KsExpr Value) : KsyValidation;

public sealed record KsyValidRange(KsExpr? Min, KsExpr? Max) : KsyValidation;

public sealed record KsyValidAnyOf(IReadOnlyList<KsExpr> Values) : KsyValidation;

public sealed record KsyValidInEnum : KsyValidation;

public sealed record KsyValidExpr(KsExpr Condition) : KsyValidation;

public enum KsyAttributeKind
{
    Seq,
    ParseInstance,
    ValueInstance,
}

public sealed class KsyAttribute
{
    public required string Id { get; init; }
    public required KsyAttributeKind Kind { get; init; }
    public required string Path { get; init; }
    public KsyDataType? DataType { get; init; }
    public KsyEnum? Enum { get; init; }
    public KsExpr? If { get; init; }
    public KsExpr? Size { get; init; }
    public bool SizeEos { get; init; }
    public byte[]? Terminator { get; init; }
    public bool Consume { get; init; } = true;
    public bool Include { get; init; }
    public bool EosError { get; init; } = true;
    public byte? PadRight { get; init; }
    public byte[]? Contents { get; init; }
    public KsyRepeatKind Repeat { get; init; }
    public KsExpr? RepeatExpr { get; init; }
    public KsyProcess? Process { get; init; }
    public KsyValidation? Valid { get; init; }
    public KsExpr? Parent { get; init; }
    public KsExpr? Pos { get; init; }
    public KsExpr? Io { get; init; }
    public KsExpr? Value { get; init; }
    public string? Doc { get; init; }
}

public sealed record KsyParam(string Id, string? TypeName, bool IsArray, KsyType? UserType, KsyEnum? Enum);

public sealed class KsyEnum : IKsEnumType
{
    private readonly Dictionary<Int128, string> _names = [];
    private readonly Dictionary<string, Int128> _values = new(StringComparer.Ordinal);

    internal KsyEnum(string name, KsyType owner)
    {
        Name = name;
        Owner = owner;
    }

    public string Name { get; }
    public KsyType Owner { get; }
    public IReadOnlyDictionary<Int128, string> Names => _names;

    internal bool TryAdd(Int128 value, string name)
    {
        if (!_values.TryAdd(name, value)) return false;
        _names.TryAdd(value, name);
        return true;
    }

    public string? GetName(Int128 value) => _names.GetValueOrDefault(value);

    public bool TryGetValue(string name, out Int128 value) => _values.TryGetValue(name, out value);
}

public interface IKsyObject : IKsStruct
{
    KsyType Type { get; }
}

public sealed class KsyType : IKsTypeInfo
{
    private readonly Dictionary<string, KsyType> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KsyEnum> _enums = new(StringComparer.Ordinal);

    internal KsyType(string name, KsyFormat format, KsyType? outer, string path)
    {
        Name = name;
        Format = format;
        Outer = outer;
        Path = path;
    }

    public string Name { get; }
    public KsyFormat Format { get; }
    public KsyType? Outer { get; }
    public string Path { get; }
    public string FullName => Outer is null ? Name : $"{Outer.FullName}::{Name}";

    public IReadOnlyList<KsyParam> Params { get; internal set; } = [];
    public IReadOnlyList<KsyAttribute> Seq { get; internal set; } = [];
    public IReadOnlyList<KsyAttribute> Instances { get; internal set; } = [];
    public IReadOnlyDictionary<string, KsyType> Types => _types;
    public IReadOnlyDictionary<string, KsyEnum> Enums => _enums;

    public KsyEndian? Endian { get; internal set; }
    public KsyByteOrder? BitEndian { get; internal set; }
    public string? Encoding { get; internal set; }
    public string? WebideRepresentation { get; internal set; }
    public KsExpr? ToStringExpr { get; internal set; }
    public string? Doc { get; internal set; }

    public long? SizeInBits { get; internal set; }

    public KsyEndian? EffectiveEndian => Endian ?? Outer?.EffectiveEndian;
    public KsyByteOrder EffectiveBitEndian => BitEndian ?? Outer?.EffectiveBitEndian ?? KsyByteOrder.Be;
    public string? EffectiveEncoding => Encoding ?? Outer?.EffectiveEncoding;

    internal void AddType(KsyType type) => _types.Add(type.Name, type);

    internal void AddEnum(KsyEnum value) => _enums.Add(value.Name, value);

    public bool IsInstance(KsValue value) =>
        value.Kind == KsValueKind.Struct && value.AsStruct is IKsyObject obj && ReferenceEquals(obj.Type, this);

    public KsyType? FindType(IReadOnlyList<string> path)
    {
        for (var scope = this; scope is not null; scope = scope.Outer)
        {
            if (scope.Types.TryGetValue(path[0], out var nested))
                return nested.Descend(path, 1);
        }

        return Format.FindTopLevel(path[0])?.Descend(path, 1);
    }

    public KsyEnum? FindEnum(IReadOnlyList<string> path)
    {
        var name = path[^1];
        if (path.Count == 1)
        {
            for (var scope = this; scope is not null; scope = scope.Outer)
            {
                if (scope.Enums.TryGetValue(name, out var found)) return found;
            }
            return null;
        }

        var owner = FindType(path.Take(path.Count - 1).ToList());
        return owner?.Enums.GetValueOrDefault(name);
    }

    private KsyType? Descend(IReadOnlyList<string> path, int index)
    {
        var current = this;
        for (var i = index; i < path.Count && current is not null; i++)
            current = current.Types.GetValueOrDefault(path[i]);
        return current;
    }
}

public sealed class KsyFormat
{
    private readonly List<KsyFormat> _imports = [];

    internal KsyFormat(string path) => SourcePath = path;

    public string SourcePath { get; }
    public string Id { get; internal set; } = "";
    public KsyType Root { get; internal set; } = null!;
    public IReadOnlyList<KsyFormat> Imports => _imports;
    public IReadOnlyList<string> FileExtensions { get; internal set; } = [];
    public byte?[]? Magic { get; internal set; }
    public bool OpaqueTypes { get; internal set; }

    internal void AddImport(KsyFormat format) => _imports.Add(format);

    internal KsyType? FindTopLevel(string id)
    {
        if (id == Id) return Root;

        var visited = new HashSet<KsyFormat> { this };
        var pending = new Queue<KsyFormat>(_imports);
        while (pending.Count > 0)
        {
            var format = pending.Dequeue();
            if (!visited.Add(format)) continue;
            if (format.Id == id) return format.Root;
            foreach (var next in format.Imports) pending.Enqueue(next);
        }
        return null;
    }
}
