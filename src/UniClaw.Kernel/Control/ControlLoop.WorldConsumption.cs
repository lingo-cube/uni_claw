using System.Collections.Immutable;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Control;

public sealed record WorldDecisionUse(string DecisionId, WorldConsumptionBasis Basis);

public sealed partial class ControlLoop
{
    private readonly Dictionary<string, WorldDecisionUse> _worldDecisionUses = new(StringComparer.Ordinal);
    public IReadOnlyList<WorldDecisionUse> WorldDecisionUses => _worldDecisionUses.Values.ToImmutableArray();

    internal void RecordWorldDecisionUse(string decisionId, WorldConsumptionBasis basis)
    {
        var value = new WorldDecisionUse(decisionId, basis);
        if (_worldDecisionUses.TryGetValue(decisionId, out var previous))
        {
            if (previous.Basis.SourceRevisionId != basis.SourceRevisionId)
                throw new InvalidOperationException("decision context source changed under the same correlation");
            return;
        }
        _worldDecisionUses.Add(decisionId, value);
    }
    internal IReadOnlyList<WorldDecisionUse> DecisionUses(IReadOnlyList<string>? ids) =>
        (ids ?? []).Where(_worldDecisionUses.ContainsKey).Select(id => _worldDecisionUses[id]).ToImmutableArray();

    internal bool HasCorrectedPlanningBasis(IReadOnlyList<string> ids, IReadOnlyList<PageCorrectionDecision> corrections) =>
        DecisionUses(ids).Any(use => corrections.Any(c => c.Applied
            && c.RevokedInterpretationEvidenceIds.Any(use.Basis.EvidenceIds.Contains)));
}
