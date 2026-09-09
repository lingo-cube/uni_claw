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
/// CDS-001 验收（S1–S7）—— desired-state satisfaction（HD-4 / ADR-0017 /
/// I-3 事前保证）。纯内存确定性。核心断言：已满足 → 零 Act intent / 零
/// dispatch（U2 / destructive correctness 闭合）。container 种子与 owner
/// 预知照抄 ControlReferencePolicyTests ProbeContainerId 模式。
/// </summary>
public sealed class DesiredStateSatisfactionTests
{
    private const string SeedValue = "ds-satisfaction-seed";

    // ---- scripted 替身 ---------------------------------------------------

    /// <summary>固定 occurrence 景观（含可选 state）的 observation strategy。</summary>
    private sealed class FixedObservationStrategy(string owner, params (string Role, string? State)[] occurrences)
        : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            occurrences.Select(o => new ProposedOccurrence(owner, o.Role, "wifi-switch", o.State)).ToArray();
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", UIWorldDoubles.T1);
    }

    /// <summary>SeedContainer 首条 evidence 探针（ControlReferencePolicyTests
    /// ProbeContainerId 同法）：确定性预知 minted root container id。</summary>
    private static string ProbeContainerId() =>
        "ctr-" + new EvidenceLedger()
            .Admit(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0))
            .Admission.EvidenceId![3..15];

    private static (UniKernel Kernel, ControlLoop Control, EffectBoundary Effects) NewKernel(
        IUiObservationStrategy observation, IControlPolicy policy)
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(), observation);
        var control = new ControlLoop(policy);
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), control,
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()), effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "set-the-toggle",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "set-switch" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "toggle-set" }));
        return (kernel, control, effects);
    }

    private static void Prime(UniKernel kernel) =>
        kernel.Process(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0));

    private static TargetSpec ToggleSpec(string? desiredState) =>
        new("toggle", "wifi-switch", "set-switch", desiredState);

    // ---- S1 载荷：state 全链可见（strategy → belief → Slice） -----------

    [Fact]
    public void S1_OccurrenceStateFlowsFromStrategyThroughBeliefToSlice()
    {
        var owner = ProbeContainerId();
        var (kernel, _, _) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "true")),
            new DescriptorTargetPolicy(new[] { ToggleSpec(null) }));
        Prime(kernel);

        var occurrence = Assert.Single(kernel.DeriveSlice(owner).Occurrences);
        Assert.Equal("toggle", occurrence.Role);
        Assert.Equal("true", occurrence.State); // Known(value)
    }

    [Fact]
    public void S1b_NullStateIsUnknownNotFalse()
    {
        var owner = ProbeContainerId();
        var (kernel, _, _) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", null)),
            new DescriptorTargetPolicy(new[] { ToggleSpec(null) }));
        Prime(kernel);

        var occurrence = Assert.Single(kernel.DeriveSlice(owner).Occurrences);
        Assert.Null(occurrence.State); // Unknown ≠ false（二态 epistemic，ADR-0017）
    }

    // ---- S2 已满足 → Observe / 零 dispatch / 零 binding ------------------

    [Fact]
    public void S2_SatisfiedSpecYieldsObserveNoDispatchNoBinding()
    {
        var owner = ProbeContainerId();
        var policy = new DescriptorTargetPolicy(new[] { ToggleSpec("true") });
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "true")), policy);
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));

        // 已满足：policy 完成 spec（visited）→ Observe；零 Act intent
        Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        Assert.Contains(("toggle", "wifi-switch"), policy.Visited);
        Assert.Empty(effects.BindingLog);   // 零 binding 认定
        Assert.Empty(effects.ReceiptLog);   // 零 dispatch
    }

    // ---- S3 未满足 → Act 全链不变 ---------------------------------------

    [Fact]
    public void S3_UnsatisfiedSpecActsThroughFullChain()
    {
        var owner = ProbeContainerId();
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "false")),
            new DescriptorTargetPolicy(new[] { ToggleSpec("true") }));
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));
        Assert.Equal(ControlIntentKind.Act, intent.Kind);

        var act = kernel.ActViaCurrentGrounding(intent,
            new TargetDescriptor("toggle", "wifi-switch"));
        Assert.True(act.Act!.Gate!.Allowed);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, act.Act.Receipt!.Outcome);
        Assert.Single(effects.ReceiptLog);
    }

    // ---- S4 Unknown → 跳过不 visited（fail-closed 不 dispatch） ----------

    [Fact]
    public void S4_UnknownStateSkipsSpecWithoutVisiting()
    {
        var owner = ProbeContainerId();
        var policy = new DescriptorTargetPolicy(new[] { ToggleSpec("true") });
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", null)), policy);
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));

        Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        Assert.Empty(policy.Visited);       // Unknown ≠ 完成——待新观察可判
        Assert.Empty(effects.ReceiptLog);   // fail-closed：零 dispatch
    }

    // ---- S5 端到端 I-3 / U2：SetSwitch(true) on already-true 零物理动作 --

    [Fact]
    public void S5_NonIdempotentSetOnAlreadySatisfiedTargetIsZeroPhysicalAction()
    {
        var owner = ProbeContainerId();
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "true")),
            new DescriptorTargetPolicy(new[] { ToggleSpec("true") }));
        Prime(kernel);

        // 多轮 control cycle：已满足 → 恒 Observe → 恒零物理动作
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));
            Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        }
        Assert.Empty(effects.ReceiptLog);
        Assert.Empty(effects.BindingLog);
    }

    // ---- S6 DesiredState=null（Click 型）不做检查 ------------------------

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public void S6_NullDesiredStateSkipsSatisfactionCheck(string? state)
    {
        var owner = ProbeContainerId();
        var (kernel, _, _) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", state)),
            new DescriptorTargetPolicy(new[] { ToggleSpec(null) }));
        Prime(kernel);

        // state 任意 → 照常 Act（Click 型 intent 无期望终态，不适用 satisfaction）
        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));
        Assert.Equal(ControlIntentKind.Act, intent.Kind);
    }
}
