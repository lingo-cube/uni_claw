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
/// SETTINGS_ROOT_REALITY_BASELINE — Phase 1 of settings-full-tree-enumeration-integration.
///
/// Answers the single question: can the GRADUATED Runtime independently prove
/// ContainerComplete(Root) on the REAL Android Settings root
/// (serial resolved via RealDeviceTestConfiguration, com.android.settings)
/// through the production pipeline? NO recursion, NO new
/// capability, NO classifier/normalizer/provenance/completeness changes.
///
/// The semantic root identity is established from PRODUCTION structured evidence
/// (structural rule: Settings foreground + interactive structured rows -> the
/// declared entry page "SettingsRoot") — no Settings title hardcodes. The
/// authorization surface is AUDIT-ONLY (every candidate receives an explicit
/// audit receipt; recursion authorization is Phase 2+), so Phase 1 never taps a
/// child. The first real production pressure stops the phase and is classified.
/// </summary>
[Collection("RealDevice")]
public sealed class SettingsRootRealityBaselineTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private static string AdbPath => RealDeviceTestConfiguration.AdbPath;
    private static string Serial => RealDeviceTestConfiguration.SettingsSerial;
    private const string VisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RunId = "settings-root-baseline-001";
    private const string AgentInstanceId = "SETTINGS-ROOT-BASELINE-001";

    private static int _agentCreations;

    /// <summary>
    /// Structural (classifier-derived) root identity rule — NO Settings title
    /// hardcodes: the page is the declared Settings root iff the foreground is
    /// com.android.settings AND the structured channel carries the root-specific
    /// search action bar (the Search bar's LOCAL_CONTROL affordance,
    /// rid=com.android.settings:id/search_action_bar). Child pages (Network,
    /// Apps, etc.) lack this marker and return null — the exploration fails
    /// closed (Phase 1 boundary: the traversal cannot leave the root). The
    /// search bar is a supporting root structural marker, NOT the sole identity
    /// authority (the entry boundary contract establishes the root identity at
    /// launch; the marker distinguishes the root from children during
    /// exploration continuity).
    /// </summary>
    private static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, App, StringComparison.Ordinal))
            return null;
        // Root identity: the root-specific search action bar (LOCAL_CONTROL,
        // rid=com.android.settings:id/search_action_bar) is present on the
        // root and absent on all child pages. This is a structural/role-based
        // marker, not a title hardcode.
        var hasSearchBar = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal));
        return hasSearchBar ? RootPage : null;
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

    private static ViewportExplorationEvidence ExploreWhileNew(ImmutableArray<Observation> observations)
    {
        if (observations.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(true, "explore");
        var latest = observations[^1];
        var latestSigs = NavSignatures(latest).ToHashSet(StringComparer.Ordinal);
        var prior = observations.Take(observations.Length - 1)
            .SelectMany(o => NavSignatures(o)).ToHashSet(StringComparer.Ordinal);
        var hasNew = latestSigs.Any(s => !prior.Contains(s));
        return new ViewportExplorationEvidence(
            hasNew,
            hasNew ? "new source appeared; scroll more" : "no new source; exhausted");
    }

    /// <summary>First-occurrence-per-title inventory (the graduated pattern) over the real Settings sources.</summary>
    private static BranchInventoryEvidence Inventory(ImmutableArray<Observation> observations, int semanticDepth)
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
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "no navigation occurrences (bounded leaf)",
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty);
        var required = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var (title, occurrence) in first)
        {
            required[title] = occurrence.ObservationSequence;
            grounding[title] = new NavigationSourceOccurrenceReference(
                occurrence.ObservationSequence, occurrence.OccurrenceIdentity);
        }
        return new BranchInventoryEvidence(required.ToImmutable(), $"inventory: {first.Count} sources", grounding.ToImmutable());
    }

    /// <summary>Phase-1 AUDIT-ONLY authorization surface: every candidate receives an explicit audit receipt (not granted).</summary>
    private static CandidateAuthorizationEvidence AuditAuthorization(Observation observation, ObservedElement candidate)
        => new(false, $"Phase-1 root reality baseline: source '{candidate.Text}' audited (DISCOVERED/GROUNDED), recursion authorization is Phase 2+.");

    private static GoalEvidence AuditGoal(Observation observation)
        => new(false, "Phase-1 audit goal (no full-tree claim).", observation.SequenceNumber);

    /// <summary>
    /// Test-side raw-XML audit: counts interactive nodes (clickable / checkable /
    /// Switch / CheckBox) whose bounds attribute is missing or malformed
    /// (negative-height etc.) — the persistent recycled-container artifacts the
    /// viewport eligibility admission boundary excludes. This counts the RAW
    /// dump, so it can only go DOWN when the pipeline is healthy; the admitted
    /// structured channel must carry ZERO of them.
    /// </summary>
    private static int CountRawInvalidInteractiveNodes(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return 0;
        System.Xml.Linq.XDocument document;
        try
        {
            document = System.Xml.Linq.XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return 0;
        }
        int count = 0;
        foreach (var node in document.Descendants("node"))
        {
            var className = (string?)node.Attribute("class") ?? "";
            var clickable = string.Equals((string?)node.Attribute("clickable"), "true", StringComparison.Ordinal);
            var checkable = string.Equals((string?)node.Attribute("checkable"), "true", StringComparison.Ordinal);
            var isSwitch = className.Contains("Switch", StringComparison.Ordinal)
                || className.Contains("CheckBox", StringComparison.Ordinal);
            if (!clickable && !checkable && !isSwitch)
                continue;
            var bounds = (string?)node.Attribute("bounds");
            if (string.IsNullOrWhiteSpace(bounds))
            {
                count++;
                continue;
            }
            var match = System.Text.RegularExpressions.Regex.Match(bounds, @"\[(\d+),(\d+)\]\[(\d+),(\d+)\]");
            if (!match.Success)
            {
                count++;
                continue;
            }
            var x1 = int.Parse(match.Groups[1].Value);
            var y1 = int.Parse(match.Groups[2].Value);
            var x2 = int.Parse(match.Groups[3].Value);
            var y2 = int.Parse(match.Groups[4].Value);
            if (x2 < x1 || y2 < y1 || x1 < 0 || y1 < 0)
                count++;
        }
        return count;
    }

    private sealed class StructuredEnvironment : IEnvironment
    {
        private readonly PhysicalEnvironment _inner;
        public StructuredEnvironment(PhysicalEnvironment inner) => _inner = inner;
        public IReadOnlyList<DeviceAction> ActionHistory => _inner.ActionHistory;
        public IReadOnlyList<Observation> ObservationHistory => _inner.ObservationHistory;
        public List<Observation> AllObservations { get; } = new();
        public List<string> RawXmls { get; } = new();

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var observation = await _inner.ObserveAsync(cancellationToken);
            var runner = new AdbProcessRunner();
            _ = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/settings_root.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var cat = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "cat", "/sdcard/settings_root.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var xml = System.Text.Encoding.UTF8.GetString(cat.StandardOutput);
            RawXmls.Add(xml);
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
    public async Task SettingsRoot_RealDevice_Phase1_RootContainerRealityBaseline()
    {
        _agentCreations = 0;
        var setupRunner = new AdbProcessRunner();
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "force-stop", App }, TimeSpan.FromSeconds(30), CancellationToken.None);
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "start", "-a", "android.settings.SETTINGS" }, TimeSpan.FromSeconds(30), CancellationToken.None);
        for (int i = 0; i < 20; i++)
        {
            var probe = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/ready_settings.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            var probeCat = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "cat", "/sdcard/ready_settings.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            if (System.Text.Encoding.UTF8.GetString(probeCat.StandardOutput).Contains("com.android.settings", StringComparison.Ordinal))
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
            launchIntentAction: "android.settings.SETTINGS");
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
                var evidence = AuditGoal(observation);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: AuditAuthorization,
            ViewportExplorationEvaluator: ExploreWhileNew,
            BranchInventoryEvaluator: Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-1 root reality baseline: prove ContainerComplete(Root) on real Settings (audit-only, no recursion)",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, RunId, CancellationToken.None);

        // ── evidence dump ────────────────────────────────────────────────────
        var evidence = new System.Text.StringBuilder();
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"AGENT_ID={AgentInstanceId} (creations={_agentCreations})");
        evidence.AppendLine($"RUN_ID={RunId}");
        evidence.AppendLine("OBSERVATIONS=" + string.Join(",", environment.ObservationHistory.Select(o => o.SequenceNumber)));
        // SCROLL-ARTIFACT ELIGIBILITY AUDIT: for every capture, count the raw
        // XML's interactive nodes with invalid/null bounds (the persistent
        // recycled-container artifacts) vs the number ADMITTED into the Agent's
        // structured evidence with invalid bounds (must be ZERO — the admission
        // boundary excludes NON_ACTIONABLE_STRUCTURAL_ARTIFACTS).
        for (int i = 0; i < environment.RawXmls.Count && i < environment.AllObservations.Count; i++)
        {
            var rawArtifacts = CountRawInvalidInteractiveNodes(environment.RawXmls[i]);
            var admitted = environment.AllObservations[i].StructuredElements;
            var admittedInvalid = admitted.Count(e => e.Bounds is not { IsValid: true });
            evidence.AppendLine($"ARTIFACT_AUDIT[{environment.AllObservations[i].SequenceNumber}] raw_invalid_interactive={rawArtifacts} admitted_total={admitted.Length} admitted_invalid={admittedInvalid}");
        }
        foreach (var observation in environment.ObservationHistory)
            evidence.AppendLine($"OBS_TEXT[{observation.SequenceNumber}]=" + string.Join(" | ", observation.Elements.Select(e => e.Text)));
        foreach (var observation in environment.AllObservations)
        {
            foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(observation))
            {
                // SourceElementIndex is per-source: primary affordances index the
                // Vision element array, auxiliary ones the structured array.
                var detail = affordance.SourceTier == UniClaw.Runtime.Capabilities.Perception.Semantic.V2.SemanticSourceTier.Primary
                    ? $"vision[{affordance.SourceElementIndex}] text={observation.Elements[affordance.SourceElementIndex].Text}"
                    : $"structured[{affordance.SourceElementIndex}] class={observation.StructuredElements[affordance.SourceElementIndex].Class} clickable={observation.StructuredElements[affordance.SourceElementIndex].Clickable} focusable={observation.StructuredElements[affordance.SourceElementIndex].Focusable} checkable={observation.StructuredElements[affordance.SourceElementIndex].Checkable} title={observation.StructuredElements[affordance.SourceElementIndex].RawText} rid={observation.StructuredElements[affordance.SourceElementIndex].ResourceId} cd={observation.StructuredElements[affordance.SourceElementIndex].ContentDescription} bounds={observation.StructuredElements[affordance.SourceElementIndex].Bounds}";
                evidence.AppendLine($"AFFORD[{observation.SequenceNumber}] {affordance.Classification} {detail}");
            }
        }
        foreach (var observation in environment.AllObservations)
        {
            var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(observation);
            var sigs = occurrences.Select(o => o.StructuredSignature).ToArray();
            evidence.AppendLine($"SIG[{observation.SequenceNumber}] count={sigs.Length} distinct={sigs.Distinct(StringComparer.Ordinal).Count()} sigs=[{string.Join(" | ", sigs)}]");
        }
        var acceptedForNorm = environment.AllObservations.ToImmutableArray();
        var normalization = SourceEquivalenceNormalizer.Normalize(acceptedForNorm);
        evidence.AppendLine($"NORMALIZATION_RAW_HISTORY resolved={normalization.IsResolved} uniqueSources={normalization.UniqueSourceSignatures.Length} (full-history dump incl. backward revisits; ordered-overlap is only defined over the forward discovery window)");
        // The AUTHORITATIVE normalization status is the production discovery
        // epoch recorded in the trace ("open-world container inventory
        // complete: sources=N, unresolved=0; discovery epoch FROZEN"). The
        // discovery epoch normalizes ONLY the forward accepted observations —
        // backward-revisit evidence is consistency-validated against the frozen
        // epoch, never re-normalized with it (non-monotonic evidence rule).
        var epochTrace = agent.Trace.FirstOrDefault(t =>
            t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        var sources = 0;
        var unresolved = -1;
        if (epochTrace?.Reason is { } reason)
        {
            var sourcesMatch = System.Text.RegularExpressions.Regex.Match(reason, @"sources=(\d+)");
            var unresolvedMatch = System.Text.RegularExpressions.Regex.Match(reason, @"unresolved=(\d+)");
            if (sourcesMatch.Success) sources = int.Parse(sourcesMatch.Groups[1].Value);
            if (unresolvedMatch.Success) unresolved = int.Parse(unresolvedMatch.Groups[1].Value);
        }
        evidence.AppendLine($"NORMALIZATION={(epochTrace is null ? "NO_EPOCH" : "PASS")} uniqueSources={sources} unresolved={unresolved} epochFrozen={epochTrace is not null}");
        evidence.AppendLine($"RAW_COUNT={environment.RawXmls.Count} OBS_COUNT={environment.AllObservations.Count}");
        System.Console.WriteLine($"[EVID] RAW_COUNT={environment.RawXmls.Count} OBS_COUNT={environment.AllObservations.Count}");
        // ROLE-STABILITY AUDIT: dump the raw hierarchy of the LAST observation
        // (the backward-revisit frame that triggered the coverage audit) plus any
        // observation whose captured TitleText is the Storage usage value.
        if (environment.RawXmls.Count > 0)
            evidence.AppendLine("RAW_XML_LAST=" + environment.RawXmls[^1]);
        for (int i = 0; i < environment.AllObservations.Count && i < environment.RawXmls.Count; i++)
        {
            var obs = environment.AllObservations[i];
            var hasUsageTitle = SourceEquivalenceNormalizer.OccurrencesOf(obs)
                .Any(o => o.StructuredSignature.StartsWith("38% used", StringComparison.Ordinal));
            if (hasUsageTitle)
                evidence.AppendLine($"RAW_XML[{obs.SequenceNumber}]=" + environment.RawXmls[i]);
        }
        evidence.AppendLine("ACTIONS=" + string.Join(",", environment.ActionHistory.Select(a => a.GetType().Name)));
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE {entry.RunState} | {entry.ContainerId} | {entry.StepId} | {entry.Reason}");
        evidence.AppendLine("GOAL_EVIDENCE=" + string.Join(";", receipts.Select(r => $"{r.Satisfied}@{r.SourceObservationSequence}")));
        File.WriteAllText("/tmp/settings_root_evidence.txt", evidence.ToString());

        // ── Phase-1 truth: the evidence (trace) decides ContainerComplete(Root) ──
        // The test asserts the graduated pipeline RAN on the real Settings root
        // (foreground ownership + root identity + production structured evidence)
        // and that the SCROLL-ARTIFACT eligibility boundary admitted ZERO
        // invalid-bound interaction evidence (the persistent recycled-container
        // artifacts never enter the Agent's actionable structured evidence).
        Assert.Equal(1, _agentCreations);
        Assert.Contains(environment.ObservationHistory, o =>
            string.Equals(o.ForegroundApplication, App, StringComparison.Ordinal));
        Assert.Contains(environment.AllObservations, o => !o.StructuredElements.IsDefaultOrEmpty);
        Assert.All(
            environment.AllObservations.SelectMany(o => o.StructuredElements),
            e => Assert.True(e.Bounds is { IsValid: true } && e.Bounds.Width > 0 && e.Bounds.Height > 0,
                "admitted interaction evidence must be a positive-area viewport occurrence (no NON_ACTIONABLE_STRUCTURAL_ARTIFACT)"));
    }
}
