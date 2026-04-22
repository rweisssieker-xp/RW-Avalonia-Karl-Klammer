using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Bündelt lesende UIA-Informationen zum Vordergrundfenster (Form-/Kontext-Summary, §15/§2 Handbuch).
/// </summary>
public static class ForegroundUiAutomationContext
{
    private const int DefaultMaxChars = 12_000;

    /// <summary>
    /// Kompakte Liste interessanter Steuerelemente (Edit, Button, …) bis <paramref name="maxItems"/> Einträge.
    /// </summary>
    public static string BuildFormSummary(NexusSettings settings, int maxItems = 48, int maxDepth = 14)
    {
        if (!OperatingSystem.IsWindows())
            return "(not Windows)";

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return "(UIA form summary: requires safety profile “power-user”)";

        try
        {
            var root = AutomationElement.FromHandle(UiAutomationSnapshotHelpers.GetForegroundWindow());
            if (root == null)
                return "(no foreground automation root)";

            var lines = new List<string>(maxItems + 8);
            lines.Add($"Window: “{root.Current.Name}” · class hint · depth≤{maxDepth}");

                var acc = new List<string>(maxItems);
                Walk(root, 0, maxDepth, maxItems, acc);
                var focus = TryBuildFocusedControl(settings, root);
                if (!string.IsNullOrWhiteSpace(focus))
                    lines.Add("Focused control: " + focus);
                foreach (var line in acc)
                    lines.Add(line);

            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (sb.Length + line.Length + 1 > DefaultMaxChars)
                {
                    sb.AppendLine("… (truncated)");
                    break;
                }

                sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return "(UIA summary error: " + ex.Message + ")";
        }
    }

    public static string? BuildDeepSelectionSummary(NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var root = AutomationElement.FromHandle(UiAutomationSnapshotHelpers.GetForegroundWindow());
            if (root == null)
                return null;

            var sb = new StringBuilder();
            var gridHints = new List<string>(6);
            CollectSelectionFromGrid(root, gridHints, 4);
            if (gridHints.Count > 0)
            {
                sb.AppendLine("Grid/list selection:");
                foreach (var g in gridHints)
                    sb.AppendLine("  - " + g);
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Kurze Zeile zu Grid-/Listen-Selektion, falls <see cref="SelectionPattern"/> oder <see cref="TablePattern"/> greift.
    /// </summary>
    public static string? TryReadSelectionHint(NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var root = AutomationElement.FromHandle(UiAutomationSnapshotHelpers.GetForegroundWindow());
            if (root == null)
                return null;

            var selHost = root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.IsSelectionPatternAvailableProperty, true));
            if (selHost != null
                && selHost.TryGetCurrentPattern(SelectionPattern.Pattern, out var spObj)
                && spObj is SelectionPattern sp)
            {
                var sel = sp.Current.GetSelection();
                if (sel == null || sel.Length == 0)
                    return "Selection: (empty)";

                var names = new List<string>(Math.Min(sel.Length, 8));
                foreach (AutomationElement a in sel)
                {
                    try
                    {
                        names.Add(a.Current.Name ?? "(unnamed)");
                    }
                    catch
                    {
                        names.Add("(?)");
                    }

                    if (names.Count >= 8)
                        break;
                }

                return "Selection: " + string.Join(" · ", names);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void Walk(AutomationElement el, int depth, int maxDepth, int maxItems, List<string> acc)
    {
        if (acc.Count >= maxItems || depth > maxDepth)
            return;

        try
        {
            var ct = el.Current.ControlType;
            if (IsInteresting(ct))
            {
                var name = el.Current.Name ?? "";
                var aid = el.Current.AutomationId ?? "";
                var line = $"{DepthIndent(depth)}{ControlLabel(ct)} · id={aid} · name={Truncate(name, 120)}";
                acc.Add(line);
            }
        }
        catch
        {
            /* transient UIA */
        }

        AutomationElement? child;
        try
        {
            child = TreeWalker.RawViewWalker.GetFirstChild(el);
        }
        catch
        {
            return;
        }

        while (child != null && acc.Count < maxItems)
        {
            Walk(child, depth + 1, maxDepth, maxItems, acc);
            try
            {
                child = TreeWalker.RawViewWalker.GetNextSibling(child);
            }
            catch
            {
                break;
            }
        }
    }

    private static string? TryBuildFocusedControl(NexusSettings settings, AutomationElement root)
    {
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null)
                return null;
            if (focused.Current.ProcessId != root.Current.ProcessId)
                return null;
            var name = focused.Current.Name ?? "(unnamed)";
            var type = focused.Current.ControlType?.LocalizedControlType ?? "control";
            var aid = focused.Current.AutomationId ?? "";
            return $"{type} · id={aid} · name={Truncate(name, 120)}";
        }
        catch
        {
            return null;
        }
    }

