using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-005 — scrollable 容器与返回键的 occurrence 投影：真实录制的
/// Settings hierarchy 经 typed 投影 + WorldModel reconcile 后，滚动容器以
/// role=scrollable（resource-id 为稳定身份）暴露，返回键以 ui.element
/// "Navigate up" 暴露——两者都是可 grounding 的 effect 目标。
/// </summary>
public sealed class ScrollableOccurrenceTests
{
    [Fact]
    public void SettingsRoot_ExposesScrollableOccurrencesWithResourceIdIdentity()
    {
        var world = Reconcile("7e41f85e06e4.xml");
        var scrollables = world.Current!.Occurrences!
            .Where(o => o.Role == "scrollable")
            .Select(o => o.SemanticDescriptor)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("com.android.settings:id/main_content_scrollable_container", scrollables);
        Assert.Contains("com.android.settings:id/settings_homepage_container", scrollables);
        // 滚动容器身份唯一（resource-id）：两个容器的 descriptor 互不相同。
        Assert.Equal(scrollables.Length, scrollables.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void InternetPage_ExposesScrollableContainerAndNavigateUpTarget()
    {
        var world = Reconcile("a5d983aa849b.xml");
        var occurrences = world.Current!.Occurrences!;

        Assert.Contains(occurrences, o => o.Role == "scrollable"
            && o.SemanticDescriptor == "com.android.settings:id/content_parent");
        // 返回键：content-desc "Navigate up" 的 ImageButton（clickable）。
        Assert.Contains(occurrences, o => o.Role == "ui.element"
            && o.SemanticDescriptor == "Navigate up");
    }

    [Fact]
    public void ScrollableOccurrences_CarrySpatialBounds()
    {
        var world = Reconcile("7e41f85e06e4.xml");
        var scrollables = world.Current!.Occurrences!
            .Where(o => o.Role == "scrollable")
            .ToArray();

        Assert.NotEmpty(scrollables);
        Assert.All(scrollables, o => Assert.NotNull(o.Locator));
    }

    private static WorldModel Reconcile(string xmlFile)
    {
        var repo = FindRepoRoot();
        var xml = File.ReadAllText(Path.Combine(repo, "platforms", "perception",
            "evaluation", "validation", "fastscreen-v1", "uia", xmlFile));
        var captureId = $"scroll-occ-{xmlFile}";
        var parsed = UiAutomatorDump.ParseHierarchyObservation(
            xml,
            new UiAutomatorDump.UiHierarchyParseContext(
                captureId, DateTimeOffset.UtcNow, "emulator-5554", "settings-test", 35,
                ObservationCycleId: "scroll-cycle-001",
                Space: CoordinateSpace.DeviceViewport(1080, 2400)));

        var ledger = new EvidenceLedger();
        var world = new WorldModel(
            new HashSet<string> { "ui.node.*" },
            new ProductAssociationStrategy(),
            new UiHierarchyOccurrenceStrategy());
        foreach (var proposal in TypedHierarchyProposalProjector.Project(
                     parsed.Observation!, ObservationContext.External))
        {
            var (admission, record) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
            var relevance = world.JudgeRelevance(record!);
            Assert.True(relevance.IsRelevant, relevance.Reason);
            world.Reconcile(record!, relevance);
        }
        Assert.NotNull(world.Current);
        Assert.NotNull(world.Current.Occurrences);
        return world;
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
