using Avalonia;
using Avalonia.Markup.Xaml;
using System;

namespace ZeroIchi.Localization;

public sealed class LocalizeExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Localizer.Instance.GetObservable(key).ToBinding();
}
