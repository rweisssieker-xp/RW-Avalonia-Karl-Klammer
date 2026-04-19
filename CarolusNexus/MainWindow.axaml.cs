using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CarolusNexus.Models;
using CarolusNexus.Services;
using CarolusNexus.Views;

namespace CarolusNexus;

public partial class MainWindow : Window
{
    private KarlCompanionWindow? _companion;
    private readonly SettingsStore _settingsStore = new();
    private NexusSettings _settings = new();
    private readonly DispatcherTimer _dashboardTimer;
    private readonly DispatcherTimer _f8Timer;
    private bool _f8Down;

    public MainWindow()
    {
        InitializeComponent();
        AppPaths.DiscoverRepoRoot();
        AppPaths.EnsureDataTree();

        NexusShell.AppendGlobalLog = line => TabDiagnostics!.Append(line);

        _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _dashboardTimer.Tick += (_, _) => RefreshDashboard();
        _f8Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _f8Timer.Tick += OnF8Poll;

        Loaded += OnLoaded;
        Closing += (_, _) =>
        {
            _companion?.Close();
            _companion = null;
            _dashboardTimer.Stop();
            _f8Timer.Stop();
        };

        BtnRefreshAll.Click += OnRefreshAll;
        BtnSaveSettings.Click += OnSaveSettings;
        BtnReindex.Click += OnReindex;
        BtnRefreshApp.Click += OnRefreshApp;
        BtnExportDiag.Click += (_, _) => TabDiagnostics.ExportDiagnostics();

        _dashboardTimer.Start();
        _f8Timer.Start();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _settings = _settingsStore.LoadOrDefault();
        TabSetup.Apply(_settings);
        TabSetup.RefreshEnvSummary();
        RefreshDashboard();
        RefreshHeaderBadges();
        ApplyKarlCursor();

        if (!OperatingSystem.IsWindows())
        {
            CompanionToggle.IsChecked = false;
            CompanionToggle.IsEnabled = false;
        }
        else
        {
            _companion = new KarlCompanionWindow();
            CompanionToggle.IsCheckedChanged += OnCompanionToggleChanged;
            if (CompanionToggle.IsChecked == true)
                _companion.Show();
        }

        NexusShell.Log("Carolus Nexus gestartet — UI gemäß Handbuch; Provider/Automation/RAG sind Stubs.");
    }

    private void OnF8Poll(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows())
            return;
        var down = (Win32AsyncKey.GetAsyncKeyState(Win32AsyncKey.VkF8) & 0x8000) != 0;
        if (down && !_f8Down)
            NexusShell.LogStub("Push-to-Talk Hotkey F8 gedrückt (Aufnahme-Stub)");
        if (!down && _f8Down)
            NexusShell.LogStub("Push-to-Talk F8 losgelassen (STT+Ask Stub)");
        _f8Down = down;
    }

    private void OnCompanionToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_companion == null)
            return;
        if (CompanionToggle.IsChecked == true)
            _companion.Show();
        else
            _companion.Hide();
    }

    private void ApplyKarlCursor()
    {
        var karl = KarlCursorFactory.Create();
        Cursor = karl;
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
            button.Cursor = karl;
        foreach (var scene in this.GetVisualDescendants().OfType<OfficeScene3D>())
            scene.Cursor = karl;
    }

    private void OnRefreshAll(object? sender, RoutedEventArgs e)
    {
        _settings = _settingsStore.LoadOrDefault();
        TabSetup.Apply(_settings);
        TabSetup.RefreshEnvSummary();
        TabKnowledge.RefreshList();
        TabHistory.Refresh();
        RefreshDashboard();
        RefreshHeaderBadges();
        NexusShell.LogStub("refresh all");
    }

    private void OnSaveSettings(object? sender, RoutedEventArgs e)
    {
        _settings = TabSetup.Gather();
        _settingsStore.Save(_settings);
        NexusShell.Log("settings.json gespeichert.");
        RefreshHeaderBadges();
    }

    private void OnReindex(object? sender, RoutedEventArgs e)
    {
        NexusShell.LogStub("reindex knowledge");
        TabKnowledge.RefreshList();
    }

    private void OnRefreshApp(object? sender, RoutedEventArgs e)
    {
        NexusShell.LogStub("refresh active app");
        TileLive.Text = $"Aktives Fenster (Stub) @ {DateTime.Now:T}";
    }

    private void RefreshHeaderBadges()
    {
        BadgeEnv.Text = $"Environment: {_settings.Provider} / {_settings.Mode}";
        BadgeKnow.Text = $"Knowledge: {(_settings.UseLocalKnowledge ? "ein" : "aus")}";
        TileEnv.Text = $"Provider {_settings.Provider}, Modell „{_settings.Model}“, Safety {_settings.Safety.Profile}";
        TileMemory.Text = $"RAG-Index: {AppPaths.KnowledgeIndex} — {(File.Exists(AppPaths.KnowledgeIndex) ? "vorhanden" : "noch nicht erzeugt")}";
    }

    private void RefreshDashboard()
    {
        var knowCount = Directory.Exists(AppPaths.KnowledgeDir)
            ? Directory.GetFiles(AppPaths.KnowledgeDir).Length
            : 0;
        static string Cap(string? s, int max = 3500)
        {
            if (string.IsNullOrEmpty(s)) return "(leer)";
            return s.Length <= max ? s : s[..max] + "\n…";
        }

        TabDashboard.RefreshSummaries(
            env: $"Provider: {_settings.Provider}\nModus: {_settings.Mode}\nModell: {_settings.Model}\n.env: {(DotEnvSummary.FileExists ? "ja" : "fehlt")}",
            know: $"Dateien in knowledge\\: {knowCount}\nIndex: {(File.Exists(AppPaths.KnowledgeIndex) ? "ja" : "nein")}",
            live: TileLive.Text ?? "—",
            proactive: "(Stub) Keine proaktiven Vorschläge ohne LLM-Backend.",
            gov: $"Profil: {_settings.Safety.Profile}\nPanic: {_settings.Safety.PanicStopEnabled}",
            rituals: Cap(File.Exists(AppPaths.AutomationRecipes) ? File.ReadAllText(AppPaths.AutomationRecipes) : null),
            watch: Cap(File.Exists(AppPaths.WatchSessions) ? File.ReadAllText(AppPaths.WatchSessions) : null)
        );
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnOpenHandbookClick(object? sender, RoutedEventArgs e)
    {
        var md = Path.Combine(AppPaths.RepoRoot, "docs", "Carolus-Nexus-Benutzerhandbuch.md");
        if (!File.Exists(md))
        {
            NexusShell.Log($"Handbuch nicht gefunden: {md}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = md,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            NexusShell.Log($"Handbuch öffnen fehlgeschlagen: {ex.Message}");
        }
    }
}
