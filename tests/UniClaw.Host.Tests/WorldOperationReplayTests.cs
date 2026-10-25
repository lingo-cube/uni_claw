using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Core;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalWorldStoragePreservesEveryVerifiedNavigationAndViewportRevision(bool navigationCycle)
    {
        var root = Directory.CreateTempSubdirectory("world-operations-original-").FullName;
        try
        {
            var runId = Guid.NewGuid().ToString("N"); string wid, eid; ReplayResult original; IReadOnlyList<WorldBeliefRevision>? fullHistory = null;
            using (var e = new FileEvidenceJournal(Path.Combine(root, "evidence")))
            using (var w = new FileWorldJournal(Path.Combine(root, "world")))
            {
                wid = w.RecordingId; eid = e.RecordingId;
                original = Execute(navigationCycle: navigationCycle, includePageCaptures: true,
                    recording: new(w, runId, eid), originalLedger: new(e), captureHistory: rows => fullHistory = rows.ToArray());
            }
            using var er = new FileEvidenceJournal(Path.Combine(root, "evidence"), true);
            using var wr = new FileWorldJournal(Path.Combine(root, "world"), true);
            var history = WorldHistoryRead.ReadOriginal(wr, wid, runId, EvidenceLedger.RestoreReadOnly(er, eid));
            Assert.Equal(4, original.Receipts.Count);
            Assert.Equal(fullHistory!.Count, history.HistoricalRevisions.Count);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            for (var i = 0; i < fullHistory!.Count; i++)
                AssertOriginalJson(JsonSerializer.SerializeToElement(fullHistory![i], json), history.HistoricalRevisions[i]);
            Assert.Contains(history.Operations, r => r.Operation == "navigation");
            Assert.Equal(navigationCycle ? 0 : 2, history.Operations.Count(r => r.Operation == "viewport"));
            if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_HISTORY_EXPORT") is { Length: > 0 } output)
            {
                var destination = Path.Combine(output, navigationCycle ? "navigation" : "viewport");
                Directory.CreateDirectory(destination);
                foreach (var file in Directory.GetFiles(root)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
                File.WriteAllText(Path.Combine(destination, "original-expected.json"), JsonSerializer.Serialize(original, json));
                File.WriteAllText(Path.Combine(destination, "full-original-history.json"), JsonSerializer.Serialize(fullHistory, json));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void AssertOriginalJson(JsonElement expected, JsonElement actual)
    {
        Assert.True(JsonElement.DeepEquals(expected, actual));
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var before = expected.EnumerateObject().ToArray(); var after = actual.EnumerateObject().ToArray();
            Assert.Equal(before.Select(p => p.Name), after.Select(p => p.Name));
            for (var i = 0; i < before.Length; i++) AssertOriginalJson(before[i].Value, after[i].Value);
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            var before = expected.EnumerateArray().ToArray(); var after = actual.EnumerateArray().ToArray();
            Assert.Equal(before.Length, after.Length);
            for (var i = 0; i < before.Length; i++) AssertOriginalJson(before[i], after[i]);
        }
    }

    [Fact]
    public void RuntimePageCaptures_ExposeOwnerAccumulationAfterActualScrollVerification()
    {
        var result = Execute(includePageCaptures: true);
        Assert.Equal(4, result.Receipts.Count);
        var aligned = result.PageViews!.Where(v => v.AccumulatedContent!.RegistrationSourceRefs.Count > 0).ToArray();
        Assert.NotEmpty(aligned);
        Assert.All(aligned, view =>
        {
            Assert.Contains(view.AccumulatedContent!.Items, item => item.SemanticDescriptor == "Alpha");
            Assert.Contains("whole-page-coverage-unproven", view.AccumulatedContent.Unknown);
            foreach (var source in view.AccumulatedContent.RegistrationSourceRefs)
                Assert.Contains(result.Receipts, receipt => source.Contains(receipt.ReceiptId, StringComparison.Ordinal));
        });
        var visits = result.PageViews.SelectMany(v => v.Observations)
            .Where(o => o.VisitSourceRef is not null).Select(o => o.VisitSourceRef).Distinct().ToArray();
        Assert.Equal(2, visits.Length); // confirmed root→child and child→root, never one visit per capture
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_CAPTURE_EXPORT") is { Length: > 0 } output)
            File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void ConfirmedNavigationCycle_ReusesRootIdentityButDistinguishesThreeObservationRounds()
    {
        var result = Execute(includePageCaptures: true, navigationCycle: true);
        Assert.Equal(4, result.Graph.Edges.Count);
        Assert.Empty(result.Graph.ViewportTransitions);
        var final = result.PageViews!.Last();
        Assert.Equal(3, final.Observations.Count);
        Assert.Equal(3, final.Observations.Select(o => o.CaptureEvidenceId).Distinct().Count());
        Assert.Equal(2, final.Observations.Count(o => o.VisitSourceRef is not null));
        Assert.Single(final.Observations.Where(o => o.IsCurrentCapture));
        Assert.Equal(3, result.Graph.Nodes.Count);
        Assert.All(final.Observations, o => Assert.Equal(final.ContainerId, o.ContainerId));
    }

    [Fact]
    public void OrdinaryRecaptureAfterReturn_DoesNotBorrowThePreviousNavigationVisit()
    {
        var result = Execute(includePageCaptures: true, navigationCycle: true, ordinaryRecapture: true);
        var page = result.PageViews!.Last();
        Assert.Equal(4, page.Observations.Count);
        Assert.Equal(2, page.Observations.Count(o => o.VisitSourceRef is not null));
        Assert.Null(Assert.Single(page.Observations.Where(o => o.IsCurrentCapture)).VisitSourceRef);
        Assert.Equal(4, result.Graph.Edges.Count);
    }

    [Fact]
    public void RuntimeReplay_UsesActualNavigationEdgesReceiptsAndScrollTransforms()
    {
        var first = Execute(); var second = Execute();
        var firstOwner = CheckAttemptReferences(first);
        var secondOwner = CheckAttemptReferences(second);
        Assert.NotEqual(firstOwner, secondOwner);
        // 先验证独立issuer及每张回执的关联，只在测试副本中重命名第二issuer。
        var renamed = second with { Receipts = second.Receipts.Select(r => r with
            { AttemptCorrelation = r.AttemptCorrelation! with { OwnerId = firstOwner } }).ToArray() };
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(renamed));
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

    private static string CheckAttemptReferences(ReplayResult result)
    {
        Assert.NotEmpty(result.Receipts);
        Assert.All(result.Receipts, receipt =>
        {
            var attempt = Assert.IsType<AttemptReference>(receipt.AttemptCorrelation);
            Assert.True(Guid.TryParseExact(attempt.OwnerId, "N", out _));
            Assert.Equal(receipt.ReceiptId, attempt.ReceiptId);
            Assert.Equal(receipt.IntentId, attempt.IntentId);
            Assert.Equal(receipt.BindingId, attempt.BindingId);
        });
        return Assert.Single(result.Receipts.Select(r => r.AttemptCorrelation!.OwnerId).Distinct());
    }

    private sealed record ContentPoint(string OperationReceiptId, string RevisionId, string Label, double X, double Y);
    private sealed record ReplayResult(WorldGraphProjection Graph, IReadOnlyList<EffectReceipt> Receipts,
        IReadOnlyList<WorldBeliefRevision> Revisions, IReadOnlyList<ContentPoint> Content, string Status,
        IReadOnlyList<WorldPageView>? PageViews = null);

    private static ReplayResult Execute(string? recordedSequence = null, bool includePageCaptures = false,
        bool navigationCycle = false, bool ordinaryRecapture = false,
        WorldHistoryRecording? recording = null, EvidenceLedger? originalLedger = null,
        Action<IReadOnlyList<WorldBeliefRevision>>? captureHistory = null)
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*",
                PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy(), history: recording);
        var effects = new EffectBoundary(new RecordedDriver());
        var plan = new AgentPlanPolicy();
        var kernel = new UniKernel(originalLedger ?? new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0.AddMinutes(1), TimeSpan.FromMinutes(5))), effects);
        var captures = navigationCycle
            ? new[] { ("root", 0), ("child", 0), ("root", 0), ("other", 0), ("root", 0) }
            : new[] { ("root", 0), ("child", 0), ("child", -200), ("child", 0), ("root", 0) };
        var captureIndex = 0;
        var visualFrames = new List<object>();
        var visualOutput = includePageCaptures && !navigationCycle
            ? Environment.GetEnvironmentVariable("UNICLAW_WORLD_VIS_SCROLL_EXPORT") : null;
        void CaptureVisualization()
        {
            if (string.IsNullOrWhiteSpace(visualOutput) || world.Current?.CurrentPageObservation?.ContainerId is not { } root)
                return;
            var revision = world.Current;
            var capture = revision.CurrentPageObservation.Capture;
            var full = kernel.DeriveSlice(root);
            var upper = kernel.DeriveSlice(root, region: new SliceRegion(capture.CaptureId, capture.Space,
                new SpatialLocator(0, 0, 1, .5, "device-viewport")));
            var lower = kernel.DeriveSlice(root, region: new SliceRegion(capture.CaptureId, capture.Space,
                new SpatialLocator(0, .5, 1, 1, "device-viewport")));
            var slices = new[] { full, upper, lower }.Select(slice => new
            {
                sliceId = CoreSemanticProjection.SliceId(slice).Value, slice,
                coreSlice = CoreSemanticProjection.ProjectSlice(slice, revision),
                presentation = SliceContentPresentation.Project(slice, "device-viewport",
                    slice.Region?.Bounds ?? new SpatialLocator(0, 0, 1, 1, "device-viewport"))
            }).ToArray();
            visualFrames.Add(new
            {
                key = "capture-" + capture.CaptureId, label = "实际运行边界：" + capture.CaptureId,
                revision, knowledge = kernel.ProjectWorldKnowledge(),
                graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}"),
                pages = new[] { world.DerivePageView(root, revision.RevisionId) }, slices,
                receipts = effects.ReceiptLog.ToArray(), verifications = kernel.PostActionVerifications.ToArray()
            });
        }
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
                CaptureVisualization();
                if (captureIndex >= captures.Length) return null;
                var (page, shift) = captures[captureIndex];
                var time = T0.AddSeconds(captureIndex * 10); var capture = "capture-" + captureIndex++;
                var proposals = new List<ObservationProposal>();
                foreach (var subject in includePageCaptures ? new[] { "ui.screen.route" } : new[] { "ui.screen", "ui.screen.route" })
                    proposals.Add(new(new(subject, page), IngressKind.Observation, directive.Context,
                        new("replay.page", time, "scope:" + subject + ":" + capture, new[] { "recorded-input:" + capture })));
                var xml = page == "child" && recorded is not null
                    ? File.ReadAllText(Path.Combine(WorldReplayDiagnostics.Corpus, "uia", recorded[shift == 0 ? 0 : 1] + ".xml"))
                    : PageXml(page, shift);
                if (includePageCaptures && page == "root")
                    xml = xml.Replace("<hierarchy>", "<hierarchy><node text=\"Root\" bounds=\"[0,0][1000,90]\"/>", StringComparison.Ordinal);
                if (includePageCaptures)
                {
                    var observation = PageIdentityRecovery.Capture(capture, capture, "replay-session", time,
                        1000, 1000, xml, null) with { DeviceId = "fixture-device" };
                    var proposal = new ObservationProposal(new(PageObservationCodec.CaptureSubject,
                        PageObservationCodec.Encode(observation)), IngressKind.Observation, directive.Context,
                        new("controlled-layout", time, "capture:" + capture, ["controlled-xml:" + capture]));
                    proposals.Add(proposal);
                    var evidenceId = new EvidenceLedger().Admit(proposal).Admission.EvidenceId!;
                    var title = page == "root" ? "Root" : "Back";
                    var identity = PageIdentityRecovery.Identity(new(observation, evidenceId, null), title,
                        page, directive.Context);
                    Assert.NotNull(identity);
                    proposals.Add(identity!);
                }
                var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
                    new(capture, time, "fixture-device", "replay-session", 35,
                        ObservationCycleId: capture, Space: page == "child" && recorded is not null
                            ? CoordinateSpace.DeviceViewport(1080, 2400) : CoordinateSpace.DeviceViewport(1000, 1000,
                                captureId: includePageCaptures ? capture : null)));
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
                var step = navigationCycle ? new AgentActionStep("ui.element", completed % 2 == 0 ? "Open" : "Back", "tap", null)
                    : completed switch
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
        CaptureVisualization();
        if (!string.IsNullOrWhiteSpace(visualOutput))
        {
            if (!Path.IsPathFullyQualified(visualOutput)) throw new InvalidOperationException("visual export must be absolute");
            var payload = new
            {
                schemaVersion = "world-visualization.v1", id = "world-visualization:scroll",
                title = "滚动、返回与三个实际 Slice 范围",
                provenance = new { coreCommit = "56a75fd7", inputClass = "真实运行链 / 受控布局与delivery",
                    sourceTest = nameof(RuntimePageCaptures_ExposeOwnerAccumulationAfterActualScrollVerification),
                    limitations = new[] { "受控XML、时钟、咨询与delivery，不是真机或实时模型。", "只导出运行边界；中间修订未逐帧采集。" } },
                frames = visualFrames
            };
            Directory.CreateDirectory(Path.GetDirectoryName(visualOutput)!);
            File.WriteAllText(visualOutput, JsonSerializer.Serialize(payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.True(captureIndex == 5, $"{result.Status}:{result.Reason}; consumed={captureIndex}");
        Assert.All(kernel.PostActionVerifications, v => Assert.True(v.IsVerified, v.RejectionReason));
        if (ordinaryRecapture)
        {
            var observed = PageIdentityRecovery.Capture("ordinary", "ordinary-cycle", "replay-session",
                T0.AddSeconds(60), 1000, 1000,
                "<hierarchy><node text=\"Root\" bounds=\"[0,0][1000,90]\"/><node text=\"Open\" bounds=\"[100,300][800,340]\"/></hierarchy>", null)
                with { DeviceId = "fixture-device" };
            var admitted = kernel.Process(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(observed)),
                IngressKind.Observation, ObservationContext.External,
                new("controlled-layout", observed.CaptureTime, "capture:ordinary", ["controlled-xml:ordinary"])));
            var identity = PageIdentityRecovery.Identity(new(observed, admitted.Admission.EvidenceId!, null),
                "Root", "root", ObservationContext.External);
            Assert.NotNull(identity);
            kernel.Process(identity!);
        }
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
        var views = includePageCaptures ? world.RevisionHistory.Where(r => r.CurrentPageObservation?.ContainerId is not null)
            .Select(r => world.DerivePageView(r.CurrentPageObservation!.ContainerId!, r.RevisionId)).ToArray() : null;
        captureHistory?.Invoke(world.RevisionHistory);
        return new(graph, effects.ReceiptLog, revisions, content, result.Status.ToString(), views);
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
