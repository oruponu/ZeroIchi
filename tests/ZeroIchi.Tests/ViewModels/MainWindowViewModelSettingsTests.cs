using ZeroIchi.Services;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

public sealed class MainWindowViewModelSettingsTests
{
    [Fact]
    public async Task ShowSettings_OpensTheSettingsDialog()
    {
        var dialog = new StubDialogService();
        var vm = CreateViewModel(dialog);

        await vm.ShowSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.SettingsCount);
    }

    private sealed class StubDialogService : IDialogService
    {
        public int SettingsCount { get; private set; }

        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync(string fileName) => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;

        public Task ShowSettingsAsync()
        {
            SettingsCount++;
            return Task.CompletedTask;
        }

        public Task ShowAboutAsync() => Task.CompletedTask;
    }
}
