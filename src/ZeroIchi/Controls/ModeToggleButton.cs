using Avalonia.Controls.Primitives;
using System;

namespace ZeroIchi.Controls;

public sealed class ModeToggleButton : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
        if (IsChecked != true) base.Toggle();
    }
}
