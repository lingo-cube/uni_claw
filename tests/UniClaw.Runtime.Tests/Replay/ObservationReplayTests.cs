using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Replay;

/// <summary>S2 executable proofs over version-controlled persistent assets.</summary>
public sealed class ObservationReplayTests
{
    private const string ManifestFile = "settings-wifi-reality-seeded-v1.json";
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");

    [Fact]
    public void PersistentManifest_HasVersionedStableReferencedContractsAndHonestProvenance()
    {
        var manifest = LoadManifest();

        Assert.Equal(HarnessAssetSchema.CurrentVersion, manifest.SchemaVersion);
        Assert.Empty(HarnessAssetManifestValidator.Validate(manifest));
        Assert.Equal(AssetMaturity.RealitySeeded, manifest.Provenance);
        Assert.All(manifest.Frames, frame => Assert.Equal(AssetMaturity.RealitySeeded, frame.Provenance));
        Assert.All(manifest.Replays, replay => Assert.Equal(AssetMaturity.RealitySeeded, replay.Provenance));
        Assert.All(manifest.Scenarios, scenario => Assert.Equal(AssetMaturity.RealitySeeded, scenario.Provenance));
        Assert.DoesNotContain(manifest.Frames, frame => frame.Provenance == AssetMaturity.RecordedReality);
        Assert.Contains("OFF-to-ON transition is synthetic", manifest.Source, StringComparison.Ordinal);

        var roundTripped = HarnessAssetManifestJson.Deserialize(HarnessAssetManifestJson.Serialize(manifest));
        Assert.Empty(HarnessAssetManifestValidator.Validate(roundTripped));
        Assert.Equal(manifest.ManifestId, roundTripped.ManifestId);
    }

    [Fact]
    public void FrameImageAssociation_IsExplicitAndSupportsMultipleDerivedAnalyses()
    {
        const string hash = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var frame = new FrameAsset
        {
            FrameId = "frame-image",
            CaptureSessionId = "session-image",
            ScreenshotArtifactId = "raw-image",
            NormalizedScreenshotArtifactId = "normalized-image",
            ArtifactIds = ["raw-image", "normalized-image", "ocr", "detector"],
        };
        var manifest = new HarnessAssetManifest
        {
            ManifestId = "image-association-contract",
            CaptureSessions = [new CaptureSession
            {
                CaptureSessionId = "session-image",
                FrameIds = [frame.FrameId],
            }],
            Frames = [frame],
            Artifacts =
            [
                new Artifact
                {
                    ArtifactId = "raw-image", FrameId = frame.FrameId,
                    Type = ArtifactType.RawScreenshot, Format = "image/png", ContentHash = hash,
                },
                new Artifact
                {
                    ArtifactId = "normalized-image", FrameId = frame.FrameId,
                    Type = ArtifactType.NormalizedScreenshot, Format = "image/png",
                    DerivedFromArtifactId = "raw-image", TransformDescription = "Normalized copy",
                },
                new Artifact
                {
                    ArtifactId = "ocr", FrameId = frame.FrameId, Type = ArtifactType.OcrResult,
                    Format = "application/json", DerivedFromArtifactId = "raw-image",
                },
                new Artifact
                {
                    ArtifactId = "detector", FrameId = frame.FrameId, Type = ArtifactType.DetectorResult,
                    Format = "application/json", DerivedFromArtifactId = "raw-image",
                },
            ],
        };

        Assert.Empty(HarnessAssetManifestValidator.Validate(manifest));
        Assert.Equal(2, manifest.Artifacts.Count(x => x.DerivedFromArtifactId == "raw-image"
            && x.Type is ArtifactType.OcrResult or ArtifactType.DetectorResult));
    }

    [Fact]
    public void ObservationFrame_WithoutScreenshot_IsValid()
    {
        var manifest = LoadManifest();

        Assert.Empty(HarnessAssetManifestValidator.Validate(manifest));
        Assert.All(manifest.Frames, frame => Assert.Null(frame.ScreenshotArtifactId));
        Assert.All(manifest.Frames, frame => Assert.NotNull(frame.Observation));
    }

    [Fact]
    public async Task ReplayEnvironment_ActionDivergenceAndExhaustionFailClosed()
    {
        var observation = new Observation([], "settings", 1);
        var expected = new DeviceAction.LaunchApp("settings");
        var script = new ReplayScript(
            [observation],
            [new ReplayDispatch(expected, new ActionResult(ActionResultOutcome.Dispatched, null, null))]);

        var mismatch = new ReplayEnvironment(script);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mismatch.ExecuteAsync(new DeviceAction.LaunchApp("other"), CancellationToken.None));

