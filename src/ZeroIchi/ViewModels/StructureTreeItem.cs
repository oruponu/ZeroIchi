using Avalonia;
using System.ComponentModel;
using System.Windows.Input;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.ViewModels;

public sealed class StructureTreeItem(FileStructureNode node, int depth, bool isExpanded = false)
    : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IsExpandedChanged = new(nameof(IsExpanded));
    private static readonly PropertyChangedEventArgs ExpandIconChanged = new(nameof(ExpandIcon));

    private bool _isExpanded = isExpanded;

    public FileStructureNode Node { get; } = node;
    public int Depth { get; } = depth;
    public bool HasChildren { get; } = node.HasChildren;
    public string Name => Node.Name;
    public string Description => Node.Description;
    public Thickness Indent => new(Depth * 16, 0, 0, 0);
    public ICommand? ToggleExpandCommand { get; init; }

    public string ExpandIcon => _isExpanded ? "\u25bc" : "\u25b6";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, IsExpandedChanged);
            PropertyChanged?.Invoke(this, ExpandIconChanged);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
