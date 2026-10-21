using System.Text.Json;
using UniClaw.Host;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldOperationReplayTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RuntimeReplay_UsesActualNavigationEdgesReceiptsAndScrollTransforms()
    {
        var first = Execute(); var second = Execute();
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(4, first.Receipts.Count);
        Assert.Equal(2, first.Graph.Edges.Count);
        Assert.Equal(2, first.Graph.ViewportTransitions.Count);
        var forward = first.Graph.ViewportTransitions[0]; var reverse = first.Graph.ViewportTransitions[1];
        Assert.Equal("Aligned", forward.AlignmentStatus);
        Assert.Equal(.2, forward.OffsetY!.Value, 9);
        Assert.Equal("Aligned", reverse.AlignmentStatus);
        Assert.Equal(-.2, reverse.OffsetY!.Value, 9);
        Assert.Equal(0, forward.OffsetY.Value + reverse.OffsetY.Value, 9);
        Assert.Contains(forward.Correspondences, pair => pair.Stationary);
        Assert.All(first.Content.Where(item => item.Label == "Alpha"), item => Assert.Equal(.4, item.Y, 9));
        Assert.All(first.Content.Where(item => item.Label == "Beta"), item => Assert.Equal(.6, item.Y, 9));
        Assert.All(first.Graph.ViewportTransitions, edge =>
        {
            var receipt = Assert.Single(first.Receipts, r => r.ReceiptId == edge.OperationReceiptId);
            Assert.Equal(receipt.BindingId, edge.OperationBindingId);
            Assert.Equal(receipt.TargetSubject, edge.SourceTargetOccurrenceId);
            Assert.Contains(edge.OperationReceiptId, edge.VerificationSourceRef);
        });
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "operation-replay.json"), JsonSerializer.Serialize(first, new JsonSerializerOptions { WriteIndented = true }));
            WorldOperationReplayReport.Write(output, JsonSerializer.Serialize(first));
        }
    }

    [Theory]
    [InlineData("seq-scroll-apps")]
    [InlineData("seq-scroll-devopts")]
    public void RecordedScroll_PreservesRealOperationRelationsWithoutInventingOffsets(string sequence)
    {
        var result = Execute(sequence);
        Assert.Equal(4, result.Receipts.Count);
        Assert.Equal(2, result.Graph.Edges.Count);
        Assert.Equal(2, result.Graph.ViewportTransitions.Count);
        Assert.All(result.Graph.ViewportTransitions, edge =>
        {
            Assert.NotEqual("Aligned", edge.AlignmentStatus);
            Assert.Null(edge.OffsetX); Assert.Null(edge.OffsetY);
            Assert.Contains(result.Receipts, receipt => receipt.ReceiptId == edge.OperationReceiptId);
        });
        Assert.Empty(result.Content);
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, sequence + ".json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed record ContentPoint(string OperationReceiptId, string RevisionId, string Label, double X, double Y);
    private sealed record ReplayResult(WorldGraphProjection Graph, IReadOnlyList<EffectReceipt> Receipts,
        IReadOnlyList<WorldBeliefRevision> Revisions, IReadOnlyList<ContentPoint> Content, string Status);

    private static ReplayResult Execute(string? recordedSequence = null)
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        var effects = new EffectBoundary(new RecordedDriver());
        var plan = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0.AddMinutes(1), TimeSpan.FromMinutes(5))), effects);
        var captures = new[] { ("root", 0), ("child", 0), ("child", -200), ("child", 0), ("root", 0) };
        var captureIndex = 0;
        string[]? recorded = null;
        if (recordedSequence is not null)
        {
            using var sequence = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorldReplayDiagnostics.Corpus, "sequences", recordedSequence + ".json")));
            recorded = sequence.RootElement.GetProperty("frames").EnumerateArray().Select(frame => frame.GetString()!).ToArray();
        }
        var driver = new KernelRunDriver(kernel, plan, new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (captureIndex >= captures.Length) return null;
                var (page, shift) = captures[captureIndex];
                var time = T0.AddSeconds(captureIndex * 10); var capture = "capture-" + captureIndex++;
                var proposals = new List<ObservationProposal>();
                foreach (var subject in new[] { "ui.screen", "ui.screen.route" })
                    proposals.Add(new(new(subject, page), IngressKind.Observation, directive.Context,
                        new("replay.page", time, "scope:" + subject + ":" + capture, new[] { "recorded-input:" + capture })));
                var xml = page == "child" && recorded is not null
                    ? File.ReadAllText(Path.Combine(WorldReplayDiagnostics.Corpus, "uia", recorded[shift == 0 ? 0 : 1] + ".xml"))
                    : PageXml(page, shift);
                var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
                    new(capture, time, "fixture-device", "replay-session", 35,
                        ObservationCycleId: capture, Space: page == "child" && recorded is not null
                            ? CoordinateSpace.DeviceViewport(1080, 2400) : CoordinateSpace.DeviceViewport(1000, 1000)));
                proposals.AddRange(TypedHierarchyProposalProjector.Project(parsed.Observation!, directive.Context));
                return new RunDriverInput.Observation(proposals);
            },
            ConsultAgent = context =>
            {
                var completed = effects.ReceiptLog.Count;
                if (completed >= 4) return new AgentDecision.NoAction(new(context.DecisionId, "bounded replay complete"));
                var scrollables = context.Elements?.Where(e => e.Role == "scrollable").ToArray() ?? [];
                var scroll = scrollables.FirstOrDefault(e => e.Text == "com.android.settings:id/recycler_view")
                    ?? scrollables.FirstOrDefault();
                var step = completed switch
                {
                    0 => new AgentActionStep("ui.element", "Open", "tap", null),
                    1 => new AgentActionStep("scrollable", scroll?.Text, "swipe-up", null),
                    2 => new AgentActionStep("scrollable", scroll?.Text, "swipe-down", null),
                    _ => new AgentActionStep("ui.element", recorded is null ? "Back" : "Navigate up", "tap", null),
                };
                return new AgentDecision.Act(new(context.DecisionId, new[] { step }, "recorded operation replay"));
            },
        });
        Assert.True(kernel.AdmitContract(new("viewport-v1", "bounded operation replay", new HashSet<string> { "settings" },
            new HashSet<string> { "tap", "swipe-up", "swipe-down" }, new HashSet<string>(), new[] { "record operations" },
            TaskInstanceId: "viewport-task", ProductSessionId: "viewport-session", ContractRef: "viewport-contract")).Accepted);
        Assert.True(driver.Activate().Accepted);
        var result = driver.Drive();
        Assert.True(captureIndex == 5, $"{result.Status}:{result.Reason}; consumed={captureIndex}");
        Assert.All(kernel.PostActionVerifications, v => Assert.True(v.IsVerified, v.RejectionReason));
        var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}")!;
        var ids = graph.Edges.SelectMany(edge => new[] { edge.SourceRevisionId, edge.TargetRevisionId })
            .Concat(graph.ViewportTransitions.SelectMany(edge => new[] { edge.SourceRevisionId, edge.TargetRevisionId })).ToHashSet();
        var revisions = world.RevisionHistory.Where(revision => ids.Contains(revision.RevisionId)).ToArray();
        var content = new List<ContentPoint>();
        var x = 0.0; var y = 0.0;
        foreach (var edge in graph.ViewportTransitions)
        {
            if (edge.OffsetX is not { } dx || edge.OffsetY is not { } dy) break;
            x += dx; y += dy;
            var target = revisions.Single(revision => revision.RevisionId == edge.TargetRevisionId);
            foreach (var pair in edge.Correspondences.Where(pair => !pair.Stationary))
            {
                var occurrence = target.Occurrences!.Single(item => item.OccurrenceId == pair.TargetOccurrenceId);
                content.Add(new(edge.OperationReceiptId, target.RevisionId, occurrence.SemanticDescriptor!, occurrence.Locator!.X1 + x, occurrence.Locator.Y1 + y));
            }
        }
        return new(graph, effects.ReceiptLog, revisions, content, result.Status.ToString());
    }

    private sealed class RecordedDriver : IEffectDriver
    {
        private int _step;
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "recorded-driver:" + request.EffectClass, T0.AddSeconds(++_step * 10 - 5));
    }

    private static string PageXml(string page, int shift)
    {
        static string Button(string label, int y) => $"<node text=\"{label}\" resource-id=\"id/{label}\" class=\"android.widget.Button\" clickable=\"true\" enabled=\"true\" checkable=\"false\" scrollable=\"false\" bounds=\"[100,{y}][800,{y + 40}]\"/>";
        return "<hierarchy>" + (page == "root" ? Button("Open", 300)
            : Button("Back", 30) + "<node resource-id=\"id/list\" class=\"android.widget.ScrollView\" clickable=\"false\" enabled=\"true\" checkable=\"false\" scrollable=\"true\" bounds=\"[0,100][1000,950]\">"
                + Button("Sticky", 120) + Button("Alpha", 400 + shift) + Button("Beta", 600 + shift) + "</node>") + "</hierarchy>";
    }
}
