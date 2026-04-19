using System;
using System.Linq;
using Avalonia.Controls;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class SetupTab : UserControl
{
    public SetupTab()
    {
        InitializeComponent();
        ProviderBox.ItemsSource = new[] { "anthropic", "openai", "openai-compatible" };
        ModeBox.ItemsSource = new[] { "companion", "agent", "automation", "watch" };
        UiThemeBox.ItemsSource = new[] { "Dark", "Light", "Default" };
        SafetyProfile.ItemsSource = new[] { "strict", "balanced", "power-user" };
    }

    public void Apply(NexusSettings s)
    {
        ProviderBox.SelectedItem = s.Provider;
        ModeBox.SelectedItem = s.Mode;
        ModelBox.Text = s.Model;
        UiThemeBox.SelectedItem = string.IsNullOrWhiteSpace(s.UiTheme) ? "Dark" : s.UiTheme;
        SpeakResponses.IsChecked = s.SpeakResponses;
        UseLocalKnowledge.IsChecked = s.UseLocalKnowledge;
        SuggestAutomations.IsChecked = s.SuggestAutomations;
        IncludeUiaInAsk.IsChecked = s.IncludeUiaContextInAsk;
        SafetyProfile.SelectedItem = s.Safety.Profile;
        NeverAutoSend.IsChecked = s.Safety.NeverAutoSend;
        NeverAutoPost.IsChecked = s.Safety.NeverAutoPostBook;
        PanicStop.IsChecked = s.Safety.PanicStopEnabled;
        DenylistBox.Text = s.Safety.Denylist;
        WatchIntervalBox.Text = s.WatchSnapshotIntervalSeconds.ToString();
        ProactiveLlm.IsChecked = s.ProactiveDashboardLlm;
        ProactiveIntervalBox.Text = s.ProactiveLlmMinIntervalSeconds.ToString();
        EnableToolHost.IsChecked = s.EnableLocalToolHost;
        ToolHostPortBox.Text = s.LocalToolHostPort.ToString();
    }

    public NexusSettings Gather()
    {
        static int ParseInt(string? text, int fallback, int lo, int hi) =>
            int.TryParse(text?.Trim(), out var v) ? Math.Clamp(v, lo, hi) : fallback;

        return new NexusSettings
        {
            Provider = ProviderBox.SelectedItem?.ToString() ?? "anthropic",
            Mode = ModeBox.SelectedItem?.ToString() ?? "companion",
            Model = ModelBox.Text?.Trim() ?? "",
            UiTheme = UiThemeBox.SelectedItem?.ToString() ?? "Dark",
            SpeakResponses = SpeakResponses.IsChecked == true,
            UseLocalKnowledge = UseLocalKnowledge.IsChecked == true,
            SuggestAutomations = SuggestAutomations.IsChecked == true,
            IncludeUiaContextInAsk = IncludeUiaInAsk.IsChecked == true,
            WatchSnapshotIntervalSeconds = ParseInt(WatchIntervalBox.Text, 45, 15, 600),
            ProactiveDashboardLlm = ProactiveLlm.IsChecked == true,
            ProactiveLlmMinIntervalSeconds = ParseInt(ProactiveIntervalBox.Text, 180, 60, 3600),
            EnableLocalToolHost = EnableToolHost.IsChecked == true,
            LocalToolHostPort = ParseInt(ToolHostPortBox.Text, 17888, 1024, 65535),
            Safety = new SafetySettings
            {
                Profile = SafetyProfile.SelectedItem?.ToString() ?? "balanced",
                NeverAutoSend = NeverAutoSend.IsChecked == true,
                NeverAutoPostBook = NeverAutoPost.IsChecked == true,
                PanicStopEnabled = PanicStop.IsChecked == true,
                Denylist = DenylistBox.Text ?? "",
            },
        };
    }

    public void RefreshEnvSummary()
    {
        EnvPathHint.Text = AppPaths.EnvFile;
        var keys = DotEnvSummary.ListKeys(AppPaths.EnvFile);
        EnvSummary.Text = keys.Count == 0
            ? "(keine .env oder leer — Vorlage: windows\\.env.example)"
            : string.Join("\r\n", keys.Select(k => k + "=***"));
    }
}
