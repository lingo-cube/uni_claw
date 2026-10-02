using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsTraversalOutcomeTests
{
    [Fact]
    public void ScreenIdentity_UsesStableSettingsRouteFingerprint()
    {
        var repo = FindRepoRoot();
        var rootXml = File.ReadAllText(Path.Combine(repo, "platforms", "perception",
            "evaluation", "validation", "fastscreen-v1", "uia", "7e41f85e06e4.xml"));
        var childXml = File.ReadAllText(Path.Combine(repo, "platforms", "perception",
            "evaluation", "validation", "fastscreen-v1", "uia", "a5d983aa849b.xml"));

        Assert.Equal("android.settings|route:Settings",
            SettingsTraversalLiveFeed.DeriveScreenIdentity(rootXml));
        Assert.Equal("android.settings|route:Internet",
            SettingsTraversalLiveFeed.DeriveScreenIdentity(childXml));
    }

    [Fact]
    public void TypedHierarchyCheckedSwitch_FulfillsSettingsOutcomeObligation()
    {
        var repo = FindRepoRoot();
        var xmlPath = Path.Combine(repo, "platforms", "perception", "evaluation",
            "validation", "fastscreen-v1", "uia", "1e572c8f5092.xml");
        if (!File.Exists(xmlPath))
            xmlPath = Path.Combine(repo, "platforms", "perception", "evaluation",
            "validation", "fastscreen-v1", "uia", "1e572c8f5092.xml");
        Assert.True(File.Exists(xmlPath), xmlPath);

        var captureId = "settings-outcome-capture";
        var parsed = UiAutomatorDump.ParseHierarchyObservation(
            File.ReadAllText(xmlPath),
            new UiAutomatorDump.UiHierarchyParseContext(
                captureId, DateTimeOffset.UtcNow, "emulator-5554", "settings-test", 35,
                ObservationCycleId: "settings-cycle-001",
                Space: CoordinateSpace.DeviceViewport(1080, 2400)));
        Assert.NotNull(parsed.Observation);

        var proposals = TypedHierarchyProposalProjector.Project(
            parsed.Observation!, ObservationContext.External);
        Assert.Contains(proposals, p => p.Claim.Subject.EndsWith(".checked", StringComparison.Ordinal)
            && p.Claim.Value == "checked");

        var ledger = new EvidenceLedger();
        var world = new WorldModel(
            new HashSet<string> { "ui.node.*" },
            new ProductAssociationStrategy(),
            new UiHierarchyOccurrenceStrategy());
        foreach (var proposal in proposals)
        {
            var (admission, record) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
            var relevance = world.JudgeRelevance(record!);
            Assert.True(relevance.IsRelevant, relevance.Reason);
            world.Reconcile(record!, relevance);
        }

        var view = world.DeriveOutcomeAssuranceView(
            new[] { "ui.role.switch.checked" },
            new[] { ("obj-switch-checked", new TargetDescriptor("switch", "Use developer options"), "checked") },
            ledger.CanonicalRecords);

        var fact = Assert.Single(view.EntityFacts!);
        var switches = world.Current!.Occurrences!.Where(o => o.Role == "switch")
            .Select(o => $"{o.SemanticDescriptor}|{o.State}|{o.Locator?.SpatialFrameId}|{string.Join(',', o.EvidenceBasis)}")
            .ToArray();
        var checkedClaims = world.Current.WorldState
            .Where(kv => kv.Key.EndsWith(".checked", StringComparison.Ordinal))
            .Select(kv => $"{kv.Key}={kv.Value.Value}|{kv.Value.EvidenceId}")
            .ToArray();
        Assert.True(fact.Kind == EntityObligationFactKind.Satisfied,
            $"kind={fact.Kind} switches=[{string.Join(";", switches)}] checked=[{string.Join(";", checkedClaims)}]");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }
}
