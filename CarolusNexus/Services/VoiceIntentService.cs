using System;

namespace CarolusNexus.Services;

/// <summary>Leichtgewichtige Intent-Erkennung für Spracheingaben (vor LLM).</summary>
public static class VoiceIntentService
{
    public static bool TryResolve(string transcript, out VoiceIntentResult result)
    {
        result = default;
        var t = (transcript ?? "").Trim();
        if (t.Length == 0)
            return false;

        var lower = t.ToLowerInvariant();

        if (lower.Contains("smoke test", StringComparison.Ordinal) || lower == "smoke")
        {
            result = new VoiceIntentResult("smoke_test", "smoke test", false);
            return true;
        }

        if (lower.Contains("trockenlauf", StringComparison.Ordinal) ||
            lower.StartsWith("dry run", StringComparison.OrdinalIgnoreCase))
        {
            result = new VoiceIntentResult(
                "dry_run_hint",
                "Explain briefly: to dry-run an operator flow, open Operator flows, select a flow, and use dry run.",
                false);
            return true;
        }

        if (lower.Contains("stop listening", StringComparison.Ordinal) ||
            lower.Contains("halt", StringComparison.Ordinal) && lower.Length < 24)
        {
            result = new VoiceIntentResult("stop", null, true);
            return true;
        }

        return false;
    }

    public readonly struct VoiceIntentResult
    {
        public VoiceIntentResult(string kind, string? prefillPrompt, bool skipLlm)
        {
            Kind = kind;
            PrefillPrompt = prefillPrompt;
            SkipLlm = skipLlm;
        }

        public string Kind { get; }
        public string? PrefillPrompt { get; }
        public bool SkipLlm { get; }
    }
}
