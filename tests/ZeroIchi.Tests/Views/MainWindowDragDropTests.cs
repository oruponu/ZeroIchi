using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowDragDropTests
{
    private const DragDropEffects AllEffects = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;

    private static async Task<DragDropEffects?> DragAsync(RawDragEventType type, Func<MainWindow, Task<IDataTransfer>> createData)
    {
        var window = new MainWindow { DataContext = CreateViewModel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var data = await createData(window);
            DragDropEffects? effects = null;
            var routedEvent = type == RawDragEventType.DragEnter ? DragDrop.DragEnterEvent : DragDrop.DragOverEvent;
            window.AddHandler(routedEvent, (_, e) => effects = e.DragEffects, handledEventsToo: true);

            window.DragDrop(new Point(100, 100), RawDragEventType.DragEnter, data, AllEffects);
            if (type == RawDragEventType.DragOver)
                window.DragDrop(new Point(120, 100), RawDragEventType.DragOver, data, AllEffects);

            return effects;
        }
        finally
        {
            await HeadlessTestApp.CloseAsync(window);
        }
    }

    [Theory]
    [InlineData(RawDragEventType.DragEnter)]
    [InlineData(RawDragEventType.DragOver)]
    public Task DraggingText_IsNotAccepted(RawDragEventType type) => HeadlessTestApp.RunAsync(async () =>
    {
        var effects = await DragAsync(type, _ =>
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DataFormat.Text, "text"));
            return Task.FromResult<IDataTransfer>(data);
        });

        Assert.Equal(DragDropEffects.None, effects);
    });

    [Theory]
    [InlineData(RawDragEventType.DragEnter)]
    [InlineData(RawDragEventType.DragOver)]
    public Task DraggingAFile_IsAcceptedAsCopy(RawDragEventType type) => HeadlessTestApp.RunAsync(async () =>
    {
        var path = Path.GetTempFileName();
        try
        {
            var effects = await DragAsync(type, async window =>
            {
                var file = await window.StorageProvider.TryGetFileFromPathAsync(path);
                Assert.NotNull(file);
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create<IStorageItem>(DataFormat.File, file));
                return data;
            });

            Assert.Equal(DragDropEffects.Copy, effects);
        }
        finally
        {
            File.Delete(path);
        }
    });
}
