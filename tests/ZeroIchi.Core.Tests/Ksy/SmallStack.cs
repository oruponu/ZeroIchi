namespace ZeroIchi.Tests.Ksy;

internal static class SmallStack
{
    public static Exception? Run(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                caught = e;
            }
        }, 256 * 1024);
        thread.Start();
        thread.Join();
        return caught;
    }
}
