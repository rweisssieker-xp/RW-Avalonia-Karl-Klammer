using System.Collections.Generic;

namespace CarolusNexus.Models;

public sealed record KnowledgeSourceRef(string Label, string? FullPath);

public sealed record KnowledgeContextBundle(string ContextText, IReadOnlyList<KnowledgeSourceRef> Sources);
