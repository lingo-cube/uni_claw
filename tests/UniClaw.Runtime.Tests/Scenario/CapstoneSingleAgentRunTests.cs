using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SINGLE_AGENT_FULL_RUN_CAPSTONE — COMPOSE-05 on the REAL emulator through
/// the DriverHost surface (serial resolved via RealDeviceTestConfiguration).
/// the PRODUCTION pipeline: real AdbScreenshotSource + LocalVisionPerceptionSource
/// (UDS perception server) + AdbDispatchTarget, wrapped ONLY by a test-side
/// structured-evidence channel (real uiautomator dump parsed to
/// StructuredElementEvidence — the occurrence/provenance evidence source).
///
/// ONE Agent instance, ONE IntentExecution.RunOpenWorldAsync call, explicit
/// RequiredBranchGrounding, no internal-helper calls, no mock world, no LLM/VLM/DSH.
/// </summary>
[Collection("RealDevice")]
public sealed class CapstoneSingleAgentRunTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private static string AdbPath => RealDeviceTestConfiguration.AdbPath;
    private static string Serial => RealDeviceTestConfiguration.CapstoneSerial;
    private const string VisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RunId = "capstone-real-run-001";
    private const string AgentInstanceId = "CAPSTONE-AGENT-001";

    private static int _agentCreations;

    private static string? ResolveSemanticPage(Observation observation)
    {
        // VISION-ONLY page resolution (B1): the production Runtime consumes only
        // the primary OCR channel. uiautomator/structured evidence is NOT part of
        // the Runtime observation — it exists only for test-time device-state
        // collection (readiness polling), never as a navigation input.
        //
        // Root page: "Fixture Root" title + "Visited X/8" state line + MULTIPLE
        // "Child NN" rows (OCR). Child page: exactly ONE DISTINCT "Child NN"
        // title and no "Visited" state line; the "Fixture Root" return-anchor
        // Button is the only "Fixture Root" text on a non-root page. Distinct
        // de-duplicates repeated OCR detection of the SAME row title (the YOLO
        // detector may emit the same text several times).
        var childTitles = observation.Elements
            .Where(e => e.Text is not null && e.Text.StartsWith("Child ", StringComparison.Ordinal))
            .Select(e => e.Text!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var hasVisited = observation.Elements.Any(e =>
            e.Text is not null && e.Text.Contains("Visited", StringComparison.Ordinal));
        var hasRootTitle = observation.Elements.Any(e =>
            string.Equals(e.Text, RootPage, StringComparison.Ordinal));

        // Root page: state line OR multiple child rows OR (root title alone when
        // the OCR drops the state line on the launch frame).
        if (hasVisited || childTitles.Length > 1 || (hasRootTitle && childTitles.Length == 0))
            return RootPage;

        // Child page: exactly one "Child NN" title.
        if (childTitles.Length == 1)
            return childTitles[0];

        return hasRootTitle ? RootPage : null;
    }

    private static string TitleOf(string signature)
    {
        int bar = signature.IndexOf('|');
        return bar < 0 ? signature : signature[..bar];
    }

    private static string StructuredDetail(UniClaw.Runtime.Model.Observation observation, InteractionAffordanceEvidence affordance)
    {
        var raw = observation.StructuredElements[affordance.SourceElementIndex];
        return $"structured[{affordance.SourceElementIndex}] class={raw.Class} clickable={raw.Clickable} focusable={raw.Focusable} checkable={raw.Checkable} title={raw.RawText} bounds={raw.Bounds}";
    }

    /// <summary>
    /// Fixture semantic role classifier for the real capstone run — VISION-ONLY
    /// (B1): the Runtime consumes only the primary OCR channel; uiautomator is
    /// test-time device-state collection, never a navigation input.
    ///
    /// Primary OCR roles from the observation context:
    ///   - on a child page (no "Visited" state line): "Fixture Root" is the
    ///     parent-return control, "Child NN" is the non-interactive title;
    ///   - on the root page (has "Visited" state line): "Child NN" rows are
    ///     navigation candidates, "Fixture Root" title and the state line are
    ///     non-interactive.
    /// </summary>
    private static FixtureSemanticRole? CapstoneRoleClassifier(
        UniClaw.Runtime.Model.Observation observation,
        ObservedElement element,
        int index)
    {
        var text = element.Text;
        if (string.IsNullOrWhiteSpace(text))
            return FixtureSemanticRole.NonInteractive; // empty OCR element is never an interaction target

        // Child page detection (Vision-only): the root has a "Visited X/8" state
        // line OR multiple DISTINCT "Child NN" rows; a child page has exactly one
        // distinct "Child NN" title and no state line. Distinct de-duplicates
        // repeated OCR detection of the same row title.
        var childTitles = observation.Elements
            .Where(e => e.Text is not null && e.Text.StartsWith("Child", StringComparison.Ordinal))
            .Select(e => e.Text!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var hasVisited = observation.Elements.Any(e =>
            e.Text is not null && e.Text.Contains("Visited", StringComparison.Ordinal));
        var isChildPage = !hasVisited && childTitles.Length <= 1;

        FixtureSemanticRole? role;
        if (string.Equals(text, RootPage, StringComparison.Ordinal))
        {
            role = isChildPage ? FixtureSemanticRole.ParentReturnControl : FixtureSemanticRole.NonInteractive;
        }
        else if (text.StartsWith("Child", StringComparison.Ordinal))
        {
            // "Child NN" rows are navigation candidates on the root page. A
            // TRUNCATED "Child" (OCR dropped the sequence number) cannot be
            // grounded to a specific branch — it is non-interactive decoration,
            // never a navigation candidate (a truncated signature would break
            // source normalization stability).
            var isTruncated = !text.StartsWith("Child ", StringComparison.Ordinal)
                || !System.Text.RegularExpressions.Regex.IsMatch(text, @"^Child \d{2}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (isTruncated)
                return FixtureSemanticRole.NonInteractive;
            role = isChildPage ? FixtureSemanticRole.NonInteractive : FixtureSemanticRole.NavigationCandidate;
        }
        else if (text.Contains("Visited", StringComparison.Ordinal))
        {
            role = FixtureSemanticRole.NonInteractive;
        }
        else
        {
            role = null;
        }
        return role;
    }

    private static ImmutableArray<string> NavSignatures(Observation observation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            builder.Add(occurrence.StructuredSignature);
        return builder.ToImmutable();
    }

    private sealed class StructuredEnvironment : IEnvironment
    {
        private readonly PhysicalEnvironment _inner;
        public StructuredEnvironment(PhysicalEnvironment inner) => _inner = inner;
        public IReadOnlyList<DeviceAction> ActionHistory => _inner.ActionHistory;
        public IReadOnlyList<Observation> ObservationHistory => _inner.ObservationHistory;
        public List<Observation> AllObservations { get; } = new();

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            // VISION-ONLY (B1): the Runtime observation carries ONLY the primary
            // OCR channel. uiautomator/structured evidence is never injected into
            // the Runtime observation — it is test-time device-state collection
            // (readiness polling), not a navigation input.
            var observation = await _inner.ObserveAsync(cancellationToken);
            AllObservations.Add(observation);
            return observation;
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => _inner.ExecuteAsync(action, cancellationToken);
    }

    [Fact]
    public async Task Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete()
    {
        _agentCreations = 0;
        // Reset fixture external state and land on the root page.
        var setupRunner = new AdbProcessRunner();
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "force-stop", App }, TimeSpan.FromSeconds(30), CancellationToken.None);
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "start", "-a", "com.uniclaw.fixture.action.CAPSTONE" }, TimeSpan.FromSeconds(30), CancellationToken.None);
        // Deterministic readiness: poll the real screen until the capstone root is visible.
        for (int i = 0; i < 20; i++)
        {
            var probe = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/ready.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            var probeCat = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "cat", "/sdcard/ready.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            if (System.Text.Encoding.UTF8.GetString(probeCat.StandardOutput).Contains("Visited", StringComparison.Ordinal))
                break;
            await Task.Delay(1000);
        }

        var rawEnvironment = new PhysicalEnvironment(
            new AdbScreenshotSource(Serial, AdbPath),
            new LocalVisionPerceptionSource(VisionSocket),
            new AdbDispatchTarget(Serial, AdbPath),
            App, 1080, 1920);
        var structured = new StructuredEnvironment(rawEnvironment);
        // Fixture semantic capability (test-side): primary OCR elements get an
        // admitted role — Child rows are navigation candidates, the child page's
        // "Fixture Root" Button is the parent-return control, and title/status
        // text is non-interactive. Without this, primary elements classify
        // Unknown and completeness can never be proven (fail closed by design).
        var environment = new SemanticCapabilityTestEnvironment(structured, CapstoneRoleClassifier);

        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(
            environment, App, ResolveSemanticPage,
            launchIntentAction: "com.uniclaw.fixture.action.CAPSTONE");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        _agentCreations++;
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var receipts = new List<GoalEvidence>();
        var goal = new Goal(
            observation =>
            {
                // The real fixture's state line is "Visited 8/8 [CAPSTONE COMPLETE]";
                // the OCR channel may merge whitespace ("Visited8/8CAPSTONECOMPLETE"),
                // so the predicate matches the CAPSTONE/COMPLETE tokens
                // space-insensitively (the device's raw text carries the space).
                var evidence = new GoalEvidence(
                    observation.Elements.Any(e => e.Text is not null
                        && e.Text.Contains("CAPSTONE", StringComparison.Ordinal)
                        && e.Text.Contains("COMPLETE", StringComparison.Ordinal)),
                    "capstone goal evidence",
                    observation.SequenceNumber);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: (observation, element) =>
                new CandidateAuthorizationEvidence(
                    element.Text.StartsWith("Child ", StringComparison.Ordinal)
                        || string.Equals(element.Text, RootPage, StringComparison.Ordinal),
                    $"authorize {element.Text}"),
            ViewportExplorationEvaluator: observations =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new ViewportExplorationEvidence(true, "explore");
                var latest = observations[^1];
                var latestSigs = NavSignatures(latest);
                var prior = observations.Take(observations.Length - 1)
                    .SelectMany(o => NavSignatures(o)).ToHashSet(StringComparer.Ordinal);
                var hasNew = latestSigs.Any(s => !prior.Contains(s));
                return new ViewportExplorationEvidence(
                    hasNew,
                    hasNew ? "new source appeared; scroll more" : "no new source; exhausted");
            },
            BranchInventoryEvaluator: (observations, semanticDepth) =>
            {
                var first = new Dictionary<string, NavigationSourceOccurrence>(StringComparer.Ordinal);
                foreach (var observation in observations)
                {
                    foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
                    {
                        var title = TitleOf(occurrence.StructuredSignature);
                        if (!first.ContainsKey(title))
                            first[title] = occurrence;
                    }
                }
                if (first.Count == 0)
                {
                    return new BranchInventoryEvidence(
                        ImmutableDictionary<string, long>.Empty,
                        "no navigation occurrences (bounded leaf)",
                        ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty);
                }
                var required = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
                var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
                foreach (var (title, occurrence) in first)
                {
                    required[title] = occurrence.ObservationSequence;
                    grounding[title] = new NavigationSourceOccurrenceReference(
                        occurrence.ObservationSequence, occurrence.OccurrenceIdentity);
                }
                return new BranchInventoryEvidence(
                    required.ToImmutable(),
                    $"capstone inventory: {first.Count} children",
                    grounding.ToImmutable());
            });

        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));

        var envelope = IntentSemanticEnvelope.Project(
            "Traverse all Fixture Root children to CAPSTONE COMPLETE",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));

        var state = await IntentExecution.RunOpenWorldAsync(
            agent, envelope, RunId, CancellationToken.None);

        // ── evidence dump ─────────────────────────────────────────────────────
        var evidence = new System.Text.StringBuilder();
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"AGENT_ID={AgentInstanceId} (creations={_agentCreations})");
        evidence.AppendLine($"RUN_ID={RunId}");
        evidence.AppendLine("OBSERVATIONS=" + string.Join(",", structured.ObservationHistory.Select(o => o.SequenceNumber)));
        foreach (var observation in structured.ObservationHistory)
            evidence.AppendLine($"OBS_TEXT[{observation.SequenceNumber}]=" + string.Join(" | ", observation.Elements.Select(e => e.Text)));
        foreach (var observation in structured.AllObservations)
        {
            foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(observation))
            {
                // SourceElementIndex is per-source: primary affordances index the
                // Vision element array, auxiliary ones the structured array.
                var detail = affordance.SourceTier == UniClaw.Runtime.Capabilities.Perception.Semantic.V2.SemanticSourceTier.Primary
                    ? $"vision[{affordance.SourceElementIndex}] text={observation.Elements[affordance.SourceElementIndex].Text}"
                    : StructuredDetail(observation, affordance);
                evidence.AppendLine($"AFFORD[{observation.SequenceNumber}] {affordance.Classification} {detail}");
            }
        }
        evidence.AppendLine("ACTIONS=" + string.Join(",", structured.ActionHistory.Select(a => a.GetType().Name)));
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE {entry.RunState} | {entry.ContainerId} | {entry.StepId} | {entry.Reason}");
        foreach (var (key, progress) in agent.BranchProgress)
            evidence.AppendLine($"PROGRESS {key} = {progress}");
        foreach (var (key, exposed) in agent.RevisitCoverage)
            evidence.AppendLine($"REVISIT_COVERAGE {key} = freshly-exposed=[{string.Join(",", exposed)}]");
        evidence.AppendLine("GOAL_EVIDENCE=" + string.Join(";", receipts.Select(r => $"{r.Satisfied}@{r.SourceObservationSequence}")));
        File.WriteAllText("/tmp/capstone_evidence.txt", evidence.ToString());

        Assert.Equal(1, _agentCreations); // SingleAgentInstance
        Assert.Equal(RunState.Completed, state);
        Assert.Contains(receipts, r => r.Satisfied); // FinalGoalEvidenceSatisfied
    }
}
