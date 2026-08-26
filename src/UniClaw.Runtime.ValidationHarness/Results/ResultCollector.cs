using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// Result collector (WI-EVH-003 4.1–4.3): aggregates ONE
/// <see cref="ValidationResult"/> from only existing Runtime public facts —
/// the <c>run.strategy.start</c> admission receipt, the frozen read-only wire
/// surface (<c>run.snapshot.get</c>, <c>run.events.after/drain</c>,
/// <c>run.trap.get</c>, <c>evidence.get</c>) via
/// <see cref="IRuntimeReadSurface"/>, and — on the Tier-A in-process
/// composition only — the Agent public read model via
/// <see cref="ITierALedgerAttestation"/>.
///
/// Truthfulness (4.2): every field is classified
/// <see cref="ResultFieldClassification.DirectProjection"/> /
/// <see cref="ResultFieldClassification.DerivedReadModel"/> /
/// <see cref="ResultFieldClassification.Unavailable"/>. Facts with no truthful
/// source on the current surface (full GoalEvidence; the Ledger on wire tiers)
/// are recorded explicitly as Unavailable WITH their classification — never
/// guessed, never fabricated. The collector copies runtime facts only; no
/// Emulator inference, Memory, or Plan content enters the Result (the directive
/// is never read here — even the Tier-A ledger attestation is the Agent's own
/// read-only projection, not harness-authored content).
/// </summary>
public sealed class ResultCollector
{
    private static readonly TimeSpan SnapshotPollDelay = TimeSpan.FromMilliseconds(100);
    // Tier B (real emulator): a full multi-page exploration spans real
    // screenshot→OCR perception cycles (hundreds of ms each) and easily
    // exceeds a 5-second window; the bounded wait must cover the realistic
    // real-device pace while staying finite (fail-closed on timeout).
    private const int MaxTerminalPolls = 600;

    private readonly IRuntimeReadSurface _surface;
    private readonly StrategyRunAdmissionView _admission;
    private readonly ImmutableArray<EvidenceRef> _evidenceRefsToResolve;

    /// <summary>
    /// Create the collector over one read surface and one admission receipt.
    /// Optional refs are resolved through <c>evidence.get</c> during
    /// aggregation (unresolvable refs are recorded, never dropped).
    /// </summary>
    public ResultCollector(
        IRuntimeReadSurface surface,
        StrategyRunAdmissionView admission,
        IEnumerable<EvidenceRef>? evidenceRefsToResolve = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(admission);
        _surface = surface;
        _admission = admission;
        _evidenceRefsToResolve = evidenceRefsToResolve?.ToImmutableArray() ?? [];
    }

    /// <summary>
    /// Aggregate the complete Result for the admitted run. Bounded read-only
    /// terminal wait (run.snapshot.get polling — zero driver calls after
    /// admission), then the final snapshot / event stream / trap / evidence
    /// resolution are read through the surface; the Tier-A ledger attestation
    /// (if the surface provides it) compiles the read-only ledger view
    /// post-terminal.
    /// </summary>
    public async Task<ValidationResult> CollectAsync(CancellationToken cancellationToken = default)
    {
        // Rejected admission — no run exists; only admission facts are truthful.
        if (!_admission.Accepted || string.IsNullOrWhiteSpace(_admission.RunId))
        {
            return BuildRejectedResult();
        }

        var runId = _admission.RunId!;
        await WaitForTerminalAsync(runId, cancellationToken).ConfigureAwait(false);

        var snapshot = await _surface.GetRunSnapshotAsync(runId, cancellationToken).ConfigureAwait(false);
        var eventPage = await _surface.GetRuntimeEventsAfterAsync(runId, cursor: null, cancellationToken).ConfigureAwait(false);
        var trap = await _surface.GetRunTrapAsync(runId, cancellationToken).ConfigureAwait(false);

        var ledgerView = GetAllocatableLedgerView(runId);
        var terminal = BuildTerminalSection(snapshot, eventPage.Events);

        return new ValidationResult(
            Admission: BuildAdmissionSection(runId, ledgerView),
            Lifecycle: BuildLifecycleSection(eventPage),
            Snapshot: BuildSnapshotSection(snapshot),
            Trap: BuildTrapSection(trap),
            Evidence: await BuildEvidenceSectionAsync(runId, eventPage.Events, cancellationToken).ConfigureAwait(false),
            Coverage: BuildCoverageSection(runId, ledgerView),
            Terminal: terminal,
            Boundary: BoundarySection.Placeholder);
    }

