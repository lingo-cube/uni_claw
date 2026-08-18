using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
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
/// SINGLE_AGENT_FULL_RUN_CAPSTONE — COMPOSE-05 on the REAL emulator-5556 through
/// the PRODUCTION pipeline: real AdbScreenshotSource + LocalVisionPerceptionSource
/// (UDS perception server) + AdbDispatchTarget, wrapped ONLY by a test-side
/// structured-evidence channel (real uiautomator dump parsed to
/// StructuredElementEvidence — the occurrence/provenance evidence source).
///
/// ONE Agent instance, ONE IntentExecution.RunOpenWorldAsync call, explicit
/// RequiredBranchGrounding, no internal-helper calls, no mock world, no LLM/VLM/DSH.
/// </summary>
public sealed class CapstoneSingleAgentRunTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private const string AdbPath = "/Users/fran/Android/Sdk/platform-tools/adb";
    private const string Serial = "emulator-5556";
    private const string VisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RunId = "capstone-real-run-001";
    private const string AgentInstanceId = "CAPSTONE-AGENT-001";

    private static int _agentCreations;

    private static string? ResolveSemanticPage(Observation observation)
    {
        // Structured evidence (real uiautomator) is the primary signal; OCR
        // element texts are a fallback (OCR can drop rows on noisy frames).
        var structured = observation.StructuredElements;

        // Child page: a "Fixture Root" element whose class is a Button (the
        // return anchor / popup dialog button). The child page's title TextView
        // is NOT in the structured channel (not clickable), so the child
        // identity comes from the OCR/vision channel. Once a child page is
        // detected, the resolver MUST NOT fall through to the root-page rule
        // (the "Fixture Root" Button would otherwise classify as the root).
        if (structured.Any(se =>
                string.Equals(se.TitleText, RootPage, StringComparison.Ordinal)
                && se.Class is not null
                && se.Class.Contains("Button", StringComparison.Ordinal)))
        {
            var child = structured.FirstOrDefault(se =>
                se.TitleText is not null
                && se.TitleText.StartsWith("Child ", StringComparison.Ordinal));
            if (child?.TitleText is not null)
                return child.TitleText;
            var childPageOcr = observation.Elements.FirstOrDefault(e =>
                e.Text.StartsWith("Child ", StringComparison.Ordinal));
            if (childPageOcr?.Text is not null)
                return childPageOcr.Text;
        }

        // Root page: the "Fixture Root" title TextView or the Visited state line.
        if (structured.Any(se =>
                string.Equals(se.TitleText, RootPage, StringComparison.Ordinal))
            || structured.Any(se =>
                se.TitleText is not null
                && se.TitleText.Contains("Visited", StringComparison.Ordinal)))
        {
            return RootPage;
        }

        // OCR element fallbacks.
        if (observation.Elements.Any(e =>
                string.Equals(e.Text, RootPage, StringComparison.Ordinal)))
        {
            return RootPage;
        }
        var ocrChild = observation.Elements.FirstOrDefault(e =>
            e.Text.StartsWith("Child ", StringComparison.Ordinal));
        return ocrChild?.Text;
    }

    private static string TitleOf(string signature)
    {
        int bar = signature.IndexOf('|');
        return bar < 0 ? signature : signature[..bar];
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
            var observation = await _inner.ObserveAsync(cancellationToken);
            var runner = new AdbProcessRunner();
            _ = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/cap.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var cat = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "cat", "/sdcard/cap.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var xml = System.Text.Encoding.UTF8.GetString(cat.StandardOutput);
            if (string.IsNullOrWhiteSpace(xml))
                return observation;
            try
            {
                var structured = AdbUiHierarchySource.Parse(xml, 1080, 1920);
                var decorated = observation with { StructuredElements = structured };
                AllObservations.Add(decorated);
                return decorated;
            }
            catch
            {
                AllObservations.Add(observation);
                return observation;
            }
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
        var environment = new StructuredEnvironment(rawEnvironment);

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
        evidence.AppendLine("OBSERVATIONS=" + string.Join(",", environment.ObservationHistory.Select(o => o.SequenceNumber)));
        foreach (var observation in environment.ObservationHistory)
            evidence.AppendLine($"OBS_TEXT[{observation.SequenceNumber}]=" + string.Join(" | ", observation.Elements.Select(e => e.Text)));
        foreach (var observation in environment.AllObservations)
        {
            foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(observation))
            {
                var raw = observation.StructuredElements[affordance.SourceElementIndex];
                evidence.AppendLine($"AFFORD[{observation.SequenceNumber}] {affordance.Classification} class={raw.Class} clickable={raw.Clickable} focusable={raw.Focusable} checkable={raw.Checkable} title={raw.TitleText} bounds={raw.Bounds}");
            }
        }
        evidence.AppendLine("ACTIONS=" + string.Join(",", environment.ActionHistory.Select(a => a.GetType().Name)));
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE {entry.RunState} | {entry.ContainerId} | {entry.StepId} | {entry.Reason}");
        foreach (var (key, progress) in agent.BranchProgress)
            evidence.AppendLine($"PROGRESS {key} = {progress}");
        evidence.AppendLine("GOAL_EVIDENCE=" + string.Join(";", receipts.Select(r => $"{r.Satisfied}@{r.SourceObservationSequence}")));
        File.WriteAllText("/tmp/capstone_evidence.txt", evidence.ToString());

        Assert.Equal(1, _agentCreations); // SingleAgentInstance
        Assert.Equal(RunState.Completed, state);
        Assert.True(receipts.Any(r => r.Satisfied)); // FinalGoalEvidenceSatisfied
    }
}
