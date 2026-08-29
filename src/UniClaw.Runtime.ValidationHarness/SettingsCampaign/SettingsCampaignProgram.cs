// Phase 2.6 — Real-Emulator iterative campaign entry over REAL Android Settings.
// Validation-side composition only (spec "Validation tooling, never runtime or
// planning capability"): composes the REAL production pipeline (CanonicalVisionHostFactory
// UDS vision host + AdbScreenshotSource + LocalVisionPerceptionSource + AdbDispatchTarget)
// with the harness-local SettingsStrategyBinding (design D6), drives N independent
// Runtime Runs through the graduated IterativeCampaignRunner (one run.strategy.start
// per round, zero emulator mid-run intervention), and emits the campaign outcome as
// deterministic JSON evidence.
//
// The upper-agent planner here is the CONSERVATIVE Stage-A posture: it does not adapt
// between rounds yet (Stage B adds evidence-informed PlanDeltas; this entry exists to
// prove the real-Settings composition executes one autonomous conservative run and to
// harvest the first ScenarioKnowledgeFixture increment).
//
// Usage: dotnet run --project src/UniClaw.Runtime.ValidationHarness -- settingscampaign <rounds>
// Environment: emulator-5554 booted (real Android Settings present), vision stack at
// platforms/perception (governance receipt + .venv-local-vision).
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.World;
using UniClaw.Runtime.ValidationHarness.Campaign;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Scenarios;
using UniClaw.Runtime.ValidationHarness.SettingsBinding;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using UniClaw.Runtime.ValidationHarness.SettingsCampaign.Adaptation;
using UniClaw.Vision.Host;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign;

