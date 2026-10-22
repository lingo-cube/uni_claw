using System.Text.Json;
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

namespace UniClaw.Simulation.Tests;

/// <summary>Reviewed synthetic tri-state source; no XML/ExactProof or device claim.
/// Expected verification is independent of the source adapter and product state.</summary>
public sealed class ControlledCheckedReplayTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record Expected(bool Verified, string Phase, string? RejectionReasonContains);
    private sealed record Case(string Id, string Before, string After, string Desired, string Checkable,
        int PostSecond, int PostNodes, Expected Expected);
    private sealed record Corpus(string Source, Case[] Cases);
    private sealed record Decision(string Phase, string? FailureReason);
    private sealed record Result(string Status, EffectReceipt[] Receipts, PostActionEffectVerification[] Verifications,
        Decision[] Decisions, EvidenceRecord[] PostEvidence, Slice Slice);

    [Theory, Trait("Scenario", "SCN-REPLAY-006")]
    [InlineData("turn off"), InlineData("turn on"), InlineData("partial"), InlineData("unknown")]
    [InlineData("unsupported"), InlineData("unchanged"), InlineData("stale"), InlineData("equal timestamp")]
    [InlineData("ambiguous post"), InlineData("not checkable"), InlineData("checkable missing")]
    public void ControlledChecked_IndependentPositiveAndRefusalCases(string id)
    {
        var corpus = JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(),
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/controlled-checked.json")), Json)!;
        var input = corpus.Cases.Single(c => c.Id == id);
        var actual = Replay(input);
        var verification = Assert.Single(actual.Verifications);
        var firstDivergence = verification.IsVerified != input.Expected.Verified ? "verification.isVerified"
            : actual.Decisions[^1].Phase != input.Expected.Phase ? "consultation.phase"
            : input.Expected.RejectionReasonContains is not null
                && verification.RejectionReason?.Contains(input.Expected.RejectionReasonContains, StringComparison.Ordinal) != true
                ? "verification.rejectionReason" : null;
        if (Environment.GetEnvironmentVariable("UNICLAW_CONTROLLED_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, id.Replace(' ', '-') + ".json"),
                JsonSerializer.Serialize(new { corpus.Source, input, actual, firstDivergence }, Json));
        }
        Assert.Equal("TerminalNotProven", actual.Status); // No goal-evaluation claim in this carrier.
        Assert.Single(actual.Receipts);
        Assert.Equal(2, actual.Decisions.Length);
        Assert.Equal(input.Expected.Verified, verification.IsVerified);
        Assert.Equal(input.Expected.Phase, actual.Decisions[^1].Phase);
        if (input.Expected.RejectionReasonContains is not null)
            Assert.Contains(input.Expected.RejectionReasonContains, verification.RejectionReason ?? string.Empty);
    }

    private static Result Replay(Case input)
    {
        var ledger = new EvidenceLedger(); var adapter = new ControlledObservationAdapter();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*" },
            new ProductAssociationStrategy(), adapter);
        var effects = new EffectBoundary(new Delivery()); var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0.AddSeconds(3), TimeSpan.FromMinutes(1))), effects);
        var initial = false; var post = false; var decisions = new List<Decision>();
        RunDriverInput Observe(ObservationDirective directive, bool isPost)
        {
            var capture = Capture(input, isPost); adapter.Use(capture);
            var time = capture.Metadata.CaptureTimestamp;
            var proposals = new List<ObservationProposal>();
            foreach (var subject in new[] { "ui.screen", "ui.screen.route" })
                proposals.Add(new(new(subject, "controlled-page"), IngressKind.Observation, directive.Context,
                    new("controlled-reviewed-context", time, subject + ":" + capture.Metadata.CaptureId, ["synthetic:sim011:v1"])));
            proposals.AddRange(TypedHierarchyProposalProjector.Project(capture, directive.Context));
            return new RunDriverInput.Observation(proposals);
        }
        var runtime = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (!initial) { initial = true; return Observe(directive, false); }
                if (!post && directive.Context == ObservationContext.PostActionEffectFlow)
                { post = true; return Observe(directive, true); }
                return null;
            },
            ConsultAgent = context =>
            {
                decisions.Add(new(context.Phase.ToString(), context.FailureReason));
                return effects.ReceiptLog.Count == 0
                    ? new AgentDecision.Act(new(context.DecisionId,
                        [new("switch", "Controlled switch", "tap", input.Desired)], "reviewed controlled state action"))
                    : new AgentDecision.NoAction(new(context.DecisionId, "bounded controlled replay complete"));
            }
        });
        Assert.True(kernel.AdmitContract(new("sim011", "controlled semantic checked replay", new HashSet<string>
            { "ui.screen", "ui.screen.route", "ui.node.*" }, new HashSet<string> { "tap" }, new HashSet<string>(),
            ["controlled regression"], TaskInstanceId: "sim011:" + input.Id, ProductSessionId: "sim011", ContractRef: "sim011:v1")).Accepted);
        Assert.True(runtime.Activate().Accepted);
        var result = runtime.Drive();
        Assert.All(ledger.AdmissionLog, a => Assert.Equal(AdmissionDecision.Accepted, a.Decision));
        Assert.All(world.Current!.Occurrences!, o => Assert.All(o.EvidenceBasis, id => Assert.Contains(id, ledger.CanonicalRecords.Keys)));
        var slice = world.DeriveSlice(world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);
        return new(result.Status.ToString(), effects.ReceiptLog.ToArray(), kernel.PostActionVerifications.ToArray(),
            decisions.ToArray(), ledger.CanonicalRecords.Values.Where(r => r.Context == ObservationContext.PostActionEffectFlow).ToArray(), slice);
    }

    private static UiHierarchyObservation Capture(Case input, bool post)
    {
        var id = input.Id.Replace(' ', '-') + (post ? ":post" : ":before");
        var capabilities = HierarchyCapability.CheckedTriState | HierarchyCapability.SemanticText;
        if (post && input.After == "unsupported") capabilities = HierarchyCapability.SemanticText;
        var metadata = new CaptureMetadata(id, 36, UiHierarchyAcquirerKind.AccessibilityNodeInfo,
            "synthetic-reviewed-v1", UiHierarchyFormat.AccessibilityTree, T0.AddSeconds(post ? input.PostSecond : -2),
            null, "synthetic-device", "sim011", id, new(capabilities),
            new(CoverageCompleteness.CompleteWithinDeclaredSurface), CoordinateSpace.DeviceViewport(100, 100, captureId: id));
        var nodes = Enumerable.Range(0, post ? input.PostNodes : 1).Select(index =>
        {
            ObservedValue<T> Known<T>(T value) => ObservedValue<T>.Observed(value, new(id));
            ObservedValue<T> Missing<T>() => ObservedValue<T>.Unknown("controlled-unavailable", new(id));
            var raw = post ? input.After : input.Before;
            var state = raw switch
            {
                "checked" => CheckedSemantics.MapTriState(CheckedState.Checked, new(id)),
                "unchecked" => CheckedSemantics.MapTriState(CheckedState.Unchecked, new(id)),
                "partial" => CheckedSemantics.MapTriState(CheckedState.Partial, new(id)),
                "unsupported" => ObservedValue<CheckedState>.Unsupported("controlled-no-capability", new(id)),
                _ => Missing<CheckedState>()
            };
            var checkable = !post || input.Checkable == "true" ? Known(true)
                : input.Checkable == "false" ? Known(false) : Missing<bool>();
            return new UiNodeObservation(new(id, index), null, null, index, null,
                Known("fixture.Switch"), Known("fixture:switch:" + index), Known("controlled.fixture"), Known("Controlled switch"),
                Missing<string>(), Missing<string>(), checkable, state, Known(true), Known(false), Known(false),
                Known(false), Known(true), Known(true), Known(true), Known(false), Known(new UiBounds(10 + index * 30, 10, 30 + index * 30, 30)));
        }).ToArray();
        return new(metadata, Array.Empty<UiWindowOccurrence>(), nodes);
    }

    // This adapter models the existing Switch-class occurrence path, including
    // invalid checkable evidence. It does not grant semantic state validity:
    // consumers must enforce that independently from role/class and appearance.
    private sealed class ControlledObservationAdapter : IUiObservationStrategy
    {
        private UiHierarchyObservation? _capture;
        private string? _lastSubject;
        private readonly List<string> _basis = new();
        public void Use(UiHierarchyObservation capture)
        {
            _capture = capture; _basis.Clear();
            _lastSubject = TypedHierarchyProposalProjector.Project(capture, ObservationContext.External)[^1].Claim.Subject;
        }
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (record.Provenance.Hierarchy?.CaptureId == _capture?.Metadata.CaptureId)
            {
                _basis.Add(record.EvidenceId);
                if (record.Claim.Subject == _lastSubject)
                    return _capture!.Nodes.Select(n => new ProposedOccurrence(
                        previous?.WorldState.GetValueOrDefault(WorldModel.CurrentContainerSubject)?.Value,
                        "switch", n.Text.Value, n.Checked.State == FieldState.Observed ? n.Checked.Value.ToString().ToLowerInvariant() : null,
                        new SpatialLocator(n.Bounds.Value!.X1 / 100d, n.Bounds.Value.Y1 / 100d,
                            n.Bounds.Value.X2 / 100d, n.Bounds.Value.Y2 / 100d, "device-viewport"),
                        new NativeLocator("android.resource-id", n.ResourceId.Value!), _capture.Metadata.Space)
                    { SupportingEvidenceIds = _basis.ToArray() }).ToArray();
            }
            return previous?.Occurrences?.Select(o => new ProposedOccurrence(o.OwningContainerId, o.Role,
                o.SemanticDescriptor, o.State, o.Locator, o.Native, o.Space) { SupportingEvidenceIds = o.EvidenceBasis }).ToArray()
                ?? Array.Empty<ProposedOccurrence>();
        }
    }
    private sealed class Delivery : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted,
            "synthetic controlled delivery", T0.AddSeconds(1));
    }
}
