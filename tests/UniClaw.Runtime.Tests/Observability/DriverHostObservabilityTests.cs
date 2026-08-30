using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
using UniClaw.Runtime.Tests.Replay;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Observability;

/// <summary>
/// OBS-F1 (zero-model), OBS-F6 (EvidenceRef logical identity), OBS-F7
/// (cursor/duplicate safety), OBS-F8 (projection failure isolation),
/// plus the task 4.2 persistence boundary (determinism, append-only store reuse).
/// </summary>
public sealed class DriverHostObservabilityTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");

    // ── OBS-F1: zero LLM/VLM — works with no model/provider installed ────

    [Fact]
    public void ZeroModel_ReadOnlyObservability_WorksEndToEnd()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var snapshot = observability.GetRunSnapshot(ReadOnlyObservabilityFixtures.RunId);
        Assert.Equal(RunState.Completed, snapshot.RunState.Value);

        var page = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId);
        Assert.NotEmpty(page.Events);
        Assert.Contains(page.Events, e => e.Kind == RuntimeEventKind.RunCompleted);
    }

    [Fact]
    public void DriverHostAssembly_ContainsNoCognitiveProviderTypes()
    {
        var bannedTokens = new[] { "LLM", "VLM", "IBrain", "IDecisionProvider", "DecisionEngine", "OpenAI", "Anthropic", "DeepSeek", "TokenBudget" };

        foreach (var type in typeof(DriverHostObservability).Assembly.GetTypes())
        {
            Assert.DoesNotContain(bannedTokens, token => type.Name.Contains(token, StringComparison.OrdinalIgnoreCase));
            var ns = type.Namespace ?? "";
            Assert.DoesNotContain(bannedTokens, token => ns.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        // The observability surface references no provider/model types at all.
        Assert.DoesNotContain(typeof(DriverHostObservability).Assembly.GetTypes(),
            t => t.Namespace?.Contains("Provider", StringComparison.OrdinalIgnoreCase) == true);
    }

    // ── OBS-F6: EvidenceRef logical identity, never a filesystem path ────

    private static TraceCaptureBundle BuildBundle()
        => new()
        {
            CaptureSessionId = "cap-1",
            TraceId = ReadOnlyObservabilityFixtures.TraceId,
            ScenarioId = "scenario-wifi",
            Provenance = "RecordedReality",
            FinalState = CaptureState.Persisted,
            Records =
            [
                new CaptureRecord { Order = 0, Kind = CaptureRecordKind.Observation, SequenceNumber = 7, FrameId = "f1" },
                new CaptureRecord { Order = 1, Kind = CaptureRecordKind.ActionDispatch, ActionId = "Action-1", FrameId = "f1" },
            ],
            Artifacts =
            [
                new CaptureArtifact { ArtifactId = "a1", FrameId = "f1", FileName = "frame.png", ContentHash = "sha256-h1", ByteCount = 1234 },
            ],
        };

    [Fact]
    public void EvidenceRef_LogicalLocator_NeverFilesystemPath()
    {
        var bundle = BuildBundle();
        var catalog = EvidenceCatalog.FromBundle(bundle, ReadOnlyObservabilityFixtures.RunId);

        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun(),
            catalog);

        // Observation events carry a logical ref bound to the perception record.
        var observationEvent = Assert.Single(
            projection.Events.Where(e => e.Kind == RuntimeEventKind.ObservationProduced && e.ObservationSequence == 7));
        var observationRef = Assert.Single(observationEvent.EvidenceRefs);
        Assert.Equal("capture:cap-1:record:0", observationRef.Locator);
        Assert.Equal(EvidenceKind.PerceptionOutput, observationRef.Kind);
        Assert.Equal(UniClaw.Runtime.Harness.AssetMaturity.RecordedReality, observationRef.Maturity);
        Assert.False(ContainsPathSeparator(observationRef.Locator), "Locator must be a logical key, never a path");
        Assert.False(observationRef.Locator.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        // Dispatch events carry a logical ref bound to the action journal record.
        var dispatchEvent = Assert.Single(projection.Events.Where(e => e.Kind == RuntimeEventKind.ActionDispatched));
        var dispatchRef = Assert.Single(dispatchEvent.EvidenceRefs);
        Assert.Equal("capture:cap-1:record:1", dispatchRef.Locator);
        Assert.Equal(EvidenceKind.ActionJournal, dispatchRef.Kind);

        // Artifact refs carry content identity, never a file name/location.
        Assert.True(catalog.TryGetObservationRef(7, out var _));
        var artifactResolution = catalog.Resolve(new EvidenceRef { Locator = "capture:cap-1:artifact:a1", RunId = ReadOnlyObservabilityFixtures.RunId });
        Assert.True(artifactResolution.Found);
        Assert.Equal("sha256-h1", artifactResolution.Artifact!.ContentHash);
        Assert.Equal(1234, artifactResolution.Artifact.ByteCount);
        Assert.Equal("sha256-h1", artifactResolution.Ref!.ContentIdentity);
    }

    [Fact]
    public void EvidenceRef_SameLogicalEvidence_ResolvableAcrossPhysicalRepresentations()
    {
        var bundle = BuildBundle();
        var catalogA = EvidenceCatalog.FromBundle(bundle, ReadOnlyObservabilityFixtures.RunId);
        // A different physical location/instance of the same logical capture resolves identically.
        var bundleCopy = bundle with { CaptureSessionId = "cap-1", Records = bundle.Records, Artifacts = bundle.Artifacts };
        var catalogB = EvidenceCatalog.FromBundle(bundleCopy, ReadOnlyObservabilityFixtures.RunId);

        var refA = new EvidenceRef { Locator = "capture:cap-1:record:0", RunId = ReadOnlyObservabilityFixtures.RunId };
        var refB = new EvidenceRef { Locator = "capture:cap-1:record:0", RunId = ReadOnlyObservabilityFixtures.RunId };

        var resolutionA = catalogA.Resolve(refA);
        var resolutionB = catalogB.Resolve(refB);
        Assert.True(resolutionA.Found);
        Assert.True(resolutionB.Found);
        Assert.Equal(resolutionA.Ref!.Locator, resolutionB.Ref!.Locator);
        Assert.Equal(resolutionA.Ref!.ContentIdentity, resolutionB.Ref!.ContentIdentity);
    }

    [Fact]
    public void EvidenceRef_PathLikeLocator_IsNeverResolved()
    {
        var catalog = EvidenceCatalog.FromBundle(BuildBundle(), ReadOnlyObservabilityFixtures.RunId);

        // A filesystem-looking locator is simply not found — resolution is logical-only.
        var resolution = catalog.Resolve(new EvidenceRef
        {
            Locator = "/tmp/evidence/frame.png",
            RunId = ReadOnlyObservabilityFixtures.RunId,
        });
        Assert.False(resolution.Found);
        Assert.Contains("not found", resolution.Diagnostic);

        // And via the surface: unknown run / unknown ref → truthful diagnostic, no throw.
        var observability = new DriverHostObservability();
        var surfaceResolution = observability.GetEvidence(new EvidenceRef
        {
            Locator = "capture:cap-1:record:0",
            RunId = "run-not-registered",
        });
        Assert.False(surfaceResolution.Found);
        Assert.Contains("No evidence catalog", surfaceResolution.Diagnostic);
    }

    // ── OBS-F7: cursor reconnect, duplicate-safe, append-only ─────────────

    [Fact]
    public void CursorReconnect_IsDuplicateSafe_AndIdempotent()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var firstRead = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId, cursor: null);
        Assert.NotEmpty(firstRead.Events);
        var allEventIds = firstRead.Events.Select(e => e.EventId).ToArray();
        Assert.Equal(allEventIds.Length, allEventIds.Distinct().Count());

        // Reconnect from the returned cursor → only newer events (none here).
        var secondRead = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId, firstRead.NextCursor);
        Assert.Empty(secondRead.Events);

        // Re-draining the SAME cursor re-delivers the same events with stable ids (duplicate-safe).
        var replay = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId, cursor: null);
        Assert.Equal(allEventIds, replay.Events.Select(e => e.EventId).ToArray());

        // Stale cursor from a different run is rejected gracefully (start from 0).
        var otherRunPage = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId, new EventCursor("other-run", 999));
        Assert.Equal(allEventIds.Length, otherRunPage.Events.Length);
    }

    [Fact]
    public void AppendOnly_NoRewrite_AndRegisterRunIsIdempotent()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        var before = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId).Events;

        // Re-registering the same run must not duplicate events.
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        var after = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId).Events;
        Assert.Equal(before.Length, after.Length);
        Assert.Equal(before.Select(e => e.EventId), after.Select(e => e.EventId));

        // A second, independent run gets its own monotonic sequence space.
        observability.RegisterRun(
            "run-2",
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            new AgentStateSnapshot { RunId = "run-2", State = RunState.Failed, Reason = "x" });
        var run2 = observability.GetRuntimeEvents("run-2").Events;
        Assert.Single(run2);
        Assert.Equal(1, run2[0].Sequence);
        Assert.Equal("evt-run-2-1", run2[0].EventId);
    }

    [Fact]
    public void SubscribeRunEvents_DrainsOnlyNewEvents()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        using var subscription = observability.SubscribeRunEvents(ReadOnlyObservabilityFixtures.RunId);
        var first = subscription.Drain();
        Assert.NotEmpty(first.Events);
        Assert.Empty(subscription.Drain().Events);
    }

    // ── OBS-F8: projection failure isolation — Runtime execution unaffected ─

    [Fact]
    public void PathologicalInputs_NeverThrow_AndRecordDiagnostics()
    {
        var observability = new DriverHostObservability();

        // Malformed viewport reasons, mismatched run ids, garbage spans — fail-open.
        var projection = observability.RegisterRun(
            "run-pathological",
            new TraceRun { TraceRunId = "t", RunId = "other-run" },
            new AgentStateSnapshot
            {
                RunId = "run-pathological",
                State = RunState.Running,
                Trace =
                [
                    new DecisionRecord("run-pathological") { Reason = "viewport exploration unparseable" },
                    new DecisionRecord("run-pathological") { RunState = RunState.Idle },
                ],
            });

        Assert.NotEmpty(projection.Diagnostics);
        // The healthy parts still projected (no fabricated viewport event).
        Assert.DoesNotContain(projection.Events, e => e.Kind == RuntimeEventKind.ViewportExplorationDecision);
        Assert.Empty(observability.GetRuntimeEvents("run-pathological").Events);

        // Empty trace + empty snapshot: clean, no throw (matched run ids).
        var empty = observability.RegisterRun(
            "run-empty",
            new TraceRun { TraceRunId = "t", RunId = "run-empty" },
            new AgentStateSnapshot { RunId = "run-empty", State = RunState.Idle });
        Assert.Empty(empty.Diagnostics);
    }

    [Fact]
    public async Task ProjectionFailure_LeavesRuntimeResultUnaffected()
    {
        // A real Runtime run — its semantic result is decided before/during execution,
        // entirely independent of the downstream telemetry projection.
        var env = SimulationPresets.WifiOn();
        var agent = BuildAgent(env);
        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "obs-f8");

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.True(satisfied.Evidence.Satisfied);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);

        // Now a deliberately MISMATCHED projection: telemetry failure must not
        // alter the kernel result already produced.
        var observability = new DriverHostObservability();
        var projection = observability.RegisterRun(
            "obs-f8-run",
            new TraceRun { TraceRunId = "t", RunId = "deliberately-different-run-id" },
            AgentStateSnapshot.From(agent));

        Assert.Contains(projection.Diagnostics, d => d.Contains("differs from AgentStateSnapshot.RunId"));
        Assert.Equal(RunState.Completed, observability.GetRunSnapshot("obs-f8-run").RunState.Value);
        Assert.True(satisfied.Evidence.Satisfied);
    }

    // ── Task 4.2: persistence boundary — deterministic, append-only reuse ─

    [Fact]
    public void Projection_IsDeterministic_AcrossReprojection()
    {
        var first = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        var second = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    [Fact]
    public async Task ExistingAppendOnlyCaptureStore_StillSavesImmutableObservabilityTrace()
    {
        // Persistence continues to go through the existing append-only boundary only —
        // the DriverHost adds no persistence path of its own.
        var bundle = new TraceCaptureBundle
        {
            CaptureSessionId = $"persist-{Guid.NewGuid():N}",
            TraceId = ReadOnlyObservabilityFixtures.TraceId,
            Provenance = "LiveCapture",
            FinalState = CaptureState.Finalizing,
            ObservabilityTrace = ReadOnlyObservabilityFixtures.CompletedTrace(),
        };

        var tmpDir = Path.Combine(Path.GetTempPath(), $"obs-driverhost-{Guid.NewGuid():N}");
        try
        {
            var store = new FileTraceCaptureStore(tmpDir);
            var persisted = await store.SaveAsync(bundle);
            Assert.True(persisted.Success, string.Join(", ", persisted.Errors));

            // Append-only: re-saving the same session id fails closed (no overwrite).
            var again = await store.SaveAsync(bundle);
            Assert.False(again.Success);
            Assert.Contains("already exists", again.Errors.FirstOrDefault() ?? "");
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static bool ContainsPathSeparator(string locator)
        => locator.Contains('/') || locator.Contains('\\');

    private static RuntimeAgent BuildAgent(SimulationEnvironment env)
    {
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(startup, traversal, ct => env.ObserveAsync(ct), _ => "Settings",
            Factory, recovery, pages, criteria);
    }
}
