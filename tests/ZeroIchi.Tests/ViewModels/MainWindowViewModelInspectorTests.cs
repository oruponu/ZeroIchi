using System.Text;
using ZeroIchi.Models;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

public sealed class MainWindowViewModelInspectorTests : IDisposable
{
    static MainWindowViewModelInspectorTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelInspectorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(int length)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, Enumerable.Range(0, length).Select(i => (byte)i).ToArray());
        return path;
    }

    private static void AssertRowsMatch(MainWindowViewModel vm)
    {
        var expected = DataInspector.Inspect(vm.Document!.Buffer, vm.CursorPosition, vm.IsInspectorBigEndian);
        Assert.Equal(expected, vm.InspectorRows.Select(r => new DataInspectorEntry(r.Label, r.Value)));
    }

    [Fact]
    public async Task CursorMove_UpdatesValuesWithoutReplacingRows()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(0x20));
        try
        {
            var rows = vm.InspectorRows.ToList();

            vm.CursorPosition = 1;

            Assert.Equal(rows.Count, vm.InspectorRows.Count);
            Assert.All(rows.Zip(vm.InspectorRows), pair => Assert.Same(pair.First, pair.Second));
            AssertRowsMatch(vm);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task CursorMove_NearEndOfFile_MatchesInspectedEntries()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(0x20));
        try
        {
            vm.CursorPosition = 0x1F;
            AssertRowsMatch(vm);

            vm.CursorPosition = 0x1D;
            AssertRowsMatch(vm);

            vm.CursorPosition = 0;
            AssertRowsMatch(vm);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task CursorMove_WhileInspectorHidden_ClearsRows()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(0x20));
        try
        {
            vm.IsInspectorVisible = false;
            vm.CursorPosition = 1;

            Assert.Empty(vm.InspectorRows);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
