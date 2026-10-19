using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelFileTypeTests
{
    [Fact]
    public void WarmUpFileTypeDetection_DoesNotThrow()
    {
        var exception = Record.Exception(MainWindowViewModel.WarmUpFileTypeDetection);

        Assert.Null(exception);
    }
}
