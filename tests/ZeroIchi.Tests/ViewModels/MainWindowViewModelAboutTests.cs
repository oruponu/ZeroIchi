using ZeroIchi.Services;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

public sealed class MainWindowViewModelAboutTests
{
    [Fact]
    public async Task ShowAbout_OpensTheAboutDialog()
    {
        var dialog = new StubDialogService();
        var vm = CreateViewModel(dialog);

        await vm.ShowAboutCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.AboutCount);
    }

    private sealed class StubDialogService : IDialogService
    {
        public int AboutCount { get; private set; }

        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;

        public Task ShowAboutAsync()
        {
            AboutCount++;
            return Task.CompletedTask;
        }
    }
}
