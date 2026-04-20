using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace CarolusNexus.Services;

/// <summary>Best-effort-Undo (z. B. Zwischenablage nach type-Versuch).</summary>
public static class UndoStackService
{
    private const int MaxEntries = 8;
    private static readonly Stack<UndoEntry> Stack = new();
    private static readonly object Gate = new();

    public static void PushClipboardText(string? previous)
    {
        lock (Gate)
        {
            while (Stack.Count >= MaxEntries)
                Stack.Pop();
            Stack.Push(new UndoEntry(UndoKind.ClipboardText, previous));
        }
    }

    /// <summary>Letzte Aktion rückgängig machen (STA für Clipboard).</summary>
    public static string? TryUndoLast()
    {
        UndoEntry? e;
        lock (Gate)
        {
            if (!Stack.TryPop(out var x))
                return "nothing to undo";
            e = x;
        }

        try
        {
            if (e.Kind == UndoKind.ClipboardText)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        if (e.Payload == null)
                            Clipboard.Clear();
                        else
                            Clipboard.SetText(e.Payload);
                    }
                    catch
                    {
                        /* ignore */
                    }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join(5000);
                return "clipboard restored";
            }
        }
        catch (Exception ex)
        {
            return "undo failed: " + ex.Message;
        }

        return "unknown undo kind";
    }

    private enum UndoKind
    {
        ClipboardText
    }

    private sealed record UndoEntry(UndoKind Kind, string? Payload);
}
