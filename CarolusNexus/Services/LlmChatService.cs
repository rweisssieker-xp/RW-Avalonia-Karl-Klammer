using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class LlmChatService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(6)
    };

    public static async Task<string> CompleteAsync(
        NexusSettings settings,
        string userPrompt,
        bool includeScreenshots,
        bool useKnowledge,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return "Bitte einen Prompt eingeben.";

        var env = DotEnvStore.Load();
        var system = BuildSystemPrompt(settings);
        var augmented = userPrompt;
        if (useKnowledge)
        {
            var k = KnowledgeSnippetService.BuildContext();
            if (!string.IsNullOrWhiteSpace(k))
                augmented = "Kontext aus lokalem Wissen (Auszüge):\n\n" + k + "\n\n---\n\nNutzeranfrage:\n" + userPrompt;
        }

        return settings.Provider switch
        {
            "anthropic" => await CompleteAnthropicAsync(env, settings.Model, system, augmented, includeScreenshots, ct),
            "openai" => await CompleteOpenAiAsync(env, settings.Model, system, augmented, includeScreenshots, false, ct),
            "openai-compatible" => await CompleteOpenAiAsync(env, settings.Model, system, augmented, includeScreenshots, true, ct),
            _ => "Unbekannter Provider: " + settings.Provider
        };
    }

    public static async Task<string> SmokeAsync(NexusSettings settings, CancellationToken ct = default)
    {
        var env = DotEnvStore.Load();
        const string prompt = "Antworte exakt mit einem Wort: bereit";
        var system = "Du bist ein Healthcheck. Antworte nur mit einem Wort.";
        return settings.Provider switch
        {
            "anthropic" => await CompleteAnthropicAsync(env, settings.Model, system, prompt, false, ct),
            "openai" => await CompleteOpenAiAsync(env, settings.Model, system, prompt, false, false, ct),
            "openai-compatible" => await CompleteOpenAiAsync(env, settings.Model, system, prompt, false, true, ct),
            _ => "Unbekannter Provider."
        };
    }

    private static string BuildSystemPrompt(NexusSettings s)
    {
        var soul = SoulPrompt.LoadOrDefault();
        var mode = s.Mode switch
        {
            "agent" => "Modus: agent — aktionsorientiert, strukturierte Vorschläge.",
            "automation" => "Modus: automation — kurz, fokus auf reproduzierbare Schritte.",
            "watch" => "Modus: watch — protokolliere relevante Beobachtungen knapp.",
            _ => "Modus: companion — freundlich, klar, hilfreich."
        };
        return soul + "\n\n" + mode;
    }

    private static async Task<string> CompleteAnthropicAsync(
        IReadOnlyDictionary<string, string> env,
        string model,
        string system,
        string userText,
        bool includeScreenshots,
        CancellationToken ct)
    {
        if (!env.TryGetValue("ANTHROPIC_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
            return "Fehlt ANTHROPIC_API_KEY in windows\\.env.";

        var content = new List<object>();
        if (includeScreenshots && OperatingSystem.IsWindows())
        {
            foreach (var (label, b64) in ScreenCaptureWin.CaptureAllMonitorsPngBase64())
            {
                content.Add(new
                {
                    type = "image",
                    source = new { type = "base64", media_type = "image/png", data = b64 }
                });
                _ = label;
            }
        }

        content.Add(new { type = "text", text = userText });

        var body = new
        {
            model,
            max_tokens = 8192,
            system,
            messages = new object[]
            {
                new { role = "user", content = content.ToArray() }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.TryAddWithoutValidation("x-api-key", key);
        req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return $"Anthropic HTTP {(int)resp.StatusCode}: {json}";

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("content", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return json;
        var sb = new StringBuilder();
        foreach (var block in arr.EnumerateArray())
        {
            if (block.TryGetProperty("text", out var t))
                sb.Append(t.GetString());
        }

        return sb.Length > 0 ? sb.ToString() : json;
    }

    private static async Task<string> CompleteOpenAiAsync(
        IReadOnlyDictionary<string, string> env,
        string model,
        string system,
        string userText,
        bool includeScreenshots,
        bool _,
        CancellationToken ct)
    {
        if (!env.TryGetValue("OPENAI_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
            return "Fehlt OPENAI_API_KEY in windows\\.env.";

        var baseUrl = env.TryGetValue("OPENAI_BASE_URL", out var bu) && !string.IsNullOrWhiteSpace(bu)
            ? bu.TrimEnd('/')
            : "https://api.openai.com/v1";
        var url = baseUrl + "/chat/completions";

        var parts = new List<object>();
        if (includeScreenshots && OperatingSystem.IsWindows())
        {
            foreach (var (_, b64) in ScreenCaptureWin.CaptureAllMonitorsPngBase64())
            {
                parts.Add(new
                {
                    type = "image_url",
                    image_url = new { url = "data:image/png;base64," + b64 }
                });
            }
        }

        parts.Add(new { type = "text", text = userText });

        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new
                {
                    role = "user",
                    content = parts.ToArray()
                }
            },
            max_tokens = 8192
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return $"OpenAI-kompatibel HTTP {(int)resp.StatusCode}: {json}";

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var ch) || ch.GetArrayLength() == 0)
            return json;
        var msg = ch[0];
        if (!msg.TryGetProperty("message", out var m))
            return json;
        if (m.TryGetProperty("content", out var content))
        {
            if (content.ValueKind == JsonValueKind.String)
                return content.GetString() ?? json;
            if (content.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (var p in content.EnumerateArray())
                {
                    if (p.TryGetProperty("text", out var tx))
                        sb.Append(tx.GetString());
                }

                return sb.Length > 0 ? sb.ToString() : json;
            }
        }

        return json;
    }
}
