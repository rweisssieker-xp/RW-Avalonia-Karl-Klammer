using System;
using Microsoft.UI.Xaml;

namespace CarolusNexus_WinUI;

/// <summary>Maps settings UI theme string (Dark/Light/Default) to WinUI <see cref="ApplicationTheme"/>.</summary>
public static class WinUiThemeApplier
{
    public static void Apply(string? theme)
    {
        if (Application.Current is not App app)
            return;
        var t = (theme ?? "Dark").Trim();
        // WinUI ApplicationTheme has Light/Dark only; map Default/System to Light (readable in mixed shell).
        app.RequestedTheme = t.Equals("Dark", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }
}
