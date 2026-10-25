using System.Text.RegularExpressions;
using System.Xml.Linq;
using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void Current_MatchesTheProjectVersion()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot.FullName, "src", "ZeroIchi", "ZeroIchi.csproj"));

        Assert.Equal(project.Descendants("Version").Single().Value, AppVersion.Current);
    }

    [Fact]
    public void Current_HasNoBuildMetadata()
    {
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), AppVersion.Current);
    }
}
