using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZeroIchi.Localization;
using ZeroIchi.Models;

namespace ZeroIchi.ViewModels;

public sealed partial class SettingsDialogViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsManager _settings;
    private readonly Func<string, string, Task> _reportError;
    private readonly bool _isInitialized;

    public SettingsDialogViewModel(SettingsManager settings, Func<string, string, Task> reportError)
    {
        _settings = settings;
        _reportError = reportError;
        Languages = [new LanguageOption(null), new LanguageOption(LanguageSetting.Japanese), new LanguageOption(LanguageSetting.English)];
        var current = LanguageSetting.Normalize(settings.Current.Language);
        SelectedLanguage = Languages.First(option => option.Code == current);
        Localizer.Instance.CultureChanged += OnCultureChanged;
        _isInitialized = true;
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    internal Task PendingSave { get; private set; } = Task.CompletedTask;

    public Task SaveIfChangedAsync() => _settings.HasUnsavedChanges ? SaveAsync() : Task.CompletedTask;

    public void Dispose() => Localizer.Instance.CultureChanged -= OnCultureChanged;

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (!_isInitialized || value is null) return;

        _settings.Change(_settings.Current with { Language = value.Code });
        Localizer.Instance.SetCulture(LanguageSetting.Resolve(value.Code));
        PendingSave = SaveAsync();
    }

    private async Task SaveAsync()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _reportError(Strings.SettingsSaveFailed, ex.Message);
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var option in Languages)
            option.RefreshText();
    }
}
