using System.Collections.Generic;

namespace CarolusNexus.Models;

/// <summary>How local knowledge was merged into Ask (<see cref="Services.KnowledgeSnippetService"/>).</summary>
public enum KnowledgeRetrievalTier
{
    /// <summary>No knowledge folder or empty result.</summary>
    None = 0,

    /// <summary>OpenAI-compatible embedding search over <c>knowledge-embeddings.json</c>.</summary>
    SemanticEmbedding,

    /// <summary>SQLite FTS5 over indexed chunks.</summary>
    Fts,

    /// <summary>Token overlap scoring over <c>knowledge-chunks.json</c> without vectors.</summary>
    KeywordChunks,

    /// <summary>First text files from the knowledge directory (no query match path).</summary>
    SequentialFiles
}

/// <summary>Knowledge excerpts plus transparency for the Ask UI (tier + hints).</summary>
public sealed record KnowledgeAugmentationResult(
    KnowledgeContextBundle Bundle,
    KnowledgeRetrievalTier Tier,
    IReadOnlyList<string> Hints);