    private static void CollectSelectionFromGrid(
        AutomationElement root,
        List<string> entries,
        int maxGrids,
        int maxItemsPerGrid = 5)
    {
        if (entries.Count >= maxGrids)
            return;
        AutomationElement? node = null;
        try
        {
            node = TreeWalker.RawViewWalker.GetFirstChild(root);
        }
        catch
        {
            return;
        }

        while (node != null && entries.Count < maxGrids)
        {
            try
            {
                var ct = node.Current.ControlType;
                if (ct == ControlType.DataGrid || ct == ControlType.Table)
                {
                    var name = node.Current.Name ?? "(unnamed grid)";
                    var selected = TryReadSelectedNames(node, maxItemsPerGrid);
                    if (!string.IsNullOrWhiteSpace(selected))
                        entries.Add($"{name}: {selected}");
                }
                else if (ct == ControlType.List || ct == ControlType.ListItem)
                {
                    var name = node.Current.Name ?? "(unnamed list)";
                    var selected = TryReadSelectedNames(node, maxItemsPerGrid);
                    if (!string.IsNullOrWhiteSpace(selected))
                        entries.Add($"{name}: {selected}");
                }
            }
            catch
            {
                // ignore transient UIA
            }

            if (entries.Count >= maxGrids)
                break;

            AutomationElement? child;
            try
            {
                child = node.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table));
            }
            catch
            {
                child = null;
            }

            if (child != null && entries.Count < maxGrids)
                CollectSelectionFromGrid(child, entries, maxGrids, maxItemsPerGrid);

            try
            {
                node = TreeWalker.RawViewWalker.GetNextSibling(node);
            }
            catch
            {
                break;
            }
        }
    }

    private static string? TryReadSelectedNames(AutomationElement host, int maxItems)
    {
        try
        {
            if (!host.TryGetCurrentPattern(SelectionPattern.Pattern, out var p) || p is not SelectionPattern sp)
                return null;
            var sel = sp.Current.GetSelection();
            if (sel == null || sel.Length == 0)
                return null;

            var names = new List<string>(Math.Min(sel.Length, maxItems));
            foreach (AutomationElement a in sel)
            {
                names.Add(string.IsNullOrWhiteSpace(a.Current.Name) ? "(unnamed)" : a.Current.Name);
                if (names.Count >= maxItems)
                    break;
            }

            if (names.Count == 0)
                return null;
            return string.Join(" · ", names);
        }
        catch
        {
            return null;
        }
    }

    private static string DepthIndent(int d)
    {
        if (d <= 0)
            return "";
        return new string(' ', Math.Min(d * 2, 24));
    }

    private static bool IsInteresting(ControlType ct)
    {
        return ct == ControlType.Edit
               || ct == ControlType.Button
               || ct == ControlType.CheckBox
               || ct == ControlType.RadioButton
               || ct == ControlType.ComboBox
               || ct == ControlType.List
               || ct == ControlType.ListItem
               || ct == ControlType.DataItem
               || ct == ControlType.TabItem
               || ct == ControlType.MenuItem
               || ct == ControlType.Hyperlink
               || ct == ControlType.Text;
    }

    private static string ControlLabel(ControlType ct)
    {
        try
        {
            return ct.LocalizedControlType;
        }
        catch
        {
            return "control";
        }
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max)
            return s;
        return s[..max] + "…";
    }
}
