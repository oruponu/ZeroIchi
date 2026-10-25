using ZeroIchi.Services;

namespace ZeroIchi.Tests;

internal sealed class StubDialogService : IDialogService
{
    public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
    public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
    public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
    public Task ShowAboutAsync() => Task.CompletedTask;
}

internal sealed class StubClipboardService : IClipboardService
{
    public Task SetBytesAsync(byte[] bytes, string? text) => Task.CompletedTask;
    public Task<byte[]?> TryGetBytesAsync() => Task.FromResult<byte[]?>(null);
    public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
}

internal sealed class StubWindowService : IWindowService
{
    public void Close() { }
}
