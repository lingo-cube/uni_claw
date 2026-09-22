using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

/// <summary>
/// RFS-001 — Kernel internal run driver 最小 legal activation seam（P24 /
/// baseline §24.1 不变量 44）+ P25 机械入口校验 + Kernel 级 activation gate
/// （D20）+ 可恢复 Drive phase 状态机（D21/D22，不变量 43 屏障）。
/// </summary>
public sealed class KernelRunDriverTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 13, 9, 5, 0, TimeSpan.Zero);

    private sealed class Composition
    {
        public required UniKernel Kernel { get; init; }
        public required AgentPlanPolicy Plan { get; init; }
        public required RunDriverInputs Inputs { get; init; }
        public required EffectBoundary Effects { get; init; }
    }

    private static Composition Compose(
        Func<ObservationDirective, RunDriverInput?>? nextInput = null,
        Func<AgentDecisionContext, AgentDecision?>? consult = null)
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
            new WorldModel(new HashSet<string>()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        return new Composition { Kernel = kernel, Plan = plan, Inputs = inputs, Effects = effects };
    }

    private sealed class AlwaysFresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "test:sufficient");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test:ok", T0);
    }

    /// <summary>测试域 Rogue decision（closed union 之外的派生类型）：unknown-decision-kind 防御面。</summary>
    private sealed record RogueDecision : AgentDecision;

    private static ExecutionContract Contract() => new(
        "v1", "switch-on", new HashSet<string> { "live.frame" },
        new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "objective" },
        new[] { new RunObligation("wifi", RunObligationKind.MaterialEffect, "switch.wifi", "on", true) });

    private static RunDriverInput.Observation SeedObservation() => new(new[]
    {
        new ObservationProposal(
            new ObservationClaim("live.frame", "seed"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("test", T0, "scope:live.frame", new[] { "test:seed" })),
    });

    // ---- activation gate（D20：Kernel 级 latch，多 driver 共享）---------------

    [Fact]
    public void Activate_WithoutAcceptedContract_FailsClosed()
    {
        var c = Compose();
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);

        var activation = driver.Activate();

        Assert.False(activation.Accepted);
        Assert.Equal("no-accepted-contract", activation.Reason);
        Assert.Null(activation.RunId);
    }

    [Fact]
    public void Activate_AfterAdmission_IsLegalAndIdempotent_SameRunId()
    {
        var c = Compose();
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);

        var first = driver.Activate();
        var second = driver.Activate();

        Assert.True(first.Accepted);
        Assert.False(first.AlreadyActivated);
        Assert.Equal(c.Kernel.RunId, first.RunId);
        Assert.True(second.Accepted);
        Assert.True(second.AlreadyActivated);
        Assert.Equal(first.RunId, second.RunId);
    }

    [Fact]
    public void TwoDrivers_OverOneKernel_ShareActivationGate()
    {
        var c = Compose(nextInput: _ => null);
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver1 = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        var driver2 = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);

        var first = driver1.Activate();
        var second = driver2.Activate();

        // 同一 UniKernel 上的第二个 driver：gate 已被置位 → AlreadyActivated
        Assert.True(second.Accepted);
        Assert.True(second.AlreadyActivated);
        Assert.Equal(first.RunId, second.RunId);

        // 重复 activation 可返回同一 Run 关联，但不得把第二个
        // driver 变成该 Run 的 execution owner。
        var result = driver2.Drive();
        Assert.Equal(RunDriveStatus.NotActivated, result.Status);
        Assert.Equal("activation-owned-by-another-driver", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    // ---- Drive fail-closed 前置 ----------------------------------------------

    [Fact]
    public void Drive_BeforeActivation_FailsClosed_ZeroEffects()
    {
        var c = Compose();
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.NotActivated, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    // ---- P25 机械入口校验 ------------------------------------------------------

    [Fact]
    public void Drive_AgentNoResponse_FailsClosed_ZeroEffects_NonTerminal()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: _ => null); // P25 no-response
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("no-response", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.False(c.Kernel.IsRunTerminal);
    }

    [Fact]
    public void Drive_CorrelationMismatch_FailsClosed_ZeroEffects()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: _ => new AgentDecision.Act(new AgentActionProposal(
                "decision-forged",
                new[] { new AgentActionStep("switch", null, "tap", "on") },
                "stale-id"))); // correlation 失配
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("correlation-mismatch", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    [Fact]
    public void Drive_EffectClassOutsideContract_FailsClosed_ZeroEffects()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                new[] { new AgentActionStep("switch", null, "swipe", null) },
                "not-allowed-class")));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("effect-class-not-allowed", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    [Fact]
    public void Drive_EmptyStepsActProposal_FailsClosed()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId, Array.Empty<AgentActionStep>(), "no-steps")));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("empty-steps", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    [Fact]
    public void Drive_ExcessiveStepsActProposal_FailsClosed()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                Enumerable.Repeat(new AgentActionStep("switch", null, "tap", "on"), 17).ToArray(),
                "too-many-steps")));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("too-many-steps", result.Reason);
        Assert.Empty(c.Effects.ReceiptLog);
    }

    [Fact]
    public void Drive_StepMissingTargetRole_FailsClosed()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                new[] { new AgentActionStep(" ", null, "tap", "on") },
                "missing-role")));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("missing-target-role", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    [Fact]
    public void Drive_StepMissingEffectClass_FailsClosed()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                new[] { new AgentActionStep("switch", null, "", "on") },
                "missing-class")));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("missing-effect-class", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    [Fact]
    public void Drive_UnknownDecisionKind_FailsClosed_DefensiveDefault()
    {
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: _ => new RogueDecision()); // closed union 之外的派生类型
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("unknown-decision-kind", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    [Fact]
    public void Drive_DecisionId_IsRunCorrelatedAndDeterministic()
    {
        AgentDecisionContext? captured = null;
        var c = Compose(
            nextInput: _ => SeedObservation(),
            consult: ctx =>
            {
                captured = ctx;
                // 越权 effect class：在 terminal 编排前 fail closed，便于断言 context
                return new AgentDecision.Act(new AgentActionProposal(
                    ctx.DecisionId,
                    new[] { new AgentActionStep("switch", null, "swipe", null) },
                    "capture-only"));
            });
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        driver.Drive();

        Assert.NotNull(captured);
        var expected = $"decision-{c.Kernel.RunId[^12..]}-1";
        Assert.Equal(expected, captured!.DecisionId);
        Assert.Equal(c.Kernel.RunId, captured.RunId);
    }

    // ---- cancel 路径 ------------------------------------------------------------

    [Fact]
    public void Drive_CancelWithoutDeclaredSafeStopObligation_FailsClosed_NonTerminal()
    {
        var c = Compose(nextInput: _ => new RunDriverInput.Cancel("host-cancel", T1));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted); // 无 SafeStop obligation
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, result.Status);
        Assert.False(c.Kernel.IsRunTerminal);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
    }

    // ---- 可恢复 Drive：单步 waiting + resume → Completed（不变量 43 屏障）------

    /// <summary>stateful observation double（同 EntityObligationFulfillmentTests 约定）：
    /// claim value 片段 "role:descriptor@state"，'+' 连接多 occurrence。</summary>
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

    /// <summary>SeedContainer 首条 evidence 探针：确定性预知 minted root container id。</summary>
    private static string ProbeContainerId(string seedValue)
    {
        var (admission, _) = new EvidenceLedger().Admit(
            UIWorldDoubles.Observation(seedValue, UIWorldDoubles.T0));
        return "ctr-" + admission.EvidenceId![3..15];
    }

    /// <summary>RUN-004 multi-turn compat：首次回放脚本，后续 NoAction。</summary>
    private static Func<AgentDecisionContext, AgentDecision?> MultiTurnCompat(
        Func<AgentDecisionContext, AgentDecision> script)
    {
        var calls = 0;
        return ctx =>
        {
            calls++;
            return calls == 1
                ? script(ctx)
                : new AgentDecision.NoAction(new AgentNoActionProposal(
                    ctx.DecisionId, "script-exhausted"));
        };
    }

    private static ObservationProposal PostActionObservation(string value, DateTimeOffset t) => new(
        new ObservationClaim(UIWorldDoubles.Observed, value),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance("provider.scripts", t, "scope:ui.container", new[] { "raw://capture", "encode:v1" }));

    [Fact]
    public void Drive_SingleStep_WaitsForPostActionEvidence_ThenResume_Completes()
    {
        var cid = ProbeContainerId("switch:primary@off");
        var plan = new AgentPlanPolicy();
        var postActionArrived = false;
        var inputs = new RunDriverInputs
        {
            NextInput = expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : postActionArrived
                    ? new RunDriverInput.Observation(new[] { PostActionObservation("switch:primary@on", UIWorldDoubles.T1) })
                    : null,
            ConsultAgent = MultiTurnCompat(ctx =>
                new AgentDecision.Act(new AgentActionProposal(
                    ctx.DecisionId,
                    new[] { new AgentActionStep("switch", "primary", "toggle", "on") },
                    "flip-the-switch"))),
        };
        var effects = new EffectBoundary(new OkDriver());
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
            effects);
        Assert.True(kernel.AdmitContract(new ExecutionContract(
            Version: "v1",
            Objective: "turn-switch-on",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "toggle" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "switch-on" },
            Obligations: new[]
            {
                new RunObligation(
                    "obl-switch-on", RunObligationKind.Objective,
                    Subject: "switch", RequiredValue: "on", Mandatory: true,
                    EntityScope: new TargetDescriptor("switch", "primary")),
            })).Accepted);

        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        // 第一次 Drive：初始观察 → decision → dispatch → 等待 post-action 证据
        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Equal("post-action-evidence", waiting.Reason);
        Assert.Equal(1, waiting.DeliveredEffects); // 已 dispatch 恰一步
        Assert.Equal(1, effects.ReceiptLog.Count);

        // 证据未到时再次 Drive：仍等待，不发生新 dispatch（不变量 43 屏障）
        var stillWaiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, stillWaiting.Status);
        Assert.Equal(1, effects.ReceiptLog.Count);

        // post-action 证据到达 → resume：消费证据 → 无剩余 step → terminal Completed
        postActionArrived = true;
        var completed = driver.Drive();
        Assert.Equal(RunDriveStatus.Completed, completed.Status);
        Assert.NotNull(completed.Outcome);
        Assert.Equal(1, completed.DeliveredEffects);
        Assert.True(kernel.IsRunTerminal); // RUN-004：Completed = 真终局
    }

    /// <summary>
    /// CORE-013 Slice 3：执行源提交失败 → Dispatch fail-closed 的
    /// execution-commit-failed reason 经 UniKernel.Act → KernelRunDriver
    /// 既有 GateRejected 路径透传（零 driver 调用、零 Receipt；
    /// UniKernel / KernelRunDriver 零改动——reason 词汇为追加，非新状态）。
    /// </summary>
    [Fact]
    public void Drive_ExecutionSourceCommitFailure_FailsClosed_ReasonPropagates()
    {
        var cid = ProbeContainerId("switch:primary@off");
        var plan = new AgentPlanPolicy();
        var inputs = new RunDriverInputs
        {
            NextInput = expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : null,
            ConsultAgent = MultiTurnCompat(ctx =>
                new AgentDecision.Act(new AgentActionProposal(
                    ctx.DecisionId,
                    new[] { new AgentActionStep("switch", "primary", "toggle", "on") },
                    "flip-the-switch"))),
        };
        var failingSource = new FailingCommitSource();
        var effects = new EffectBoundary(new OkDriver(), failingSource);
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
            effects);
        Assert.True(kernel.AdmitContract(new ExecutionContract(
            Version: "v1",
            Objective: "turn-switch-on",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "toggle" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "switch-on" },
            Obligations: new[]
            {
                new RunObligation(
                    "obl-switch-on", RunObligationKind.Objective,
                    Subject: "switch", RequiredValue: "on", Mandatory: true,
                    EntityScope: new TargetDescriptor("switch", "primary")),
            })).Accepted);

        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        // RUN-004 多轮化：门拒绝 → 再咨询 → NoAction → 终局如实未证
        //（原 GateRejected 即终——现回边给 agent 重议机会；MultiTurnCompat
        //  第二次答 NoAction → TerminalNotProven = 目标未达的诚实报告）
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, effects.ReceiptLog.Count);
        Assert.Equal(1, failingSource.CommitCalls); // 恰一次提交尝试
        Assert.False(kernel.IsRunTerminal);
    }

    /// <summary>CORE-013 Slice 3 double：提交恒 Failure 的执行源。</summary>
    private sealed class FailingCommitSource : IReliableExecutionSource
    {
        public int CommitCalls { get; private set; }

        public ExecutionCommit CommitPrepare(ExecutionRegistration registration)
        {
            CommitCalls++;
            return new ExecutionCommit(ExecutionCommitOutcome.Failure, null);
        }

        public IReadOnlyList<PendingExecution> DiscoverPending() => Array.Empty<PendingExecution>();
        public ExecutionAttemptView? GetAttempt(string attemptId) => null;
        public void AppendSubmission(string attemptId, string note) =>
            throw new NotSupportedException("commit-failed source 无追加面");
        public void AppendReceipt(string attemptId, EffectReceipt? receipt) =>
            throw new NotSupportedException("commit-failed source 无追加面");
        public void AppendLateFeedback(string? attemptId, ExecutionFeedback feedback) =>
            throw new NotSupportedException("commit-failed source 无追加面");
        public string LinkRetry(string attemptId) =>
            throw new NotSupportedException("commit-failed source 无关联面");
        public ExecutionLink LinkCompensation(string attemptId) =>
            throw new NotSupportedException("commit-failed source 无关联面");
    }

    /// <summary>
    /// PER-009 S6b：driver 聚焦复查环路（A 方案，台账 #20）——
    /// Observe ∧ TargetSubject → 有界（≤3）Focused 拉取 → 耗尽诚实失败。
    /// 悬案由 policy.ConflictedSubjects 注入（DeriveControlBeliefView 推导
    /// 属组合接线，另行覆盖）；聚焦指令经驱动面传导为本测试主断言面。
    /// </summary>