    // ---- admission ----------------------------------------------------------

    private AdmissionSection BuildAdmissionSection(string runId, ExplorationLedgerView? ledgerView)
    {
        // On Tier A the strategy identity + declared depth are truthfully
        // compiled from the Agent's own ledger projection; on wire tiers no
        // frozen surface exposes them, so they are recorded unavailable with
        // classification (spec: unavailable surface data is marked, not fabricated).
        var strategyId = ledgerView is null
            ? ResultField<string>.Unavailable(
                "Strategy identity is not exposed by the frozen DriverHost wire surface; Tier-A ledger attestation only.")
            : ResultField<string>.Derived(
                ledgerView.RuntimeExecutionIntentReference,
                "ExplorationLedgerView.RuntimeExecutionIntentReference (Tier-A in-process read model)");
        var declaredDepth = ledgerView is null
            ? ResultField<int>.Unavailable(
                "Declared exploration depth is not exposed by the frozen DriverHost wire surface; Tier-A ledger attestation only.")
            : ResultField<int>.Derived(
                ledgerView.DeclaredMaximumDepth,
                "ExplorationLedgerView.DeclaredMaximumDepth (Tier-A in-process read model)");

        return new AdmissionSection(
            RunId: ResultField<string>.Direct(runId, "run.strategy.start admission receipt (result.runId)"),
            StrategyId: strategyId,
            Accepted: ResultField<bool>.Direct(_admission.Accepted, "run.strategy.start admission receipt (result.accepted)"),
            RejectionCode: ResultField<string?>.Direct(_admission.RejectionCode, "run.strategy.start admission receipt (result.rejectionCode)"),
            RejectionReason: ResultField<string?>.Direct(_admission.RejectionReason, "run.strategy.start admission receipt (result.rejectionReason)"),
            DeclaredMaximumDepth: declaredDepth);
    }

    private ValidationResult BuildRejectedResult()
    {
        var strategyId = ResultField<string>.Unavailable(
            "Admission rejected — no strategy run exists to attest an identity (wire admission receipt carries runId only on Accept).");
        var depth = ResultField<int>.Unavailable(
            "Admission rejected — no strategy run exists to attest a declared depth.");
        return new ValidationResult(
            Admission: new AdmissionSection(
                ResultField<string>.Unavailable("No run was admitted; runId is absent from the admission receipt."),
                strategyId,
                ResultField<bool>.Direct(_admission.Accepted, "run.strategy.start admission receipt (result.accepted)"),
                ResultField<string?>.Direct(_admission.RejectionCode, "run.strategy.start admission receipt (result.rejectionCode)"),
                ResultField<string?>.Direct(_admission.RejectionReason, "run.strategy.start admission receipt (result.rejectionReason)"),
                depth),
            Lifecycle: new LifecycleSection(ResultField<ImmutableArray<SurfaceRuntimeEvent>>.Unavailable(
                "No run was admitted; no lifecycle event stream exists.")),
            Snapshot: EmptySnapshotSection("No run was admitted; no snapshot exists."),
            Trap: new TrapSection(
                ResultField<bool>.Unavailable("No run was admitted; no trap read exists."),
                ResultField<Trap?>.Unavailable("No run was admitted; no trap read exists."),
                ResultField<string?>.Unavailable("No run was admitted; no trap read exists.")),
            Evidence: new EvidenceSection(ResultField<ImmutableArray<ValidationEvidenceEntry>>.Unavailable(
                "No run was admitted; no evidence resolutions exist.")),
            Coverage: BuildCoverageSection(string.Empty, ledgerView: null),
            Terminal: new TerminalSection(
                ResultField<RunState>.Unavailable("No run was admitted; no terminal state exists."),
                ResultField<string?>.Unavailable("No run was admitted; no terminal reason exists."),
                ResultField<bool?>.Unavailable("No run was admitted; no completion ordering exists.")),
            Boundary: BoundarySection.Placeholder);
    }

