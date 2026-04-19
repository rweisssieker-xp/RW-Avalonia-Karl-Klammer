using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace CarolusNexus.Services;

public static class TextToSpeechService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static async Task<string> SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Kein Text für TTS.";

        var env = DotEnvStore.Load();
        if (!env.TryGetValue("ELEVENLABS_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
            return "Fehlt ELEVENLABS_API_KEY in windows\\.env für TTS.";
        if (!env.TryGetValue("ELEVENLABS_VOICE_ID", out var voiceId) || string.IsNullOrWhiteSpace(voiceId))
            return "Fehlt ELEVENLABS_VOICE_ID in windows\\.env für TTS.";

        voiceId = voiceId.Trim();
        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("xi-api-key", key.Trim());
        req.Headers.TryAddWithoutValidation("Accept", "audio/mpeg");
        var body = JsonSerializer.Serialize(new { text, model_id = "eleven_multilingual_v2" });
        req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return $"ElevenLabs TTS HTTP {(int)resp.StatusCode}: {err}";
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        ms.Position = 0;

        await Task.Run(() => PlayMp3(ms.ToArray()), ct).ConfigureAwait(false);
        return "";
    }

    private static void PlayMp3(byte[] mp3)
    {
        using var ms = new MemoryStream(mp3, writable: false);
        using var reader = new Mp3FileReader(ms);
        using var wo = new WaveOutEvent();
        wo.Init(reader);
        wo.Play();
        while (wo.PlaybackState == PlaybackState.Playing)
            Thread.Sleep(80);
    }
}