        var exhausted = new ReplayEnvironment(script);
        Assert.Equal(observation, await exhausted.ObserveAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => exhausted.ObserveAsync(CancellationToken.None));
        _ = await exhausted.ExecuteAsync(expected, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => exhausted.ExecuteAsync(expected, CancellationToken.None));
    }

    [Fact]
    public async Task RealitySeededManifest_ObservationReplayRunsThroughGraduatedRuntime()
    {
        var result = await ExecuteRealitySeededReplay();

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result.Result);
        Assert.True(satisfied.Evidence.Satisfied);
        Assert.Equal(3, satisfied.Evidence.SourceObservationSequence);
        Assert.Equal(RunState.Completed, result.Agent.State);
        Assert.Collection(
            result.Environment.ActionHistory,
            action => Assert.Equal(new DeviceAction.LaunchApp("settings"), action),
            action => Assert.Equal(
                new DeviceAction.SetSwitch(1, true, new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f)),
                action));
        Assert.Equal([1L, 2L, 3L], result.Environment.ObservationHistory.Select(x => x.SequenceNumber));
    }

    [Fact]
    public async Task SamePersistentAssetsAndRuntimeBaseline_ReplayDeterministically()
    {
        var first = await ExecuteRealitySeededReplay();
        var second = await ExecuteRealitySeededReplay();

        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.Agent.State, second.Agent.State);
        Assert.Equal(first.Environment.ActionHistory, second.Environment.ActionHistory);
        Assert.Equal(
            CanonicalObservations(first.Environment.ObservationHistory),
            CanonicalObservations(second.Environment.ObservationHistory));
        Assert.Equal(first.Agent.Reason, second.Agent.Reason);
    }

    [Fact]
    public async Task RejectedExternalResponse_ReplaysThroughRuntimeAsFailure()
    {
        var manifest = LoadManifest();
        var replay = Assert.Single(manifest.Replays);
        var rejectedDispatches = replay.Dispatches.SetItem(
            1,
            replay.Dispatches[1] with
            {
                Outcome = ActionResultOutcome.Rejected,
                Info = "Recorded rejection response.",
            });
        var rejectedManifest = manifest with
        {
            Replays = [replay with { Dispatches = rejectedDispatches }],
        };
        var environment = new ReplayEnvironment(
            ReplayScriptFactory.FromManifest(rejectedManifest, replay.ReplayId));
        var agent = BuildAgent(environment);

        var result = await agent.RunSemanticGoalAsync(
            Assert.IsType<SemanticGoalInput>(Assert.Single(manifest.Scenarios).Input.GoalInput),
            [Wifi],
            [SetEnabled],
            "srh-failure-replay");

        Assert.IsType<SemanticRunResult.ExecutionFailed>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Equal(2, environment.ActionHistory.Count);
        Assert.Equal(2, environment.ObservationHistory.Count);
    }

    [Fact]
    public void ScenarioAndTraceContracts_AreBehaviorOrientedAndDiagnosticTextIsNotProtocol()
    {
        var manifest = LoadManifest();
        var scenario = Assert.Single(manifest.Scenarios);
        var trace = Assert.Single(manifest.Traces);

        Assert.Equal(ScenarioOutcome.Satisfied, scenario.Expected.Outcome);
        Assert.True(scenario.Expected.RequiresFreshObservation);
        Assert.True(scenario.Expected.RequiresGoalEvidence);
        Assert.Equal(Enumerable.Range(0, trace.Events.Length), trace.Events.Select(x => x.Order));

        var serialized = HarnessAssetManifestJson.Serialize(manifest);
        Assert.DoesNotContain("BindingReconciler", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetGrounder", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private class", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(SemanticRunResult Result, RuntimeAgent Agent, ReplayEnvironment Environment)>
        ExecuteRealitySeededReplay()
    {
        var manifest = LoadManifest();
        var scenario = Assert.Single(manifest.Scenarios);
        var goal = Assert.IsType<SemanticGoalInput>(scenario.Input.GoalInput);
        var environment = new ReplayEnvironment(
            ReplayScriptFactory.FromManifest(manifest, scenario.World.ReplayId!));
        var agent = BuildAgent(environment);

        var result = await agent.RunSemanticGoalAsync(
            goal,
            [Wifi],
            [SetEnabled],
            "srh-reality-seeded-replay");
        return (result, agent, environment);
    }

    private static RuntimeAgent BuildAgent(ReplayEnvironment environment)
    {
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        var pageCriteria = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var bindingCriteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        return new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            _ => "Settings",
            Factory,
            recovery,
            pageCriteria,
            bindingCriteria);
    }

    private static HarnessAssetManifest LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Replay", "Assets", ManifestFile);
        return HarnessAssetManifestJson.Deserialize(File.ReadAllText(path));
    }

    private static string[] CanonicalObservations(IEnumerable<Observation> observations)
        => observations.Select(observation => string.Join(
            "|",
            observation.SequenceNumber,
            observation.ForegroundApplication,
            string.Join(";", observation.Elements.Select(element =>
                $"{element.Index}:{element.Text}:{element.SwitchState}:{element.PerceptionType}:{element.Bounds}"))))
            .ToArray();
}
