using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.ViewModels;

public sealed partial class StructureTreeItem(FileStructureNode node, int depth, bool isExpanded = false)
    : ObservableObject
{
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#808080"));
    private static readonly IBrush NumericBrush = new SolidColorBrush(Color.Parse("#B5CEA8"));
    private static readonly IBrush AsciiBrush = new SolidColorBrush(Color.Parse("#CE9178"));
    private static readonly IBrush BytesBrush = DefaultBrush;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandIcon))]
    private bool _isExpanded = isExpanded;

    public FileStructureNode Node { get; } = node;
    public int Depth { get; } = depth;
    public bool HasChildren { get; } = node.HasChildren;

    public string Name => Node.Name;
    public string Description => Node.Description;
    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    public IBrush DescriptionForeground => Node.ValueKind switch
    {
        ValueKind.Numeric => NumericBrush,
        ValueKind.Ascii => AsciiBrush,
        ValueKind.Bytes => BytesBrush,
        _ => DefaultBrush,
    };

    public ICommand? ToggleExpandCommand { get; init; }

    public string ExpandIcon => IsExpanded ? "\u25bc" : "\u25b6";
}
