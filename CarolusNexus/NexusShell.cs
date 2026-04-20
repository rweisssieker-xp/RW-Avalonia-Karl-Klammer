using System;

namespace CarolusNexus;

/// <summary>Zentrale Stubs: globales Log für alle Tabs (Diagnostics-Anzeige).</summary>
public static class NexusShell
{
    public static Action<string>? AppendGlobalLog { get; set; }

    public static void Log(string message) =>
        AppendGlobalLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");

    public static void LogStub(string action) =>
        Log($"{action} — stub (no provider/automation backend wired).");
}