/// <summary>
    /// PER-009 S6b：全栈接线——悬案由 driver 从 world 真实派生
    /// （UniKernel.CurrentConflictedSubjects → policy，单一真相源；
    /// 手工注入式测试随接线落地删除：policy 规则由
    /// AgentPlanPolicyConflictTests 覆盖，driver 层只认 world 派生）。
    /// 初始观察含同 subject 冲突对（A3 语义：同 producer 异值 → 显式 Conflict）。
    /// </summary>
    [Fact]
    public void FocusedLoop_DrivenByRealWorldConflict_NoManualInjection()
    {
        var cid = ProbeContainerId("switch:primary@off");
        var conflictSubject = SharedSubjects.State("switch:primary");
        var plan = new AgentPlanPolicy();
        var focusedDirectives = new List<ObservationDirective>();
        RunDriverInput Observation() => new RunDriverInput.Observation(new ObservationProposal[]
        {
            // 容器约定（seed association）
            UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
            // 冲突对：同 subject 异值（处理序即建立→挑战 → 显式 Conflict）
            UIWorldDoubles.SubjectObservation(conflictSubject, "on", UIWorldDoubles.T0),
            UIWorldDoubles.SubjectObservation(conflictSubject, "off", UIWorldDoubles.T0),
        });
        var inputs = new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Depth == ObservationDepth.Focused)
                {
                    focusedDirectives.Add(directive);
                    return Observation();
                }
                return directive.Context == ObservationContext.External
                    ? Observation()
                    : null;
            },
            ConsultAgent = MultiTurnCompat(ctx =>
                new AgentDecision.Act(new AgentActionProposal(
                    ctx.DecisionId,
                    new[] { new AgentActionStep("switch", "primary", "toggle", "on") },
                    "flip-the-switch"))),
        };
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(
                new HashSet<string> { UIWorldDoubles.Observed, conflictSubject },
                new SeedContainerAssociationStrategy(),
                new StatefulObservationStrategy(cid),
                new RoleContinuityStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        Assert.True(kernel.AdmitContract(new ExecutionContract(
            Version: "v1",
            Objective: "turn-switch-on",
            Scope: new HashSet<string> { UIWorldDoubles.Observed, conflictSubject },
            AllowedEffects: new HashSet<string> { "toggle" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "switch-on" },
            Obligations: new[]
            {
                new RunObligation(
                    "obl-switch-on", RunObligationKind.Objective,
                    Subject: "switch", RequiredValue: "on", Mandatory: true,
                    EntityScope: new TargetDescriptor("switch", "primary")),
            })).Accepted);

        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);
        // 注意：无手工 ConflictedSubjects——接线自 world 派生

        var exhausted = driver.Drive();

        // 全栈链：world Conflict → driver 推送 → policy 聚焦 → 驱动面 Focused
        Assert.True(focusedDirectives.Count >= 1);
        Assert.All(focusedDirectives, d =>
        {
            Assert.Equal(ObservationDepth.Focused, d.Depth);
            Assert.Equal(new[] { conflictSubject }, d.Subjects!);
        });
        Assert.Equal(RunDriveStatus.GroundingFailed, exhausted.Status);
        Assert.Equal("focused-reobserve-exhausted", exhausted.Reason);
        Assert.Equal(0, effects.ReceiptLog.Count);
    }
}
