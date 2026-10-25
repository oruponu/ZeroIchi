using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public enum KsySeverity
{
    Error,
    Warning,
}

public sealed record KsyDiagnostic(KsySeverity Severity, string File, int Line, int Column, string Path, string Message)
{
    public override string ToString() => $"{File}({Line},{Column}): {Severity} {Path}: {Message}";
}

public sealed record KsyLoadResult(KsyFormat? Format, IReadOnlyList<KsyDiagnostic> Diagnostics);
