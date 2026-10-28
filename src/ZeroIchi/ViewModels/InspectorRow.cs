using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeroIchi.ViewModels;

public sealed partial class InspectorRow(string label, string value) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    public partial string Value { get; set; } = value;
}
