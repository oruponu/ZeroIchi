namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

internal static class KsMethods
{
    public static KsValue Call(KsExpr node, KsValue target, string name, KsValue[] args)
    {
        if (target.Kind == KsValueKind.Null)
            throw KsEvaluator.Error(node, "Value is null");

        throw KsEvaluator.Error(node,
            $"{KsValue.KindName(target.Kind)} has no method '{name}' taking {args.Length} argument(s)");
    }
}
