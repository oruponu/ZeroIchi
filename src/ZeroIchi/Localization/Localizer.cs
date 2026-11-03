using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace ZeroIchi.Localization;

public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IndexerChangedArgs = new("Item");

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
        Strings.Culture = culture;
        PropertyChanged?.Invoke(this, IndexerChangedArgs);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public IObservable<string> GetObservable(string key) => new LocalizedStringObservable(this, key);

    private sealed class LocalizedStringObservable(Localizer localizer, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            EventHandler onCultureChanged = (_, _) => observer.OnNext(localizer[key]);
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
