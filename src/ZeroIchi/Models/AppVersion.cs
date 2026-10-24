using System.Reflection;

namespace ZeroIchi.Models;

public static class AppVersion
{
    public static string Current { get; } = typeof(AppVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
}
