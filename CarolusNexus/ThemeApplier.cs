using System;
using Avalonia;
using Avalonia.Styling;

namespace CarolusNexus;

public static class ThemeApplier
{
    public static void ApplyUiTheme(string? theme)
    {
        var app = Application.Current;
        if (app == null)
            return;

        var t = (theme ?? "Dark").Trim();
        app.RequestedThemeVariant = t.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : t.Equals("Default", StringComparison.OrdinalIgnoreCase) || t.Equals("System", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Default
                : ThemeVariant.Dark;
    }
}
