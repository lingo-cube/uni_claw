using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
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
/// TRC-001 S3 tracer bullet（评审硬化二轮）：InMemory / Disabled 双
/// adapter；binding 词表 = evidence.admit + world.reconcile；catalog 构造
/// 收口 + 事件/reason 封闭 + artifact 深冻结；emission 观测由 Kernel
/// 组合缝在真实 emission 点经 internal sink 标记（公共面不可伪造），
/// 未观测即 finalize → RecorderTerminal=Quarantined 诚实降级。
/// caller-owned lifecycle（G3）：caller 先在 Run Model admit contract
/// （RUN-001 铸造真实 RunId）→ BeginRun(RunId) → 组装 kernel。
/// </summary>
public sealed class RunTraceBulletTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 9, 10, 5, 0, TimeSpan.Zero);

    private static ObservationProposal Observation(
        string subject,
        string value,
        string producer = "provider.scripts",
        IngressKind kind = IngressKind.Observation,
        ObservationContext context = ObservationContext.External,
        IReadOnlyList<string>? lineage = null) =>
        new(new ObservationClaim(subject, value), kind, context,
            new Provenance(producer, T1, $"scope:{subject}", lineage ?? new[] { "raw://capture", "encode:v1" }));

    /// <summary>fail-closed 输入（producer 缺失 → admission rejected）。</summary>
    private static ObservationProposal BrokenObservation(string subject) =>
        new(new ObservationClaim(subject, "x"), IngressKind.Observation, ObservationContext.External,
            new Provenance("", T1, $"scope:{subject}", new[] { "raw://capture" }));

    private static ExecutionContract Contract() => new(
        "v1", "trace-bullet",
        new HashSet<string> { "app" },
        new HashSet<string> { "effect.ui" },
        new HashSet<string> { "effect.fs" },
        new List<string> { "criterion" });

    // ---- 六 owner 全组装（emission-reaching 场景，OUT-003 同构）-------

    private sealed class ScriptedPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, "tap", "screen.home");
    }

    private sealed class ScriptedDriver : IEffectDriver
    {
        public DispatchResult Deliver(CanonicalBinding binding) =>
            new(DispatchOutcome.Delivered, "scripted:ok", T1);
    }

    /// <summary>emission-reaching traced kernel：contract obligations 走
    /// Completion 路径（objective + material effect 双 mandatory）。</summary>
    private static (RunTraceScope Scope, UniKernel Kernel, RunModel Run) BuildEmittingTracedKernel()
    {
        var run = new RunModel();
        Assert.True(run.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "verify-home-screen",
            Scope: new HashSet<string> { "screen.home" },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string> { "swipe" },
            ProofCriteria: new[] { "objective-home-active", "effect-home-active" },
            Obligations: new[]
            {
                new RunObligation("objective-home-active", RunObligationKind.Objective, "screen.home", "active", Mandatory: true),
                new RunObligation("effect-home-active", RunObligationKind.MaterialEffect, "screen.home", "active", Mandatory: true),
            })).Accepted);
        var scope = RunTraceFactory.BeginRun(new RunCorrelation(run.RunId));
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(new HashSet<string> { "screen.home" }),
            scope.Trace, run,
            new ControlLoop(new ScriptedPolicy()),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new ScriptedDriver()));
        return (scope, kernel, run);
    }

    /// <summary>驱动到真实 emission：rev-1 → 拒绝输入 → act → post-action
    /// 效果观察 → EvaluateTerminal（Outcome 非 null）。</summary>
    private static void DriveToEmission(UniKernel kernel)
    {
        kernel.Process(Observation("screen.home", "idle")); // admit + reconcile（rev-1）
        kernel.Process(BrokenObservation("screen.home"));   // 拒绝（封闭 disposition）
        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.NotNull(act.Receipt);
        kernel.Process(Observation("screen.home", "active",
            producer: "effect.boundary.observer",
            context: ObservationContext.PostActionEffectFlow,
            lineage: new[] { $"dispatch:{act.Receipt!.ReceiptId}" }));
        var eval = kernel.EvaluateTerminal();
        Assert.NotNull(eval.Outcome); // 真实 emission 发生（组合缝在此标记）
    }

    /// <summary>轻量 kernel（仅 ledger+world+run）用于 on/off 等价对照。</summary>
    private static (UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run) BuildKernel(
        IRunTrace trace)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var run = new RunModel();
        Assert.True(run.AdmitContract(Contract()).Accepted);
        var kernel = new UniKernel(ledger, world, trace, run);
        return (kernel, ledger, world, run);
    }

    // ---- Acceptance 1：tracing on/off → canonical outputs 完全一致 -------

    [Fact]
    public void CanonicalEquivalence_TracingOnOff()
    {
        var onLedger = new EvidenceLedger();
        var onWorld = new WorldModel(new HashSet<string> { "screen.home" });
        var onRun = new RunModel();
        Assert.True(onRun.AdmitContract(Contract()).Accepted);
        var onScope = RunTraceFactory.BeginRun(new RunCorrelation(onRun.RunId));
        var onKernel = new UniKernel(onLedger, onWorld, onScope.Trace, onRun);

        var (offKernel, offLedger, offWorld, offRun) = BuildKernel(DisabledRunTrace.Instance);

        foreach (var k in new[] { onKernel, offKernel })
        {
            k.Process(Observation("screen.home", "visible"));
            k.Process(BrokenObservation("screen.home"));
        }

        // Owner logs / canonical state 逐项一致（Acceptance 1；record 含 List
        // 成员无深度相等 → 键字段投影比较）
        Assert.Equal(
            offLedger.AdmissionLog.Select(a => (a.Decision, a.EvidenceId, a.RejectionReason)),
            onLedger.AdmissionLog.Select(a => (a.Decision, a.EvidenceId, a.RejectionReason)));
        Assert.Equal(offWorld.Current!.RevisionId, onWorld.Current!.RevisionId);
        Assert.Equal(offWorld.RelevanceLog.Count, onWorld.RelevanceLog.Count);
        Assert.Equal(offRun.History.Count, onRun.History.Count);
        Assert.Equal(offRun.RunId, onRun.RunId);
    }

    // ---- Acceptance 2：Recorder 抛错不得改变 Runtime 行为 -----------------

    [Fact]
    public void ThrowingTrace_DoesNotChangeRuntimeBehavior()
    {
        var (onKernel, onLedger, onWorld, onRun) = BuildKernel(new ThrowingRunTrace());
        var (offKernel, offLedger, offWorld, offRun) = BuildKernel(DisabledRunTrace.Instance);

        foreach (var k in new[] { onKernel, offKernel })
        {
            k.Process(Observation("screen.home", "visible"));
            k.Process(BrokenObservation("screen.home"));
        }

        Assert.Equal(
            offLedger.AdmissionLog.Select(a => (a.Decision, a.EvidenceId, a.RejectionReason)),
            onLedger.AdmissionLog.Select(a => (a.Decision, a.EvidenceId, a.RejectionReason)));
        Assert.Equal(offWorld.Current!.RevisionId, onWorld.Current!.RevisionId);
        Assert.Equal(offRun.History.Count, onRun.History.Count);
    }

    // ---- Acceptance 6/7/8/12：真实 emission 驱动的确定性 causal graph -----

    [Fact]
    public void Artifact_EmissionAnchoredDeterministic_WithRealRunId()
    {
        var first = BuildEmittingTracedKernel();
        var second = BuildEmittingTracedKernel();
        DriveToEmission(first.Kernel);
        DriveToEmission(second.Kernel);

        var a1 = first.Scope.FinalizeArtifact();
        var a2 = second.Scope.FinalizeArtifact();

        // 同一确定性场景 → 归一化 causal graph 全等（Acceptance 7）
        Assert.Equal(Normalize(a1), Normalize(a2));

        // Acceptance 8（二轮语义）：emission 由 Kernel 组合缝在真实发射点
        // 标记——不是 caller 自证；Finalized 且零 diagnostic
        Assert.Equal(RecorderTerminal.Finalized, a1.RecorderTerminal);
        Assert.Equal(RecorderTerminal.Finalized, a2.RecorderTerminal);
        Assert.Empty(a1.RecorderDiagnostics);

        // Acceptance 12：消费 RUN-001 真实 RunId（完整 64 位 hex）
        Assert.Equal(first.Run.RunId, a1.RunId);
        Assert.StartsWith("run-", a1.RunId);
        Assert.Equal("run-".Length + 64, a1.RunId.Length);
        Assert.StartsWith("trc-", a1.TraceId);

        // 结构：rev-1 的 admit(root) → reconcile(child，显式 causation)；
        // 链上存在拒绝 span 与回流 span
        Assert.True(a1.Spans.Length >= 4);
        var admit1 = a1.Spans[0];
        var reconcile1 = a1.Spans[1];
        Assert.Equal("evidence.admit", admit1.SpanDefinitionId);
        Assert.Null(admit1.ParentSpanId);
        Assert.Equal("world.reconcile", reconcile1.SpanDefinitionId);
        Assert.Equal(admit1.SpanId, reconcile1.ParentSpanId);

        var allEvents = a1.Spans.SelectMany(s => s.Events).ToList();
        Assert.Contains(allEvents, e => e.EventId == "admitted"
            && e.References.Any(r => r.Kind == TraceReferenceKind.Evidence));
        Assert.Contains(allEvents, e => e.EventId == "reconciled"
            && e.References.Any(r => r.Kind == TraceReferenceKind.WorldRevision));
        var rejected = allEvents.Single(e => e.EventId == "admission-rejected");
        Assert.False(string.IsNullOrEmpty(rejected.ReasonCode));
        Assert.Empty(rejected.References);

        // Acceptance 9 反向：正常关闭的 span 全部 Completed（domain 拒绝 ≠
        // trace failure）
        Assert.All(a1.Spans, s => Assert.Equal(StructuralOutcome.Completed, s.StructuralOutcome));
    }

    // ---- Acceptance 8（三轮）：emission/lifecycle 面全量不可达 ----------

    [Fact]
    public void EmissionAndLifecycle_NotReachableFromAnyPublicSurface()
    {
        // Spec（评审三轮）：扫描 Trace 命名空间全部 exported public types
        // （含具体 adapter）——显式接口实现（internal sink）不在公共方法表
        var exported = typeof(RunTraceArtifact).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("UniClaw.Kernel.Trace") == true && t.IsVisible)
            .ToList();

        Assert.Contains(typeof(DisabledRunTrace), exported); // 扫描面确实覆盖 public adapter

        foreach (var type in exported)
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.True(!method.Name.Contains("Mark"),
                $"{type.Name}.{method.Name} 泄漏 Mark 能力");
            Assert.True(method.Name != "Finalize",
                $"{type.Name}.{method.Name} 泄漏 lifecycle finalize 能力");
        }
    }

    // ---- Acceptance 8（二轮）：未观测 emission → Quarantined 诚实降级 ----

    [Fact]
    public void Finalize_WithoutObservedEmission_QuarantinedButUsable()
    {
        // 失败 run（未到 terminal）的诊断 artifact：可用但显式非 Finalized
        var (scope, kernel, _) = BuildEmittingTracedKernel();
        kernel.Process(Observation("screen.home", "idle")); // admit + reconcile 后中断

        var artifact = scope.FinalizeArtifact();
        Assert.Equal(RecorderTerminal.Quarantined, artifact.RecorderTerminal);
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason == "runtime-outcome-emission-not-observed");
        Assert.True(artifact.Spans.Length >= 2); // 诊断内容保留
        Assert.Equal(artifact, scope.FinalizeArtifact()); // 幂等
    }

    // ---- Acceptance 8：finalize 幂等；Disabled artifact 显式空 ------------

    [Fact]
    public void Finalize_Idempotent_DisabledArtifactExplicitlyEmpty()
    {
        var scope = RunTraceFactory.BeginRun(new RunCorrelation("run-fin"));
        var span = scope.Trace.StartOperation(TraceCatalog.EvidenceAdmit, null, Array.Empty<TraceReference>());
        span.Complete(StructuralOutcome.Completed);

        var first = scope.FinalizeArtifact();
        var second = scope.FinalizeArtifact();
        Assert.Equal(first, second);
        Assert.Equal(RecorderTerminal.Quarantined, first.RecorderTerminal); // 无 emission（此处也无从伪造）

        var disabled = RunTraceFactory.BeginDisabled(new RunCorrelation("run-fin"));
        disabled.Trace.StartOperation(TraceCatalog.EvidenceAdmit, null, Array.Empty<TraceReference>());
        var artifact = disabled.FinalizeArtifact();
        Assert.Empty(artifact.Spans);
        Assert.Equal(RecorderTerminal.Finalized, artifact.RecorderTerminal); // 零捕获即零观测义务
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason == "tracing-disabled");
    }

    // ---- Acceptance 9：未关闭 span → Incomplete，不伪造成功 ---------------

    [Fact]
    public void UnclosedSpan_MarkedIncomplete()
    {
        var scope = RunTraceFactory.BeginRun(new RunCorrelation("run-inc"));
        var span = scope.Trace.StartOperation(TraceCatalog.EvidenceAdmit, null, Array.Empty<TraceReference>());
        span.Record(TraceCatalog.Admitted, Array.Empty<TraceReference>());
        span.Dispose(); // 未显式 Complete

        var artifact = scope.FinalizeArtifact();
        Assert.Equal(StructuralOutcome.Incomplete, artifact.Spans.Single().StructuralOutcome);
    }

    // ---- Acceptance 4/G4：词表与 reason code 执法 → 丢弃 + TraceDiagnostic

    [Fact]
    public void Vocabulary_Enforced_DiagnosticsRecorded()
    {
        var scope = RunTraceFactory.BeginRun(new RunCorrelation("run-vocab"));

        // 1) ref kind 越界 + 2) 跨 operation 事件（Admitted 属 evidence.admit）
        var reconcileSpan = scope.Trace.StartOperation(
            TraceCatalog.WorldReconcile,
            parent: null,
            references: new[] { new TraceReference(TraceReferenceKind.Binding, "b-1") });
        reconcileSpan.Record(TraceCatalog.Admitted, Array.Empty<TraceReference>());
        reconcileSpan.Complete(StructuralOutcome.Completed);

        // 3) 必携 reason 的拒收事件缺 code；4) code 非封闭集成员
        var admitSpan = scope.Trace.StartOperation(TraceCatalog.EvidenceAdmit, null, Array.Empty<TraceReference>());
        admitSpan.Record(TraceCatalog.AdmissionRejected, Array.Empty<TraceReference>(), reasonCode: null);
        admitSpan.Record(TraceCatalog.AdmissionRejected, Array.Empty<TraceReference>(), reasonCode: "bogus-code");
        admitSpan.Complete(StructuralOutcome.Completed);

        var artifact = scope.FinalizeArtifact();
        // 4 条词表执法 + 1 条 emission 未观测（adapter 级用例，无 run）
        Assert.Equal(5, artifact.RecorderDiagnostics.Length);
        Assert.All(artifact.Spans, s => Assert.Empty(s.Events)); // 非法事件全部丢弃
        Assert.All(artifact.Spans, s => Assert.Empty(s.References)); // 非法 ref 丢弃
    }

    // ---- S1 硬化：catalog 全量登记 / 构造封闭 / 冻结不可篡改 --------------

    [Fact]
    public void Catalog_Totality_FrozenConstructionClosed()
    {
        // 全 11 项登记（binding 2 + provisional 9），OperationId 无重复
        Assert.Equal(11, TraceCatalog.All.Count);
        Assert.Equal(11, TraceCatalog.All.Select(d => d.OperationId).Distinct().Count());
        Assert.Equal(2, TraceCatalog.Binding.Count);
        Assert.All(TraceCatalog.Binding, d => Assert.False(d.IsProvisional));
        Assert.Equal(9, TraceCatalog.Provisional.Count);
        Assert.All(TraceCatalog.Provisional, d => Assert.True(d.IsProvisional));
        Assert.Contains(TraceCatalog.All, d => d.OperationId == "runtime.emit-outcome"); // P1-2 登记
        Assert.Contains(TraceCatalog.All, d => d.OperationId == "run.execute");

        // G4：reason code 封闭集 = owner 词汇（EvidenceLedger check 名单）
        Assert.True(TraceCatalog.AdmissionRejected.ReasonCodeRequired);
        Assert.True(TraceCatalog.AdmissionRejected.AllowedReasonCodes.Contains("source-identity"));
        Assert.Equal(9, TraceCatalog.AdmissionRejected.AllowedReasonCodes.Count);
        Assert.False(TraceCatalog.Admitted.ReasonCodeRequired);
        Assert.Empty(TraceCatalog.Admitted.AllowedReasonCodes);

        // Standards V1：Frozen 集合不可强转篡改
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<TraceReferenceKind>)TraceCatalog.EvidenceAdmit.AllowedReferenceKinds)
            .Add(TraceReferenceKind.Binding));
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<string>)TraceCatalog.AdmissionRejected.AllowedReasonCodes).Add("forged"));
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<SpanDefinition>)TraceCatalog.Binding).Add(TraceCatalog.EvidenceAdmit));

        // provisional 登记 ≠ 可录制：recorder 拒绝 + diagnostic
        var scope = RunTraceFactory.BeginRun(new RunCorrelation("run-prov"));
        var returned = scope.Trace.StartOperation(
            TraceCatalog.Provisional.First(d => d.OperationId == "run.execute"), null, Array.Empty<TraceReference>());
        returned.Complete(StructuralOutcome.Completed);
        var artifact = scope.FinalizeArtifact();
        Assert.Empty(artifact.Spans);
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason.StartsWith("operation-provisional"));
    }

    // ---- Acceptance 5：artifact 结构只含引用/枚举/序数，无 domain 载荷 ----

    [Fact]
    public void Artifact_ShapeHoldsNoDomainPayload()
    {
        var allowed = new HashSet<Type> { typeof(string), typeof(int), typeof(bool) };
        // 只审 public artifact 面（internal recorder buffer 非投影面）
        var traceTypes = typeof(RunTraceArtifact).Assembly.GetTypes()
            .Where(t => t.Namespace == "UniClaw.Kernel.Trace" && t.IsVisible)
            .ToHashSet();

        foreach (var type in traceTypes)
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.True(IsAllowed(property.PropertyType),
                $"{type.Name}.{property.Name} 携带非法载荷类型 {property.PropertyType.Name}");
        }
        return;

        bool IsAllowed(Type t)
        {
            if (allowed.Contains(t) || t.IsEnum)
                return true;
            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                if (def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlySet<>)
                                                    || def == typeof(FrozenSet<>) || def == typeof(ImmutableArray<>))
                    return IsAllowed(t.GetGenericArguments()[0]);
            }
            return traceTypes.Contains(t); // 只允许 Trace 命名空间内的组合类型
        }
    }

    // ---- Acceptance 3/10：六个 L2 无任何 Trace 类型引用（架构 guard）-----

    [Fact]
    public void ArchitectureGuard_L2TypesDoNotReferenceTrace()
    {
        var traceTypes = typeof(RunTraceArtifact).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("UniClaw.Kernel.Trace") == true)
            .ToHashSet();
        var l2Prefixes = new[]
        {
            "UniClaw.Kernel.Evidence", "UniClaw.Kernel.World", "UniClaw.Kernel.Run",
            "UniClaw.Kernel.Control", "UniClaw.Kernel.Assurance", "UniClaw.Kernel.Effects",
        };
        var violations = new List<string>();

        foreach (var type in typeof(RunTraceArtifact).Assembly.GetTypes()
                     .Where(t => l2Prefixes.Any(p => t.Namespace?.StartsWith(p) == true)))
        {
            foreach (var ctor in type.GetConstructors())
            foreach (var p in ctor.GetParameters())
                if (MentionsTrace(p.ParameterType))
                    violations.Add($"{type.Name}..ctor({p.Name})");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (MentionsTrace(method.ReturnType))
                    violations.Add($"{type.Name}.{method.Name}() -> {method.ReturnType.Name}");
                foreach (var p in method.GetParameters())
                    if (MentionsTrace(p.ParameterType))
                        violations.Add($"{type.Name}.{method.Name}({p.Name})");
            }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (MentionsTrace(property.PropertyType))
                    violations.Add($"{type.Name}.{property.Name}");
        }

        Assert.Empty(violations);
        return;

        bool MentionsTrace(Type t)
        {
            if (traceTypes.Contains(t))
                return true;
            if (t.IsArray)
                return MentionsTrace(t.GetElementType()!);
            if (t.IsGenericType)
                return t.GetGenericArguments().Any(MentionsTrace);
            return false;
        }
    }

    /// <summary>归一化 causal graph 投影（Acceptance 7 的比较面：抹平
    /// 集合引用语义，保留因果结构与引用语义）。</summary>
    private static string Normalize(RunTraceArtifact artifact) =>
        artifact.RunId + "|" + artifact.TraceId + "|" + artifact.RecorderTerminal + "|" + string.Join("|", artifact.Spans.Select(s =>
            $"{s.SpanId}<-{s.ParentSpanId ?? "root"}:{s.SpanDefinitionId}:{s.StructuralOutcome}:"
            + string.Join(",", s.Events.Select(e =>
                $"{e.EventId}[{string.Join(",", e.References.Select(r => $"{r.Kind}:{r.Value}"))}]{e.ReasonCode ?? ""}"))));

    private sealed class ThrowingRunTrace : IRunTrace
    {
        public ITraceOperationScope StartOperation(
            SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
            => throw new InvalidOperationException("recorder exploded (test double)");
    }
}
