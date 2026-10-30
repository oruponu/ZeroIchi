using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZeroIchi.Tests.ViewModels;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowStructureTreeTests : IDisposable
{
    static MainWindowStructureTreeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const string Yaml = """
        meta:
          id: zi
        seq:
          - id: magic
            contents: ZI
          - id: hdr
            type: hdr
        types:
          hdr:
            seq:
              - id: len
                type: u1
        """;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowStructureTreeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task RunAsync(Action<MainWindowViewModel, MainWindow> action)
    {
        var path = Path.Combine(_tempDir, "a.bin");
        File.WriteAllBytes(path, "ZI\u0005"u8.ToArray());
        var vm = MainWindowViewModelStructureUpdateTests.Create(
            MainWindowViewModelStructureUpdateTests.Registry(null, Yaml), TimeSpan.Zero);
        await vm.OpenFileAsync(path);
        await vm.StructureUpdate;
        // The headless app has no theme, so the list creates no rows without one
        var theme = new FluentTheme();
        Application.Current!.Styles.Add(theme);
        var window = new MainWindow { DataContext = vm, Width = 900, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            action(vm, window);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
            window.Close();
            Application.Current!.Styles.Remove(theme);
        }
    }

    private static ListBoxItem Row(MainWindow window, string name) =>
        window.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Single(i => i.DataContext is StructureTreeItem { Name: var n } && n == name);

    private static void Click(MainWindow window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void SelectInsideMagic(MainWindowViewModel vm)
    {
        vm.CursorPosition = 1;
        vm.SelectionStart = 1;
        vm.SelectionLength = 1;
        Assert.Equal("magic", vm.SelectedStructureItem?.Name);
    }

    [Fact]
    public Task ClickingTheSelectedNode_SelectsItsRange() => HeadlessTestApp.RunAsync(() => RunAsync((vm, window) =>
    {
        SelectInsideMagic(vm);

        Click(window, Row(window, "magic"));

        Assert.Equal((0L, 0L, 2L), (vm.CursorPosition, vm.SelectionStart, vm.SelectionLength));
    }));

    [Fact]
    public Task ClickingTheExpandButton_KeepsTheSelection() => HeadlessTestApp.RunAsync(() => RunAsync((vm, window) =>
    {
        SelectInsideMagic(vm);

        Click(window, Row(window, "hdr").GetVisualDescendants().OfType<Button>().Single());

        Assert.Contains(vm.StructureTreeItems, i => i.Name == "len");
        Assert.Equal((1L, 1L, 1L), (vm.CursorPosition, vm.SelectionStart, vm.SelectionLength));
    }));
}
