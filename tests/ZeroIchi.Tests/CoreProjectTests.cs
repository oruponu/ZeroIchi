using System.Xml.Linq;

namespace ZeroIchi.Tests;

public sealed class CoreProjectTests
{
    [Fact]
    public void CoreProject_ReferencesNoUiPackages()
    {
        var project = XDocument.Load(Path.Combine(ThirdPartyNoticesTests.FindRoot(), "src", "ZeroIchi.Core", "ZeroIchi.Core.csproj"));
        var packages = project.Descendants("PackageReference").Select(reference => reference.Attribute("Include")!.Value);

        Assert.DoesNotContain(packages, package =>
            package.StartsWith("Avalonia", StringComparison.Ordinal) ||
            package.StartsWith("CommunityToolkit", StringComparison.Ordinal));
    }
}
