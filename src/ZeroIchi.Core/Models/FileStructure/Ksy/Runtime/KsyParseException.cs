using System;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

public sealed class KsyParseException(FileStructureError error) : Exception(error.Message)
{
    public KsyParseException(string message) : this(FileStructureError.Other(message))
    {
    }

    public FileStructureError Error { get; } = error;
    internal bool IsBudget { get; init; }
    internal bool IsLimit { get; init; }
}
