using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// In-memory ScenarioKnowledgeFixture — the scenario-scoped, versioned,
/// provenance-bearing validation knowledge store (spec requirement
/// "ScenarioKnowledgeFixture as a validation test asset" + design D2/D3).
/// This WorkItem implements the RECORD CONTRACT + GATES only: freezing to
/// human-readable persistent assets ("Knowledge persistence and cross-campaign
/// reuse") is a LATER WorkItem — NO file IO here, NO Memory service/db/api,
/// NO runtime injection path.
///
/// Invariants:
/// <list type="bullet">
/// <item><b>Scope-bound</b>: only records whose <see cref="KnowledgeScope.Matches"/>
/// the fixture's <see cref="OwnerScope"/> (scenario/app/capability-id/
/// capability-version/locale/android — created-from run set excluded) can be
/// admitted; cross-context knowledge cannot leak in.</item>
/// <item><b>Provenance-gated admission</b>: every admission re-runs
/// <see cref="KnowledgeAdmission.TryAdmit"/> (observed-result source only;
/// every forbidden source class rejected with its explicit marker). A record
/// with identical canonical content to an admitted one (same RecordId) is
/// rejected as a duplicate — re-applying old knowledge without fresh evidence
/// is impossible at the store level.</item>
/// <item><b>CURRENT FRESH EVIDENCE FIRST</b>:
/// <see cref="ApplyFreshEvidence"/> downgrades the old record and returns a
/// NEW fixture instance (immutable-list semantics; the prior instance remains
/// as retrievable history). There is NO API that re-activates a downgraded
/// record or force-applies old knowledge over fresh evidence — the ABSENCE of
/// that API is itself the guarantee (spec: "History never rewritten;
/// HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH").</item>
/// </list>
/// </summary>
public sealed class ScenarioKnowledgeFixture
{
    private ImmutableArray<ScenarioKnowledgeRecord> _records;

    /// <summary>Fixture binding scope (scenario/app/capability/version/locale/android).</summary>
    public KnowledgeScope OwnerScope { get; }

    /// <summary>Every admitted record, in admission order (immutable snapshot;
    /// includes downgraded records — statistics and history never delete).</summary>
    public IReadOnlyList<ScenarioKnowledgeRecord> Records => _records;

    /// <summary>Create an empty fixture bound to the given scenario scope.
    /// Scope-bound means cross-context admission is impossible (design D2:
    /// no implicit global knowledge, no automatic cross-context reuse).</summary>
    public ScenarioKnowledgeFixture(KnowledgeScope ownerScope)
    {
        ArgumentNullException.ThrowIfNull(ownerScope);
        OwnerScope = ownerScope;
        _records = ImmutableArray<ScenarioKnowledgeRecord>.Empty;
    }

    private ScenarioKnowledgeFixture(KnowledgeScope ownerScope, ImmutableArray<ScenarioKnowledgeRecord> records)
    {
        OwnerScope = ownerScope;
        _records = records;
    }

    /// <summary>
    /// Apply the admission gate to a candidate and, when admitted, add it to
    /// this fixture (admission is the explicit record-creation act — the
    /// outcome reports the gate result). In addition to the stateless gate,
    /// the store enforces: (1) scope binding — the candidate's scope must
    /// <see cref="KnowledgeScope.Matches"/> <see cref="OwnerScope"/>; (2) no
    /// duplicate canonical content — an already-admitted RecordId is rejected
    /// (re-admission would re-apply old knowledge without fresh evidence).
    /// The default source is <see cref="KnowledgeAdmissionSource.ObservedResult"/>;
    /// pass a forbidden marker explicitly to exercise the rejection path.
    /// </summary>
    public KnowledgeAdmission Admit(
        ScenarioKnowledgeRecord candidate,
        KnowledgeAdmissionSource source = KnowledgeAdmissionSource.ObservedResult)
    {
        var gate = KnowledgeAdmission.TryAdmit(candidate, source);
        if (gate is KnowledgeAdmission.Rejected rejected)
        {
            return rejected;
        }

        if (!candidate.Scope.Matches(OwnerScope))
        {
            return new KnowledgeAdmission.Rejected(
                "Scope mismatch: a knowledge record may only be admitted into a fixture whose "
                + "scenario/app/capability-id/capability-version/locale/android context it matches "
                + "(implicit global knowledge and automatic cross-context reuse are forbidden).",
                null);
        }

        if (_records.Any(r => r.RecordId == candidate.RecordId))
        {
            return new KnowledgeAdmission.Rejected(
                $"A record with RecordId '{candidate.RecordId}' (identical canonical content) is already "
                + "admitted; re-admission would re-apply old knowledge without fresh evidence.",
                null);
        }

        _records = _records.Add(candidate);
        return new KnowledgeAdmission.Admitted(candidate);
    }

