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
/// RUN-005 Slice A — V6 fail-closed 的 driver 边界面：Policy decision 经
/// ConsultAgent seam 进入 NeedDecision 后被机械校验拒绝（零新 Effect、
/// 非终局、映射回既有 AgentDecisionFailed 语义）。边界测试，不含
/// Policy 执行路径（Slice B）。
/// </summary>
public sealed class KernelRunDriverPolicyValidationTests
{
    private sealed class Composition
    {
        public required UniKernel Kernel { get; init; }
        public required AgentPlanPolicy Plan { get; init; }
        public required RunDriverInputs Inputs { get; init; }
        public required EffectBoundary Effects { get; init; }
    }

    private sealed class AlwaysFresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "test:sufficient");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test:ok", new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero));
    }

    /// <summary>测试域 rogue predicate：closed AST 之外的派生节点。</summary>
    private sealed record RoguePredicate : PolicyPredicate;

    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    /// <summary>无 container 的世界（relevance 含观察 subject，但无 association
    /// strategy → belief 存在、Containers 为空 → 无 active execution lease）。</summary>
    private static Composition ComposeFlat(Func<AgentDecisionContext, AgentDecision?> consult)
    {
        var plan = new AgentPlanPolicy();
        var inputs = new RunDriverInputs
        {
            NextInput = _ => new RunDriverInput.Observation(new[]
            {
                new ObservationProposal(
                    new ObservationClaim("temp", "24"),
                    IngressKind.Observation,
                    ObservationContext.External,
                    new Provenance("test", T0, "scope:temp", new[] { "test:seed" })),
            }),
            ConsultAgent = consult,
        };
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(new HashSet<string> { "temp" }),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        return new Composition { Kernel = kernel, Plan = plan, Inputs = inputs, Effects = effects };
    }

    /// <summary>单 root container 世界（seed association）——active lease 可绑定。</summary>
    private static Composition ComposeSeeded(Func<AgentDecisionContext, AgentDecision?> consult)
    {
        var plan = new AgentPlanPolicy();
        var inputs = new RunDriverInputs
        {
            NextInput = _ => new RunDriverInput.Observation(new[]
            {
                new ObservationProposal(
                    new ObservationClaim("ui.container.observed", "switch:primary@off"),
                    IngressKind.Observation,
                    ObservationContext.External,
                    new Provenance("test", T0, "scope:ui.container", new[] { "test:seed" })),
            }),
            ConsultAgent = consult,
        };
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(
                new HashSet<string> { "ui.container.observed" },
                new SeedContainerAssociationStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        return new Composition { Kernel = kernel, Plan = plan, Inputs = inputs, Effects = effects };
    }

    private static ExecutionContract Contract(int maxTotalSteps = 256) => new(
        "v1", "cool-down", new HashSet<string> { "temp" },
        new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "temp-20" },
        MaxTotalSteps: maxTotalSteps);

    private static AgentDecision.Policy Policy(
        string decisionId,
        string policyId = "pol-1",
        IReadOnlyList<PolicyPredicate>? match = null,
        PolicyActionTemplate? template = null,
        IReadOnlyList<PolicyPredicate>? termination = null,
        IReadOnlyList<PolicyGuard>? guards = null,
        int maxApplications = 4) => new(
        decisionId,
        new PolicyProposal(
            policyId,
            match ?? new[] { new PolicyPredicate.ClaimInSet("temp", new[] { "24", "23", "22", "21" }) },
            template ?? new PolicyActionTemplate("minus-button", "temperature", "tap", null),
            termination ?? new[] { new PolicyPredicate.ClaimEquals("temp", "20") },
            guards ?? new[] { new PolicyGuard.ObservationUnchanged("temp", 2) },
            maxApplications,
        "step-down-until-20"));

    private static void AssertFailClosed(Composition c, RunDriveResult result, string reason)
    {
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.False(c.Kernel.IsRunTerminal);
    }

    // ---- V6a closed vocabulary ---------------------------------------------------

    [Fact]
    public void Drive_PolicyUnknownAstNode_FailsClosed()
    {
        var c = ComposeFlat(ctx => Policy(ctx.DecisionId,
            match: new PolicyPredicate[] { new RoguePredicate() }));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:unknown-node");
    }

    // ---- V6b 非空合取 --------------------------------------------------------------

    [Fact]
    public void Drive_PolicyEmptyTermination_FailsClosed()
    {
        var c = ComposeFlat(ctx => Policy(ctx.DecisionId, termination: Array.Empty<PolicyPredicate>()));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:empty-termination");
    }

    [Fact]
    public void Drive_PolicyEmptyMatch_FailsClosed()
    {
        var c = ComposeFlat(ctx => Policy(ctx.DecisionId, match: Array.Empty<PolicyPredicate>()));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:empty-match");
    }

    // ---- V6c bounds ----------------------------------------------------------------

    [Fact]
    public void Drive_PolicyMaxApplicationsZero_FailsClosed()
    {
        var c = ComposeFlat(ctx => Policy(ctx.DecisionId, maxApplications: 0));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:max-applications-invalid");
    }

    [Fact]
    public void Drive_PolicyMaxApplicationsExceedsStepsRemaining_FailsClosed()
    {
        var c = ComposeSeeded(ctx => Policy(ctx.DecisionId, maxApplications: 5));
        Assert.True(c.Kernel.AdmitContract(Contract(maxTotalSteps: 4)).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        // MaxApplications(5) > StepsRemaining(4)——不得扩大合同步数预算
        AssertFailClosed(c, driver.Drive(), "policy:max-applications-exceeds-steps");
    }

    // ---- V6d 模板目标与效应类 ---------------------------------------------------------

    [Fact]
    public void Drive_PolicyForbiddenEffectClass_FailsClosed()
    {
        var c = ComposeSeeded(ctx => Policy(ctx.DecisionId,
            template: new PolicyActionTemplate("minus-button", null, "swipe", null)));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:effect-class-not-allowed");
    }

    [Fact]
    public void Drive_PolicyBlankTargetRole_FailsClosed()
    {
        var c = ComposeSeeded(ctx => Policy(ctx.DecisionId,
            template: new PolicyActionTemplate("  ", null, "tap", null)));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:missing-target-role");
    }

    // ---- V6g lease 绑定 -------------------------------------------------------------

    [Fact]
    public void Drive_PolicyWithoutActiveLease_FailsClosed()
    {
        // 无 association strategy：belief 存在但 Containers 为空 → 无 active lease
        var c = ComposeFlat(ctx => Policy(ctx.DecisionId));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "policy:no-active-lease");
    }

    // ---- V6e DecisionId 回带（V1 三态同律）--------------------------------------------

    [Fact]
    public void Drive_PolicyCorrelationMismatch_FailsClosed()
    {
        var c = ComposeSeeded(_ => Policy("decision-forged"));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        AssertFailClosed(c, driver.Drive(), "correlation-mismatch");
    }

    // ---- Slice A 边界：V6 通过后的诚实占位 ---------------------------------------------

    [Fact]
    public void Drive_ValidPolicy_AfterV6Pass_FailsClosedPendingSliceB()
    {
        var c = ComposeSeeded(ctx => Policy(ctx.DecisionId));
        Assert.True(c.Kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Inputs);
        Assert.True(driver.Activate().Accepted);

        // V6 全过（含 lease 绑定单 root container）→ Slice A 无展开运行时：
        // 诚实 fail closed（零新 Effect、非终局），不伪装执行。Slice B 以
        // adoption + PolicyExpand 循环替换本出口。
        AssertFailClosed(c, driver.Drive(), "policy-execution-not-implemented");

        // 幂等重 Drive：同一次咨询的重验不得误报 V6f duplicate
        AssertFailClosed(c, driver.Drive(), "policy-execution-not-implemented");
    }
}
