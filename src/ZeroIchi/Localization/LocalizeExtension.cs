using Avalonia.Data;
using Avalonia.Markup.Xaml;
using System;

namespace ZeroIchi.Localization;

public sealed class LocalizeExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
}
