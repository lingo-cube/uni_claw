using System.Collections.Immutable;
using UniClaw.Runtime.Harness.Catalog;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Replay;
using UniClaw.Runtime.Tests.Scenario;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// TC-06: Golden Scenario Admission — catalog-driven replay.
///
/// Loads the canonical ScenarioCatalog and replays registered scenarios
/// through the graduated Runtime. No private call-order assertions.
/// No semantic inference from scenario metadata.
/// </summary>
public sealed class ScenarioCatalogTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ImmutableArray<SemanticObject> Objects = [Wifi];
    private static readonly ImmutableArray<Capability> Capabilities = [SetEnabled];
    private const string SettingsApp = "com.android.settings";

    private static readonly ElementBounds WifiToggleBounds = new(0.856f, 0.414f, 0.936f, 0.444f);

    private static RuntimeAgent BuildAgent(UniClaw.Runtime.Environment.IEnvironment env)
    {
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(
            SettingsApp,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, SettingsApp, _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(startup, traversal, ct => env.ObserveAsync(ct), _ => "Settings",
            Factory, recovery, pages, criteria);
    }

    private static ReplayDispatch D(DeviceAction action) =>
        new(action, new ActionResult(ActionResultOutcome.Dispatched, action.ToString(), "catalog-replay"));

    // ── CATALOG VALIDATION ───────────────────────────────────────────────

    [Fact]
    public void Catalog_LoadsWithoutErrors()
    {
        var path = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var stream = File.OpenRead(path);
        var (catalog, errors) = ScenarioCatalog.Load(stream);

        Assert.Empty(errors);
        Assert.NotNull(catalog);
    }

    [Fact]
    public void Catalog_HasAllGoldenScenarios()
    {
        var path = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var stream = File.OpenRead(path);
        var (catalog, _) = ScenarioCatalog.Load(stream);
        Assert.NotNull(catalog);

        var ids = catalog.ScenarioIds.ToHashSet();
        Assert.Contains("wifi-enable-golden-v1-case-a", ids);
        Assert.Contains("wifi-enable-golden-v1-case-b", ids);
        Assert.Contains("wifi-enable-golden-v1-case-c", ids);
        Assert.Equal(3, catalog.ScenarioIds.Length);
    }

    [Fact]
    public void Catalog_RejectsDuplicateIds()
    {
        var json = """
        {
          "catalogId": "test",
          "scenarios": [
            { "scenarioId": "dup" },
            { "scenarioId": "dup" }
          ]
        }
        """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var (catalog, errors) = ScenarioCatalog.Load(stream);

        Assert.Null(catalog);
        Assert.Contains(errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Catalog_RejectsEmptyScenarioId()
    {
        var json = """
        {
          "catalogId": "test",
          "scenarios": [
            { "scenarioId": "" }
          ]
        }
        """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var (catalog, errors) = ScenarioCatalog.Load(stream);

        Assert.Null(catalog);
        Assert.Contains(errors, e => e.Contains("missing"));
    }

    [Fact]
    public void Catalog_LookupById_Succeeds()
    {
        var path = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var stream = File.OpenRead(path);
        var (catalog, _) = ScenarioCatalog.Load(stream);
        Assert.NotNull(catalog);

        var entry = catalog.GetRequired("wifi-enable-golden-v1-case-a");
        Assert.Equal("wifi-enable-golden-v1-case-a", entry.ScenarioId);
        Assert.Equal("ALREADY_SATISFIED", entry.Category);
        Assert.Equal("RECORDED_REALITY", entry.Provenance);
    }

    [Fact]
    public void Catalog_LookupMissing_Throws()
    {
        var path = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var stream = File.OpenRead(path);
        var (catalog, _) = ScenarioCatalog.Load(stream);
        Assert.NotNull(catalog);

        Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired("nonexistent"));
    }

    // ── CATALOG-DRIVEN REPLAY: CASE A (Already ON) ───────────────────────

    [Fact]
    public async Task Catalog_CaseA_AlreadyOn_ReplaySucceeds()
    {
        // Load catalog
        var catPath = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var catStream = File.OpenRead(catPath);
        var (catalog, _) = ScenarioCatalog.Load(catStream);
        Assert.NotNull(catalog);

        var entry = catalog.GetRequired("wifi-enable-golden-v1-case-a");
        Assert.Equal("RECORDED_REALITY", entry.Provenance);

        // Build replay from golden-run-v1 data
        var onObs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0,
                    new ElementBounds(0.05f, 0.40f, 0.50f, 0.44f), "menuItem"),
                new ObservedElement("", true, 1, WifiToggleBounds, "toggle"),
            ],
            SettingsApp, 1);

        var script = new ReplayScript(
            [onObs, onObs with { SequenceNumber = 2 }],
            [D(new DeviceAction.LaunchApp(SettingsApp))]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "catalog-case-a");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
    }

    // ── CATALOG-DRIVEN REPLAY: CASE B (OFF→ON) ──────────────────────────

    [Fact]
    public async Task Catalog_CaseB_OffToOn_ReplaySucceeds()
    {
        var catPath = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var catStream = File.OpenRead(catPath);
        var (catalog, _) = ScenarioCatalog.Load(catStream);
        Assert.NotNull(catalog);

        var entry = catalog.GetRequired("wifi-enable-golden-v1-case-b");
        Assert.Equal("HAPPY_PATH", entry.Category);
        Assert.Equal("RECORDED_REALITY", entry.Provenance);

        var offObs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0,
                    new ElementBounds(0.05f, 0.40f, 0.50f, 0.44f), "menuItem"),
                new ObservedElement("", false, 1, WifiToggleBounds, "toggle"),
            ],
            SettingsApp, 1);

        var onObs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0,
                    new ElementBounds(0.05f, 0.40f, 0.50f, 0.44f), "menuItem"),
                new ObservedElement("", true, 1, WifiToggleBounds, "toggle"),
            ],
            SettingsApp, 3);

        var script = new ReplayScript(
            [offObs, offObs with { SequenceNumber = 2 }, onObs],
            [
                D(new DeviceAction.LaunchApp(SettingsApp)),
                D(new DeviceAction.SetSwitch(1, true, WifiToggleBounds)),
            ]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "catalog-case-b");

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Contains(env.ActionHistory, a => a is DeviceAction.SetSwitch s && s.TargetState == true);
        Assert.True(satisfied.Evidence.SourceObservationSequence >= 3);
    }

    // ── PROVENANCE AUDIT ─────────────────────────────────────────────────

    [Fact]
    public void Catalog_Provenance_IsPreserved()
    {
        var path = TestRepositoryPaths.RepoPath(
            "tests", "UniClaw.Runtime.Tests", "Perception", "Assets", "scenario-catalog.json");
        using var stream = File.OpenRead(path);
        var (catalog, _) = ScenarioCatalog.Load(stream);
        Assert.NotNull(catalog);

        var caseA = catalog.GetRequired("wifi-enable-golden-v1-case-a");
        var caseB = catalog.GetRequired("wifi-enable-golden-v1-case-b");
        var caseC = catalog.GetRequired("wifi-enable-golden-v1-case-c");

        // Screenshots + perception = RECORDED_REALITY
        Assert.Equal("RECORDED_REALITY", caseA.Provenance);
        Assert.Equal("RECORDED_REALITY", caseB.Provenance);
        // Manual SwitchState annotation = REALITY_SEEDED
        Assert.Equal("REALITY_SEEDED", caseC.Provenance);

        // No provenance promotion
        Assert.NotEqual("RECORDED_REALITY", caseC.Provenance);
    }
}
