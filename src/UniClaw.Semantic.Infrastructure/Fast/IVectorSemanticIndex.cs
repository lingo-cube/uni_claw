namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Retrieval boundary: query vector + indexed prototype vectors
/// → ranked semantic candidates.
///
/// A vector index answers ONLY "which prototypes are nearest". Its internals
/// MUST NOT contain: acceptance threshold, candidate policy, evidence
/// sufficiency, previous-identity conflict, structural rules, or SemanticEvidence
/// creation. It returns the full (or top-K) ranking; acceptance is the
/// Candidate Policy's job.
/// </summary>
public interface IVectorSemanticIndex
{
    /// <summary>
    /// Retrieves ranked candidates for a query vector, nearest first. The result
    /// is ordered by similarity only — no acceptance decision is made.
    /// </summary>
    IReadOnlyList<SemanticCandidate> Retrieve(EmbeddingVector queryVector);
}