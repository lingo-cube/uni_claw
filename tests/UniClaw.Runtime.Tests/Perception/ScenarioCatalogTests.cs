using System.Collections.Immutable;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Harness.Catalog;
using UniClaw.Runtime.Harness.Replay;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Replay;
using UniClaw.Runtime.Tests.Scenario;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class ScenarioCatalogTests
{
    private const string SettingsApp = "com.android.settings";
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");

    [Fact]
    public void Catalog_DeepLoadsAndResolvesAllReviewedScenarios()
    {
        var catalog = LoadCatalog();
        Assert.Equal(3, catalog.ScenarioIds.Length);
        foreach (var id in catalog.ScenarioIds)
        {
            var resolved = catalog.ResolveRequired(id);
            Assert.Equal(id, resolved.Scenario.ScenarioId);
            Assert.NotEmpty(resolved.Replays);
        }
    }

    [Fact]
    public async Task SC_CAT_002_Catalog_CaseA_AlreadyOn_ReplaysFromManifest()
    {
        var resolved = LoadCatalog().ResolveRequired("wifi-enable-golden-v1-case-a");
        var env = new ReplayEnvironment(ReplayScriptFactory.FromManifest(resolved.Manifest, resolved.Replays[0].ReplayId));
        var result = await BuildAgent(env).RunSemanticGoalAsync(Goal(resolved), [Wifi], [SetEnabled], "catalog-case-a");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.DoesNotContain(env.ActionHistory, action => action is DeviceAction.SetSwitch);
    }

    [Fact]
    public async Task Catalog_CaseB_OffToOn_ReplaysFromManifest()
    {
        var resolved = LoadCatalog().ResolveRequired("wifi-enable-golden-v1-case-b");
        var env = new ReplayEnvironment(ReplayScriptFactory.FromManifest(resolved.Manifest, resolved.Replays[0].ReplayId));
        var result = Assert.IsType<SemanticRunResult.Satisfied>(await BuildAgent(env).RunSemanticGoalAsync(Goal(resolved), [Wifi], [SetEnabled], "catalog-case-b"));
        Assert.Contains(env.ActionHistory, action => action is DeviceAction.SetSwitch { TargetState: true });
        Assert.True(result.Evidence.SourceObservationSequence >= 3);
    }

    [Fact]
    public async Task Catalog_CaseC_UnknownWorld_DoesNotDispatchMutation()
    {
        var resolved = LoadCatalog().ResolveRequired("wifi-enable-golden-v1-case-c");
        var env = new ReplayEnvironment(ReplayScriptFactory.FromManifest(resolved.Manifest, resolved.Replays[0].ReplayId));
        var result = await BuildAgent(env).RunSemanticGoalAsync(Goal(resolved), [Wifi], [SetEnabled], "catalog-case-c");
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.DoesNotContain(env.ActionHistory, action => action is DeviceAction.SetSwitch);
    }

    [Fact]
    public void Catalog_ProvenanceAndReferencesRemainBounded()
    {
        var catalog = LoadCatalog();
        var a = catalog.ResolveRequired("wifi-enable-golden-v1-case-a");
        var b = catalog.ResolveRequired("wifi-enable-golden-v1-case-b");
        var c = catalog.ResolveRequired("wifi-enable-golden-v1-case-c");
        Assert.Equal(AssetMaturity.RecordedReality, a.Scenario.Provenance);
        Assert.Equal(AssetMaturity.RecordedReality, b.Scenario.Provenance);
        Assert.Equal(AssetMaturity.RealitySeeded, c.Scenario.Provenance);
        Assert.All(a.Replays.Concat(b.Replays), replay => Assert.Equal(AssetMaturity.RecordedReality, replay.Provenance));
        Assert.All(c.Frames, frame => Assert.Equal(AssetMaturity.RealitySeeded, frame.Provenance));
        Assert.All(a.Frames.Concat(b.Frames), frame => Assert.NotNull(frame.Observation));
    }

    private static ScenarioCatalog LoadCatalog()
    {
        var root = TestRepositoryPaths.RepoPath("tests", "UniClaw.Runtime.Tests", "Perception", "Assets");
        using var stream = File.OpenRead(Path.Combine(root, "scenario-catalog.json"));
        var (catalog, errors) = ScenarioCatalog.Load(stream, root);
        Assert.True(errors.IsEmpty, string.Join(System.Environment.NewLine, errors));
        Assert.NotNull(catalog);
        return catalog!;
    }

    private static SemanticGoalInput Goal(ScenarioCatalogResolution resolved)
        => Assert.IsType<SemanticGoalInput>(resolved.Scenario.Input.GoalInput);

    private static RuntimeAgent BuildAgent(UniClaw.Runtime.Environment.IEnvironment env)
    {
        var criteria = new ElementBindingCriteria([Wifi], ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"), ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(SettingsApp, ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, SettingsApp, _ => "Settings");
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(startup, traversal, ct => semanticEnv.ObserveAsync(ct), _ => "Settings", Factory, recovery, pages, criteria);
    }
}
