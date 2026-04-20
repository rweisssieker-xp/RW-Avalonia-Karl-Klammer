using System;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;
using Microsoft.UI.Dispatching;

namespace CarolusNexus_WinUI;

public static class WinUiShellState
{
    public static readonly SettingsStore SettingsStore = new();

    public static NexusSettings Settings { get; set; } = new();

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
