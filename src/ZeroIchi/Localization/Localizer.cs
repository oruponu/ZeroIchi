using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace ZeroIchi.Localization;

public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IndexerChangedArgs = new("Item");
    private static readonly PropertyChangedEventArgs CultureChangedArgs = new(nameof(Culture));

    public static Localizer Instance { get; } = new();

    private Localizer()
    {
        Strings.Culture ??= CultureInfo.CurrentUICulture;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CultureChanged;

    public CultureInfo Culture => Strings.Culture!;

    public string this[string key] => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;

    public void SetCulture(CultureInfo culture)
    {
        Dispatcher.UIThread.VerifyAccess();
        Strings.Culture = culture;
        PropertyChanged?.Invoke(this, IndexerChangedArgs);
        PropertyChanged?.Invoke(this, CultureChangedArgs);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public IObservable<string> GetObservable(string key) => new LocalizedStringObservable(this, key);

    private sealed class LocalizedStringObservable(Localizer localizer, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            var target = new WeakReference<IObserver<string>>(observer);
            EventHandler? onCultureChanged = null;
            onCultureChanged = (_, _) =>
            {
                if (target.TryGetTarget(out var alive))
                    alive.OnNext(localizer[key]);
                else
                    localizer.CultureChanged -= onCultureChanged;
            };
            localizer.CultureChanged += onCultureChanged;
            observer.OnNext(localizer[key]);
            return new Subscription(() => localizer.CultureChanged -= onCultureChanged);
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
