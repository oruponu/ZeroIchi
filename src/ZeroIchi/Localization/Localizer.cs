using System;
using System.ComponentModel;
using System.Globalization;

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
}
