using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Campaign;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using UniClaw.Runtime.ValidationHarness.PlanDelta;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign.Adaptation;

/// <summary>
/// Evidence-informed knowledge extraction for the Phase 2.6 validation-side
/// adaptation planner (spec "Frozen iterative loop with independent runs" +
/// "ScenarioKnowledgeFixture as a validation test asset"; design D3
/// provenance-gated admission; Phase 2.5 "UniAgent emulator" precedent — the
/// harness interprets a run's frozen Result into validation knowledge, exactly
/// as an upper agent would, and NEVER touches the Runtime).
///
/// This is an OBSERVATION-side extractor: it reads ONE round's own frozen
/// evidence (<c>Result.Terminal</c> state+reason, <c>Result.Lifecycle.Events</c>
/// kinds, <c>Result.Snapshot.Diagnostics</c>, and the round's rendered report
/// JSON) and proposes knowledge candidates whose provenance is the observed
/// run. Every candidate goes through the stateless
/// <see cref="KnowledgeAdmission.TryAdmit"/> provenance gate with source
/// <see cref="KnowledgeAdmissionSource.ObservedResult"/> — a candidate that
/// fails the gate is RETURNED as rejected, never forced (honest accounting,
/// spec "Provenance-gated admission" / "Forbidden knowledge sources are
/// rejected").
///
/// The knowledge rules are typed and conservative — each rule cites the source
/// evidence it actually read via deterministic locator refs (see
/// <see cref="BuildRoundEvidenceSummary"/>, the round's citation universe):
///  1. Failed with a terminal reason containing "normalization"/"unresolved"
///     → KnownUnresolved anchored at "settings-root-inventory";
///  2. Completed → KnownContainer for the strategy's semantic root;
///  3. Failed with a depth-boundary reason (contains "depth")
///     → KnownRecordOnly for the root's child class at the declared depth;
///  4. Failed with a launch/foreground reason → KnownUnresolved anchored at
///     "settings-entry".
/// Validation asset only — TEST_KNOWLEDGE != RUNTIME_TRUTH / ACTION_AUTHORITY.
/// </summary>
public static class RoundKnowledgeExtractor
{
    /// <summary>The unresolved root-inventory anchor (rule 1).</summary>
    public const string RootInventoryAnchor = "settings-root-inventory";

    /// <summary>The unresolved settings-entry anchor (rule 4).</summary>
    public const string SettingsEntryAnchor = "settings-entry";

    /// <summary>Typed semantic container anchor prefix (rule 2), e.g.
    /// "settings.container:Settings" — the strategy's semantic root
    /// container identity, never a path or selector.</summary>
    public const string ContainerAnchorPrefix = "settings.container:";

    /// <summary>Typed depth-boundary anchor prefix (rule 3), e.g.
    /// "settings.depth-boundary:Settings:depth:1".</summary>
    public const string DepthBoundaryAnchorPrefix = "settings.depth-boundary:";

    // ── Evidence-locator formats (deterministic, truthful) ───────────────────

    /// <summary>Locator of the round's terminal section
    /// (<c>Result.Terminal</c>: state + reason).</summary>
    public static string TerminalRef(string runId) => $"run:{runId}:terminal";

    /// <summary>Locator of the round's projected lifecycle event stream
    /// (<c>Result.Lifecycle.Events</c>).</summary>
    public static string EventsRef(string runId) => $"run:{runId}:events";

    /// <summary>Locator of the round's snapshot diagnostics
    /// (<c>Result.Snapshot.Diagnostics</c>).</summary>
    public static string SnapshotRef(string runId) => $"run:{runId}:snapshot";

    /// <summary>Locator of the round's rendered report JSON. Declared in the
    /// evidence universe only when the round actually carries a report.</summary>
    public static string ReportRef(string runId) => $"run:{runId}:report";

    // ── Round evidence universe ───────────────────────────────────────────────

