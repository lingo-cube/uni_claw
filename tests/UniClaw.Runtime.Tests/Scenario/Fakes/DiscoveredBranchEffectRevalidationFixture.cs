using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-CAND-009 Task 1.1 deterministic external-world and evidence capability. The fixture scripts
/// only one bounded parent P, A's observable external effect, external Launcher drift, recovered-world
/// true/false/unobservable evidence, and remaining B navigation. It exposes accepted SC-P3-CAND-008
/// inventory evidence (required siblings A and B under P), SC-P3-CAND-006 independent authorization of
/// A, and SC-P3-CAND-004 historical progress proving A evidence-completed while B remains required and
/// unresolved. It does not decide branch validity, contribution, resume, escalation, GoalEvidence, or
/// final RunState — those remain Agent authority (Task 2.1). Read-only reuse of the frozen CAND-004/005/
/// 006/008 vocabularies; no existing Fake is modified.
/// </summary>
internal sealed class DiscoveredBranchEffectRevalidationFixture
{
    internal const string DefaultRunId = "sc-p3-cand-009-fixture-run";
    internal const string ActiveParentSemanticPage = "ParentP";
    internal const string ConflictingParentSemanticPage = "OtherParentP";
    internal const string BranchA = "Branch A";
    internal const string BranchB = "Branch B";
    internal const string MismatchedIdentity = "Branch C";
    private const string ExternalEffect = "A external effect";
    private const string RecoveredParentScreen = "RecoveredParentP";

    private readonly ScriptedEnvironment _environment;
    private readonly string _progressParent;

    private DiscoveredBranchEffectRevalidationFixture(
        string runId,
        ScriptedEnvironment environment,
        BranchEffectCriterion? carrier,
        string progressParent)
    {
        RunId = runId;
        _environment = environment;
        _progressParent = progressParent;
        InitialPlan = new Plan([new PlanStep(BranchB, "Tap")]);
        Carrier = carrier;
        Goal = new Goal(
            EvaluateGoal,
            AuthorizeA,
            BranchInventoryEvaluator: EvaluateInventory,
            DiscoveredBranchEffectCriterion: carrier);
    }

    internal string RunId { get; }

    /// <summary>Immutable initial Plan whose targets never include the discovered branch A.</summary>
    internal Plan InitialPlan { get; }

    /// <summary>Singular Goal-held discovered-branch effect criterion (null = absent carrier path).</summary>
    internal BranchEffectCriterion? Carrier { get; }

    /// <summary>Goal carrying the approved optional carrier field plus frozen CAND-006/CAND-008 surfaces.</summary>
    internal Goal Goal { get; }

    internal ScriptedEnvironment Environment => _environment;

    internal static DiscoveredBranchEffectRevalidationFixture Positive(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.Positive);

    internal static DiscoveredBranchEffectRevalidationFixture Contradicted(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.Contradicted);

    internal static DiscoveredBranchEffectRevalidationFixture Unresolved(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.Unresolved);

    internal static DiscoveredBranchEffectRevalidationFixture AbsentCarrier(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.AbsentCarrier);

    internal static DiscoveredBranchEffectRevalidationFixture IdentityMismatch(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.IdentityMismatch);

    internal static DiscoveredBranchEffectRevalidationFixture AmbiguousParent(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.AmbiguousParent);

    internal static DiscoveredBranchEffectRevalidationFixture StaleEvidence(string runId = DefaultRunId)
        => Create(runId, RevalidationPath.StaleEvidence);

