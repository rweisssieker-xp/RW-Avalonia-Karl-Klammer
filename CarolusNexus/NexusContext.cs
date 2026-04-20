using System;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus;

public static class NexusContext
{
    public static Func<NexusSettings> GetSettings { get; set; } = () => new();

    /// <summary>Optional: Win32-Automation auf UI-Thread (Avalonia/WinUI). Wenn null, synchron auf dem Aufrufer.</summary>
    public static Func<Func<string>, Task<string>>? RunWin32StepOnUiThreadAsync { get; set; }
}
