using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-EVH-002 capability tests (tasks 3.1-3.4): the Emulator Driver boundary —
/// legal directives transported exactly once with the admission outcome in the
/// immutable call log; the seven forbidden payload-content categories rejected
/// deterministically BEFORE any wire call (call log proves zero transport);
/// a goal-only dispatch yields DIRECTIVE_REQUIRED with zero inference; and the
/// call log is immutable after build. Structure is EvidenceFixture → Runtime
/// Execution → Evidence Evaluation. The forbidden-content test INPUTS carry
/// coordinate/path/action strings on purpose — that is the point — but no
/// assertion depends on fixed coordinates, page text, UI paths, click counts,
/// or action histories: assertions check the deterministic rejection, the
/// admission outcome, and the call-log boundary proof.
/// </summary>
public sealed class EmulatorDriverTests
{
    private const string Goal = "Explore the settings scope and record everything reachable.";

    // ── Spec scenario "Legal directive is transported" ─────────────────────────

    [Fact]
    public async Task LegalDirective_IsTransportedExactlyOnce_AdmissionAcceptAndRunIdInCallLog()
    {
        // ── EvidenceFixture ─────────────────────────────────────────────────
        var world = FixtureComposition.CreateSettingsWorld();
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(world),
            FixtureComposition.CreateCompiler());
        var directive = DirectiveFixtureCatalog.BuildLegalDirective("evh-driver-legal-1");
        var transport = new LoopbackEmulatorTransport(host.BoundPort);
        var driver = new EmulatorDriver(transport);

        // ── Runtime Execution: exactly ONE run.strategy.start via the wire ──
        var result = await driver.StartAsync(Goal, directive, FixtureComposition.FixtureDeviceText);

        // ── Evidence Evaluation ─────────────────────────────────────────────
        var transported = Assert.IsType<DriverDispatchResult.Transported>(result);
        Assert.True(transported.Admission.Accepted, "The fixture directive must be deterministically accepted.");
        Assert.False(string.IsNullOrWhiteSpace(transported.Admission.RunId), "An accepted run carries a DriverHost-owned runId.");
        Assert.Equal("Idle", transported.Admission.RunState);
        Assert.Null(transported.Admission.RejectionCode);

        Assert.Equal(1, transport.SentRequestCount); // exactly one wire call
        var entry = Assert.Single(driver.CallLog.Entries);
        Assert.Equal(EmulatorDriver.StartStrategyMethod, entry.Method);
        Assert.Equal(EmulatorCallOutcome.Accepted, entry.Outcome);
        Assert.Equal(transported.Admission.RunId, entry.Detail);
        Assert.Equal(64, entry.PayloadDigest.Length); // SHA-256 hex digest
        Assert.True(entry.TimestampUtc <= DateTimeOffset.UtcNow);