    /// <summary>
    /// Deterministic external-world walk: P → A's child → A effect applied → P (stale pre-Recovery
    /// snapshot) → external drift → restore via LaunchApp → fresh recovered-world P → B's child.
    /// The fixture scripts only world mechanics and evidence; it does not run Agent or Recovery.
    /// </summary>
    internal async Task<DiscoveredBranchEffectWorldEvidence> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();

        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 0: ParentP (seq 1)
        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(0), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 1: ChildA (seq 2)
        dispatches.Add(await _environment.ExecuteAsync(
            new DeviceAction.SetSwitch(0, true), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 2: ChildAComplete (seq 3)
        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(1), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 3: ParentAfterA — stale pre-Recovery snapshot
        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 4: Launcher (external drift)
        dispatches.Add(await _environment.ExecuteAsync(
            new DeviceAction.LaunchApp("Settings"), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 5: RecoveredParentP — fresh post-verification
        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(1), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));   // 6: ChildB (seq 7)

        var observationsArray = observations.ToImmutable();
        var inventory = EvaluateInventory(ImmutableArray.Create(observationsArray[0]), 0);
        var progress = new BranchProgressEvidence(
            _progressParent,
            ImmutableDictionary<string, long>.Empty
                .Add(BranchA, observationsArray[0].SequenceNumber)
                .Add(BranchB, observationsArray[0].SequenceNumber),
            ImmutableDictionary<string, long>.Empty
                .Add(BranchA, observationsArray[2].SequenceNumber));
        var authorization = AuthorizeA(observationsArray[0], observationsArray[0].Elements[0]);
        var matchedCarrier = MatchCarrier(
            Carrier,
            inventory,
            progress,
            ActiveParentSemanticPage);
        var staleSnapshot = observationsArray[3];
        var freshRecovered = observationsArray[5];

        return new DiscoveredBranchEffectWorldEvidence(
            RunId,
            InitialPlan,
            Goal,
            Carrier,
            matchedCarrier,
            inventory,
            progress,
            authorization,
            observationsArray,
            dispatches.ToImmutable(),
            _environment.ActionHistory.ToImmutableArray(),
            ActiveParentSemanticPage,
            staleSnapshot,
            freshRecovered,
            matchedCarrier?.Evaluator(staleSnapshot),
            matchedCarrier?.Evaluator(freshRecovered));
    }

    /// <summary>
    /// Pure test-side exact-match surface expressing the approved identity boundary: the carrier is
    /// matchable only when its identity is present in both accepted inventory evidence and historical
    /// completion provenance under the same active parent scope. Missing, mismatched, or conflicting
    /// parent scope stays unmatched (unresolved). This is read-only evidence expression; Agent retains
    /// interpretation and retain/invalidate/resume authority (Task 2.1).
    /// </summary>
    internal static BranchEffectCriterion? MatchCarrier(
        BranchEffectCriterion? carrier,
        BranchInventoryEvidence inventory,
        BranchProgressEvidence progress,
        string activeParentSemanticPage)
    {
        if (carrier is null)
            return null;
        if (!string.Equals(progress.ParentSemanticPage, activeParentSemanticPage, StringComparison.Ordinal))
            return null;
        if (inventory.RequiredBranchEvidence?.ContainsKey(carrier.BranchIdentity) != true)
            return null;
        if (!progress.CompletedSiblingEvidence.ContainsKey(carrier.BranchIdentity))
            return null;
        return carrier;
    }

    /// <summary>SC-P3-CAND-008 bounded inventory evaluator: P evidence proves the complete required sibling inventory {A, B}.</summary>
    internal static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "No accepted same-Container evidence is available.");
        var current = observations[^1];
        if (semanticDepth == 0 && Has(current, BranchA) && Has(current, BranchB))
        {
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty
                    .Add(BranchA, current.SequenceNumber)
                    .Add(BranchB, current.SequenceNumber),
                "P evidence proves the complete required sibling inventory {A, B}.",
                GroundingFor(current, BranchA, BranchB));
        }
        return new BranchInventoryEvidence(
            null,
            $"Evidence at seq={current.SequenceNumber} does not prove a complete inventory for depth={semanticDepth}.");
    }

    /// <summary>Primary-eligible canonical occurrence grounding for the required branches.</summary>
    private static ImmutableDictionary<string, NavigationSourceOccurrenceReference>? GroundingFor(
        Observation observation, params string[] branches)
    {
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
        {
            if (!occurrence.CanonicalOccurrence.EligibleForAuthorization
                || occurrence.CanonicalOccurrence.Reference.ElementIndex >= observation.Elements.Length)
                continue;
            var text = observation.Elements[occurrence.CanonicalOccurrence.Reference.ElementIndex].Text;
            if (branches.Contains(text, StringComparer.Ordinal))
                grounding[text] = new NavigationSourceOccurrenceReference(occurrence.ObservationSequence, occurrence.OccurrenceIdentity);
        }
        return grounding.Count == branches.Length ? grounding.ToImmutable() : null;
    }

    /// <summary>
    /// SC-P3-CAND-006 bounded independent authorization: the discovered A candidate is authorized
    /// from fresh P evidence; anything else stays unresolved. Not a dispatch, effect, or completion truth.
    /// </summary>
    internal static CandidateAuthorizationEvidence AuthorizeA(
        Observation observation,
        ObservedElement candidate)
    {
        if (!observation.Elements.Contains(candidate))
            throw new ArgumentException("Candidate must be contained in the supplied Observation.", nameof(candidate));
        return string.Equals(candidate.Text, BranchA, StringComparison.Ordinal)
            ? new CandidateAuthorizationEvidence(true, "Fresh evidence independently authorizes the discovered branch A.")
            : new CandidateAuthorizationEvidence(null, "Available evidence cannot authorize this candidate.");
    }

    /// <summary>Whole-Goal completion evidence stays independent and evidence-controlled (I-10).</summary>
    private static GoalEvidence EvaluateGoal(Observation observation)
        => new(
            observation.Elements.Any(element =>
                string.Equals(element.Text, "Independent goal evidence", StringComparison.Ordinal)),
            "Goal completion remains independently evidence-controlled.",
            observation.SequenceNumber);

    /// <summary>
    /// Deterministic side-effect-free Observation-only evaluator: exactly one "A external effect"
    /// element yields its SwitchState; otherwise the effect is unobservable (null). Reads only the
    /// supplied Observation and the captured immutable element text.
    /// </summary>
    private static bool? EvaluateBranchAEffect(Observation observation)
    {
        var matches = observation.Elements
            .Where(element => string.Equals(element.Text, ExternalEffect, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0].SwitchState : null;
    }

    private static DiscoveredBranchEffectRevalidationFixture Create(string runId, RevalidationPath path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var recoveredEffectState = path switch
        {
            RevalidationPath.Positive or RevalidationPath.AbsentCarrier
                or RevalidationPath.IdentityMismatch or RevalidationPath.AmbiguousParent => true,
            RevalidationPath.Contradicted => false,
            _ => (bool?)null,
        };
        var carrier = path switch
        {
            RevalidationPath.AbsentCarrier => null,
            RevalidationPath.IdentityMismatch => new BranchEffectCriterion(MismatchedIdentity, EvaluateBranchAEffect),
            _ => new BranchEffectCriterion(BranchA, EvaluateBranchAEffect),
        };
        var progressParent = path == RevalidationPath.AmbiguousParent
            ? ConflictingParentSemanticPage
            : ActiveParentSemanticPage;
        var stale = path == RevalidationPath.StaleEvidence
            ? new Dictionary<long, long> { [4] = 3 }
            : null;
        var environment = new ScriptedEnvironment(
            ActiveParentSemanticPage,
            launchNextScreenName: RecoveredParentScreen,
            Screens(recoveredEffectState),
            observeScreenTransitions: new Dictionary<long, string> { [5] = "Launcher" },
            observeSequenceOverrides: stale);
        return new DiscoveredBranchEffectRevalidationFixture(runId, environment, carrier, progressParent);
    }

    private static IEnumerable<ScreenConfig> Screens(bool? recoveredEffectState)
    {
        yield return new ScreenConfig(
            "ParentP",
            "Settings",
            [
                new ElementConfig(BranchA, null, TapTo("ChildA")),
                new ElementConfig(BranchB, null, TapTo("ChildB")),
            ]);
        yield return new ScreenConfig(
            "ChildA",
            "Settings",
            [
                new ElementConfig(
                    ExternalEffect,
                    false,
                    new TransitionConfig(ScreenTransitionAction.SetSwitch, "ChildAComplete", true)),
                new ElementConfig("Return to Parent P", null, TapTo("ParentAfterA")),
            ]);
        yield return new ScreenConfig(
            "ChildAComplete",
            "Settings",
            [
                new ElementConfig(ExternalEffect, true, null),
                new ElementConfig("Return to Parent P", null, TapTo("ParentAfterA")),
            ]);
        yield return new ScreenConfig(
            "ParentAfterA",
            "Settings",
            [
                new ElementConfig(BranchA, null, TapTo("ChildA")),
                new ElementConfig(BranchB, null, TapTo("ChildB")),
                new ElementConfig(ExternalEffect, true, null),
            ]);
        yield return new ScreenConfig(
            RecoveredParentScreen,
            "Settings",
            RecoveredParentElements(recoveredEffectState));
        yield return new ScreenConfig("Launcher", "Launcher", []);
        yield return new ScreenConfig(
            "ChildB",
            "Settings",
            [
                new ElementConfig("Complete B work", null, TapTo("ChildBComplete")),
                new ElementConfig("Return to Parent P", null, TapTo(RecoveredParentScreen)),
            ]);
        yield return new ScreenConfig(
            "ChildBComplete",
            "Settings",
            [
                new ElementConfig("B local effect", null, null),
                new ElementConfig("Return to Parent P", null, TapTo(RecoveredParentScreen)),
            ]);
    }

    private static ImmutableArray<ElementConfig> RecoveredParentElements(bool? recoveredEffectState)
    {
        var elements = ImmutableArray.CreateBuilder<ElementConfig>();
        elements.Add(new ElementConfig(BranchA, null, TapTo("ChildA")));
        elements.Add(new ElementConfig(BranchB, null, TapTo("ChildB")));
        if (recoveredEffectState is not null)
        {
            elements.Add(new ElementConfig(ExternalEffect, recoveredEffectState, null));
        }
        return elements.ToImmutable();
    }

    private static bool Has(Observation observation, string text)
        => observation.Elements.Any(element =>
            string.Equals(element.Text, text, StringComparison.Ordinal));

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);

    private enum RevalidationPath
    {
        Positive,
        Contradicted,
        Unresolved,
        AbsentCarrier,
        IdentityMismatch,
        AmbiguousParent,
        StaleEvidence,
    }
}

/// <summary>Immutable test-only SC-P3-CAND-009 external-world and evidence snapshot.</summary>
internal sealed record DiscoveredBranchEffectWorldEvidence(
    string RunId,
    Plan InitialPlan,
    Goal Goal,
    BranchEffectCriterion? Carrier,
    BranchEffectCriterion? MatchedCarrier,
    BranchInventoryEvidence Inventory,
    BranchProgressEvidence HistoricalProgress,
    CandidateAuthorizationEvidence AAuthorization,
    ImmutableArray<Observation> Observations,
    ImmutableArray<ActionResult> Dispatches,
    ImmutableArray<DeviceAction> ActionHistory,
    string ActiveParentSemanticPage,
    Observation StalePreRecoveryObservation,
    Observation FreshRecoveredObservation,
    bool? StaleCriterionOutcome,
    bool? FreshCriterionOutcome);
