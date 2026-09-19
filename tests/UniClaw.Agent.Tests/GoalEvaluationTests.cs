using System.Reflection;
using UniClaw.Agent;
using UniClaw.Agent.Evaluation;
using UniClaw.Agent.Goal;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

using UniClaw.Kernel.Trace;

namespace UniClaw.Agent.Tests;

/// <summary>
/// GEV-004 验收 1..12 + 反例 A-J —— UniAgent 监督面确定性用例。
/// 纯内存 fake world（level: DETERMINISTIC）。判定格穷举与反镜像双例用
/// hand-crafted envelope（envelope 是 OUT-003 已冻结的 immutable 数据形状）；
/// GoalEval17 驱动真实 UniKernel 到 terminal，证明「第一个消费者」的
/// 端到端路径。测试验证行为，不验证实现细节。
/// </summary>
public sealed class GoalEvaluationTests
{
    // ---- helpers（hand-crafted envelope：OUT-003 冻结形状的直接构造）------

    private static ObligationStatus Status(string id, bool satisfied, bool mandatory = true) =>
        new(id, RunObligationKind.Objective, mandatory, satisfied, satisfied ? "ev-1" : null);

    private static RuntimeOutcome Envelope(
        TerminalClassification classification,
        string runId = "run-1",
        string proofId = "proof-1",
        params ObligationStatus[] obligations) =>
        new(runId, proofId, classification, obligations,
            new HashSet<string> { "ev-1" },
            new HashSet<string>(),
            new HashSet<string>(),
            UnresolvedUncertainty: 0,
            Reason: "scripted");

    private static PrimaryGoal Goal(
        IReadOnlyList<GoalCriterion> required,
        IReadOnlyList<GoalCriterion>? preferred = null,
        string goalId = "goal-1") =>
        new(goalId, "home screen active", required, preferred ?? Array.Empty<GoalCriterion>());

    // ---- 验收 8/10：判定格四档穷举 -----------------------------------------

    [Fact]
    public void GoalEval1_SatisfiedWhenRequiredAndPreferredAllMet()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", true), Status("eff", true) }));