    // ---- lifecycle ----------------------------------------------------------

    private static LifecycleSection BuildLifecycleSection(SurfaceEventPage eventPage)
        => new(ResultField<ImmutableArray<SurfaceRuntimeEvent>>.Derived(
            eventPage.Events,
            "run.events.after (projected event stream; each event carries its audited A/B source classification)"));

    // ---- snapshot -----------------------------------------------------------

    private static SnapshotSection BuildSnapshotSection(RunSnapshot snapshot)
        => new(
            RunId: ResultField<string>.Direct(snapshot.RunId, "run.snapshot.get (runId)"),
            RunState: ToResultField(snapshot.RunState),
            CurrentSemanticPage: ToResultField(snapshot.CurrentSemanticPage),
            ActiveTrap: ToResultField(snapshot.ActiveTrap),
            CurrentGoal: ToResultField(snapshot.CurrentGoal),
            LastDecision: ToResultField(snapshot.LastDecision),
            LastAction: ToResultField(snapshot.LastAction),
            RecoveryState: ToResultField(snapshot.RecoveryState),
            LatestGoalEvidence: ToResultField(snapshot.LatestGoalEvidence),
            CurrentObservationSequence: ToResultField(snapshot.CurrentObservationSequence),
            CurrentContainerSummary: ToResultField(snapshot.CurrentContainerSummary),
            BindingsSummary: ToResultField(snapshot.BindingsSummary),
            StateBeliefsSummary: ToResultField(snapshot.StateBeliefsSummary),
            Diagnostics: ResultField<ImmutableArray<string>>.Direct(
                snapshot.Diagnostics,
                "run.snapshot.get (projection diagnostics — never runtime authority)"));

    private static SnapshotSection EmptySnapshotSection(string reason)
        => new(
            ResultField<string>.Unavailable(reason),
            ResultField<RunState>.Unavailable(reason),
            ResultField<string?>.Unavailable(reason),
            ResultField<Trap?>.Unavailable(reason),
            ResultField<GoalSummary?>.Unavailable(reason),
            ResultField<DecisionSummary?>.Unavailable(reason),
            ResultField<ActionSummary?>.Unavailable(reason),
            ResultField<RecoverySummary?>.Unavailable(reason),
            ResultField<GoalEvidenceSummary?>.Unavailable(reason),
            ResultField<long?>.Unavailable(reason),
            ResultField<string?>.Unavailable(reason),
            ResultField<string?>.Unavailable(reason),
            ResultField<string?>.Unavailable(reason),
            ResultField<ImmutableArray<string>>.Unavailable(reason));

    /// <summary>Maps a frozen classified snapshot field onto the harness-local
    /// classification, preserving truth source and partial flag.</summary>
    private static ResultField<T> ToResultField<T>(SnapshotField<T> field)
    {
        var classification = field.Classification switch
        {
            SnapshotFieldClassification.DirectPublicProjection => ResultFieldClassification.DirectProjection,
            SnapshotFieldClassification.DerivedReadModel => ResultFieldClassification.DerivedReadModel,
            _ => ResultFieldClassification.Unavailable,
        };
        // PARTIAL-TRUTH PRESERVATION (spec: unavailable surface data is
        // marked, not fabricated): a Runtime UnavailablePartial field carries
        // a REAL partial value (e.g. trace State+Reason when full
        // GoalEvidence is not public). Mapping it to plain Unavailable while
        // keeping the value would violate "populated ⇒ classified"; dropping
        // the value would hide real evidence. Such a field therefore maps to
        // its evidence-bearing classification (DerivedReadModel — the value
        // derives from the trace read model) with IsPartial=true, so the
        // report renders "partial, source stated" — exactly what the Runtime
        // snapshot semantics declare.
        if (field.IsPartial && field.Value is not null && classification == ResultFieldClassification.Unavailable)
        {
            classification = ResultFieldClassification.DerivedReadModel;
        }
        return new ResultField<T>
        {
            Value = field.Value,
            Classification = classification,
            TruthSource = field.TruthSource,
            IsPartial = field.IsPartial,
        };
    }

