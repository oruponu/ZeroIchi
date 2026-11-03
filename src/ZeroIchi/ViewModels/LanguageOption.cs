using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;
using ZeroIchi.Localization;

namespace ZeroIchi.ViewModels;

public sealed class LanguageOption(string? code) : ObservableObject
{
    public string? Code { get; } = code;

    public string DisplayName => Code is null ? Strings.SettingsLanguageSystem : CultureInfo.GetCultureInfo(Code).NativeName;

    public void RefreshText() => OnPropertyChanged(nameof(DisplayName));
}
