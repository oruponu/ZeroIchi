using System.Globalization;
using System.Reflection;
using System.Resources;
using ZeroIchi.Localization;

namespace ZeroIchi.Tests.Localization;

internal static class FakeEnglishStrings
{
    private static readonly FieldInfo ResourceManagerField =
        typeof(Strings).GetField("resourceMan", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The generated Strings class has no resourceMan field.");

    public static IDisposable Use()
    {
        var original = Strings.ResourceManager;
        ResourceManagerField.SetValue(null, new PrefixingResourceManager(original));
        return new Restorer(original);
    }

    private sealed class PrefixingResourceManager(ResourceManager inner) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture)
        {
            var value = inner.GetString(name, culture);
            return culture?.Name == "en-US" && value is not null ? "en:" + value : value;
        }

        public override string? GetString(string name) => GetString(name, null);
    }

    private sealed class Restorer(ResourceManager original) : IDisposable
    {
        public void Dispose() => ResourceManagerField.SetValue(null, original);
    }
}