        Assert.Equal(GoalSatisfaction.Satisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.Final, evaluation.Disposition);
        Assert.All(evaluation.CriterionResults, r => Assert.Equal(CriterionOutcome.Met, r.Outcome));
    }

    [Fact]
    public void GoalEval2_PartiallySatisfiedWhenPreferredUnmet()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", true), Status("eff", false) }));

        Assert.Equal(GoalSatisfaction.PartiallySatisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.NeedsFollowUp, evaluation.Disposition);
        Assert.Contains(evaluation.CriterionResults,
            r => r.Criterion is GoalCriterion.ObligationFulfilled { ObligationId: "eff" }
                && r.Outcome == CriterionOutcome.Unmet);
    }

    [Fact]
    public void GoalEval3_UnsatisfiedWhenRequiredUnmet()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        // required 未满足支配 preferred 满足（验收 8：required 支配）
        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", false), Status("eff", true) }));

        Assert.Equal(GoalSatisfaction.Unsatisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.Final, evaluation.Disposition);
    }

    [Fact]
    public void GoalEval4_UndeterminedWhenObligationIdAbsent()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj"),
                new GoalCriterion.ObligationFulfilled("ghost") }));

        // id 缺席 = Unverifiable（≠ false）；不得伪装不满足（反例 C）
        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", true) }));

        Assert.Equal(GoalSatisfaction.Undetermined, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.NeedsFollowUp, evaluation.Disposition);
        Assert.Contains(evaluation.CriterionResults,
            r => r.Criterion is GoalCriterion.ObligationFulfilled { ObligationId: "ghost" }
                && r.Outcome == CriterionOutcome.Unverifiable);

        // 结构强制唯一长期不变量：Undetermined ⇒ NeedsFollowUp（D6；反例 I）
        Assert.Throws<ArgumentException>(() => new GoalEvaluation(
            "e1", "goal-1", "run-1", "proof-1",
            GoalSatisfaction.Undetermined, EvaluationDisposition.Final,
            Array.Empty<CriterionResult>(), "x"));
    }

    // ---- 验收 9 / 反例 E：反镜像双例 ----------------------------------------

    [Fact]
    public void GoalEval5_FailureOutcomeWithMetObligationsIsSatisfied()
    {
        // Runtime Outcome != Goal Evaluation：外部现实可能已满足 Goal，
        // 只是本次 Run 自身失败（mandatory Failure obligation 满足时
        // classification 取 Failure，其余 obligation 可同为 Satisfied）。
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Failure,
            obligations: new[] { Status("obj", true), Status("eff", true) }));

        Assert.Equal(GoalSatisfaction.Satisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.Final, evaluation.Disposition);
    }

    [Fact]
    public void GoalEval6_CompletionWithPreferredGapIsPartiallySatisfied()
    {
        // Completion ≠ Satisfied：contract 只覆盖 Goal 的一部分。
        var agent = new UniAgent(Goal(
            new GoalCriterion[]
            {
                new GoalCriterion.ClassificationIs(TerminalClassification.Completion),
                new GoalCriterion.ObligationFulfilled("obj"),
            },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", true), Status("eff", false) }));

        Assert.Equal(GoalSatisfaction.PartiallySatisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.NeedsFollowUp, evaluation.Disposition);
    }

    // ---- 验收 8 附加：四类 terminal classification 全部合法输入 --------------

    [Fact]
    public void GoalEval7_SafeStopOutcomeIsEvaluable()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") }));

        var evaluation = agent.Evaluate(Envelope(TerminalClassification.SafeStop,
            obligations: new[] { Status("obj", true) }));

        Assert.Equal(GoalSatisfaction.Satisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.Final, evaluation.Disposition);
    }

    [Fact]
    public void GoalEval8_EscalationOutcomeIsEvaluable()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ClassificationIs(TerminalClassification.Escalation) },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Escalation,
            obligations: new[] { Status("eff", false) }));

        Assert.Equal(GoalSatisfaction.PartiallySatisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.NeedsFollowUp, evaluation.Disposition);
    }

    // ---- 验收 1：唯一 Goal Evaluation Authority ------------------------------

    [Fact]
    public void GoalEval9_UniAgentIsSoleGoalEvaluationProducer()
    {
        // Agent 程序集唯一公共产出路径 = UniAgent.Evaluate（反射扫 public
        // 方法，排除 record 编译器生成的拷贝方法与访问器；OUT-003
        // Outcome16 先例同法）
        var producers = typeof(GoalEvaluation).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => !m.IsSpecialName
                && !m.Name.Contains("<Clone>$", StringComparison.Ordinal)
                && m.ReturnType == typeof(GoalEvaluation))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(new[] { "UniAgent.Evaluate" }, producers);
    }

    // ---- 验收 5 / 反例 A：criterion 语义身份边界 ------------------------------

    [Fact]
    public void GoalEval10_CriterionGraphHasNoEnvelopeStructureTypes()
    {
        var visited = new HashSet<Type>();
        var kernelTypes = new HashSet<string>();
        void Walk(Type? type)
        {
            if (type is null || !visited.Add(type))
                return;
            if (type.Namespace?.StartsWith("UniClaw.Kernel", StringComparison.Ordinal) == true)
                kernelTypes.Add(type.Name);
            foreach (var property in type.GetProperties())
            {
                Walk(property.PropertyType);
                foreach (var argument in property.PropertyType.GetGenericArguments())
                    Walk(argument);
                if (property.PropertyType.IsArray)
                    Walk(property.PropertyType.GetElementType());
            }
        }

        Walk(typeof(GoalCriterion));
        foreach (var nested in typeof(GoalCriterion).GetNestedTypes())
            Walk(nested);

        // criterion 图只允许 TerminalClassification 稳定词汇；
        // RuntimeOutcome / ObligationStatus（envelope 结构）不得出现（反例 A）
        Assert.Equal(new[] { "TerminalClassification" }, kernelTypes.OrderBy(x => x).ToArray());
    }

    // ---- 验收 2 / 反例 D：消费面结构断言 --------------------------------------

    [Fact]
    public void GoalEval11_EvaluationSurfaceIsGoalOutcomeContextOnly()
    {
        // (a) Evaluate 签名只携带 outcome + context（goal ctor 注入；
        //     §3.9 三元输入；Evidence / WorldBelief / Run State 不得入签名）
        var evaluate = typeof(UniAgent).GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Instance)!;
        Assert.Equal(new[] { "RuntimeOutcome", "GoalEvaluationContext" },
            evaluate.GetParameters().Select(p => p.ParameterType.Name).ToArray());

        // (b) Agent 程序集全部公共类型的对象图：Kernel 类型仅限 envelope
        //     公开词汇（RuntimeOutcome / TerminalClassification / ObligationStatus
        //     / RunObligationKind）；Evidence / EffectReceipt / WorldBelief /
        //     Run State / Outcome Proof 等运行时内部类型零出现（反例 D 结构面）
        var allowed = new HashSet<string>
        {
            "RuntimeOutcome", "TerminalClassification", "ObligationStatus", "RunObligationKind",
        };
        var visited = new HashSet<Type>();
        var violations = new List<string>();
        void Walk(Type? type)
        {
            if (type is null || !visited.Add(type))
                return;
            if (type.Namespace?.StartsWith("UniClaw.Kernel", StringComparison.Ordinal) == true
                && !allowed.Contains(type.Name))
                violations.Add(type.Name);
            foreach (var property in type.GetProperties())
            {
                Walk(property.PropertyType);
                foreach (var argument in property.PropertyType.GetGenericArguments())
                    Walk(argument);
                if (property.PropertyType.IsArray)
                    Walk(property.PropertyType.GetElementType());
            }
        }

        foreach (var type in typeof(UniAgent).Assembly.GetTypes().Where(t => t.IsPublic))
            Walk(type);
        Assert.Empty(violations);
    }

    // ---- 验收 6 / 反例 B：vacuous guard ---------------------------------------

    [Fact]
    public void GoalEval12_VacuousGoalWithoutRequiredCriteriaIsRejected()
    {
        // required 空 → 构造拒绝（authoring 点失败优于评价点降级）
        Assert.Throws<ArgumentException>(() => Goal(
            required: Array.Empty<GoalCriterion>(),
            preferred: new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));

        // preferred 空 + required 非空 = 合法纯二值 goal
        var agent = new UniAgent(Goal(new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") }));
        var evaluation = agent.Evaluate(Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", true) }));
        Assert.Equal(GoalSatisfaction.Satisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.Final, evaluation.Disposition);
    }

    // ---- 验收 11 / 反例 H：纯确定性幂等 ---------------------------------------

    [Fact]
    public void GoalEval13_EvaluateIsDeterministicAndIdempotent()
    {
        var agent = new UniAgent(Goal(new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") }));
        var envelope = Envelope(TerminalClassification.Completion, obligations: new[] { Status("obj", true) });

        var first = agent.Evaluate(envelope);
        var second = agent.Evaluate(envelope);

        // 同输入 → 同 EvaluationId、逐字段等值（幂等；不产生重复事实）。
        // record 默认相等对 IReadOnlyList 属性按引用比较，故逐字段断言。
        Assert.Equal(first.EvaluationId, second.EvaluationId);
        Assert.Equal(first.Satisfaction, second.Satisfaction);
        Assert.Equal(first.Disposition, second.Disposition);
        Assert.Equal(first.Rationale, second.Rationale);
        Assert.Equal(first.CriterionResults, second.CriterionResults);

        // 不同输入身份 → 不同 EvaluationId
        var otherRun = agent.Evaluate(Envelope(TerminalClassification.Completion,
            runId: "run-2", obligations: new[] { Status("obj", true) }));
        Assert.NotEqual(first.EvaluationId, otherRun.EvaluationId);
    }

    // ---- 验收 12 / 反例 J：无 envelope 无评价 ---------------------------------

    [Fact]
    public void GoalEval14_EvaluateWithoutOutcomeFailsClosed()
    {
        var agent = new UniAgent(Goal(new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") }));
        Assert.Throws<ArgumentNullException>(() => agent.Evaluate(null!));
    }

    // ---- 验收 3 / 反例 F：对 RuntimeOutcome 只读 -------------------------------

    [Fact]
    public void GoalEval15_EvaluationDoesNotMutateOutcome()
    {
        var agent = new UniAgent(Goal(
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("obj") },
            new GoalCriterion[] { new GoalCriterion.ObligationFulfilled("eff") }));
        var envelope = Envelope(TerminalClassification.Completion,
            obligations: new[] { Status("obj", true), Status("eff", true) });

        var evaluation = agent.Evaluate(envelope);

        // envelope 值不变（record immutable；Agent 无写入路径）；
        // 「评价记录不携带 envelope 对象」由下方属性扫描断言。
        Assert.Equal(TerminalClassification.Completion, envelope.Classification);
        Assert.All(envelope.Obligations, o => Assert.True(o.Satisfied));
        // 评价记录只携带 refs，不携带 envelope 对象本身
        Assert.All(evaluation.GetType().GetProperties(),
            p => Assert.NotEqual(typeof(RuntimeOutcome), p.PropertyType));
    }

    // ---- 验收 4 / 反例 G：单向依赖 + Kernel 零改动 -----------------------------

    [Fact]
    public void GoalEval16_KernelDoesNotReferenceAgent()
    {
        // (a) build 层：Agent csproj 引用 Kernel；Kernel csproj 零 Agent 引用；
        //     slnx 注册两个新项目（ADR-0008）
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("UniClaw.Kernel.slnx").Length == 0)
            dir = dir.Parent!;
        Assert.NotNull(dir);
        var root = dir.FullName;

        var agentCsproj = File.ReadAllText(Path.Combine(root,
            "src", "UniClaw.Agent", "UniClaw.Agent.csproj"));
        Assert.Contains("UniClaw.Kernel.csproj", agentCsproj);

        var kernelCsproj = File.ReadAllText(Path.Combine(root,
            "src", "UniClaw.Kernel", "UniClaw.Kernel.csproj"));
        Assert.DoesNotContain("UniClaw.Agent", kernelCsproj);

        var slnx = File.ReadAllText(Path.Combine(root, "UniClaw.Kernel.slnx"));
        Assert.Contains("src/UniClaw.Agent/UniClaw.Agent.csproj", slnx);
        Assert.Contains("tests/UniClaw.Agent.Tests/UniClaw.Agent.Tests.csproj", slnx);

        // (b) metadata 层：Kernel 程序集引用表不含 Agent
        var kernelRefs = typeof(UniKernel).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name).ToArray();
        Assert.DoesNotContain("UniClaw.Agent", kernelRefs);

        // 注：src/UniClaw.Kernel byte-level 零改动由 VERIFY 阶段 git diff 证明。
    }

    // ---- 端到端：第一个消费者（真实 UniKernel emission → UniAgent）------------

    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 6, 10, 5, 0, TimeSpan.Zero);

    private static readonly IReadOnlySet<string> RelevantSubjects = new HashSet<string>
    {
        "screen.home", "screen.home.failure",
    };

    private static ObservationProposal Observation(
        string subject, string value, DateTimeOffset captureTime,
        string producer = "provider.scripts", IReadOnlyList<string>? lineage = null,
        IngressKind kind = IngressKind.Observation,
        ObservationContext context = ObservationContext.External) =>
        new(new ObservationClaim(subject, value), kind, context,
            new Provenance(producer, captureTime, $"scope:{subject}",
                lineage ?? new[] { "raw://capture", "encode:v1" }));

    private sealed class ScriptedPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, "tap", "screen.home");
    }

    private sealed class ScriptedDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", T1);
    }

    /// <summary>UIW-004 迁移替身（与 Kernel.Tests 的 SeedContainerAssociationStrategy
    /// 同构；跨测试程序集不可见故本地复制）：首条 evidence 铸一个 root container，
    /// 使 DeriveSlice(root)（container-anchored 新形状）可用——场景语义不变。</summary>
    private sealed class SeedContainerAssociationStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new AssociationProposal(
                AssociationDispositionKind.New, MatchedContainerId: null,
                new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                Relations: Array.Empty<ProposedRelation>(), Reason: "seed-root-container")
            : new AssociationProposal(
                AssociationDispositionKind.Insufficient, MatchedContainerId: null,
                Candidates: Array.Empty<AssociationCandidate>(),
                Relations: Array.Empty<ProposedRelation>(), Reason: "seed-once");
    }

    /// <summary>FRS-007：happy-path freshness 替身（恒 Sufficient）。</summary>
    private sealed class SatisfyingFreshness : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "scripted:sufficient");
    }

    [Fact]
    public void GoalEval17_ConsumesRealKernelEmittedEnvelope()
    {
        // OUT-003 冻结路径驱动真实 terminal：Kernel 零改动的消费证明
        var ledger = new EvidenceLedger();
        var world = new WorldModel(RelevantSubjects, new SeedContainerAssociationStrategy());
        var run = new RunModel();
        var control = new ControlLoop(new ScriptedPolicy());
        var assurance = new RuntimeAssurance(new SatisfyingFreshness());
        var effects = new EffectBoundary(new ScriptedDriver());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, run, control, assurance, effects);

        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "verify-home-screen",
            Scope: new HashSet<string> { "screen.home" },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string> { "swipe" },
            ProofCriteria: new[] { "objective-home-active", "effect-home-active" },
            Obligations: new[]
            {
                new RunObligation("objective-home-active", RunObligationKind.Objective,
                    "screen.home", "active", Mandatory: true),
                new RunObligation("effect-home-active", RunObligationKind.MaterialEffect,
                    "screen.home", "active", Mandatory: true),
            }));
        kernel.Process(Observation("screen.home", "idle", T0));

        var intent = kernel.SelectIntent(kernel.DeriveSlice(kernel.CurrentBelief!.Containers.Single().Identity.ContainerId));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
        kernel.Process(Observation("screen.home", "active", T1,
            producer: "effect.boundary.observer",
            context: ObservationContext.PostActionEffectFlow,
            lineage: new[] { $"dispatch:{act.Receipt!.ReceiptId}" }));

        var terminal = kernel.EvaluateTerminal();
        Assert.NotNull(terminal.Outcome);   // 真实 exactly-once emission

        var agent = new UniAgent(Goal(new GoalCriterion[]
        {
            new GoalCriterion.ClassificationIs(TerminalClassification.Completion),
            new GoalCriterion.ObligationFulfilled("effect-home-active"),
        }));
        var evaluation = agent.Evaluate(terminal.Outcome!);

        Assert.Equal(GoalSatisfaction.Satisfied, evaluation.Satisfaction);
        Assert.Equal(EvaluationDisposition.Final, evaluation.Disposition);
        Assert.Equal(run.RunId, evaluation.RunId);
        Assert.Equal(terminal.Outcome!.OutcomeProofId, evaluation.OutcomeProofId);
    }
}
