using System.ComponentModel;
using System.Globalization;
using System.Text;
using ZeroIchi.Localization;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelCultureTests : IDisposable
{
    static MainWindowViewModelCultureTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelCultureTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static void SwitchToEnglish() => Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));

    private static void SwitchToJapanese() => Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ja"));

    private static List<string?> RecordPropertyNames(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public Task CultureChange_RebuildsTheLiveTexts() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(new byte[16]));
        try
        {
            vm.SelectionStart = 0;
            vm.SelectionLength = 4;
            vm.GoToOffsetText = "zz";
            vm.GoToOffsetCommand.Execute(null);
            vm.IsHexSearch = true;
            vm.SearchText = "zz";
            var names = RecordPropertyNames(vm);

            SwitchToEnglish();

            Assert.Equal("Invalid value", vm.GoToOffsetError);
            Assert.Equal("Invalid hex", vm.SearchStatusText);
            Assert.Equal("00000000 - 00000003 (length 4)", vm.StatusBarPositionText);
            Assert.Contains(nameof(MainWindowViewModel.GoToOffsetError), names);
            Assert.Contains(nameof(MainWindowViewModel.SearchStatusText), names);
            Assert.Contains(nameof(MainWindowViewModel.StatusBarPositionText), names);
        }
        finally
        {
            SwitchToJapanese();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task CultureChange_RebuildsTheUntitledTitle() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateViewModel();
        try
        {
            var names = RecordPropertyNames(vm);

            SwitchToEnglish();

            Assert.Equal("Untitled - ZeroIchi", vm.Title);
            Assert.Contains(nameof(MainWindowViewModel.Title), names);
        }
        finally
        {
            SwitchToJapanese();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task CultureChange_RebuildsTheStructureTreeItemTexts() => HeadlessTestApp.RunAsync(async () =>
    {
        const string yaml = "meta:\n  id: zi\nseq:\n  - id: magic\n    contents: ZI\n  - id: len\n    type: u1\n";
        var registry = KsyTestRegistry.Create(KsyModelBuilder.Parse(yaml).Format!);
        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            KsyFormats = registry,
        };
        await vm.OpenFileAsync(CreateTestFile("ZI"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            var len = vm.StructureTreeItems.Single(i => i.Name == "len");
            var unparsed = vm.StructureTreeItems.Single(i => i.Node.IsUnparsed);
            Assert.Equal("(未完了)  データ不足: 位置 0x2 で 1 バイト必要 (残り 0 バイト)", len.Description);
            Assert.Equal("未解析", unparsed.Name);
            var lenNames = RecordPropertyNames(len);
            var unparsedNames = RecordPropertyNames(unparsed);

            SwitchToEnglish();

            Assert.Equal("(incomplete)  Not enough data at offset 0x2: needed 1, remaining 0", len.Description);
            Assert.Equal("Not parsed", unparsed.Name);
            Assert.Contains(nameof(StructureTreeItem.Description), lenNames);
            Assert.Contains(nameof(StructureTreeItem.Name), unparsedNames);
        }
        finally
        {
            SwitchToJapanese();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task CultureChange_AfterTheGoToOffsetErrorIsCleared_KeepsItEmpty() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(new byte[16]));
        try
        {
            vm.GoToOffsetText = "zz";
            vm.GoToOffsetCommand.Execute(null);
            vm.IsHexGoToOffset = false;
            Assert.Equal("", vm.GoToOffsetError);

            SwitchToEnglish();

            Assert.Equal("", vm.GoToOffsetError);
        }
        finally
        {
            SwitchToJapanese();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task CultureChange_WhenNotSaving_KeepsTheProgressTextEmpty() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile(new byte[16]));
        try
        {
            SwitchToEnglish();

            Assert.Equal("", vm.SaveProgressText);
        }
        finally
        {
            SwitchToJapanese();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task CultureChange_WithoutADocument_KeepsTheStatusBarEmpty() => HeadlessTestApp.RunAsync(() =>
    {
        var vm = CreateViewModel();
        vm.SelectionLength = 4;
        vm.Document = null;
        Assert.Equal("", vm.StatusBarPositionText);
        try
        {
            SwitchToEnglish();

            Assert.Equal("", vm.StatusBarPositionText);
        }
        finally
        {
            SwitchToJapanese();
        }
        return Task.CompletedTask;
    });
}
