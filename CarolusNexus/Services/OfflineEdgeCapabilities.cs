namespace CarolusNexus.Services;

/// <summary>Kurzbeschreibung Offline-/Edge-Verhalten für Setup und Diagnose.</summary>
public static class OfflineEdgeCapabilities
{
    public static string Describe()
    {
        return
            "Local-first: Win32/UIA/CV-Tokens laufen ohne Cloud. " +
            "STT/TTS: optional ElevenLabs/Whisper oder SAPI offline. " +
            "LLM/Vision: benötigt konfigurierten Provider außer rein lokalem openai-compatible (z. B. Ollama). " +
            "Gebündelte On-Device-Modelle werden nicht mitgeliefert — Nutzer installiert Whisper/Ollama lokal.";
    }
}
