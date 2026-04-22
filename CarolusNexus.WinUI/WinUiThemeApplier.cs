using System;
using Microsoft.UI.Xaml;

namespace CarolusNexus_WinUI;

/// <summary>Maps settings UI theme string (Dark/Light/Default) to WinUI <see cref="ApplicationTheme"/>.</summary>
public static class WinUiThemeApplier
{
    public static void Apply(string? themeMode, string? legacyTheme = null)
    {
        try
        {
            if (Application.Current is not App app)
                return;

            var mode = (themeMode ?? legacyTheme ?? "system").Trim();
            if (mode.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                app.RequestedTheme = ApplicationTheme.Light;
            }
            else if (mode.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                app.RequestedTheme = ApplicationTheme.Dark;
            }
            else if (mode.Equals("System", StringComparison.OrdinalIgnoreCase)
                     || mode.Equals("HighContrast", StringComparison.OrdinalIgnoreCase))
            {
                // Keep host/system default for System and HighContrast.
            }
        }
        catch
        {
            // Startup order / unpackaged: theme can fail before shell is ready; ignore.
        }
    }
}
