using ZeroIchi.Models;

namespace ZeroIchi.Tests.Models;

public sealed class SettingsManagerTests
{
    private static readonly AppSettings English = new() { Language = "en" };

    [Fact]
    public void Constructor_LoadsBothValues()
    {
        var manager = new SettingsManager(new StubSettingsStore { Loaded = English });

        Assert.Equal(English, manager.Current);
        Assert.Equal(English, manager.Saved);
        Assert.False(manager.HasUnsavedChanges);
    }

    [Fact]
    public void Change_UpdatesOnlyTheCurrentValue()
    {
        var store = new StubSettingsStore();
        var manager = new SettingsManager(store);

        manager.Change(English);

        Assert.Equal(English, manager.Current);
        Assert.Equal(AppSettings.Default, manager.Saved);
        Assert.True(manager.HasUnsavedChanges);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Save_Succeeds_AlignsTheSavedValue()
    {
        var store = new StubSettingsStore();
        var manager = new SettingsManager(store);
        manager.Change(English);

        manager.Save();

        Assert.Equal(English, store.Saved);
        Assert.Equal(English, manager.Saved);
        Assert.False(manager.HasUnsavedChanges);
    }

    [Fact]
    public void Save_Fails_KeepsTheSavedValueAndThrows()
    {
        var manager = new SettingsManager(new StubSettingsStore { FailSave = true });
        manager.Change(English);

        Assert.Throws<IOException>(manager.Save);

        Assert.Equal(AppSettings.Default, manager.Saved);
        Assert.True(manager.HasUnsavedChanges);
    }
}
