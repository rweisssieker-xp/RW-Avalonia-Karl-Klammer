# KI und RAG — Umgebungsvariablen (eine Übersicht)

Alle Werte werden typischerweise über `windows/.env` (Repo) bzw. den konfigurierten Dotenv-Pfad geladen — siehe [`DotEnvStore`](../CarolusNexus/Services/DotEnvStore.cs). **Keine API-Keys in Git committen.**

## Chat / Completion (Haupt-LLM)

| Variable | Zweck |
|----------|--------|
| `OPENAI_API_KEY` | OpenAI oder kompatibler Anbieter — **auch** für Embedding-RAG, wenn semantische Suche aktiv sein soll. |
| `OPENAI_BASE_URL` | Optional: Basis-URL für OpenAI-kompatible APIs (Default `https://api.openai.com/v1`). |
| Anthropic | Für Anthropic-Provider siehe die in `DotEnvStore` / UI dokumentierten Keys (z. B. `ANTHROPIC_API_KEY`). |

Die gewählte **Provider-/Modell-Kombination** steht in den App-Einstellungen (`NexusSettings`), nicht nur in `.env`.

## Semantisches RAG (Embeddings)

| Variable | Zweck |
|----------|--------|
| `OPENAI_API_KEY` | Erforderlich zum Erzeugen der Vektoren und zur **Abfrage** (Query-Embedding). |
| `OPENAI_EMBEDDING_MODEL` | Optional, Default oft `text-embedding-3-small`. |
| `RAG_EMBEDDINGS` | `0` oder `false` — **deaktiviert** Embedding-Suche und Rebuild (semantisches RAG aus). |
| `RAG_TOP_K` | Optional: Anzahl Chunk-Treffer (1–24), Default 8. |
| `MAX_EMBED_CHUNKS` | Optional: Obergrenze beim Embedding-Rebuild (50–4000). |

Implementierung: [`EmbeddingRagService`](../CarolusNexus/Services/EmbeddingRagService.cs), Anzeige/Tier: [`KnowledgeSnippetService.BuildAugmentationResult`](../CarolusNexus/Services/KnowledgeSnippetService.cs).

## Lokale Datenbestände (ohne Cloud)

- `knowledge-chunks.json` — Chunk-Index nach **Knowledge Reindex**.
- `knowledge-embeddings.json` — Vektoren; muss zum Chunk-Fingerprint passen, sonst fällt die semantische Suche weg (Ask zeigt Hinweise).
- FTS5-Datenbank — siehe [`KnowledgeFtsStore`](../CarolusNexus/Services/KnowledgeFtsStore.cs).

## Degraded Mode

Wenn Keys fehlen oder `RAG_EMBEDDINGS=0` gesetzt ist, nutzt Ask weiterhin **FTS**, **Keyword-Overlap** oder **sequenzielle Dateien** — die Ask-UI zeigt die Stufe (`[Retrieval · …]`).