    /// <summary>
    /// Build the round's evidence summary (the PlanDelta citation universe —
    /// design D5 "PlanDelta contract"): exactly the evidence locators that
    /// TRUTHFULLY exist in this round. The terminal/events/snapshot locators
    /// are always declared (the Result sections exist on every round); the
    /// report locator is declared only when the round produced a report.
    /// Requires an admitted run — knowledge extraction from a run-less round
    /// has no observed evidence to cite.
    /// </summary>
    public static RoundEvidenceSummary BuildRoundEvidenceSummary(CampaignRoundOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (string.IsNullOrWhiteSpace(outcome.RunId))
        {
            throw new ArgumentException(
                "round knowledge extraction requires an admitted run (RunId is null): the planner is a pure reader "
                + "of observed results and a run-less round has no evidence to cite.",
                nameof(outcome));
        }

        var eventKinds = outcome.Result.Lifecycle.Events.Value.IsDefault
            ? Array.Empty<string>()
            : outcome.Result.Lifecycle.Events.Value.Select(e => e.Kind).ToArray();
        var evidenceRefs = new List<string>
        {
            TerminalRef(outcome.RunId),
            EventsRef(outcome.RunId),
            SnapshotRef(outcome.RunId),
        };
        if (!string.IsNullOrWhiteSpace(outcome.Run.ReportJson))
        {
            evidenceRefs.Add(ReportRef(outcome.RunId));
        }

        var terminal = outcome.Result.Terminal.TerminalState;
        var terminalState = terminal.Classification == ResultFieldClassification.Unavailable
            ? "UNAVAILABLE"
            : terminal.Value.ToString();
        return new RoundEvidenceSummary(
            runId: outcome.RunId,
            strategyId: outcome.StrategyId,
            terminalState: terminalState,
            eventKinds: eventKinds,
            evidenceRefs: evidenceRefs);
    }

    // ── Extraction ────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract knowledge candidates from ONE round's own evidence. Every
    /// candidate carries full provenance (SourceRunId = the round's RunId,
    /// EvidenceRefs = the subset of the round's universe the rule actually
    /// read) and passes through <see cref="KnowledgeAdmission.TryAdmit"/> —
    /// the gate outcome is returned per candidate; a rejected candidate is
    /// never forced. <paramref name="source"/> allows the extraction to be
    /// exercised with a forbidden source class so tests prove the gate is
    /// never bypassed (default: the ONLY admissible source, ObservedResult).
    /// </summary>
    public static KnowledgeExtraction Extract(
        CampaignRoundOutcome outcome,
        KnowledgeScope campaignScope,
        KnowledgeAdmissionSource source = KnowledgeAdmissionSource.ObservedResult)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(campaignScope);
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        var summary = BuildRoundEvidenceSummary(outcome);
        var runId = summary.RunId;
        var scope = RoundScope(campaignScope, runId);
        var terminal = outcome.Result.Terminal.TerminalState.Value;
        var reason = outcome.Result.Terminal.TerminalReason.Value;
        var hasReport = !string.IsNullOrWhiteSpace(outcome.Run.ReportJson);
        var semanticRoot = outcome.Directive.Directive.Scope.SemanticRoot;
        var declaredDepth = outcome.Directive.Directive.Scope.MaximumDepth;
        var terminalRef = TerminalRef(runId);
        var reportRef = ReportRef(runId);
        var snapshotRef = SnapshotRef(runId);

        var candidates = new List<KnowledgeExtractionCandidate>();

        if (terminal == RunState.Completed)
        {
            Add(CompletedContainer(summary, scope, semanticRoot, outcome.RoundIndex, terminalRef, reportRef, hasReport));
        }
        else if (terminal == RunState.Failed)
        {
            if (ReasonContains(reason, "normalization", "unresolved") && hasReport)
            {
                Add(UnresolvedRootInventory(summary, scope, outcome.RoundIndex, terminalRef, reportRef));
            }
            else if (ReasonContains(reason, "depth"))
            {
                Add(DepthBoundaryChildren(summary, scope, semanticRoot, declaredDepth, outcome.RoundIndex, terminalRef, snapshotRef));
            }
            else if (ReasonContains(reason, "launch", "foreground") && hasReport)
            {
                Add(SettingsEntryUnresolved(summary, scope, outcome.RoundIndex, terminalRef, reportRef));
            }
        }

        return new KnowledgeExtraction(summary, candidates);

