using Avalonia.Platform.Storage;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ZeroIchi.Infrastructure;

public static class LauncherExtensions
{
    public static async Task TryLaunchUriAsync(this ILauncher launcher, Uri uri)
    {
        try
        {
            await launcher.LaunchUriAsync(uri);
        }
        catch (Win32Exception)
        {
        }
    }
}
