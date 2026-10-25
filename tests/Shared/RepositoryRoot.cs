namespace ZeroIchi.Tests;

internal static class RepositoryRoot
{
    public static string FullName { get; } = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZeroIchi.slnx"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("ZeroIchi.slnx was not found.");
    }
}