public static class SettingsCampaignProgram
{
    private const string App = SettingsStrategyBinding.ApplicationIdentity;
    private const string Serial = "emulator-5554";
    private const string AdbPath = "/opt/homebrew/share/android-commandlinetools/platform-tools/adb";
    private const string LaunchIntentAction = "android.settings.SETTINGS";

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var rounds) || rounds < 1 || rounds > 8)
        {
            Console.Error.WriteLine("usage: settingscampaign <rounds 1..8> [--depth N]");
            return 2;
        }

        var depth = 1;
        var adapt = false;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--adapt")
                adapt = true;
            if (args[i] == "--depth" && i + 1 < args.Length && int.TryParse(args[i + 1], out var d) && d is >= 1 and <= 4)
                depth = d;
        }

        var repoRoot = FindRepoRoot();
        // Receipt path: canonical CURRENT-ACTIVE by default; a validation-scoped
        // candidate receipt (unpromoted working-tree perception state, e.g. the
        // S1+S2+S4 candidate) may be supplied via P26_VISION_RECEIPT — exactly the
        // shadow-receipt pattern the perception repair's campaigns used. The
        // canonical artifact is never modified.
        var receipt = System.Environment.GetEnvironmentVariable("P26_VISION_RECEIPT")
            ?? Path.Combine(repoRoot, "platforms/perception/governance/artifacts/current-active-identity.json");
        if (!Path.IsPathRooted(receipt))
            receipt = Path.Combine(repoRoot, receipt);
        var python = Path.Combine(repoRoot, ".venv-local-vision/bin/python");

        using var vision = CanonicalVisionHostFactory.Create(receipt, pythonExecutable: python, repoRoot: repoRoot);
        await vision.StartAsync(cancellationToken);
        await Console.Error.WriteLineAsync($"[campaign] vision host {vision.State} at {vision.SocketPath}");

        // REAL environment (production pipeline, zero fake): screenshot → UDS vision →
        // adb dispatch, wrapped by the harness-local Settings binding composition so
        // observations carry the production SettingsSemanticCapability's admitted
        // evidence.
        //
        // STRUCTURED AUXILIARY TIER (E4 evidence repair, Stage-A round 1): the
        // real-Settings page-identity anchors (search_action_bar root marker;
        // collapsing_toolbar + "Navigate up" on children) exist ONLY in the
        // structured tier — vision facts carry no resource names
        // (SemanticObservationFactProjector.AddVisionFacts emits no ResourceName).
        // The graduated real-Settings compositions attach exactly this source
        // (SettingsSingleRecursiveChild_RealDevice_Phase2's StructuredEnvironment;
        // ValidationRunner's BuildRealEnvironment(structuredUiSource)); the
        // vision-only pattern is the FIXTURE-APP capstone composition (Tier B),
        // whose OCR-text semantics do not apply to real Settings. Primary-tier
        // authority is unchanged: auxiliary facts are corroboration only
        // (SettingsSemanticCapability) and the normalizer derives navigation
        // occurrences from admitted primary evidence. Best-effort acquisition is
        // the production seam's own fail-closed semantics.
        // ROW IDENTITY CONTEXT (DESIGN-SPEC D2/D4): C# owns the row memory.
        // Python receives X-Known-Rows on each request and returns row_id for
        // known rows. This wrapper STABILIZES each observation retroactively
        // (first-frame elements get StableKey assigned here) so every element
        // the Runtime sees carries a stable identity from the very first frame.
        var perceptionSource = new LocalVisionPerceptionSource(vision.SocketPath);
        var captureStageViews = IsEnabled(System.Environment.GetEnvironmentVariable("P26_CAPTURE_STAGE_VIEWS"));
        perceptionSource.CaptureStageViews = captureStageViews;
        // Fusion causal trace (PROJECT_LEADER_PERCEPTION_FUSION_TRACE_COVERAGE_GATE):
        // opt-in compact trace per observation; persisted next to the frames for
        // the exact-predicate answer.  Trace-only diagnostics — never consumed by
        // any Runtime decision (TRACE != CONTROL / EVIDENCE AUTHORITY / ADMISSION).
        perceptionSource.EmitTrace = !IsEnabled(System.Environment.GetEnvironmentVariable("P26_NO_TRACE"));
        var stageEvidence = new List<object>();
        var fusionTraces = new List<object>();
        var rowContext = new RowIdentityContext();
        var raw = new PhysicalEnvironment(
            new AdbScreenshotSource(Serial, AdbPath),
            perceptionSource,
            new AdbDispatchTarget(Serial, AdbPath),
            App, 1080, 1920,
            structuredUiSource: new AdbUiHierarchySource(Serial, AdbPath));
        var environment = SettingsBindingComposition.Wrap(raw);

        var traversal = new RuntimeTraversal(environment);
        // E4 diagnostic tap (validation-side): record every observation the
        // Runtime actually consumed (vision elements + structured tier + sources)
        // so normalization/resolution failures can be diagnosed from real frames
        // instead of guesses. Pure observation; no Runtime input.
        // ALSO: stabilizes row identities + refreshes X-Known-Rows (D2).
        var tapEnvironment = new ObservationTap(environment, obs =>
        {
            var stabilized = rowContext.Stabilize(obs);
            perceptionSource.KnownRowsHeader = rowContext.ToHeaderJson();
            if (perceptionSource.LastTrace is { } trace)
                fusionTraces.Add(new { sequenceNumber = stabilized.SequenceNumber, trace = trace.Clone() });
            return stabilized;
        }, obs =>
        {
            if (!captureStageViews || perceptionSource.LastStageViews is not { } views)
                return;

            var canonical = SourceGroundingNormalizer.Normalize(obs);
            var affordances = InteractionAffordanceAnalyzer.Analyze(obs);
            stageEvidence.Add(new
            {
                sequenceNumber = obs.SequenceNumber,
                fusedCandidates = obs.Elements.Select(e => new
                {
                    text = e.Text,
                    type = e.PerceptionType,
                    index = e.Index,
                    rowId = e.StableKey,
                    bounds = e.Bounds is null ? null : new { e.Bounds.X1, e.Bounds.Y1, e.Bounds.X2, e.Bounds.Y2 },
                }).ToArray(),
                canonicalOccurrences = canonical.Select(occurrence => new
                {
                    occurrenceId = occurrence.OccurrenceId,
                    sourceKind = occurrence.Reference.SourceKind.ToString(),
                    sourceId = occurrence.Reference.SourceId,
                    sourceLocalOccurrenceId = occurrence.Reference.SourceLocalOccurrenceId,
                    elementIndex = occurrence.Reference.ElementIndex,
                    text = OccurrenceText(obs, occurrence),
                    eligibleForAuthorization = occurrence.EligibleForAuthorization,
                    auxiliarySupportCount = occurrence.AuxiliarySupports.Length,
                    rowId = occurrence.Reference.SourceKind == ObservationSourceKind.PrimaryVision
                        && occurrence.Reference.ElementIndex < obs.Elements.Length
                            ? obs.Elements[occurrence.Reference.ElementIndex].StableKey
                            : null,
                    bounds = occurrence.Bounds is null ? null : new
                    {
                        occurrence.Bounds.X1,
                        occurrence.Bounds.Y1,
                        occurrence.Bounds.X2,
                        occurrence.Bounds.Y2,
                    },
                }).ToArray(),
                structuredEvidence = obs.StructuredElements.Select(se => new
                {
                    rawText = se.RawText,
                    resourceId = se.ResourceId,
                    contentDescription = se.ContentDescription,
                    @class = se.Class,
                    clickable = se.Clickable,
                    checkable = se.Checkable,
                    checkedState = se.Checked,
                    bounds = se.Bounds is null ? null : new { se.Bounds.X1, se.Bounds.Y1, se.Bounds.X2, se.Bounds.Y2 },
                    sourceNodeIdentity = se.SourceNodeIdentity,
                    parentSourceNodeIdentity = se.ParentSourceNodeIdentity,
                }).ToArray(),
                semanticAdmission = obs.AdmittedSemanticEvidence.Evidence.Select(envelope => new
                {
                    evidenceId = envelope.EvidenceId,
                    evidenceKind = envelope.EvidenceKind.ToString(),
                    candidateType = envelope.Candidate.GetType().Name,
                    occurrenceId = envelope.Candidate.OccurrenceId,
                    meaning = envelope.Meaning.SymbolId,
                    tier = envelope.Provenance.Tier.ToString(),
                    confidence = envelope.Candidate.Confidence,
                    affordanceKind = envelope.Candidate is ElementAffordanceCandidateEvidence element
                        ? element.AffordanceKind.ToString()
                        : null,
                    relationKind = envelope.Candidate is ContainerRelationCandidateEvidence relation
                        ? relation.RelationKind.ToString()
                        : null,
                }).ToArray(),
                affordances = affordances.Select(affordance => new
                {
                    occurrenceId = affordance.CanonicalOccurrence.OccurrenceId,
                    elementIndex = affordance.SourceElementIndex,
                    text = OccurrenceText(obs, affordance.CanonicalOccurrence),
                    classification = affordance.Classification.ToString(),
                    eligibleForAuthorization = affordance.EligibleForAuthorization,
                    reason = affordance.Reason,
                }).ToArray(),
                stageViews = views.Clone(),
            });
        });
        var observedFrames = tapEnvironment.Frames;
        var startup = new RuntimeStartup(
            tapEnvironment, App, SettingsStrategyBinding.ResolveSemanticPage,
            launchIntentAction: LaunchIntentAction);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup, traversal,
            ct => tapEnvironment.ObserveAsync(ct),
            SettingsStrategyBinding.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                o => string.Equals(SettingsStrategyBinding.ResolveSemanticPage(o), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var graph = new RunExecutionGraph(agent, environment);
        RunGraphFactory factory = _ => graph;

        var compiler = new StrategyContractCompiler([new SettingsStrategyBinding()]);
        using var host = new TierAHost(factory, compiler);

        // P26-G1: per-round terminal-wait ledger (composition-layer diagnostic).
        // Records whether the executor's pre-collection real-terminal wait timed
        // out for an admitted run; the campaign report surfaces it distinctly and
        // truthfully (never fabricated), without touching any Runtime fact.
        var terminalWaitLedger = new TerminalWaitLedger();

        // Conservative Stage-A directives: fresh StrategyId per round, depth fixed,
        // navigate-only constraints, both prohibited effects. With --adapt, the
        // evidence-informed SettingsAdaptationPlanner (Stage B, WI-P26-G2) replaces
        // the fixed ladder: Result → knowledge admission → contract-legal PlanDelta
        // → next directive (upper agent learns; Runtime executes fresh).
        SettingsAdaptationPlanner? adaptationPlanner = null;
        if (adapt)
        {
            var scope = new KnowledgeScope(
                ScenarioId: "settings-bounded-traversal",
                ApplicationPackage: App,
                SemanticCapabilityId: "uni-claw.settings.semantic",
                SemanticCapabilityVersion: "1",
                AndroidAssumptions: "android-35/p26_pixel/arm64-v8a/emulator",
                Locale: "en-US",
                CreatedFromRunIds: []);
            adaptationPlanner = new SettingsAdaptationPlanner(scope, roundBudget: rounds, initialDepth: depth);
        }

        var round = 0;
        CampaignPlannerDecision Planner(IReadOnlyList<CampaignRoundOutcome> prior, CancellationToken ct)
        {
            if (adaptationPlanner is not null)
                return adaptationPlanner.Plan(prior, ct);
            if (prior.Count >= rounds)
                return new CampaignPlannerDecision.Stop(
                    CampaignTermination.BoundedScopeExhaustion(
                        $"planned conservative round budget reached ({rounds}); composition proof complete"));
            round++;
            var directive = ConservativeDirective($"p26-stageA-r{round}", depth);
            return new CampaignPlannerDecision.Continue(new CampaignRoundDirective(
                $"Conservatively explore the real Android Settings scope (round {round}); record everything reachable; never mutate state.",
                directive,
                "serial:emulator-5554"));
        }

        var outcome = await IterativeCampaignRunner.RunAsync(
            Planner,
            BuildExecutor(host, terminalWaitLedger),
            maxRounds: rounds + 1,
            cancellationToken);

        var report = new
        {
            tier = "settings-campaign",
            scenario = "real-android-settings-emulator-35",
            termination = new
            {
                kind = outcome.Termination.Kind.ToString(),
                reason = outcome.Termination.Reason,
                evidenceRefs = outcome.Termination.EvidenceRefs,
            },
            adaptation = adaptationPlanner is null ? null : new
            {
                planningRounds = adaptationPlanner.PlanningRoundHistory.Select(pr => new
                {
                    roundIndex = pr.RoundIndex,
                    isNoOp = pr.PlanDelta.IsNoOp,
                    noOpReason = pr.PlanDelta.NoOpReason,
                    changes = pr.PlanDelta.IsNoOp
                        ? Array.Empty<object>()
                        : pr.PlanDelta.Changes.Select(c => (object)new
                        {
                            freedom = c.Freedom.ToString(),
                            description = c.Description,
                            knowledgeRefs = c.KnowledgeRefs,
                            evidenceRefs = c.EvidenceRefs,
                        }).ToArray(),
                    loadedKnowledge = pr.LoadedKnowledge,
                    newKnowledge = pr.NewKnowledge,
                    remainingUnknowns = pr.RemainingUnknowns,
                    nextStrategyId = pr.NextStrategy.StrategyId,
                    nextDepth = pr.NextStrategy.Scope.MaximumDepth,
                }).ToArray(),
                knowledgeRecords = adaptationPlanner.Fixture.Records.Select(k => new
                {
                    recordId = k.RecordId,
                    type = k.KnowledgeType.ToString(),
                    anchor = k.SemanticAnchor,
                    status = k.Status.ToString(),
                    sourceRunId = k.SourceRunId,
                    evidenceRefs = k.EvidenceRefs,
                    observedRole = k.ObservedRole,
                    disposition = k.Disposition,
                    confidence = k.Confidence,
                }).ToArray(),
                lifecycleStatistics = adaptationPlanner.Fixture.LifecycleStatistics()
                    .Select(s => new { type = s.Key.Item1.ToString(), status = s.Key.Item2.ToString(), count = s.Value })
                    .ToArray(),
            },
            rounds = outcome.Rounds.Select(r => new
            {
                index = r.RoundIndex,
                strategyId = r.StrategyId,
                runId = r.RunId,
                admitted = r.AdmittedRun,
                autonomy = new { passed = r.Autonomy.Passed, detail = r.Autonomy.ToString() },
                invariantsAllPass = r.AllInvariantsPass,
                invariants = r.InvariantAssertions.Select(a => new { id = a.InvariantId, passed = a.Passed, reason = a.Reason }),
                terminal = new
                {
                    state = r.Result.Terminal.TerminalState.Value.ToString(),
                    reason = r.Result.Terminal.TerminalReason.Value,
                },
                events = r.Result.Lifecycle.Events.Value.IsDefault
                    ? []
                    : r.Result.Lifecycle.Events.Value.Select(e => e.Kind).ToArray(),
                snapshotDiagnostics = r.Result.Snapshot.Diagnostics.Value.IsDefault
                    ? []
                    : r.Result.Snapshot.Diagnostics.Value.ToArray(),
                trap = new
                {
                    found = r.Result.Trap.Found.Value,
                    diagnostic = r.Result.Trap.Diagnostic.Value,
                },
                terminalWait = new
                {
                    timedOut = r.RunId is not null && terminalWaitLedger.For(r.RunId).TimedOut,
                    diagnostic = r.RunId is not null ? terminalWaitLedger.For(r.RunId).Diagnostic : null,
                },
                reportJson = r.Run.ReportJson,
                gatesAllPass = r.Gates.AllPass,
            }).ToArray(),
            campaignCallLog = outcome.CampaignCallLog.Entries.Select(e => new { e.Method, e.Outcome, e.Detail }).ToArray(),
        };

        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        // E4 diagnostic artifact: dump the observed frames (vision + structured
        // tiers) next to the JSON for normalization diagnosis.
        var framesPath = System.Environment.GetEnvironmentVariable("P26_FRAMES") ?? "/tmp/p26-frames.json";
        var framesDump = observedFrames.Select(o => new
        {
            seq = o.SequenceNumber,
            foreground = o.ForegroundApplication,
            elements = o.Elements.Select(e => new { text = e.Text, type = e.PerceptionType, idx = e.Index, row_id = e.StableKey, bounds = e.Bounds?.ToString() }).ToArray(),
            structured = o.StructuredElements.IsDefaultOrEmpty ? [] : o.StructuredElements.Select(se => new { text = se.RawText, rid = se.ResourceId, cd = se.ContentDescription, cls = se.Class, clickable = se.Clickable }).ToArray(),
        }).ToArray();
        File.WriteAllText(framesPath, JsonSerializer.Serialize(framesDump, new JsonSerializerOptions { WriteIndented = true }));
        await Console.Error.WriteLineAsync($"[campaign] observed frames dumped: {framesPath} ({observedFrames.Count} frames)");

        // Fusion causal trace artifact (gate-approved trace coverage): per-seq
        // compact fusion trace (decision causal chain + verdict).  Linked to the
        // frames dump by sequenceNumber.  Never read by any Runtime decision.
        var fusionTracesPath = System.Environment.GetEnvironmentVariable("P26_FUSION_TRACES")
            ?? "/tmp/p26-fusion-traces.json";
        File.WriteAllText(fusionTracesPath, JsonSerializer.Serialize(fusionTraces,
            new JsonSerializerOptions { WriteIndented = true }));
        await Console.Error.WriteLineAsync($"[campaign] fusion traces dumped: {fusionTracesPath} ({fusionTraces.Count} frames)");

        if (captureStageViews)
        {
            var stagePath = System.Environment.GetEnvironmentVariable("P26_STAGE_EVIDENCE")
                ?? "/tmp/p26-stage-evidence.json";
            var acceptedViewportDecisions = agent.Trace
                .Where(entry => entry.Reason?.StartsWith("scroll stability CONFIRMED", StringComparison.Ordinal) == true)
                .Select(entry => new
                {
                    sequenceNumber = AcceptedSequence(entry.Reason!),
                    entry.RunId,
                    entry.ContainerId,
                    reason = entry.Reason,
                }).ToArray();
            var stageArtifact = new
            {
                frames = stageEvidence,
                acceptedViewportDecisions,
                runtimeTrace = agent.Trace.Select(entry => new
                {
                    entry.RunId,
                    entry.ContainerId,
                    entry.StepId,
                    entry.ActionId,
                    reason = entry.Reason,
                    runState = entry.RunState?.ToString(),
                }).ToArray(),
            };
            File.WriteAllText(stagePath, JsonSerializer.Serialize(stageArtifact,
                new JsonSerializerOptions { WriteIndented = true }));
            await Console.Error.WriteLineAsync($"[campaign] stage evidence dumped: {stagePath} ({stageEvidence.Count} frames)");
        }

        return outcome.Rounds.Length > 0 && outcome.Rounds.All(r => r.Autonomy.Passed && r.AllInvariantsPass) ? 0 : 1;
    }

    /// <summary>Pure observation tap (validation-side diagnostic): records every
    /// observation without altering it — zero Runtime input, zero world effect.</summary>
    /// <summary>
    /// Observation tap with TRANSFORM semantics: the callback may replace the
    /// observation (row identity stabilization, DESIGN-SPEC D2) — the returned
    /// observation is what the Runtime actually consumes. Also records frames
    /// for E4 diagnostics.
    /// </summary>
    private sealed class ObservationTap(
        IEnvironment inner,
        Func<Observation, Observation> transform,
        Action<Observation>? evidenceTap = null) : IEnvironment
    {
        private readonly List<Observation> _frames = [];
        public IReadOnlyList<Observation> Frames => _frames;

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var observation = await inner.ObserveAsync(cancellationToken);
            var transformed = transform(observation);
            _frames.Add(transformed);
            evidenceTap?.Invoke(transformed);
            return transformed;
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => inner.ExecuteAsync(action, cancellationToken);
    }

    private static bool IsEnabled(string? value) => value is not null && value.Trim().ToLowerInvariant() is "1" or "true" or "yes";

    private static string? OccurrenceText(Observation observation, CanonicalObservationOccurrence occurrence)
        => occurrence.Reference.SourceKind switch
        {
            ObservationSourceKind.PrimaryVision when occurrence.Reference.ElementIndex < observation.Elements.Length
                => observation.Elements[occurrence.Reference.ElementIndex].Text,
            ObservationSourceKind.AuxiliaryStructured when occurrence.Reference.ElementIndex < observation.StructuredElements.Length
                => observation.StructuredElements[occurrence.Reference.ElementIndex].RawText,
            _ => null,
        };

    private static long? AcceptedSequence(string reason)
        => long.TryParse(Regex.Match(reason, @"seq=(\d+)").Groups[1].Value, out var sequence)
            ? sequence
            : null;

    /// <summary>One round's single-run composition through the real wire: transport
    /// via the graduated chain and collect from the frozen read surface. The
    /// emulator receives ZERO mid-run control — the collector's reads go through
    /// the read surface, never the driver. Mirrors ScenarioRunner.RunTierAAsync's
    /// composition exactly (only the wait pacing differs: real rounds need a
    /// longer bounded release wait and, before collection, a wait for the run's
    /// TRUE terminal — see <see cref="WaitForRunTerminalAsync"/>).</summary>
    private static CampaignRunExecutor BuildExecutor(TierAHost host, TerminalWaitLedger terminalWaitLedger)
    {
        string? lastRunId = null;
        return async (directive, priorCallLog, ct) =>
        {
            if (lastRunId is not null)
            {
                // Bounded read-only wait: let the coordinator release the previous
                // run's device reservation (ONE_ACTIVE_RUN per device).
                for (var attempt = 0; attempt < 1000; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!host.Runs.ContainsKey(lastRunId))
                        break;
                    await Task.Delay(100, ct);
                }
            }

            var transport = new LoopbackEmulatorTransport(host.BoundPort);
            var driver = new EmulatorDriver(transport, initialLog: priorCallLog);
            var sliceStart = driver.CallLog.Count;
            var dispatch = await driver.StartAsync(directive.Goal, directive.Directive, directive.Device, ct);

            StrategyRunAdmissionView admission = dispatch switch
            {
                DriverDispatchResult.Transported t => t.Admission,
                DriverDispatchResult.TransportFailed f => new StrategyRunAdmissionView(
                    false, null, null, "TRANSPORT_FAILED", f.Reason),
                _ => new StrategyRunAdmissionView(false, null, null, null, null),
            };

            // P26-G1 pacing fix (composition layer, read-only): before
            // constructing the collector's reads, wait for the run's TRUE
            // terminal. Real runs consume many screenshot→OCR perception cycles
            // and routinely outlast the collector's ~60s bounded wait; worse,
            // the observability serves the ADMISSION-PINNED snapshot (Idle,
            // empty event stream) for the entire run — the truthful terminal
            // projection is materialized only by the coordinator's finally-block
            // (ReplaceRunProjection → ReleaseReservation). Post-finalization the
            // observability registration is retained (never unregistered), so
            // every later read serves the REAL terminal. After this wait the
            // collector's own bounded wait passes on its first poll.
            if (admission.Accepted && !string.IsNullOrWhiteSpace(admission.RunId))
            {
                var terminalWait = await WaitForRunTerminalAsync(host, admission.RunId!, ct);
                terminalWaitLedger.Record(admission.RunId!, terminalWait.TimedOut, terminalWait.Diagnostic);
                if (terminalWait.TimedOut)
                {
                    // Truthful timeout surfacing: the run had not finalized
                    // within the generous bound; the collector below still
                    // reports the observability projection exactly as-of read
                    // time (never fabricated), and the round carries the
                    // distinct timeout marker in the campaign report.
                    await Console.Error.WriteLineAsync(
                        $"[campaign] run '{admission.RunId}': {terminalWait.Diagnostic}");
                }
            }

            // Tier-A read surface over the in-process host (frozen read wire in-
            // process; ledger attestation available). Real emulator runs are long
            // (screenshot→OCR perception cycles) — the collector's bounded wait
            // covers them.
            IRuntimeReadSurface surface = admission.Accepted && !string.IsNullOrWhiteSpace(admission.RunId)
                ? new TierAReadSurface(host, admission.RunId!)
                : new WireReadSurface(host.BoundPort);
            var result = await new ResultCollector(surface, admission).CollectAsync(ct);
            lastRunId = admission.RunId;

            var fullLog = driver.CallLog;
            var runLog = EmulatorCallLog.FromEntries(fullLog.Entries.Skip(sliceStart));

            var transportedPayloads = dispatch is DriverDispatchResult.Transported or DriverDispatchResult.TransportFailed
                ? new[] { StrategyPayloadJson.Freeze(directive.Directive) }
                : Array.Empty<System.Text.Json.Nodes.JsonObject>();

            var boundary = Reporting.BoundaryVerifier.Verify(
                callLog: runLog,
                result: result,
                expectedStartCount: 1,
                transportedDirectives: transportedPayloads);
            var gates = Reporting.ValidationGateEvaluator.Evaluate(
                result: result,
                boundary: boundary,
                callLog: runLog,
                expectedStartCount: 1,
                transportedDirectives: transportedPayloads);
            var report = new Reporting.ValidationReport(result, gates, boundary);

            return new ScenarioRunOutcome(
                Dispatch: dispatch,
                Admission: admission,
                RunId: admission.RunId,
                StrategyId: directive.StrategyId,
                Result: result,
                RunCallLog: runLog,
                DriverCallLog: fullLog,
                TransportedPayloads: transportedPayloads,
                Boundary: boundary,
                Gates: gates,
                Report: report,
                ReportJson: Reporting.ValidationReportRenderer.ToJson(report).ToJsonString(),
                ReportMarkdown: Reporting.ValidationReportRenderer.ToMarkdown(report));
        };
    }

    /// <summary>
    /// P26-G1 pre-collection terminal wait (composition layer, read-only).
    /// Real runs consume many screenshot→OCR perception cycles and routinely
    /// outlast the collector's ~60s bounded window; and — by the PROVEN pinned
    /// admission projection mechanism — the observability serves the
    /// admission-time snapshot (Agent.State = Idle; empty projected stream) for
    /// the ENTIRE run, so the collector can never observe a terminal there
    /// while the run executes. The truthful terminal projection is materialized
    /// ONLY by the coordinator's finally-block (ReplaceRunProjection →
    /// ReleaseReservation); the observability registration is then retained
    /// (never unregistered), so every later read serves the REAL terminal
    /// (Completed/Failed are sticky; full projected event stream).
    /// This wait ensures collection happens strictly post-finalization; the
    /// collector's own bounded wait then passes on its first poll. Fail-closed:
    /// bounded (default 40 min, ~2s polls), honoring ct; on expiry returns a
    /// distinct timeout marker — the caller surfaces it truthfully, never
    /// fabricates a terminal.
    /// </summary>
    private static async Task<(bool TimedOut, string? Diagnostic)> WaitForRunTerminalAsync(
        TierAHost host,
        string runId,
        CancellationToken ct,
        int maxAttempts = 1200,
        TimeSpan? pollDelay = null)
    {
        pollDelay ??= TimeSpan.FromSeconds(2);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // The observability projection flips to the REAL terminal the
            // instant the coordinator's finally-block replaces the pinned
            // admission projection (Completed/Failed are sticky — no reset).
            var snapshot = host.Observability.GetRunSnapshot(runId);
            if (snapshot.RunState.Value is RunState.Completed or RunState.Failed)
            {
                return (false, null);
            }

            // Release follows replacement in the same finally-block; the entry
            // leaving the coordinator's active-run view is equivalent and
            // covers any hypothetical ordering.
            if (!host.Runs.ContainsKey(runId))
            {
                return (false, null);
            }

            await Task.Delay(pollDelay.Value, ct).ConfigureAwait(false);
        }

        return (true,
            $"real-run terminal wait timed out after {maxAttempts} × {pollDelay.Value.TotalSeconds:0}s: " +
            "the run had not reached a finalized projection (coordinator release not observed). " +
            "The collector below reports the observability projection truthfully as-of read time " +
            "(pre-terminal by construction if the run is still executing).");
    }

    /// <summary>Per-round terminal-wait ledger (P26-G1): records, for each
    /// admitted run, whether the executor's pre-collection real-terminal wait
    /// timed out and why. Validation-side diagnostic only — never part of a
    /// Runtime fact, never read by any gate.</summary>
    private sealed class TerminalWaitLedger
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, (bool TimedOut, string? Diagnostic)> _entries = new(StringComparer.Ordinal);

        public void Record(string runId, bool timedOut, string? diagnostic)
        {
            lock (_gate)
            {
                _entries[runId] = (timedOut, diagnostic);
            }
        }

        public (bool TimedOut, string? Diagnostic) For(string runId)
        {
            lock (_gate)
            {
                return _entries.TryGetValue(runId, out var entry) ? entry : (false, null);
            }
        }
    }

    /// <summary>Conservative round directive (Stage A): navigate-only, both effect
    /// classes prohibited, exhaustive-within-scope completion, depth bound.</summary>
    internal static StrategyDirective ConservativeDirective(string strategyId, int depth) => new(
        strategyId,
        contractVersion: 1,
        new StrategyObjective(StrategyObjectiveKind.ExploreScope),
        new StrategyScope(App, SettingsStrategyBinding.RootIdentity, depth),
        ExplorationIntent.ExhaustiveWithinScope,
        new StrategyConstraintSet(
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            ImmutableHashSet.Create(
                StrategyProhibitedEffect.StateMutation,
                StrategyProhibitedEffect.ExternalBoundaryCrossing)),
        new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
        new StrategyAdaptationBoundary(ImmutableHashSet.Create(
            StrategyAdaptationKind.ReconcileBelief,
            StrategyAdaptationKind.ReviseExecutionHypothesis)));

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? ".";
    }
}
