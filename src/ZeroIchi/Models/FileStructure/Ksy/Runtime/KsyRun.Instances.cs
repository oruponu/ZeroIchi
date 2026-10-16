using System;
using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed partial class KsyRun
{
    public KsValue GetInstance(KsObject obj, int index)
    {
        var attribute = obj.Type.Instances[index];
        ref var state = ref obj.Instance(index);
        switch (state.Status)
        {
            case KsyInstanceStatus.Done:
                return state.Value;
            case KsyInstanceStatus.Failed:
                throw new KsyParseException(state.Error!);
            case KsyInstanceStatus.Evaluating:
                throw new KsyParseException($"{attribute.Path}: Cyclic reference to instance '{attribute.Id}'");
        }
        if (_frozen) throw new KsExpressionException($"Instance '{attribute.Id}' was not evaluated", -1, 0);

        EnterNesting();
        state.Status = KsyInstanceStatus.Evaluating;
        try
        {
            var value = attribute.Kind == KsyAttributeKind.ValueInstance
                ? EvaluateValueInstance(obj, index, attribute)
                : ReadParseInstance(obj, index, attribute);
            state.Value = value;
            state.Status = KsyInstanceStatus.Done;
            return value;
        }
        catch (Exception e) when (MarkFailed(obj, index, e))
        {
            throw;
        }
        finally
        {
            _depth--;
        }
    }

    private static bool MarkFailed(KsObject obj, int index, Exception e)
    {
        ref var state = ref obj.Instance(index);
        state.Status = KsyInstanceStatus.Failed;
        state.Error = ErrorOf(e);
        return false;
    }

    private KsValue EvaluateValueInstance(KsObject obj, int index, KsyAttribute attribute)
    {
        if (attribute.If is { } condition && !EvaluateBool(condition, obj, attribute.Path, "if")) return KsValue.Null;

        var node = NewNode(attribute.Id, attribute.Id, null, 0);
        obj.Node.Instances[index] = node;
        return Guard(node, () =>
        {
            var value = Evaluate(attribute.Value!, obj, attribute.Path, "value");
            if (attribute.Enum is { } enumType && value.Kind == KsValueKind.Int)
                value = KsValue.FromEnum(enumType, value.AsInt);
            node.Value = value;
            return value;
        });
    }

    private KsValue ReadParseInstance(KsObject obj, int index, KsyAttribute attribute)
    {
        if (attribute.If is { } condition && !EvaluateBool(condition, obj, attribute.Path, "if")) return KsValue.Null;

        var io = obj.Stream;
        if (attribute.Io is { } ioExpr)
        {
            var value = Evaluate(ioExpr, obj, attribute.Path, "io");
            io = value.Kind == KsValueKind.Stream && value.AsStream is KsStream stream
                ? stream
                : throw new KsyParseException(
                    $"{attribute.Path}/io: Expected a stream (got {KsValue.KindName(value.Kind)})");
        }

        KsStream.State? saved = attribute.Pos is null ? null : io.SaveState();
        var slot = new List<KsyNode>(1);
        try
        {
            if (attribute.Pos is { } pos) io.Seek(EvaluateLong(pos, obj, attribute.Path, "pos"));
            return ReadAttribute(obj, attribute, io, slot, obj);
        }
        finally
        {
            if (saved is { } state) io.RestoreState(state);
            if (slot.Count > 0) obj.Node.Instances[index] = slot[0];
        }
    }

    private void EvaluateForDisplay()
    {
        for (var i = 0; i < _objects.Count; i++)
        {
            var obj = _objects[i];
            if (obj.Node.IsIncomplete) continue;

            for (var j = 0; j < obj.Type.Instances.Count; j++)
            {
                if (obj.Instance(j).Status != KsyInstanceStatus.Unevaluated) continue;
                try
                {
                    GetInstance(obj, j);
                }
                catch (Exception e) when (e is not OutOfMemoryException && !IsLimit(e))
                {
                    if (obj.Node.Instances[j] is null)
                    {
                        var id = obj.Type.Instances[j].Id;
                        obj.Node.Instances[j] = new KsyNode(id, id, null, 0) { Error = ErrorOf(e), End = 0 };
                    }
                }
            }
        }
    }
}
