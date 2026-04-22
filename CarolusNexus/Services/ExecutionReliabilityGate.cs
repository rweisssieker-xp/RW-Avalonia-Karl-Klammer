using System;
using System.Collections.Generic;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Sehr kleiner Circuit-Breaker für repetitive Executor-Fehler im Plan-Run.
/// </summary>
public static class ExecutionReliabilityGate
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Bucket> Buckets = new(StringComparer.OrdinalIgnoreCase);

    private sealed class Bucket
    {
        public int ConsecutiveFailures;
        public DateTimeOffset OpenUntil;
        public string LastFailure = "";
        public DateTimeOffset LastFailureAt;
    }

    private const int MaxConsecutiveFailures = 3;
    private static readonly TimeSpan CircuitOpenWindow = TimeSpan.FromSeconds(75);

    public static string CorrelationIdFor(RecipeStep step)
    {
        var a = (step?.ActionType ?? "").Trim().ToLowerInvariant();
        var arg = (step?.ActionArgument ?? "").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(arg)
            ? a
            : a + "→" + arg;
    }

    public static bool IsOpen(RecipeStep step, out string reason, out TimeSpan blockedFor)
    {
        reason = "";
        blockedFor = TimeSpan.Zero;
        var key = CorrelationIdFor(step);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        lock (Sync)
        {
            if (!Buckets.TryGetValue(key, out var b))
                return false;

            if (DateTimeOffset.UtcNow >= b.OpenUntil)
            {
                b.ConsecutiveFailures = 0;
                b.LastFailure = "";
                return false;
            }

            blockedFor = b.OpenUntil - DateTimeOffset.UtcNow;
            reason = $"[BLOCKED] circuit-open: {b.ConsecutiveFailures} failures in a row, last='{b.LastFailure}'";
            return true;
        }
    }

    public static bool IsHardFailureMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        (message.StartsWith("[ERR]", StringComparison.Ordinal) ||
         message.StartsWith("[BLOCKED]", StringComparison.Ordinal));

    public static void RecordResult(RecipeStep step, string resultMessage)
    {
        var key = CorrelationIdFor(step);
        if (string.IsNullOrWhiteSpace(key))
            return;

        lock (Sync)
        {
            if (!Buckets.TryGetValue(key, out var b))
            {
                b = new Bucket();
                Buckets[key] = b;
            }

            if (IsHardFailureMessage(resultMessage))
            {
                b.ConsecutiveFailures++;
                b.LastFailure = resultMessage;
                b.LastFailureAt = DateTimeOffset.UtcNow;
                if (b.ConsecutiveFailures >= MaxConsecutiveFailures)
                    b.OpenUntil = DateTimeOffset.UtcNow + CircuitOpenWindow;
            }
            else
            {
                b.ConsecutiveFailures = 0;
                b.LastFailure = "";
                b.OpenUntil = DateTimeOffset.MinValue;
            }
        }
    }
}