    // ---- trap ---------------------------------------------------------------

    private static TrapSection BuildTrapSection(InspectTrapResult trap)
        => new(
            Found: ResultField<bool>.Direct(trap.Found, "run.trap.get (found)"),
            Trap: ToResultField(trap.Trap),
            Diagnostic: ResultField<string?>.Direct(trap.Diagnostic, "run.trap.get (diagnostic)"));

    // ---- evidence -----------------------------------------------------------

    private async Task<EvidenceSection> BuildEvidenceSectionAsync(
        string runId,
        ImmutableArray<SurfaceRuntimeEvent> events,
        CancellationToken cancellationToken)
    {
        // Refs carried by runtime events + refs the collector was asked to
        // resolve; each goes through evidence.get — an unresolvable ref is
        // recorded (never dropped, never "resolved" by guesswork).
        var requested = new HashSet<EvidenceRef>(EventRefEquality.Instance);
        foreach (var item in events)
        {
            foreach (var reference in item.EvidenceRefs)
            {
                requested.Add(reference);
            }
        }

        foreach (var reference in _evidenceRefsToResolve)
        {
            requested.Add(reference);
        }

        var entries = ImmutableArray.CreateBuilder<ValidationEvidenceEntry>();
        foreach (var reference in requested)
        {
            var resolution = await _surface.GetEvidenceAsync(reference, cancellationToken).ConfigureAwait(false);
            entries.Add(new ValidationEvidenceEntry(
                RequestedRef: reference,
                Resolved: ResultField<bool>.Direct(resolution.Found, $"evidence.get ({reference.Locator})"),
                CanonicalRef: ResultField<EvidenceRef?>.Direct(resolution.Ref, $"evidence.get ({reference.Locator}) canonical ref"),
                Diagnostic: ResultField<string?>.Direct(resolution.Diagnostic, $"evidence.get ({reference.Locator}) diagnostic")));
        }

        return new EvidenceSection(ResultField<ImmutableArray<ValidationEvidenceEntry>>.Direct(
            entries.ToImmutable(),
            "evidence.get (per-ref resolution outcomes; refs from runtime events + collector-supplied refs)"));
    }

    // ---- coverage (tier-scoped) ---------------------------------------------

    private CoverageSection BuildCoverageSection(string runId, ExplorationLedgerView? ledgerView)
    {
        if (ledgerView is null)
        {
            return new CoverageSection(
                Availability: ResultField<string>.Direct("wireTier-unavailable", "harness tier composition (surface type)"),
                Ledger: ResultField<ExplorationLedgerView?>.Unavailable(
                    "ExplorationLedgerView is not exposed on the frozen DriverHost wire surface (design D3 — Tier-A in-process read model only); no coverage count is fabricated."),
                Scopes: ResultField<ImmutableArray<CoverageScopeCounts>>.Unavailable(
                    "ExplorationLedgerView is not on the wire surface; per-scope counts are unavailable."),
                LedgerDigest: ResultField<string?>.Unavailable(
                    "ExplorationLedgerView is not on the wire surface; the ledger digest is unavailable."));
        }

        var scopes = ImmutableArray.CreateBuilder<CoverageScopeCounts>();
        foreach (var scope in ledgerView.Scopes)
        {
            scopes.Add(new CoverageScopeCounts(
                scope.ScopeIdentity,
                scope.Discovered,
                scope.Visited,
                scope.Pending,
                scope.Unresolved,
                scope.UnknownFrontier));
        }

        return new CoverageSection(
            Availability: ResultField<string>.Direct("tierA-attested", "harness tier composition (surface type)"),
            Ledger: ResultField<ExplorationLedgerView?>.Derived(
                ledgerView,
                "Agent.CompileExplorationLedgerView (in-process public read model; read-only evidence projection)"),
            Scopes: ResultField<ImmutableArray<CoverageScopeCounts>>.Derived(
                scopes.ToImmutable(),
                "ExplorationLedgerView.Scopes (per-scope five counts, copied verbatim)"),
            LedgerDigest: ResultField<string?>.Derived(
                ledgerView.LedgerDigest,
                "ExplorationLedgerView.LedgerDigest (deterministic digest over full ledger content)"));
    }

