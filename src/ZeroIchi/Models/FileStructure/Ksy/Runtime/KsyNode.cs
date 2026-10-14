using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed class KsyNode(string name, string? fieldId, KsStream? stream, long start)
{
    public string Name { get; } = name;
    public string? FieldId { get; } = fieldId;
    public KsStream? Stream { get; } = stream;
    public long Start { get; } = start;
    public long? End { get; set; }
    public KsValue Value { get; set; }
    public bool IsContainer { get; set; }
    public string? Error { get; set; }
    public bool IsIncomplete { get; set; }
    public List<KsyNode> Children { get; } = [];
    public KsyNode?[] Instances { get; set; } = [];
    public FileStructureNode? Built { get; set; }
}
