using System.ComponentModel;
using Avalonia.Platform.Storage;
using ZeroIchi.Infrastructure;

namespace ZeroIchi.Tests.Infrastructure;

public sealed class LauncherExtensionsTests
{
    private static readonly Uri Repository = new("https://github.com/oruponu/ZeroIchi");

    [Fact]
    public async Task TryLaunchUri_OpensTheUri()
    {
        var launcher = new StubLauncher();

        await launcher.TryLaunchUriAsync(Repository);

        Assert.Equal(Repository, launcher.Launched);
    }

    [Fact]
    public async Task TryLaunchUri_WhenNoAppCanOpenIt_DoesNotThrow()
    {
        var launcher = new StubLauncher { Failure = new Win32Exception(1155) };

        await launcher.TryLaunchUriAsync(Repository);
    }

    private sealed class StubLauncher : ILauncher
    {
        public Exception? Failure { get; init; }
        public Uri? Launched { get; private set; }

        public Task<bool> LaunchUriAsync(Uri uri)
        {
            if (Failure is not null) throw Failure;
            Launched = uri;
            return Task.FromResult(true);
        }

        public Task<bool> LaunchFileAsync(IStorageItem storageItem) => Task.FromResult(false);
    }
}
