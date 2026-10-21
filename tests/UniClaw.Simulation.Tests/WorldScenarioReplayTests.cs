using System.Text.Json;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class WorldScenarioReplayTests
{
    public static IEnumerable<object[]> Scenarios() => Directory.EnumerateFiles(
            Path.Combine(GoldenPaths.RepoRoot(), "scenarios"), "SCN-*.json")
        .Order(StringComparer.Ordinal).Where(path =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.GetProperty("execution").GetProperty("kind").GetString() == "golden-bundle";
        }).Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    [Theory, MemberData(nameof(Scenarios))]
    public void ScenarioReplay_PreservesEveryWorldRevisionAndCurrentSlices(string scenarioId)
    {
        var scenario = ScenarioLibrary.Load(scenarioId);
        ScenarioExecution Execute()
        {
            // POLICY-008's certified carrier needs the same explicit external
            // seam as its existing executable test (PolicyScenarioTests.P8).
            var options = scenarioId == "SCN-POLICY-008"
                ? scenario.Options with { Seams = new SeamOverrides { Association = new ReplayLeaseDrift(scenario.Bundle.Stimuli[1].VirtualTime) } }
                : scenario.Options;
            var execution = ScenarioRunner.Run(scenario.Bundle, options);
            if (scenario.Options.Phased)
            {
                Assert.Contains(scenarioId, new[] { "SCN-WIFI-005", "SCN-BARRIER-002" });
                Assert.True(execution.Host.SubmitStimulus(GoldenScenarioBundles.SafeStopCancelStimulus()));
                if (scenarioId == "SCN-WIFI-005")
                    Assert.True(execution.Host.SubmitStimulus(GoldenScenarioBundles.LatePostActionStimulus()));
                execution.Host.DriveOnce();
                execution = execution with { Report = ScenarioRunner.FinalizePhased(scenario.Bundle, execution.Host) };
            }
            Assert.True(execution.Report.AcceptancePassed,
                ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
            return execution;
        }

        var first = Execute();
        var second = Execute();
        var history = first.Host.Facts.Revisions;
        Assert.NotEmpty(history);
        Assert.Equal(JsonSerializer.Serialize(history), JsonSerializer.Serialize(second.Host.Facts.Revisions));
        Assert.Equal(first.Report.SemanticDigest, second.Report.SemanticDigest);
        foreach (var revision in history)
        {
            Assert.NotEmpty(revision.EvidenceBasis);
            Assert.All(revision.Occurrences ?? [], occurrence =>
            {
                Assert.NotEmpty(occurrence.EvidenceBasis);
                Assert.All(occurrence.EvidenceBasis, id => Assert.Contains(id, revision.EvidenceBasis));
                if (occurrence.Locator is { } b)
                    Assert.All(new[] { b.X1, b.Y1, b.X2, b.Y2 }, value =>
                    {
                        Assert.True(double.IsFinite(value));
                        Assert.InRange(value, 0, 1);
                    });
            });
        }
        var slices = first.Host.WorldCore.Current!.Containers!.Select(container =>
        {
            var slice = first.Host.WorldCore.DeriveSlice(container.Identity.ContainerId);
            var replay = second.Host.WorldCore.DeriveSlice(container.Identity.ContainerId);
            Assert.Equal(JsonSerializer.Serialize(slice), JsonSerializer.Serialize(replay));
            Assert.All(slice.Occurrences, o => Assert.Contains(o.OwningContainerId!, slice.InScopeContainerIds));
            return slice;
        }).ToArray();
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, scenarioId + ".json"), JsonSerializer.Serialize(new
            {
                scenarioId, revisions = history.Count, first.Report.AcceptancePassed,
                first.Report.SemanticDigest, slices,
                limitation = "Runtime replay with simulation observation strategy; not full-page visual fidelity",
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed class ReplayLeaseDrift(DateTimeOffset driftFrom) : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) =>
            input.Previous is null || input.Current.Provenance.CaptureTime >= driftFrom
                ? new(AssociationDispositionKind.New, null,
                    new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, []) }, [], "identity-drift")
                : new(AssociationDispositionKind.Insufficient, null, [], [], "seed-once");
    }
}
