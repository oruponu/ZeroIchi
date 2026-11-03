using ZeroIchi.Services;

namespace ZeroIchi.Models;

public sealed class SettingsManager
{
    private readonly ISettingsStore _store;

    public SettingsManager(ISettingsStore store)
    {
        _store = store;
        Current = Saved = store.Load();
    }

    public AppSettings Current { get; private set; }

    public AppSettings Saved { get; private set; }

    public bool HasUnsavedChanges => Current != Saved;

    public void Change(AppSettings settings) => Current = settings;

    public void Save()
    {
        var settings = Current;
        _store.Save(settings);
        Saved = settings;
    }
}
