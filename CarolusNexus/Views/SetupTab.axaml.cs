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
        AxBackendBox.ItemsSource = new[] { "foreground_uia", "odata", "com_bc" };
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
        ConversationMemory.IsChecked = s.ConversationMemoryEnabled;
        MemoryMaxCharsBox.Text = s.ConversationMemoryMaxChars.ToString();
        HighRiskSecondConfirm.IsChecked = s.HighRiskSecondConfirm;
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
        AxIntegrationEnabled.IsChecked = s.AxIntegrationEnabled;
        AxTenantLabel.Text = s.AxTestTenantLabel ?? "";
        AxBackendBox.SelectedItem = string.IsNullOrWhiteSpace(s.AxIntegrationBackend) ? "foreground_uia" : s.AxIntegrationBackend;
        AxODataBaseUrlBox.Text = s.AxODataBaseUrl ?? "";
        AxODataUseWinCred.IsChecked = s.AxODataUseDefaultCredentials;
        AxAifBaseUrlBox.Text = s.AxAifServiceBaseUrl ?? "";
        AxDataAreaBox.Text = s.AxDataAreaId ?? "";
        AxBcDllBox.Text = s.AxBusinessConnectorNetAssemblyPath ?? "";
        AxBcAosBox.Text = s.AxBcObjectServer ?? "";
        AxBcDbBox.Text = s.AxBcDatabase ?? "";
        AxBcLangBox.Text = string.IsNullOrWhiteSpace(s.AxBcLanguage) ? "en-us" : s.AxBcLanguage;
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
            ConversationMemoryEnabled = ConversationMemory.IsChecked == true,
            ConversationMemoryMaxChars = ParseInt(MemoryMaxCharsBox.Text, 8000, 2000, 32000),
            HighRiskSecondConfirm = HighRiskSecondConfirm.IsChecked != false,
            WatchSnapshotIntervalSeconds = ParseInt(WatchIntervalBox.Text, 45, 15, 600),
            ProactiveDashboardLlm = ProactiveLlm.IsChecked == true,
            ProactiveLlmMinIntervalSeconds = ParseInt(ProactiveIntervalBox.Text, 180, 60, 3600),
            EnableLocalToolHost = EnableToolHost.IsChecked == true,
            LocalToolHostPort = ParseInt(ToolHostPortBox.Text, 17888, 1024, 65535),
            AxIntegrationEnabled = AxIntegrationEnabled.IsChecked != false,
            AxTestTenantLabel = AxTenantLabel.Text?.Trim() ?? "",
            AxIntegrationBackend = AxBackendBox.SelectedItem?.ToString() ?? "foreground_uia",
            AxODataBaseUrl = AxODataBaseUrlBox.Text?.Trim() ?? "",
            AxODataUseDefaultCredentials = AxODataUseWinCred.IsChecked != false,
            AxAifServiceBaseUrl = AxAifBaseUrlBox.Text?.Trim() ?? "",
            AxDataAreaId = AxDataAreaBox.Text?.Trim() ?? "",
            AxBusinessConnectorNetAssemblyPath = AxBcDllBox.Text?.Trim() ?? "",
            AxBcObjectServer = AxBcAosBox.Text?.Trim() ?? "",
            AxBcDatabase = AxBcDbBox.Text?.Trim() ?? "",
            AxBcLanguage = string.IsNullOrWhiteSpace(AxBcLangBox.Text) ? "en-us" : AxBcLangBox.Text.Trim(),
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
            ? "(no .env or empty — template: windows\\.env.example)"
            : string.Join("\r\n", keys.Select(k => k + "=***"));
    }
}
