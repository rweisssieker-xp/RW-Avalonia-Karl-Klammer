using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// „Screen-OCR“-Ersatz: sichtbarer Text aus Screenshots über den konfigurierten Vision-fähigen LLM-Provider
/// (<see cref="LlmChatService.ExtractVisibleScreenTextAsync"/>), nicht Windows-OCR.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ScreenOcrService
{
    public static string TryExtractVisibleTextViaLlm(NexusSettings settings, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return "[SKIP] screen capture for vision requires Windows";
        try
        {
            return ExtractVisibleTextViaLlmAsync(settings, ct).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return "[ERR] " + ex.Message;
        }
    }

    public static Task<string> ExtractVisibleTextViaLlmAsync(NexusSettings settings, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult("[SKIP] screen capture for vision requires Windows");
        return LlmChatService.ExtractVisibleScreenTextAsync(settings, ct);
    }
}
