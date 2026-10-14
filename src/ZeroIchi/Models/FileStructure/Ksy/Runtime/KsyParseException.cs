using System;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

public sealed class KsyParseException(string message) : Exception(message)
{
    internal bool IsBudget { get; init; }
    internal bool IsLimit { get; init; }
}
