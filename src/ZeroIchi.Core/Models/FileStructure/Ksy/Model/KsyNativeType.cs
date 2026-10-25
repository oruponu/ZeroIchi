using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public sealed record KsyNativeType(IKsyNativeType Native, IReadOnlyList<KsExpr> Arguments) : KsyDataType;
