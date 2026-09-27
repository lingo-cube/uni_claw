namespace UniClaw.Kernel.Perception.Fusion;

public static class LineageValidator
{
    public static LineageValidationResult Validate(IReadOnlyDictionary<string, EvidenceNode> graph,
        IReadOnlyList<string> directParents, IReadOnlyList<string>? declaredBasis = null)
    {
        var leaves = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Walk(string id)
        {
            if (!graph.TryGetValue(id, out var node) || !visiting.Add(id)) return false;
            if (!visited.Add(id)) { visiting.Remove(id); return true; }
            if (node.ParentEvidenceIds.Contains(id, StringComparer.Ordinal)) return false;
            if (node.ParentEvidenceIds.Count == 0) { leaves.Add(id); visiting.Remove(id); return true; }
            foreach (var parent in node.ParentEvidenceIds) if (!Walk(parent)) return false;
            visiting.Remove(id); return true;
        }
        foreach (var parent in directParents) if (!Walk(parent))
            return new(LineageDisposition.MalformedLineage, new HashSet<string>(), "missing parent, self-reference, or ancestry cycle");
        if (declaredBasis is not null && !leaves.SetEquals(declaredBasis))
            return new(LineageDisposition.MalformedLineage, new HashSet<string>(), "declared transitive basis does not match ancestry closure");
        return new(LineageDisposition.Valid, leaves);
    }
}