        void Add(ScenarioKnowledgeRecord? candidate)
        {
            if (candidate is null)
            {
                return;
            }

            var admission = KnowledgeAdmission.TryAdmit(candidate, source);
            candidates.Add(new KnowledgeExtractionCandidate(candidate, admission));
        }
    }

    /// <summary>Rule 2: the strategy's semantic root container was observed
    /// exhausted within the declared depth (Completed terminal).</summary>
    private static ScenarioKnowledgeRecord? CompletedContainer(
        RoundEvidenceSummary summary,
        KnowledgeScope scope,
        string semanticRoot,
        int roundIndex,
        string terminalRef,
        string reportRef,
        bool hasReport)
    {
        if (!hasReport)
        {
            return null;
        }

        return new ScenarioKnowledgeRecord(
            KnowledgeType: KnowledgeType.KnownContainer,
            SemanticAnchor: string.Concat(ContainerAnchorPrefix, semanticRoot),
            SourceRunId: summary.RunId,
            EvidenceRefs: new[] { terminalRef, reportRef },
            ObservedRole: "root container exhausted within declared depth",
            Scope: scope,
            Disposition: "root container observed exhausted at the declared depth; the next plan may deepen inside the bounded scope",
            Confidence: 0.9,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: roundIndex + 1);
    }

    /// <summary>Rule 1: the root viewport normalization could not be resolved
    /// (Failed with "normalization"/"unresolved") — recorded as an unknown,
    /// never guessed.</summary>
    private static ScenarioKnowledgeRecord? UnresolvedRootInventory(
        RoundEvidenceSummary summary,
        KnowledgeScope scope,
        int roundIndex,
        string terminalRef,
        string reportRef)
        => new(
            KnowledgeType: KnowledgeType.KnownUnresolved,
            SemanticAnchor: RootInventoryAnchor,
            SourceRunId: summary.RunId,
            EvidenceRefs: new[] { terminalRef, reportRef },
            ObservedRole: "root viewport normalization unresolved",
            Scope: scope,
            Disposition: "record-only; requires upper-agent replan",
            Confidence: 0.7,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: roundIndex + 1);

    /// <summary>Rule 3: the run stopped at the declared depth boundary — the
    /// child class at that depth is record-only (informative, not a traversal
    /// target).</summary>
    private static ScenarioKnowledgeRecord? DepthBoundaryChildren(
        RoundEvidenceSummary summary,
        KnowledgeScope scope,
        string semanticRoot,
        int declaredDepth,
        int roundIndex,
        string terminalRef,
        string snapshotRef)
        => new(
            KnowledgeType: KnowledgeType.KnownRecordOnly,
            SemanticAnchor: string.Concat(DepthBoundaryAnchorPrefix, semanticRoot, ":depth:", declaredDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            SourceRunId: summary.RunId,
            EvidenceRefs: new[] { terminalRef, snapshotRef },
            ObservedRole: "children recorded at depth boundary",
            Scope: scope,
            Disposition: "record-only: the children observed at the declared depth boundary are informative, not traversal targets",
            Confidence: 0.8,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: roundIndex + 1);

    /// <summary>Rule 4: launch/foreground never resolved (Failed with
    /// "launch"/"foreground") — recorded as an unknown at the settings
    /// entry, never guessed.</summary>
    private static ScenarioKnowledgeRecord? SettingsEntryUnresolved(
        RoundEvidenceSummary summary,
        KnowledgeScope scope,
        int roundIndex,
        string terminalRef,
        string reportRef)
        => new(
            KnowledgeType: KnowledgeType.KnownUnresolved,
            SemanticAnchor: SettingsEntryAnchor,
            SourceRunId: summary.RunId,
            EvidenceRefs: new[] { terminalRef, reportRef },
            ObservedRole: "settings entry launch unresolved",
            Scope: scope,
            Disposition: "record-only; requires upper-agent replan",
            Confidence: 0.7,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: roundIndex + 1);

    /// <summary>The run's own created-from run set: the Scope the candidate
    /// carries is the campaign scope with THIS run as its provenance head
    /// (scope completeness requires a non-empty run set, design D3).</summary>
    private static KnowledgeScope RoundScope(KnowledgeScope campaignScope, string runId)
        => campaignScope with { CreatedFromRunIds = new[] { runId } };

    private static bool ReasonContains(string? reason, params string[] tokens)
        => reason is not null
           && tokens.Any(token => reason.Contains(token, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One proposed knowledge candidate with the stateless provenance-gate
/// outcome: <see cref="KnowledgeAdmission.Admitted"/> when the candidate traces
/// to the observed result, <see cref="KnowledgeAdmission.Rejected"/> otherwise
/// (returned for honest accounting — never forced).</summary>
public sealed record KnowledgeExtractionCandidate(
    ScenarioKnowledgeRecord Record,
    KnowledgeAdmission Admission);

/// <summary>
/// The round's knowledge extraction: the evidence summary (the citation
/// universe the round's PlanDelta may cite) plus every proposed candidate with
/// its gate outcome, in rule order.
/// </summary>
public sealed record KnowledgeExtraction(
    RoundEvidenceSummary EvidenceSummary,
    IReadOnlyList<KnowledgeExtractionCandidate> Candidates);