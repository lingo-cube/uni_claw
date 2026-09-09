using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ESO-002 验收 F1–F5 —— entity-scoped obligation fulfillment：owner-derived
/// tri-state fact（D1/D5）→ OutcomeAssuranceView 携带（F2）→ JudgeOutcome
/// 实判（D2/F3）→ UniKernel EvaluateTerminal E2E 正/负向（F4/F5）。
/// E2E 复用 ESO-001 管线（entity-scoped contract → standing demand）+
/// CDS-001 State 载荷 + CTL DescriptorTargetPolicy DesiredState 语义。
/// 全部确定性（level: DETERMINISTIC）。
/// </summary>
public sealed class EntityObligationFulfillmentTests
{
    private const string ObligationId = "obl-switch-on";
    private static readonly TargetDescriptor SwitchScope = new("switch", "primary");
    private static readonly DateTimeOffset ActTime = new(2026, 9, 8, 10, 5, 0, TimeSpan.Zero);

    // ---- 测试替身 ---------------------------------------------------------

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", ActTime);
    }

    /// <summary>stateful observation double（CDS-001 State 载荷）：claim value
    /// 片段约定（测试域）"role:descriptor@state"，'+' 连接多 occurrence；
    /// descriptor / state 缺省 = null（Unknown 语义）。</summary>
    private sealed class StatefulObservationStrategy(string? owner = null) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            record.Claim.Value.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment =>
                {
                    var roleParts = segment.Split(':', 2);
                    var descParts = roleParts.Length == 2 ? roleParts[1].Split('@', 2) : roleParts[0].Split('@', 2);
                    return new ProposedOccurrence(
                        owner, roleParts[0],
                        descParts.Length == 2 ? descParts[0] : null,
                        descParts.Length == 2 ? descParts[1] : null);
                })
                .ToArray();
    }

    /// <summary>SeedContainer 首条 evidence 探针（ControlReferencePolicyTests
    /// ProbeContainerId 同法）：确定性预知 minted root container id。</summary>
    private static string ProbeContainerId(string seedValue)
    {
        var (admission, _) = new EvidenceLedger().Admit(
            UIWorldDoubles.Observation(seedValue, UIWorldDoubles.T0));
        return "ctr-" + admission.EvidenceId![3..15];
    }

    private static ObservationProposal PostActionObservation(string value, DateTimeOffset t) => new(
        new ObservationClaim(UIWorldDoubles.Observed, value),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance("provider.scripts", t, "scope:ui.container", new[] { "raw://capture", "encode:v1" }));

    private static ExecutionContract SwitchContract() => new(
        Version: "v1",
        Objective: "turn-switch-on",
        Scope: new HashSet<string> { UIWorldDoubles.Observed },
        AllowedEffects: new HashSet<string> { "toggle" },
        ForbiddenEffects: new HashSet<string>(),
        ProofCriteria: new[] { "switch-on" },
        Obligations: new[]
        {
            new RunObligation(
                ObligationId, RunObligationKind.Objective,
                Subject: "switch", RequiredValue: "on", Mandatory: true,
                EntityScope: SwitchScope),
        });

    private static (UniKernel Kernel, WorldModel World, string Cid) NewSwitchKernel()
    {
        var cid = ProbeContainerId("switch:primary@off");
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(),
            new StatefulObservationStrategy(cid),
            new RoleContinuityStrategy());
        var policy = new DescriptorTargetPolicy(new[]
        {
            new TargetSpec("switch", "primary", "toggle", DesiredState: "on"),
        });
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new OkDriver()));
        return (kernel, world, cid);
    }

    private static EntityObligationFact SingleFact(string observedValue)
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            associationStrategy: null, new StatefulObservationStrategy());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance);
        kernel.Process(UIWorldDoubles.Observation(observedValue, UIWorldDoubles.T0));

        var view = world.DeriveOutcomeAssuranceView(
            Array.Empty<string>(),
            new[] { (ObligationId, SwitchScope, "on") });
        return Assert.Single(view.EntityFacts!);
    }

    // ---- F1：fact tri-state 五情形（D1 fail-closed） -----------------------

    [Fact]
    public void F1_FactDerivation_FiveCases_TriStateFailClosed()
    {
        // 恰一匹配且 State==required → Satisfied
        Assert.Equal(EntityObligationFactKind.Satisfied, SingleFact("switch:primary@on").Kind);

        // 恰一匹配且 State 有值但 ≠ required → Unsatisfied
        Assert.Equal(EntityObligationFactKind.Unsatisfied, SingleFact("switch:primary@off").Kind);

        // 零候选（Role 无匹配）→ Unknown
        Assert.Equal(EntityObligationFactKind.Unknown, SingleFact("icon:x@on").Kind);

        // 多候选（descriptor 帧内二义）→ Unknown（Identity never creates information）
        Assert.Equal(EntityObligationFactKind.Unknown,
            SingleFact("switch:primary@on+switch:primary@off").Kind);

        // State null（无 state 证据，非 false）→ Unknown
        Assert.Equal(EntityObligationFactKind.Unknown, SingleFact("switch:primary").Kind);
    }

    // ---- F2：OutcomeAssuranceView 携带 EntityFacts -------------------------

    [Fact]
    public void F2_OutcomeAssuranceView_CarriesEntityFacts()
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            associationStrategy: null, new StatefulObservationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        kernel.Process(UIWorldDoubles.Observation("switch:primary@on", UIWorldDoubles.T0));

        var view = world.DeriveOutcomeAssuranceView(
            Array.Empty<string>(),
            new[] { (ObligationId, SwitchScope, "on") });

        var fact = Assert.Single(view.EntityFacts!);
        Assert.Equal(ObligationId, fact.ObligationId);
        Assert.Equal(EntityObligationFactKind.Satisfied, fact.Kind);

        // 零 entity obligations 输入 → 无 fact（源兼容：尾部可选字段）
        var plain = world.DeriveOutcomeAssuranceView(Array.Empty<string>());
        Assert.Null(plain.EntityFacts);
    }

    // ---- F3：JudgeOutcome 实判（D2） ---------------------------------------

    private static readonly ProofObligationState EntityObligations = new(new[]
    {
        new RunObligation(
            ObligationId, RunObligationKind.Objective,
            Subject: "switch", RequiredValue: "on", Mandatory: true,
            EntityScope: SwitchScope),
    });

    private static OutcomeAssuranceView ViewWithFacts(params EntityObligationFact[] facts) => new(
        "rev-1", ConflictingClaimCount: 0,
        Claims: new Dictionary<string, ScopedClaim>(),
        Conflicts: Array.Empty<Conflict>(),
        BasisEvidenceIds: new HashSet<string>(),
        EntityFacts: facts);

    [Fact]
    public void F3_JudgeOutcome_SatisfiedFact_FulfillsObligation()
    {
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var view = new ExecutionContractView(
            "v1", "turn-switch-on",
            new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
            new[] { "switch-on" });

        var proof = assurance.JudgeOutcome(
            view, EntityObligations,
            ViewWithFacts(new EntityObligationFact(ObligationId, EntityObligationFactKind.Satisfied)),
            new Dictionary<string, EvidenceRecord>());

        Assert.NotNull(proof);
        Assert.Equal(TerminalClassification.Completion, proof!.Classification);
        var status = Assert.Single(proof.Obligations);
        Assert.Equal(ObligationId, status.ObligationId);
        Assert.True(status.Satisfied);
    }

    [Fact]
    public void F3_JudgeOutcome_UnknownFact_NotFulfilled_NoFabricatedClassification()
    {
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var view = new ExecutionContractView(
            "v1", "turn-switch-on",
            new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
            new[] { "switch-on" });

        var proof = assurance.JudgeOutcome(
            view, EntityObligations,
            ViewWithFacts(new EntityObligationFact(ObligationId, EntityObligationFactKind.Unknown)),
            new Dictionary<string, EvidenceRecord>());

        // 证据不足：不猜测分类（不伪装失败、也不伪装满足）→ 无 proof
        Assert.Null(proof);
        Assert.Empty(assurance.OutcomeProofLog);
    }

    // ---- F4：E2E 正向 off→on → Completion ----------------------------------

    [Fact]
    public void F4_E2E_EntityScopedPipeline_SwitchOffToOn_TerminalCompletion()
    {
        var (kernel, world, cid) = NewSwitchKernel();

        // ESO-001 管线：accepted entity-scoped contract → standing demand
        var admission = kernel.AdmitContract(SwitchContract());
        Assert.True(admission.Accepted);
        var demand = Assert.Single(world.ContinuityDemands);
        Assert.Equal($"demand-obl-{ObligationId}", demand.DemandId);

        // 观察帧：switch State="off"（observation strategy double 产 State）
        kernel.Process(UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0));

        // DescriptorTargetPolicy（TargetSpec DesiredState="on"，CDS 语义）：
        // off ≠ on → Unsatisfied → 签发 act
        var intent = kernel.SelectIntent(kernel.DeriveSlice(cid));
        Assert.Equal(ControlIntentKind.Act, intent.Kind);
        Assert.Equal("toggle", intent.EffectClass);

        var grounded = kernel.ActViaCurrentGrounding(intent, SwitchScope);
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, grounded.View.Result);
        Assert.NotNull(grounded.Act!.Receipt);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, grounded.Act.Receipt!.Outcome);

        // post-action 帧：State="on" → fact Satisfied → EvaluateTerminal → Completion
        kernel.Process(PostActionObservation("switch:primary@on", UIWorldDoubles.T1));

        var terminal = kernel.EvaluateTerminal();
        Assert.NotNull(terminal.Proof);
        Assert.Equal(TerminalClassification.Completion, terminal.Proof!.Classification);
        var status = Assert.Single(terminal.Proof.Obligations);
        Assert.Equal(ObligationId, status.ObligationId);
        Assert.True(status.Satisfied);
        // terminal 已被接受并投影 Runtime Outcome（exactly once）
        Assert.True(terminal.Transition.Accepted);
        Assert.NotNull(terminal.Outcome);
        Assert.Equal(TerminalClassification.Completion, terminal.Outcome!.Classification);
    }

    // ---- F5：E2E 负向 state 仍 off → 无 completion proof --------------------

    [Fact]
    public void F5_E2E_StateUnchangedOff_NoCompletionProof_StaysNonTerminal()
    {
        var (kernel, world, cid) = NewSwitchKernel();
        kernel.AdmitContract(SwitchContract());
        kernel.Process(UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0));
        var intent = kernel.SelectIntent(kernel.DeriveSlice(cid));
        var grounded = kernel.ActViaCurrentGrounding(intent, SwitchScope);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, grounded.Act!.Receipt!.Outcome);

        // post-action 帧 State 仍 "off" → fact Unsatisfied → 证据不足，不猜测
        kernel.Process(PostActionObservation("switch:primary@off", UIWorldDoubles.T1));

        var terminal = kernel.EvaluateTerminal();
        Assert.Null(terminal.Proof);
        Assert.False(terminal.Transition.Accepted);
        Assert.Equal("evidence-insufficient", terminal.Transition.Reason);
        Assert.Null(terminal.Outcome);
        // 保持 non-terminal：再次评估仍证据不足（无伪装分类）
        var again = kernel.EvaluateTerminal();
        Assert.Null(again.Proof);
        Assert.False(again.Transition.Accepted);
    }
}