        // Digest is the SHA-256 of the canonical transported params, recomputed
        // independently here (deterministic canonicalization, task 3.2).
        var expectedDigest = StrategyPayloadJson.CanonicalDigest(StrategyPayloadJson.BuildParameters(
            StrategyPayloadJson.Freeze(directive),
            FixtureComposition.FixtureDeviceText));
        Assert.Equal(expectedDigest, entry.PayloadDigest);
    }

    // ── Spec scenario "Legal directive is transported": deterministic
    //    admission Reject(code) is also recorded verbatim ────────────────────

    [Fact]
    public async Task ReusedStrategyIdentity_SecondStart_AdmissionRejectCodeRecordedInCallLog()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var directive = DirectiveFixtureCatalog.BuildLegalDirective("evh-driver-duplicate-1");

        var first = await driver.StartAsync(Goal, directive, FixtureComposition.FixtureDeviceText);
        Assert.True(Assert.IsType<DriverDispatchResult.Transported>(first).Admission.Accepted);

        // Same strategy identity again — Runtime admission answers a
        // deterministic Reject(duplicateStrategy), which the driver records.
        var second = await driver.StartAsync(Goal, directive, FixtureComposition.FixtureDeviceText);
        var secondResult = Assert.IsType<DriverDispatchResult.Transported>(second);
        Assert.False(secondResult.Admission.Accepted);
        Assert.Equal("duplicateStrategy", secondResult.Admission.RejectionCode);
        Assert.False(string.IsNullOrWhiteSpace(secondResult.Admission.RejectionReason));

        Assert.Equal(2, driver.CallLog.Count);
        Assert.Equal(EmulatorCallOutcome.Accepted, driver.CallLog.Entries[0].Outcome);
        var rejectEntry = driver.CallLog.Entries[^1];
        Assert.Equal(EmulatorCallOutcome.RejectedByAdmission, rejectEntry.Outcome);
        Assert.Contains("duplicateStrategy", rejectEntry.Detail);
        Assert.Equal(64, rejectEntry.PayloadDigest.Length);
    }

    // ── Spec scenario "Forbidden directive content is blocked before
    //    transport": one case per category; the INPUTS may contain
    //    coordinate/path strings (that is the point) — assertions check the
    //    deterministic rejection and the zero-wire proof, never the content. ─

    public static TheoryData<string, DirectiveForbiddenCategory, string> ForbiddenContentCases => new()
    {
        { "coordinate", DirectiveForbiddenCategory.Coordinate, "aim at 0.45,0.85" },
        { "ui page path", DirectiveForbiddenCategory.UiPagePath, "settings/connectivity/wifi" },
        { "click sequence", DirectiveForbiddenCategory.ClickSequence, "tap childOne then tap back" },
        { "element locator", DirectiveForbiddenCategory.ElementLocator, "#settings-list" },
        { "action selection", DirectiveForbiddenCategory.ActionSelection, "press the power control" },
        { "callback", DirectiveForbiddenCategory.Callback, "onClick handler registered" },
        { "unresolved prose", DirectiveForbiddenCategory.UnresolvedProse, "explore the whole settings application scope" },
    };

    [Theory]
    [MemberData(nameof(ForbiddenContentCases))]
    public async Task ForbiddenDirectiveContent_IsRejectedBeforeTransport_ZeroWireCalls_Logged(
        string _, DirectiveForbiddenCategory expectedCategory, string poisonedStrategyId)
    {
        var transport = new RecordingTransport();
        var driver = new EmulatorDriver(transport);
        var directive = DirectiveFixtureCatalog.BuildLegalDirective(poisonedStrategyId);

        var result = await driver.StartAsync(Goal, directive, FixtureComposition.FixtureDeviceText);

        var rejected = Assert.IsType<DriverDispatchResult.RejectedBeforeTransport>(result);
        Assert.Equal(expectedCategory, rejected.Category);
        Assert.Equal(0, transport.SentRequestCount); // call log proves zero wire calls (D5.1)

        var entry = Assert.Single(driver.CallLog.Entries);
        Assert.Equal(EmulatorDriver.StartStrategyMethod, entry.Method);
        Assert.Equal(EmulatorCallOutcome.RejectedBeforeTransport, entry.Outcome);
        Assert.Contains("REJECTED_BEFORE_TRANSPORT", entry.Detail);
        Assert.Equal(64, entry.PayloadDigest.Length); // the attempted payload is digest-logged
    }

    // ── Spec scenario "No strategy inference": goal-only → DIRECTIVE_REQUIRED ─

    [Fact]
    public async Task GoalOnlyWithoutDirective_ReturnsDirectiveRequired_ZeroWireCalls_Logged()
    {
        var transport = new RecordingTransport();
        var driver = new EmulatorDriver(transport);

        var result = await driver.StartAsync(Goal, directive: null, FixtureComposition.FixtureDeviceText);

        Assert.IsType<DriverDispatchResult.DirectiveRequired>(result);
        Assert.Equal(0, transport.SentRequestCount);
        var entry = Assert.Single(driver.CallLog.Entries);
        Assert.Equal(EmulatorDriver.StartStrategyMethod, entry.Method);
        Assert.Equal(EmulatorCallOutcome.DirectiveRequired, entry.Outcome);
        Assert.Contains("DIRECTIVE_REQUIRED", entry.Detail);
        Assert.Equal(string.Empty, entry.PayloadDigest); // no payload existed to digest
    }

    [Fact]
    public async Task GoalOnlyFixtureRecord_ReturnsDirectiveRequired_ZeroWireCalls_Logged()
    {
        var transport = new RecordingTransport();
        var driver = new EmulatorDriver(transport);

        var result = await driver.StartAsync(DirectiveFixtureCatalog.GoalOnly());

        Assert.IsType<DriverDispatchResult.DirectiveRequired>(result);
        Assert.Equal(0, transport.SentRequestCount);
        var entry = Assert.Single(driver.CallLog.Entries);
        Assert.Equal(EmulatorCallOutcome.DirectiveRequired, entry.Outcome);
        Assert.Contains("DIRECTIVE_REQUIRED", entry.Detail);
    }

    // ── Dual mode (design D2, task 3.3): recorded fixture pair drives the
    //    SAME transport path as the live handoff ─────────────────────────────

    [Fact]
    public async Task RecordedFixtureDirective_DrivesSameTransportPathAsLiveHandoff()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));

        var result = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());

        var transported = Assert.IsType<DriverDispatchResult.Transported>(result);
        Assert.True(transported.Admission.Accepted);
        var entry = Assert.Single(driver.CallLog.Entries);
        Assert.Equal(EmulatorCallOutcome.Accepted, entry.Outcome);
        Assert.Equal(transported.Admission.RunId, entry.Detail);
    }

    // ── Call-log immutability (task 3.2 / 3.4 #4) ──────────────────────────────

    [Fact]
    public void CallLog_IsImmutableAfterBuild_MutationAttemptsProduceNewInstances_OriginalUnchanged()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var runEntry = new EmulatorCallLogEntry(
            EmulatorDriver.StartStrategyMethod, "digest-run", EmulatorCallOutcome.Accepted, "run-1", timestamp);
        var built = EmulatorCallLog.Empty.Append(runEntry);
        var original = built;

        var later = new EmulatorCallLogEntry(
            EmulatorDriver.StartStrategyMethod, "digest-required", EmulatorCallOutcome.DirectiveRequired, "DIRECTIVE_REQUIRED", timestamp.AddSeconds(1));

        // Append produces a NEW instance; the original is unchanged.
        var appended = original.Append(later);
        Assert.Equal(2, appended.Count);
        Assert.Equal(1, original.Count);
        Assert.Equal(runEntry, original.Entries.Single());

        // The functional ImmutableArray surface: growing it yields a NEW array
        // — the built log's sequence cannot be mutated in place.
        var grew = original.Entries.Add(later);
        Assert.Equal(2, grew.Length);
        Assert.Equal(1, original.Count);
        Assert.Equal(runEntry, original.Entries[0]);

        // Value equality is sequence-based (mirror ExplorationLedgerView style):
        // a log rebuilt with the same entries equals the original; one extra
        // entry does not.
        Assert.Equal(EmulatorCallLog.Empty.Append(runEntry), original);
        Assert.NotEqual(EmulatorCallLog.Empty.Append(runEntry).Append(later), original);
    }

    // ── Closed-vocabulary validation (task 3.1), direct, no transport ─────────

    [Fact]
    public void UndefinedEnumValue_IsRejectedAsClosedVocabularyViolation()
    {
        var validator = new StrategyDirectiveValidator();
        var payload = StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.BuildLegalDirective("evh-vocab-1"));
        payload["objective"]!["kind"] = "exploreEverything";

        var result = validator.Validate(payload);

        var rejected = Assert.IsType<DirectiveValidationResult.Rejected>(result);
        Assert.Null(rejected.Category); // vocabulary violation, not forbidden content
        Assert.Contains("objective.kind", rejected.Reason);
        Assert.Contains("exploreEverything", rejected.Reason);
    }

    [Fact]
    public void BlankClosedStringField_IsRejected()
    {
        var validator = new StrategyDirectiveValidator();
        var payload = StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.BuildLegalDirective("evh-vocab-2"));
        payload["scope"]!["semanticRoot"] = "   ";

        var rejected = Assert.IsType<DirectiveValidationResult.Rejected>(validator.Validate(payload));
        Assert.Contains("scope.semanticRoot", rejected.Reason);
    }

    [Fact]
    public void UnknownDirectiveField_IsRejectedAgainstClosedShape()
    {
        var validator = new StrategyDirectiveValidator();
        var payload = StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.BuildLegalDirective("evh-vocab-3"));
        payload["injectedField"] = "x";

        var rejected = Assert.IsType<DirectiveValidationResult.Rejected>(validator.Validate(payload));
        Assert.Contains("injectedField", rejected.Reason);
    }

    [Fact]
    public void DepthBeyondHardMaximumGuard_IsRejectedBeforeTransport()
    {
        var validator = new StrategyDirectiveValidator();
        var payload = StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.BuildLegalDirective("evh-vocab-4"));
        payload["scope"]!["maximumDepth"] = 100;

        var rejected = Assert.IsType<DirectiveValidationResult.Rejected>(validator.Validate(payload));
        Assert.Contains("maximumDepth", rejected.Reason);
    }

    [Fact]
    public void UnsupportedContractVersion_IsRejectedBeforeTransport()
    {
        var validator = new StrategyDirectiveValidator();
        var payload = StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.BuildLegalDirective("evh-vocab-5"));
        payload["contractVersion"] = 2;

        var rejected = Assert.IsType<DirectiveValidationResult.Rejected>(validator.Validate(payload));
        Assert.Contains("contractVersion", rejected.Reason);
    }

    // ── Canonical payload digest (task 3.2) ───────────────────────────────────

    [Fact]
    public void PayloadDigest_IsCanonical_IndependentOfSetOrdering()
    {
        var ordered = StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.BuildLegalDirective("evh-digest-1"));
        var reversed = StrategyPayloadJson.Freeze(BuildAdaptationReversedDirective("evh-digest-1"));

        var digestA = StrategyPayloadJson.CanonicalDigest(StrategyPayloadJson.BuildParameters(ordered, FixtureComposition.FixtureDeviceText));
        var digestB = StrategyPayloadJson.CanonicalDigest(StrategyPayloadJson.BuildParameters(reversed, FixtureComposition.FixtureDeviceText));
        var digestOtherDevice = StrategyPayloadJson.CanonicalDigest(StrategyPayloadJson.BuildParameters(ordered, "other-device"));

        Assert.Equal(64, digestA.Length);
        Assert.Equal(digestA, digestB);
        Assert.NotEqual(digestA, digestOtherDevice);
    }

    // ── Test double ───────────────────────────────────────────────────────────

    /// <summary>Counting transport double: attests zero wire calls without a host.</summary>
    private sealed class RecordingTransport : IEmulatorTransport
    {
        public long SentRequestCount { get; private set; }

        public Task<JsonObject> SendAsync(string method, JsonObject? parameters, CancellationToken cancellationToken = default)
        {
            SentRequestCount++;
            return Task.FromResult<JsonObject>(new JsonObject { ["result"] = new JsonObject { ["accepted"] = true } });
        }
    }

    /// <summary>Same logical directive with reversed constraint/adaptation set
    /// insertion order — the canonical digest must not depend on it.</summary>
    private static StrategyDirective BuildAdaptationReversedDirective(string strategyId)
        => new(
            strategyId,
            contractVersion: StrategyContractCompiler.SupportedContractVersion,
            objective: new StrategyObjective(StrategyObjectiveKind.ExploreScope),
            scope: new StrategyScope(FixtureStrategyBinding.Application, FixtureStrategyBinding.Root, 1),
            exploration: ExplorationIntent.ExhaustiveWithinScope,
            constraints: new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                ImmutableHashSet.Create(
                    StrategyProhibitedEffect.ExternalBoundaryCrossing,
                    StrategyProhibitedEffect.StateMutation)),
            completion: new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
            adaptation: new StrategyAdaptationBoundary(
                ImmutableHashSet.Create(
                    StrategyAdaptationKind.ReviseExecutionHypothesis,
                    StrategyAdaptationKind.ReconcileBelief)));
}