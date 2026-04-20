using System;
using System.Text;

namespace CarolusNexus.Services;

/// <summary>UIA-Baum + dokumentierte Vision-Kombination für Ask-Prompts (Tier B).</summary>
public static class UiAutomationVisionFusion
{
    /// <summary>
    /// Baut den Textblock vor dem User-Prompt. Vision-Bilder werden separat vom Provider-Path angehängt;
    /// hier nur Hinweis + UIA (truncation wie in <see cref="UiAutomationSnapshot"/>).
    /// </summary>
    public static string BuildAskAugmentation(CarolusNexus.Models.NexusSettings settings, bool visionScreenshotsRequested)
    {
        var sb = new StringBuilder();
        if (settings.IncludeUiaContextInAsk && OperatingSystem.IsWindows())
        {
            var uia = UiAutomationSnapshot.TryBuildForForeground();
            if (!string.IsNullOrWhiteSpace(uia))
            {
                sb.AppendLine("[Foreground UI structure (UIA, truncated — depth/node/char caps in UiAutomationSnapshot)]");
                sb.AppendLine(uia.TrimEnd());
            }
        }

        if (visionScreenshotsRequested && OperatingSystem.IsWindows())
        {
            sb.AppendLine(
                "[Vision: one PNG per monitor may be attached by the client; primary monitor is first. " +
                "Combine with UIA labels when reasoning about controls.]");
        }

        return sb.ToString().Trim();
    }
}
