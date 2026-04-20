using System;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CarolusNexus_WinUI;

public static class WinUiShellState
{
    public static readonly SettingsStore SettingsStore = new();

    public static NexusSettings Settings { get; set; } = new();

    /// <summary>MainWindow sets this — status line under the title (Ready, PTT, …).</summary>
    public static Action<string>? SetStatusLine { get; set; }

    public static void SetStatus(string text) => SetStatusLine?.Invoke(text);

    /// <summary>Ask page: hotkey pressed (start mic).</summary>
    public static Action? OnPttPressed { get; set; }

    public static void RaisePttPressed() => OnPttPressed?.Invoke();

    /// <summary>True while recording and waiting for global hotkey release.</summary>
    public static Func<bool>? PttAwaitsHotkeyRelease { get; set; }

    /// <summary>Ask page: hotkey released → transcribe + ask.</summary>
    public static Func<Task>? OnPttReleasedAsync { get; set; }

    /// <summary>Last „refresh active app“ line (shared with Dashboard).</summary>
    public static string LiveContextLine { get; set; } = "";

    /// <summary>Set by <see cref="MainWindow"/> for pickers and tray.</summary>
    public static Window? MainWindowRef { get; set; }

    /// <summary>Main UI thread — proactive dashboard LLM updates marshal here.</summary>
    public static DispatcherQueue? UiDispatcher { get; set; }

    /// <summary>Registered by <c>SetupShellPage</c> while visible — used by header „save settings“.</summary>
    public static Func<NexusSettings>? TryGatherSettingsFromSetup { get; set; }

    /// <summary>Registered by <c>SetupShellPage</c> while visible — used by „refresh all“.</summary>
    public static Action<NexusSettings>? TryApplySettingsToSetup { get; set; }

    /// <summary>Registered by <c>SetupShellPage</c> — refresh .env key list after <c>DotEnvStore.Invalidate</c>.</summary>
    public static Action? TryRefreshSetupEnvSummary { get; set; }

    public static event Action<string>? GlobalLogLine;

    public static void RaiseLog(string line) => GlobalLogLine?.Invoke(line);

    public static void ApplyNexusContext(DispatcherQueue dq)
    {
        NexusContext.GetSettings = () => Settings;
        NexusContext.RunWin32StepOnUiThreadAsync = async work =>
        {
            var tcs = new TaskCompletionSource<string>();
            if (!dq.TryEnqueue(() =>
                {
                    try
                    {
                        tcs.SetResult(work());
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }))
                tcs.SetException(new InvalidOperationException("DispatcherQueue enqueue failed"));
            return await tcs.Task.ConfigureAwait(true);
        };

        NexusShell.AppendGlobalLog = RaiseLog;
    }
}