    /// <summary>
    /// Active advisory for a query scope: ONLY records whose status is
    /// <see cref="KnowledgeStatus.Active"/> AND whose scope MATCHES the query
    /// scope (fresh per-query <see cref="KnowledgeScope.Matches"/> check —
    /// scope mismatch, status downgrade, or a different context excludes the
    /// record). Returns an empty set for a mismatched context.
    /// </summary>
    public ImmutableArray<ScenarioKnowledgeRecord> ActiveKnowledge(KnowledgeScope queryScope)
    {
        ArgumentNullException.ThrowIfNull(queryScope);
        return _records
            .Where(r => r.Status == KnowledgeStatus.Active && r.Scope.Matches(queryScope))
            .ToImmutableArray();
    }

    /// <summary>
    /// CURRENT FRESH EVIDENCE FIRST (spec: "the fresh evidence wins, and the
    /// contradicting knowledge is downgraded ... never force-applied over
    /// current reality"; design D3). Applies ONE fresh-evidence disposition to
    /// the target record named by <paramref name="outcome"/>: the target's
    /// status transitions (Contradicted/Superseded/Invalidated/Stale) and,
    /// for supersession, its SupersededBy link is set to the replacement's
    /// RecordId when provided — completing the traceable Supersedes/SupersededBy
    /// pair against the (already admitted) replacement.
    ///
    /// HISTORY IS NEVER MUTATED IN PLACE: this method returns a NEW fixture
    /// instance with the downgraded record; the receiving instance keeps its
    /// records untouched as retrievable history. Downgrading a record that is
    /// not admitted, or with an anchor/scope that does not match the target,
    /// throws — a fresh-evidence transition must name real admitted knowledge.
    /// </summary>
    public ScenarioKnowledgeFixture ApplyFreshEvidence(
        string semanticAnchor,
        KnowledgeScope scope,
        FreshEvidenceOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(outcome.Target);

        var target = outcome.Target;
        if (!string.Equals(semanticAnchor, target.SemanticAnchor, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The fresh-evidence anchor does not match the target record's SemanticAnchor.",
                nameof(semanticAnchor));
        }

        if (!target.Scope.Matches(scope))
        {
            throw new ArgumentException(
                "The fresh-evidence scope does not match the target record's scope.",
                nameof(scope));
        }

        var index = -1;
        for (var i = 0; i < _records.Length; i++)
        {
            if (_records[i].RecordId == target.RecordId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            throw new ArgumentException(
                $"Record '{target.RecordId}' is not admitted in this fixture — fresh evidence can only "
                + "downgrade admitted knowledge.",
                nameof(outcome));
        }

        var downgraded = _records[index].WithStatus(outcome.ResultingStatus, outcome.ReplacementRecordId);
        return new ScenarioKnowledgeFixture(OwnerScope, _records.SetItem(index, downgraded));
    }

    /// <summary>
    /// Lifecycle statistics: counts of admitted records per
    /// (KnowledgeType, Status) pair across the WHOLE history (active and
    /// downgraded). Empirical Phase 3 Memory-learning input (spec
    /// "Phase 3 Memory learning inputs": which knowledge types were created,
    /// reused, caused PlanDeltas, were contradicted/superseded/invalidated) —
    /// recording statistics is NOT Memory implementation or Phase 3
    /// authorization.
    /// </summary>
    public IReadOnlyDictionary<(KnowledgeType Type, KnowledgeStatus Status), int> LifecycleStatistics()
    {
        var counts = new Dictionary<(KnowledgeType, KnowledgeStatus), int>();
        foreach (var record in _records)
        {
            var key = (record.KnowledgeType, record.Status);
            counts.TryGetValue(key, out var current);
            counts[key] = current + 1;
        }

        return counts;
    }
}