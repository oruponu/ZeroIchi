using System.ComponentModel;
using System.Globalization;
using ZeroIchi.Localization;
using ZeroIchi.Models;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class SettingsDialogViewModelTests
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja");

    private readonly List<(string Message, string Detail)> _errors = [];

    private SettingsDialogViewModel CreateViewModel(SettingsManager settings) =>
        new(settings, (message, detail) =>
        {
            _errors.Add((message, detail));
            return Task.CompletedTask;
        });

    private static List<string?> RecordPropertyNames(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    private static Task RunInJapaneseAsync(Func<Task> action) => HeadlessTestApp.RunAsync(async () =>
    {
        try
        {
            await action();
        }
        finally
        {
            Localizer.Instance.SetCulture(Japanese);
        }
    });

    [Fact]
    public void Languages_AreSystemJapaneseEnglish()
    {
        using var vm = CreateViewModel(new SettingsManager(new StubSettingsStore()));

        Assert.Equal([null, "ja", "en"], vm.Languages.Select(option => option.Code));
        Assert.Equal(["システムの設定に従う", "日本語", "English"], vm.Languages.Select(option => option.DisplayName));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("en", "en")]
    [InlineData("JA", "ja")]
    [InlineData("fr", null)]
    public void Opening_SelectsTheCurrentLanguage(string? language, string? expected)
    {
        using var vm = CreateViewModel(new SettingsManager(new StubSettingsStore { Loaded = new AppSettings { Language = language } }));

        Assert.Equal(expected, vm.SelectedLanguage.Code);
    }

    [Fact]
    public void Opening_DoesNotSaveOrChangeTheCulture()
    {
        var store = new StubSettingsStore { Loaded = new AppSettings { Language = "en" } };

        using var vm = CreateViewModel(new SettingsManager(store));

        Assert.Equal(0, store.SaveCount);
        Assert.Equal("ja", Strings.Culture!.Name);
    }

    [Fact]
    public Task SelectingALanguage_AppliesAndSaves() => RunInJapaneseAsync(async () =>
    {
        var store = new StubSettingsStore();
        var settings = new SettingsManager(store);
        using var vm = CreateViewModel(settings);

        vm.SelectedLanguage = vm.Languages[2];
        await vm.PendingSave;

        Assert.Equal("en", Strings.Culture!.Name);
        Assert.Equal("en", store.Saved!.Language);
        Assert.False(settings.HasUnsavedChanges);
    });

    [Fact]
    public Task SelectingTheSystem_UsesTheSystemCulture() => RunInJapaneseAsync(async () =>
    {
        var store = new StubSettingsStore { Loaded = new AppSettings { Language = "en" } };
        using var vm = CreateViewModel(new SettingsManager(store));

        vm.SelectedLanguage = vm.Languages[0];
        await vm.PendingSave;

        Assert.Equal(CultureInfo.CurrentUICulture, Localizer.Instance.Culture);
        Assert.Null(store.Saved!.Language);
    });

    [Fact]
    public Task SaveFailure_ReportsTheErrorAndKeepsTheLanguage() => RunInJapaneseAsync(async () =>
    {
        var settings = new SettingsManager(new StubSettingsStore { FailSave = true });
        using var vm = CreateViewModel(settings);

        vm.SelectedLanguage = vm.Languages[2];
        await vm.PendingSave;

        Assert.Equal(("Could not save the settings", "The settings file is locked."), Assert.Single(_errors));
        Assert.Equal("en", Strings.Culture!.Name);
        Assert.True(settings.HasUnsavedChanges);
    });

    [Fact]
    public Task ReopeningAfterAFailure_SelectsTheSessionValue() => RunInJapaneseAsync(async () =>
    {
        var settings = new SettingsManager(new StubSettingsStore { FailSave = true });
        using (var vm = CreateViewModel(settings))
        {
            vm.SelectedLanguage = vm.Languages[2];
            await vm.PendingSave;
        }

        using var reopened = CreateViewModel(settings);

        Assert.Equal("en", reopened.SelectedLanguage.Code);
    });

    [Fact]
    public Task SaveIfChanged_AfterAFailure_SavesAgain() => RunInJapaneseAsync(async () =>
    {
        var store = new StubSettingsStore { FailSave = true };
        var settings = new SettingsManager(store);
        using var vm = CreateViewModel(settings);
        vm.SelectedLanguage = vm.Languages[2];
        await vm.PendingSave;
        store.FailSave = false;

        await vm.SaveIfChangedAsync();

        Assert.Equal("en", store.Saved!.Language);
        Assert.False(settings.HasUnsavedChanges);
    });

    [Fact]
    public async Task SaveIfChanged_WithoutChanges_DoesNotSave()
    {
        var store = new StubSettingsStore();
        using var vm = CreateViewModel(new SettingsManager(store));

        await vm.SaveIfChangedAsync();

        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public Task SaveIfChanged_FailsAgain_ReportsTheError() => RunInJapaneseAsync(async () =>
    {
        var store = new StubSettingsStore { FailSave = true };
        using var vm = CreateViewModel(new SettingsManager(store));
        vm.SelectedLanguage = vm.Languages[2];
        await vm.PendingSave;

        await vm.SaveIfChangedAsync();

        Assert.Equal(2, _errors.Count);
        Assert.Equal(2, store.SaveCount);
    });

    [Fact]
    public Task CultureChange_RefreshesTheSystemOptionName() => RunInJapaneseAsync(() =>
    {
        using var vm = CreateViewModel(new SettingsManager(new StubSettingsStore()));
        var names = RecordPropertyNames(vm.Languages[0]);

        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Use system setting", vm.Languages[0].DisplayName);
        Assert.Contains(nameof(LanguageOption.DisplayName), names);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Dispose_StopsRefreshingTheSystemOptionName() => RunInJapaneseAsync(() =>
    {
        var vm = CreateViewModel(new SettingsManager(new StubSettingsStore()));
        vm.Dispose();
        var names = RecordPropertyNames(vm.Languages[0]);

        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));

        Assert.DoesNotContain(nameof(LanguageOption.DisplayName), names);
        return Task.CompletedTask;
    });
}
