using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed partial class KsyRun
{
    private int _depth;

    private KsObject ReadObject(KsyType type, KsObject? caller, IKsStruct? parent, KsStream io, KsValue[] args,
        KsyNode node)
    {
        EnterNesting();
        try
        {
            var obj = new KsObject(this, type, parent, caller?.RootObject, io, args, node);
            obj.Endian = ResolveEndian(obj, caller);
            for (var i = 0; i < type.Seq.Count; i++) ReadSeqAttribute(obj, i);
            return obj;
        }
        finally
        {
            _depth--;
        }
    }

    private void EnterNesting()
    {
        if (_depth >= options.MaxDepth)
            throw new KsyParseException($"Type nesting exceeds the limit ({options.MaxDepth})") { IsLimit = true };
        RuntimeHelpers.EnsureSufficientExecutionStack();
        _depth++;
    }

    private static KsyByteOrder? ResolveEndian(KsObject obj, KsObject? caller)
    {
        switch (obj.Type.EffectiveEndian)
        {
            case KsyFixedEndian fixedEndian:
                return fixedEndian.Order;
            case KsySwitchEndian switchEndian when obj.Type.Endian is not null:
            {
                var path = obj.Type.Path;
                var on = Evaluate(switchEndian.On, obj, path, "meta/endian");
                foreach (var endianCase in switchEndian.Cases)
                {
                    var match = Evaluate(endianCase.Match, obj, path, "meta/endian");
                    if (Equal(endianCase.Match, on, match, path, "meta/endian")) return endianCase.Order;
                }
                throw new KsyParseException($"{path}/meta/endian: No case matches {on}");
            }
            case KsySwitchEndian:
                return caller?.Endian;
            default:
                return null;
        }
    }

    private void ReadSeqAttribute(KsObject obj, int index)
    {
        var attribute = obj.Type.Seq[index];
        if (attribute.If is { } condition && !EvaluateBool(condition, obj, attribute.Path, "if"))
        {
            obj.SetField(index, KsValue.Null);
            return;
        }
        obj.SetField(index, ReadAttribute(obj, attribute, obj.Stream, obj.Node.Children, obj));
    }

    private KsValue ReadAttribute(KsObject obj, KsyAttribute attribute, KsStream io, List<KsyNode> siblings,
        IKsEvalScope scope) =>
        ReadElement(obj, attribute, io, attribute.Id, siblings, scope);

    private KsValue ReadElement(KsObject obj, KsyAttribute attribute, KsStream io, string name,
        List<KsyNode> siblings, IKsEvalScope scope)
    {
        var node = NewNode(name, attribute.Id, io, attribute.DataType is KsyBitType ? io.BitStart : io.Position);
        siblings.Add(node);
        return Guard(node, () =>
        {
            var value = attribute is { DataType: KsyBytesType, Contents: { } expected }
                ? ReadContents(io, expected, node)
                : ReadData(obj, attribute, attribute.DataType!, io, node, scope);
            if (attribute.Enum is { } enumType && value.Kind == KsValueKind.Int)
                value = KsValue.FromEnum(enumType, value.AsInt);
            node.Value = value;
            return value;
        });
    }

    private KsValue ReadData(KsObject obj, KsyAttribute attribute, KsyDataType type, KsStream io, KsyNode node,
        IKsEvalScope scope)
    {
        switch (type)
        {
            case KsyIntType t:
                return KsValue.FromInt(io.ReadInteger(t.Size, t.Signed, IsBigEndian(t.Order, t.Size, obj, attribute)));
            case KsyFloatType t:
                return KsValue.FromFloat(io.ReadFloat(t.Size, IsBigEndian(t.Order, t.Size, obj, attribute)));
            case KsyBitType t:
            {
                var bits = io.ReadBits(t.Width, t.BitOrder == KsyByteOrder.Be);
                return t.IsBool ? KsValue.FromBool(bits != 0) : KsValue.FromInt(bits);
            }
            case KsyUserType user when !HasSize(attribute):
                return ReadUser(obj, attribute, user, io, node, scope);
            default:
                return ReadSized(obj, attribute, type, io, node, scope);
        }
    }

    private static bool HasSize(KsyAttribute attribute) =>
        attribute.Size is not null || attribute.SizeEos || attribute.Terminator is not null;

    private KsValue ReadUser(KsObject caller, KsyAttribute attribute, KsyUserType user, KsStream io, KsyNode node,
        IKsEvalScope scope)
    {
        var args = new KsValue[user.Arguments.Count];
        for (var i = 0; i < args.Length; i++)
        {
            var value = Evaluate(user.Arguments[i], scope, attribute.Path, "type");
            if (user.Type.Params[i].Enum is { } enumType && value.Kind == KsValueKind.Int)
                value = KsValue.FromEnum(enumType, value.AsInt);
            args[i] = value;
        }

        IKsStruct? parent = caller;
        if (attribute.Parent is { } parentExpr)
        {
            var value = Evaluate(parentExpr, scope, attribute.Path, "parent");
            parent = value.Kind switch
            {
                KsValueKind.Struct => value.AsStruct,
                KsValueKind.Bool when !value.AsBool => null,
                _ => throw new KsyParseException(
                    $"{attribute.Path}/parent: Expected an object (got {KsValue.KindName(value.Kind)})"),
            };
        }

        var child = ReadObject(user.Type, caller, parent, io, args, node);
        return KsValue.FromStruct(child);
    }

    private static bool IsBigEndian(KsyByteOrder? order, int size, KsObject obj, KsyAttribute attribute)
    {
        if (size == 1) return true;
        var resolved = order ?? obj.Endian
            ?? throw new KsyParseException($"{attribute.Path}: Endianness is not determined");
        return resolved == KsyByteOrder.Be;
    }

    private KsValue ReadSized(KsObject obj, KsyAttribute attribute, KsyDataType type, KsStream io, KsyNode node,
        IKsEvalScope scope)
    {
        var range = ReadRange(attribute, io, scope);
        var processed = attribute.Process is { } process
            ? Process(process, Load(io, range, attribute), scope, attribute)
            : null;
        return type switch
        {
            KsyBytesType => processed is null
                ? KsValue.FromBytes(new KsStreamBytes(io, range, options.MaxBytesLoad))
                : KsValue.FromBytes(processed),
            KsyStrType s => KsValue.FromString(s.Encoding.GetString(processed ?? Load(io, range, attribute))),
            KsyUserType user => ReadUser(obj, attribute, user, Window(io, range, processed), node, scope),
            _ => throw new KsyParseException($"{attribute.Path}: Unsupported data type"),
        };
    }

    private static KsStream Window(KsStream io, KsRange range, byte[]? processed) =>
        processed is null ? io.Substream(range.Start, range.Length) : KsStream.FromBytes(processed);

    private static KsRange ReadRange(KsyAttribute attribute, KsStream io, IKsEvalScope scope)
    {
        KsRange raw;
        if (attribute.Size is { } size)
            raw = io.ReadRange(EvaluateLong(size, scope, attribute.Path, "size"));
        else if (attribute.SizeEos)
            raw = io.ReadToEnd();
        else if (attribute.Terminator is { } terminator)
            return io.ReadTerminated(terminator, attribute.Include, attribute.Consume, attribute.EosError);
        else
            throw new KsyParseException($"{attribute.Path}: Size is not specified");
        return io.Truncate(raw, attribute.PadRight, attribute.Terminator, attribute.Include);
    }

    private byte[] Load(KsStream io, KsRange range, KsyAttribute attribute)
    {
        if (range.Length > options.MaxBytesLoad)
        {
            throw new KsyParseException(
                $"{attribute.Path}: Byte array of {range.Length} bytes is too large to load (limit {options.MaxBytesLoad})");
        }
        return io.ReadArray(range);
    }

    private byte[] Process(KsyProcess process, byte[] data, IKsEvalScope scope, KsyAttribute attribute)
    {
        var argument = process.Argument is { } expr ? Evaluate(expr, scope, attribute.Path, "process") : KsValue.Null;
        try
        {
            return process.Kind switch
            {
                KsyProcessKind.Zlib => KsProcess.Zlib(data, options.MaxDecompressedSize),
                KsyProcessKind.Xor => KsProcess.Xor(data, argument),
                KsyProcessKind.Rol => KsProcess.Rotate(data, argument, true),
                _ => KsProcess.Rotate(data, argument, false),
            };
        }
        catch (KsExpressionException e)
        {
            throw ExpressionError(attribute.Path, "process", e);
        }
    }

    private static KsValue ReadContents(KsStream io, byte[] expected, KsyNode node)
    {
        var actual = io.ReadArray(io.ReadRange(expected.Length));
        if (!actual.AsSpan().SequenceEqual(expected))
        {
            node.Error =
                $"Contents mismatch: expected {KsyDescriptions.Hex(expected)}, got {KsyDescriptions.Hex(actual)}";
        }
        return KsValue.FromBytes(actual);
    }
}
