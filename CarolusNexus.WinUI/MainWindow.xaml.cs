using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus_WinUI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualKey = Windows.System.VirtualKey;

namespace CarolusNexus_WinUI;

public sealed partial class MainWindow : Window
{
    private readonly Frame _frame = new();
    private readonly NavigationView _nav = new()
    {
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        IsSettingsVisible = false,
        PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
        OpenPaneLength = 280
    };

    private readonly TextBlock _bandBadge = new()
    {
        Margin = new Thickness(12, 0, 12, 8),
        FontSize = 12,
        Opacity = 0.85
    };

    public MainWindow()
    {
        InitializeComponent();

        _nav.MenuItems.Add(Mk("Ask", typeof(AskShellPage), Symbol.Message));
        _nav.MenuItems.Add(Mk("Dashboard", typeof(DashboardShellPage), Symbol.Home));
        _nav.MenuItems.Add(Mk("Setup", typeof(SetupShellPage), Symbol.Setting));
        _nav.MenuItems.Add(Mk("Knowledge", typeof(KnowledgeShellPage), Symbol.Bookmarks));
        _nav.MenuItems.Add(Mk("Rituals", typeof(RitualsShellPage), Symbol.AllApps));
        _nav.MenuItems.Add(Mk("History", typeof(HistoryShellPage), Symbol.Clock));
        _nav.MenuItems.Add(Mk("Diagnostics", typeof(DiagnosticsShellPage), Symbol.Remote));
        _nav.MenuItems.Add(Mk("Console", typeof(ConsoleShellPage), Symbol.Keyboard));
        _nav.MenuItems.Add(Mk("Live Context", typeof(LiveContextShellPage), Symbol.View));

        _nav.FooterMenuItems.Add(MkFooter("Command palette (Ctrl+P)"));

        _nav.Content = _frame;
        _nav.ItemInvoked += NavOnItemInvoked;
        _nav.Loaded += (_, _) =>
        {
            _nav.SelectedItem = _nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            if (_nav.SelectedItem is NavigationViewItem first && first.Tag is Type t)
                _frame.Navigate(t);
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(_bandBadge);
        root.Children.Add(_nav);
        Grid.SetRow(_nav, 1);

        RootGrid.Children.Add(root);

        RootGrid.SizeChanged += (_, e) =>
        {
            var w = e.NewSize.Width;
            if (w <= 0)
                return;
            var band = ResponsiveLayout.GetBand(w);
            _bandBadge.Text = $"Layout band: {band} ({(int)w}px)";
        };

        var paletteAccel = new KeyboardAccelerator
        {
            Key = VirtualKey.P,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        paletteAccel.Invoked += OnCommandPaletteAccelerator;
        _nav.KeyboardAccelerators.Add(paletteAccel);
    }

    private void OnCommandPaletteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = ShowCommandPaletteAsync();
    }

    private static NavigationViewItem Mk(string content, Type pageType, Symbol symbol) =>
        new()
        {
            Content = content,
            Icon = new SymbolIcon(symbol),
            Tag = pageType
        };

    private static NavigationViewItem MkFooter(string content) =>
        new()
        {
            Content = content,
            Icon = new SymbolIcon(Symbol.Find),
            Tag = "palette"
        };

    private void NavOnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not { } tag)
            return;
        if (tag is string s && s == "palette")
        {
            _ = ShowCommandPaletteAsync();
            return;
        }

        if (tag is Type t)
            _frame.Navigate(t);
    }

    private async Task ShowCommandPaletteAsync()
    {
        var pages = new (string Label, Type PageType)[]
        {
            ("Ask", typeof(AskShellPage)),
            ("Dashboard", typeof(DashboardShellPage)),
            ("Setup", typeof(SetupShellPage)),
            ("Knowledge", typeof(KnowledgeShellPage)),
            ("Rituals", typeof(RitualsShellPage)),
            ("History", typeof(HistoryShellPage)),
            ("Diagnostics", typeof(DiagnosticsShellPage)),
            ("Console", typeof(ConsoleShellPage)),
            ("Live Context", typeof(LiveContextShellPage))
        };

        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = pages.Select(p => p.Label).ToList() };
        var dlg = new ContentDialog
        {
            Title = "Go to page",
            Content = list,
            PrimaryButtonText = "Go",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        list.DoubleTapped += async (_, _) =>
        {
            dlg.Hide();
            await NavigateFromPaletteSelectionAsync(list, pages);
        };

        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
            await NavigateFromPaletteSelectionAsync(list, pages);
    }

    private Task NavigateFromPaletteSelectionAsync(ListView list, (string Label, Type PageType)[] pages)
    {
        if (list.SelectedIndex < 0)
            return Task.CompletedTask;
        var t = pages[list.SelectedIndex].PageType;
        _frame.Navigate(t);
        foreach (NavigationViewItem? mi in _nav.MenuItems.OfType<NavigationViewItem>())
        {
            if (mi.Tag is Type mt && mt == t)
            {
                _nav.SelectedItem = mi;
                break;
            }
        }

        return Task.CompletedTask;
    }
}
