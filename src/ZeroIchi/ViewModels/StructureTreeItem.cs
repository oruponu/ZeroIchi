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
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F48771"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandIcon))]
    public partial bool IsExpanded { get; set; } = isExpanded;

    public FileStructureNode Node { get; } = node;
    public int Depth { get; } = depth;
    public bool HasChildren { get; } = node.HasChildren;

    public string Name => Node.IsUnparsed ? "未解析" : Node.Name;

    public string Description
    {
        get
        {
            var text = Node.Description;
            if (Node.IsIncomplete) text = text.Length == 0 ? "(未完了)" : $"{text} (未完了)";
            if (Node.Error is not { } error) return text;
            var message = FileStructureErrorText.Format(error);
            return text.Length == 0 ? message : $"{text}  {message}";
        }
    }

    public FontStyle FontStyle => Node.HasRange ? FontStyle.Normal : FontStyle.Italic;
    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    public IBrush DescriptionForeground => Node.Error is not null && !Node.IsUnparsed ? ErrorBrush : Node.ValueKind switch
    {
        ValueKind.Numeric => NumericBrush,
        ValueKind.Ascii => AsciiBrush,
        ValueKind.Bytes => BytesBrush,
        _ => DefaultBrush,
    };

    public ICommand? ToggleExpandCommand { get; init; }

    public string ExpandIcon => IsExpanded ? "\u25bc" : "\u25b6";
}