    /// <summary>The Tier-A read-only ledger view, or null on wire tiers /
    /// unattested compositions (compile happens post-terminal; the Agent
    /// reference is held from admission by <see cref="TierAReadSurface"/>).</summary>
    private ExplorationLedgerView? GetAllocatableLedgerView(string runId)
        => _surface is ITierALedgerAttestation attestation && attestation.CanAttest
            ? attestation.CompileExplorationLedger(runId)
            : null;

    // ---- terminal -----------------------------------------------------------

    private static TerminalSection BuildTerminalSection(
        RunSnapshot snapshot,
        ImmutableArray<SurfaceRuntimeEvent> events)
    {
        // Terminal reason: the terminal event payload reason (B-class projected
        // event) — GoalEvidenceProduced / RunCompleted / RunFailed.
        var reasonEvent = events.LastOrDefault(e =>
            e.Kind is "GoalEvidenceProduced" or "RunCompleted" or "RunFailed");
        var terminalReason = reasonEvent is null || string.IsNullOrWhiteSpace(reasonEvent.Reason)
            ? ResultField<string?>.Unavailable(
                "No terminal event payload reason is available on the projected event stream.")
            : ResultField<string?>.Derived(
                reasonEvent.Reason,
                $"projected {reasonEvent.Kind} event payload reason");

        // Ordering fact (S1): GoalEvidenceProduced precedes RunCompleted.
        var goalEvidenceEvents = events
            .Where(e => e.Kind == "GoalEvidenceProduced")
            .Select(e => e.Sequence)
            .ToArray();
        var runCompletedEvents = events
            .Where(e => e.Kind == "RunCompleted")
            .Select(e => e.Sequence)
            .ToArray();
        ResultField<bool?> backsCompletion = goalEvidenceEvents.Length == 0 || runCompletedEvents.Length == 0
            ? ResultField<bool?>.Unavailable(
                "GoalEvidenceProduced and/or RunCompleted events are absent; the ordering fact is unavailable.")
            : ResultField<bool?>.Derived(
                goalEvidenceEvents[0] < runCompletedEvents[0],
                "projected event sequence ordering (GoalEvidenceProduced before RunCompleted)");

        return new TerminalSection(
            TerminalState: ToResultField(snapshot.RunState),
            TerminalReason: terminalReason,
            GoalEvidenceBacksCompletion: backsCompletion);
    }

    // ---- terminal wait ------------------------------------------------------

    private async Task WaitForTerminalAsync(string runId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTerminalPolls; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await _surface.GetRunSnapshotAsync(runId, cancellationToken).ConfigureAwait(false);
            var state = snapshot.RunState.Value;
            if (state is RunState.Completed or RunState.Failed)
            {
                return;
            }

            await Task.Delay(SnapshotPollDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Ordinal identity on run + locator (logical evidence key).</summary>
    private sealed class EventRefEquality : IEqualityComparer<EvidenceRef>
    {
        public static readonly EventRefEquality Instance = new();

        public bool Equals(EvidenceRef? x, EvidenceRef? y)
        {
            if (x is null || y is null)
            {
                return ReferenceEquals(x, y);
            }

            return string.Equals(x.RunId, y.RunId, StringComparison.Ordinal)
                   && string.Equals(x.Locator, y.Locator, StringComparison.Ordinal);
        }

        public int GetHashCode(EvidenceRef obj) => HashCode.Combine(obj.RunId, obj.Locator);
    }
}