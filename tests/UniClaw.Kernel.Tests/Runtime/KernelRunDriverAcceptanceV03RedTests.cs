using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

/// <summary>
/// RUN-004 · Gate 1 — RED-first acceptance tests（评审 #3 缺口可执行化）。
/// 只断言评审 #3 已确认的实现缺口；GREEN 后保留为正式 regression tests。
/// 目标缺口：
///   RED1/RED2 — F5 预算通道（Ac 2 / D8）：合同预算声明被 admission/view 丢弃、
///               同 version 异预算被误判幂等；
///   RED3/RED4 — M1/T6（Ac 9）：Defer 耗尽相位与 defer-exhausted 终局不可达；
///   RED5-A/B — PhaseForCurrent 分派（Ac 1 vs Ac 3）：StepRejected 相位不可达。
/// </summary>
public sealed class KernelRunDriverAcceptanceV03RedTests
{
    private sealed class AlwaysFresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "test:sufficient");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test:ok", UIWorldDoubles.T0);
    }

    private static ExecutionContract Contract(
        int? consultations = null, int? totalSteps = null, string version = "v1") => new(
        version, "switch-on", new HashSet<string> { "live.frame" },
        new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "objective" },
        new[] { new RunObligation("wifi", RunObligationKind.MaterialEffect, "switch.wifi", "on", true) },
        consultations, totalSteps);

    private static RunDriverInput.Observation SeedObservation() => new(new[]
    {
        new ObservationProposal(
            new ObservationClaim("live.frame", "seed"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("test", UIWorldDoubles.T0, "scope:live.frame", new[] { "test:seed" })),
    });

    /// <summary>镜像 KernelRunDriverTests.Compose —— 同一装配形状，零新 harness。</summary>
    private static (UniKernel Kernel, KernelRunDriver Driver) Compose(
        Func<ObservationDirective, RunDriverInput?>? nextInput = null,
        Func<AgentDecisionContext, AgentDecision?>? consult = null,
        WorldModel? world = null)
    {
        var plan = new AgentPlanPolicy();
        var inputs = new RunDriverInputs
        {
            NextInput = nextInput ?? (_ => null),
            ConsultAgent = consult ?? (_ => null),
        };
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            world ?? new WorldModel(new HashSet<string>()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        return (kernel, new KernelRunDriver(kernel, plan, inputs));
    }

    // ---- RED1：Ac 2 / F5 —— 合同预算声明必须保存进 admitted View ----

    [Fact]
    public void Acceptance2_ContractBudget_IsPreservedIntoRunView()
    {
        var (kernel, _) = Compose();

        var admission = kernel.AdmitContract(Contract(consultations: 64, totalSteps: 128));
        Assert.True(admission.Accepted);

        // 合同声明 64/128；当前实现 View 恒为尾部默认 16/256（RunModel.AdmitContract 不读预算字段）
        Assert.Equal(64, kernel.RunView!.MaxConsultations);
        Assert.Equal(128, kernel.RunView!.MaxTotalSteps);
    }

    // ---- RED2：Ac 2 / D8 —— 同 version 异预算必须 fail-closed，而非幂等复用 ----

    [Fact]
    public void Acceptance2_SameVersionDifferentBudget_IsRejectedAsContractSignatureConflict()
    {
        var (kernel, _) = Compose();

        var first = kernel.AdmitContract(Contract(consultations: 16, totalSteps: 256, version: "v1"));
        var second = kernel.AdmitContract(Contract(consultations: 64, totalSteps: 256, version: "v1"));

        Assert.True(first.Accepted);
        // 当前实现：second 按 View.Version 相等走幂等复用（Accepted=true）——D8 fail-closed 不可达
        Assert.False(second.Accepted);
        // GREEN 收敛注记：期望 RejectionReason 收敛为 "contract-signature-conflict"
        //（当前公开 API 不含该 code，不做 exact-code 断言，先证明「非幂等成功」）。
    }

    // ---- RED3：Ac 9 / M1-T6 —— Defer 耗尽后必须出现 exhaustion 语义（非普通相位）----

    [Fact]
    public void Acceptance9_DeferRounds_ReachExhaustionPhase()
    {
        var phases = new List<AgentDecisionPhase>();
        var (kernel, driver) = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx =>
            {
                phases.Add(ctx.Phase);
                return new AgentDecision.Defer(new ObserveSpec(Subject: null, MaxRounds: 2));
            });
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        // 期望：MaxRounds=2 耗尽后 Agent 收到 exhaustion 语义（非普通相位）；当前每次咨询
        // 均为普通相位（PhaseForCurrent 恒不产 DeferRoundsExhausted），且第二轮 Defer 被
        // V5 提前拒绝——exhaustion 相位从不出现。
        var ordinary = new[]
        {
            AgentDecisionPhase.InitialPlanning,
            AgentDecisionPhase.StepVerified,
            AgentDecisionPhase.StepRejected,
            AgentDecisionPhase.VerificationFailed,
        };
        Assert.True(
            phases.Any(p => !ordinary.Contains(p)),
            $"exhaustion phase never observed; agent saw only: [{string.Join(", ", phases)}]; status={result.Status}");
    }

    // ---- RED4：Ac 9 —— 耗尽后的终问仍 Defer ⇒ defer-exhausted 确定性终局 ----

    [Fact]
    public void Acceptance9_DeferAfterExhaustion_Terminates()
    {
        var (kernel, driver) = Compose(
            nextInput: _ => SeedObservation(),
            consult: _ => new AgentDecision.Defer(new ObserveSpec(Subject: null, MaxRounds: 2)));
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        // 期望：耗尽后终问仍 Defer → 确定性终局 TerminalNotProven "defer-exhausted"；
        // 当前实现无 MaxRounds 计数/无耗尽相位 → 第二轮 Defer 被 V5 以“defer-unbounded:nested”拒绝。
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Contains("defer-exhausted", result.Reason ?? "");
    }

    // ---- RED5-A：Ac 1 —— E2（接地/门拒绝）必须暴露 StepRejected 相位 ----

    [Fact]
    public void Acceptance1_GroundingOrGateReject_ReportsStepRejected()
    {
        var phases = new List<AgentDecisionPhase>();
        var (kernel, driver) = Compose(
            nextInput: _ => SeedObservation(),
            world: new WorldModel(new HashSet<string> { "live.frame" }),
            consult: ctx =>
            {
                phases.Add(ctx.Phase);
                return phases.Count == 1
                    ? new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("switch", null, "tap", "on") },
                        "single-step"))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "after-e2"));
            });
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        Assert.True(driver.Activate().Accepted);

        driver.Drive();

        // E2（no-single-root-container 回边）后的重咨询应携带 StepRejected；
        // 当前 PhaseForCurrent 将一切失败标为 VerificationFailed（RunState 恒非空）——StepRejected 不可达。
        Assert.True(phases.Count >= 2, $"expected re-consult after E2; saw {phases.Count} consult(s)");
        Assert.Equal(AgentDecisionPhase.StepRejected, phases[1]);
    }

    // ---- RED5-B：Ac 3 —— E3（验证失败）必须暴露 VerificationFailed 相位（应已 GREEN）----

    [Fact]
    public void Acceptance3_VerificationFailure_ReportsVerificationFailed()
    {
        // 同 KernelRunDriverTests 可恢复单步模式：seed 容器 + occurrence 派生 + post 观察维持 off
        //（desired "on" 未达成 → StepVerify 失败 → E3 回边）。
        var cid = ProbeContainerId("switch:primary@off");
        var plan = new AgentPlanPolicy();
        var phases = new List<AgentDecisionPhase>();
        var inputs = new RunDriverInputs
        {
            NextInput = expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : new RunDriverInput.Observation(new[] { PostActionObservation("switch:primary@off", UIWorldDoubles.T1) }),
            ConsultAgent = ctx =>
            {
                phases.Add(ctx.Phase);
                return phases.Count == 1
                    ? new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("switch", "primary", "tap", "on") },
                        "flip-the-switch"))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "after-e3"));
            },
        };
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(
                new HashSet<string> { UIWorldDoubles.Observed },
                new SeedContainerAssociationStrategy(),
                new StatefulObservationStrategy(cid),
                new RoleContinuityStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            new EffectBoundary(new OkDriver()));
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        Assert.True(driver.Activate().Accepted);

        driver.Drive();

        Assert.True(phases.Count >= 2, $"expected re-consult after E3; saw {phases.Count} consult(s)");
        Assert.Equal(AgentDecisionPhase.VerificationFailed, phases[1]);
    }

    // ---- 夹具（镜像 KernelRunDriverTests 私有 double，非第二 harness）----

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
}