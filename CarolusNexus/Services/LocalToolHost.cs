using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Lokaler HTTP-Endpunkt (127.0.0.1) für einfache Tool-Aufrufe — MCP-ähnliche Brücke ohne volles MCP-Protokoll.</summary>
public sealed class LocalToolHost : IDisposable
{
    private static readonly JsonSerializerOptions PlanJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Start(int port, string? bearerToken)
    {
        Stop();
        var p = Math.Clamp(port, 1024, 65535);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{p}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        var token = bearerToken;
        _loop = Task.Run(() => RunLoop(token, _cts.Token));
        NexusShell.Log(
            $"Local tool host: http://127.0.0.1:{p}/health · POST /v1/invoke — tools: echo, knowledge_snippet, foreground, screen_hash, agent_state, undo_last, screen_ocr (Vision-LLM), plan_dry_run, plan_run");
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch
        {
            /* ignore */
        }

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            /* ignore */
        }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public void Dispose() => Stop();

    private void RunLoop(string? expectedToken, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch (HttpListenerException)
            {
                if (ct.IsCancellationRequested)
                    break;
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                if (ct.IsCancellationRequested)
                    break;
                continue;
            }

            try
            {
                Handle(ctx, expectedToken);
            }
            catch (Exception ex)
            {
                TryWrite(ctx.Response, 500, "text/plain", ex.Message);
            }
            finally
            {
                try
                {
                    ctx.Response.OutputStream.Close();
                }
                catch
                {
                    /* ignore */
                }
            }
        }
    }

    private static void Handle(HttpListenerContext ctx, string? expectedToken)
    {
        var req = ctx.Request;
        var path = req.Url?.AbsolutePath ?? "/";
        if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            (path.Equals("/health", StringComparison.OrdinalIgnoreCase) || path.Equals("/", StringComparison.OrdinalIgnoreCase)))
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                app = "Carolus Nexus",
                version = AppBuildInfo.Version
            });
            TryWrite(ctx.Response, 200, "application/json", json);
            return;
        }

        if (!req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            !path.Equals("/v1/invoke", StringComparison.OrdinalIgnoreCase))
        {
            TryWrite(ctx.Response, 404, "text/plain", "not found");
            return;
        }

        if (!string.IsNullOrEmpty(expectedToken))
        {
            var got = req.Headers["X-Carolus-Token"];
            if (!string.Equals(got, expectedToken, StringComparison.Ordinal))
            {
                TryWrite(ctx.Response, 401, "application/json", """{"error":"unauthorized"}""");
                return;
            }
        }

        string body;
        using (var sr = new StreamReader(req.InputStream, req.ContentEncoding))
            body = sr.ReadToEnd();

        var result = InvokeTool(body);
        TryWrite(ctx.Response, 200, "application/json", result);
    }

    private static string InvokeTool(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            var tool = root.TryGetProperty("tool", out var t) ? t.GetString() ?? "" : "";
            var args = root.TryGetProperty("args", out var a) ? a : default;

            return tool.ToLowerInvariant() switch
            {
                "echo" => JsonSerializer.Serialize(new { ok = true, tool, argsKind = args.ValueKind.ToString() }),
                "knowledge_snippet" => KnowledgeInvoke(args),
                "foreground" => ForegroundInvoke(),
                "screen_hash" => HashInvoke(),
                "agent_state" => AgentStateInvoke(),
                "undo_last" => JsonSerializer.Serialize(new { ok = true, message = UndoStackService.TryUndoLast() }),
                "screen_ocr" => ScreenOcrInvoke(),
                "plan_dry_run" => PlanInvoke(args, dryRun: true),
                "plan_run" => PlanInvoke(args, dryRun: false),
                _ => """{"ok":false,"error":"unknown tool"}"""
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    private static string KnowledgeInvoke(JsonElement args)
    {
        var q = "";
        var max = 8000;
        if (args.ValueKind == JsonValueKind.Object)
        {
            if (args.TryGetProperty("query", out var qe))
                q = qe.GetString() ?? "";
            if (args.TryGetProperty("maxChars", out var mc) && mc.TryGetInt32(out var m))
                max = Math.Clamp(m, 500, 24_000);
        }

        var text = KnowledgeSnippetService.BuildContext(q, max);
        return JsonSerializer.Serialize(new { ok = true, chars = text.Length, text });
    }

    private static string ForegroundInvoke()
    {
        if (!OperatingSystem.IsWindows())
            return JsonSerializer.Serialize(new { ok = false, error = "Windows only" });
        var (title, proc) = ForegroundWindowInfo.TryRead();
        return JsonSerializer.Serialize(new { ok = true, process = proc, title });
    }

    private static string HashInvoke()
    {
        if (!OperatingSystem.IsWindows())
            return JsonSerializer.Serialize(new { ok = false, error = "Windows only" });
        try
        {
            var h = ScreenCaptureWin.PrimaryMonitorSha256Prefix16();
            return JsonSerializer.Serialize(new { ok = true, hashPrefix = h });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    private static string AgentStateInvoke()
    {
        var s = AgentRunStateStore.Snapshot();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            s.RunId,
            s.RecipeName,
            s.TotalSteps,
            s.CurrentStepIndex,
            s.LastError
        });
    }

    private static string ScreenOcrInvoke()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new SettingsStore().LoadOrDefault();
        var text = ScreenOcrService.TryExtractVisibleTextViaLlm(settings);
        return JsonSerializer.Serialize(new { ok = true, source = "vision_llm", text });
    }

    private static string PlanInvoke(JsonElement args, bool dryRun)
    {
        try
        {
            if (args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("steps", out var stepsEl) ||
                stepsEl.ValueKind != JsonValueKind.Array)
                return """{"ok":false,"error":"args.steps array required"}""";

            var steps = JsonSerializer.Deserialize<List<RecipeStep>>(stepsEl.GetRawText(), PlanJsonOpts);
            if (steps == null || steps.Count == 0)
                return """{"ok":false,"error":"no steps"}""";

            var settings = NexusContext.GetSettings?.Invoke() ?? new SettingsStore().LoadOrDefault();
            var log = SimplePlanSimulator.RunAsync(steps, dryRun, settings, null, CancellationToken.None).GetAwaiter()
                .GetResult();
            return JsonSerializer.Serialize(new { ok = true, log });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    private static void TryWrite(HttpListenerResponse resp, int code, string mime, string body)
    {
        resp.StatusCode = code;
        resp.ContentType = mime + "; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(body);
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }
}
