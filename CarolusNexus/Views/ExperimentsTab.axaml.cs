using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CarolusNexus;
using CarolusNexus.Experiments;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class ExperimentsTab : UserControl
{
    private CancellationTokenSource? _cuCts;

    public ExperimentsTab()
    {
        InitializeComponent();
        TagLine.Text = TierCExperiments.Tag;
        BtnCuDryRun.Click += async (_, _) => await RunComputerUseSampleAsync(dryRun: true);
        BtnCuExecute.Click += async (_, _) => await RunComputerUseSampleAsync(dryRun: false);
    }

    private NexusSettings ResolveSettings() =>
        NexusContext.GetSettings?.Invoke() ?? new NexusSettings();

    private async Task RunComputerUseSampleAsync(bool dryRun)
    {
        _cuCts?.Cancel();
        _cuCts?.Dispose();
        _cuCts = new CancellationTokenSource();
        CuLogOut.Text = dryRun ? "Running dry-run…" : "Running (may simulate if not power-user)…";
        CompanionHub.Publish(CompanionVisualState.Thinking);
        ActivityStatusHub.RefreshFromStores();
        try
        {
            var log = await ComputerUseLoopService.RunThroughSimulatorAsync(
                ComputerUseLoopService.SampleTierCPlanSteps(),
                maxSteps: 3,
                dryRun,
                ResolveSettings(),
                _cuCts.Token);
            CuLogOut.Text = log;
        }
        catch (OperationCanceledException)
        {
            CuLogOut.Text = "(cancelled)";
        }
        catch (Exception ex)
        {
            CuLogOut.Text = "[ERR] " + ex.Message;
        }
        finally
        {
            CompanionHub.Publish(CompanionVisualState.Ready);
            ActivityStatusHub.RefreshFromStores();
        }
    }
}
