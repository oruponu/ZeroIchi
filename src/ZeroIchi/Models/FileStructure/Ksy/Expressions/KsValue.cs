using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public readonly struct KsValue
{
    private readonly Int128 _int;
    private readonly double _float;
    private readonly object? _ref;

    private KsValue(KsValueKind kind, Int128 intValue = default, double floatValue = default, object? reference = null)
    {
        Kind = kind;
        _int = intValue;
        _float = floatValue;
        _ref = reference;
    }

    public KsValueKind Kind { get; }

    public static KsValue Null => default;

    public static KsValue FromInt(Int128 value) => new(KsValueKind.Int, intValue: value);
    public static KsValue FromFloat(double value) => new(KsValueKind.Float, floatValue: value);
    public static KsValue FromBool(bool value) => new(KsValueKind.Bool, intValue: value ? 1 : 0);
    public static KsValue FromString(string value) => new(KsValueKind.String, reference: value);
    public static KsValue FromBytes(IKsBytes value) => new(KsValueKind.Bytes, reference: value);
    public static KsValue FromBytes(byte[] value) => FromBytes(new KsByteArray(value));
    public static KsValue FromEnum(IKsEnumType type, Int128 value) => new(KsValueKind.Enum, intValue: value, reference: type);
    public static KsValue FromStruct(IKsStruct value) => new(KsValueKind.Struct, reference: value);
    public static KsValue FromArray(IReadOnlyList<KsValue> value) => new(KsValueKind.Array, reference: value);
    public static KsValue FromStream(IKsStream value) => new(KsValueKind.Stream, reference: value);

    public Int128 AsInt => Kind == KsValueKind.Int ? _int : throw Mismatch();
    public double AsFloat => Kind == KsValueKind.Float ? _float : throw Mismatch();
    public bool AsBool => Kind == KsValueKind.Bool ? _int != 0 : throw Mismatch();
    public string AsString => Kind == KsValueKind.String ? (string)_ref! : throw Mismatch();
    public IKsBytes AsBytes => Kind == KsValueKind.Bytes ? (IKsBytes)_ref! : throw Mismatch();
    public IKsEnumType EnumType => Kind == KsValueKind.Enum ? (IKsEnumType)_ref! : throw Mismatch();
    public Int128 EnumValue => Kind == KsValueKind.Enum ? _int : throw Mismatch();
    public IKsStruct AsStruct => Kind == KsValueKind.Struct ? (IKsStruct)_ref! : throw Mismatch();
    public IReadOnlyList<KsValue> AsArray => Kind == KsValueKind.Array ? (IReadOnlyList<KsValue>)_ref! : throw Mismatch();
    public IKsStream AsStream => Kind == KsValueKind.Stream ? (IKsStream)_ref! : throw Mismatch();

    private InvalidOperationException Mismatch() => new($"Value is {KindName(Kind)}");

    public static string KindName(KsValueKind kind) => kind switch
    {
        KsValueKind.Null => "null",
        KsValueKind.Int => "integer",
        KsValueKind.Float => "float",
        KsValueKind.Bool => "boolean",
        KsValueKind.String => "string",
        KsValueKind.Bytes => "bytes",
        KsValueKind.Enum => "enum",
        KsValueKind.Struct => "struct",
        KsValueKind.Array => "array",
        KsValueKind.Stream => "stream",
        _ => kind.ToString(),
    };

    public override string ToString() => Kind switch
    {
        KsValueKind.Null => "null",
        KsValueKind.Int => _int.ToString(CultureInfo.InvariantCulture),
        KsValueKind.Float => _float.ToString("R", CultureInfo.InvariantCulture),
        KsValueKind.Bool => _int != 0 ? "true" : "false",
        KsValueKind.String => (string)_ref!,
        KsValueKind.Enum => $"{((IKsEnumType)_ref!).Name}({_int.ToString(CultureInfo.InvariantCulture)})",
        _ => KindName(Kind),
    };
}
