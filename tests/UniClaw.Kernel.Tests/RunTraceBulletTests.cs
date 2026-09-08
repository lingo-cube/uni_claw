using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// TRC-001 S3 tracer bullet：InMemory / Disabled 双 adapter；binding 词表 =
/// evidence.admit + world.reconcile（显式 parent，无 ambient）。验收锚定
/// changes/TRC-001/state.md Acceptance 1–12 的 bullet 子集。
/// caller-owned lifecycle（G3）：caller 先在 Run Model admit contract
/// （RUN-001 铸造真实 RunId）→ BeginRun(RunId) → 组装 kernel。
/// </summary>
public sealed class RunTraceBulletTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    private static ObservationProposal Observation(string subject, string value) =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance("provider.scripts", T0, $"scope:{subject}", new[] { "raw://capture", "encode:v1" }));

    /// <summary>fail-closed 输入（producer 缺失 → admission rejected）。</summary>
    private static ObservationProposal BrokenObservation(string subject) =>
        new(new ObservationClaim(subject, "x"), IngressKind.Observation, ObservationContext.External,
            new Provenance("", T0, $"scope:{subject}", new[] { "raw://capture" }));

    private static ExecutionContract Contract() => new(
        "v1", "trace-bullet",
        new HashSet<string> { "app" },
        new HashSet<string> { "effect.ui" },
        new HashSet<string> { "effect.fs" },
        new List<string> { "criterion" });

    /// <summary>
    /// bullet 场景：一条 relevant 观察（admit + reconcile）→ 一条 rejected
    /// 观察（封闭 disposition，零 reconcile）。contract 已在 BuildKernel
    /// 阶段由 caller 在 Run Model 上 admit（RunId 已铸造）。
    /// </summary>
    private static void DriveScenario(UniKernel kernel)
    {
        kernel.Process(Observation("screen.home", "visible"));
        kernel.Process(BrokenObservation("screen.home"));
    }

    /// <summary>caller-owned 组装：admit contract → mint RunId → scope → kernel。</summary>
    private static (UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run) BuildKernel(
        IRunTrace trace)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var run = new RunModel();
        var admission = run.AdmitContract(Contract());
        Assert.True(admission.Accepted);
        var kernel = new UniKernel(ledger, world, trace, run);
        return (kernel, ledger, world, run);
    }

    private static (RunTraceScope Scope, UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run)
        BuildTracedKernel()
    {
        var run = new RunModel();
        Assert.True(run.AdmitContract(Contract()).Accepted);
        var scope = RunTraceFactory.BeginRun(new RunCorrelation(run.RunId));
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var kernel = new UniKernel(ledger, world, scope.Trace, run);
        return (scope, kernel, ledger, world, run);
    }

    // ---- Acceptance 1：tracing on/off → canonical outputs 完全一致 -------

    [Fact]
    public void CanonicalEquivalence_TracingOnOff()
    {
        var (_, onKernel, onLedger, onWorld, onRun) = BuildTracedKernel();
        var (offKernel, offLedger, offWorld, offRun) = BuildKernel(DisabledRunTrace.Instance);

        DriveScenario(onKernel);
        DriveScenario(offKernel);

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

        DriveScenario(onKernel);
        DriveScenario(offKernel);

        Assert.Equal(
            offLedger.AdmissionLog.Select(a => (a.Decision, a.EvidenceId, a.RejectionReason)),
            onLedger.AdmissionLog.Select(a => (a.Decision, a.EvidenceId, a.RejectionReason)));
        Assert.Equal(offWorld.Current!.RevisionId, onWorld.Current!.RevisionId);
        Assert.Equal(offRun.History.Count, onRun.History.Count);
    }

    // ---- Acceptance 6/7/12：确定性 causal graph + 真实 RunId -------------

    [Fact]
    public void Artifact_CausalGraphDeterministic_WithRealRunId()
    {
        var first = BuildTracedKernel();
        var second = BuildTracedKernel();

        DriveScenario(first.Kernel);
        DriveScenario(second.Kernel);

        var a1 = first.Scope.FinalizeArtifact();
        var a2 = second.Scope.FinalizeArtifact();

        // 同一确定性场景 → 归一化 causal graph 全等（Acceptance 7：确定性
        // technical ids，无 random；artifact 含 List 成员 → 归一化投影比较）
        Assert.Equal(Normalize(a1), Normalize(a2));
        Assert.Empty(a2.RecorderDiagnostics);

        // Acceptance 12：消费 RUN-001 真实 RunId（非 "run-1"）
        Assert.Equal(first.Run.RunId, a1.RunId);
        Assert.StartsWith("run-", a1.RunId);
        Assert.NotEqual("run-1", a1.RunId);
        Assert.StartsWith("trc-", a1.TraceId);

        // 三 span：admit(root) → reconcile(child of admit)；第二个 admit
        // （rejected）为独立 root
        Assert.Equal(3, a1.Spans.Count);
        var admit1 = a1.Spans[0];
        var reconcile = a1.Spans[1];
        var admit2 = a1.Spans[2];
        Assert.Equal("evidence.admit", admit1.SpanDefinitionId);
        Assert.Null(admit1.ParentSpanId);
        Assert.Equal("world.reconcile", reconcile.SpanDefinitionId);
        Assert.Equal(admit1.SpanId, reconcile.ParentSpanId); // 显式 causation
        Assert.Equal("evidence.admit", admit2.SpanDefinitionId);

        // 事件 reference-first：EvidenceRef / WorldRevisionRef；rejected 只带
        // 封闭 reason code（owner 词汇），无伪造引用
        var admitted = admit1.Events.Single(e => e.EventId == "admitted");
        Assert.Contains(admitted.References, r => r.Kind == TraceReferenceKind.Evidence);
        var reconciled = reconcile.Events.Single(e => e.EventId == "reconciled");
        Assert.Contains(reconciled.References, r => r.Kind == TraceReferenceKind.WorldRevision);
        var rejected = admit2.Events.Single(e => e.EventId == "admission-rejected");
        Assert.False(string.IsNullOrEmpty(rejected.ReasonCode));
        Assert.Empty(rejected.References);

        // Acceptance 9 反向：正常关闭的 span 全部 Completed（domain 拒绝 ≠
        // trace failure）
        Assert.All(a1.Spans, s => Assert.Equal(StructuralOutcome.Completed, s.StructuralOutcome));
        Assert.Empty(a1.RecorderDiagnostics);
    }

    /// <summary>归一化 causal graph 投影（Acceptance 7 的比较面：抹平
    /// 集合引用语义，保留因果结构与引用语义）。</summary>
    private static string Normalize(RunTraceArtifact artifact) =>
        artifact.RunId + "|" + artifact.TraceId + "|" + string.Join("|", artifact.Spans.Select(s =>
            $"{s.SpanId}<-{s.ParentSpanId ?? "root"}:{s.SpanDefinitionId}:{s.StructuralOutcome}:"
            + string.Join(",", s.Events.Select(e =>
                $"{e.EventId}[{string.Join(",", e.References.Select(r => $"{r.Kind}:{r.Value}"))}]{e.ReasonCode ?? ""}"))));

    // ---- Acceptance 4：词表执法 → 丢弃 + TraceDiagnostic ------------------

    [Fact]
    public void Vocabulary_Enforced_DiagnosticsRecorded()
    {
        var scope = RunTraceFactory.BeginRun(new RunCorrelation("run-vocab"));

        var span = scope.Trace.StartOperation(
            TraceCatalog.WorldReconcile,
            parent: null,
            references: new[] { new TraceReference(TraceReferenceKind.Binding, "b-1") }); // Binding 不在 AllowedReferenceKinds
        span.Record("unknown-event", Array.Empty<TraceReference>(), reasonCode: "x");
        span.Complete(StructuralOutcome.Completed);

        var artifact = scope.FinalizeArtifact();
        var spanRecord = artifact.Spans.Single();
        Assert.Empty(spanRecord.References); // 非法 ref 被丢弃
        Assert.Empty(spanRecord.Events);     // 非法 event 被丢弃
        Assert.Equal(2, artifact.RecorderDiagnostics.Count);
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

        var disabled = RunTraceFactory.BeginDisabled(new RunCorrelation("run-fin"));
        disabled.Trace.StartOperation(TraceCatalog.EvidenceAdmit, null, Array.Empty<TraceReference>());
        var artifact = disabled.FinalizeArtifact();
        Assert.Empty(artifact.Spans);
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason == "tracing-disabled");
    }

    // ---- Acceptance 9：未关闭 span → Incomplete，不伪造成功 ---------------

    [Fact]
    public void UnclosedSpan_MarkedIncomplete()
    {
        var scope = RunTraceFactory.BeginRun(new RunCorrelation("run-inc"));
        var span = scope.Trace.StartOperation(TraceCatalog.EvidenceAdmit, null, Array.Empty<TraceReference>());
        span.Record("admitted", Array.Empty<TraceReference>());
        span.Dispose(); // 未显式 Complete

        var artifact = scope.FinalizeArtifact();
        Assert.Equal(StructuralOutcome.Incomplete, artifact.Spans.Single().StructuralOutcome);
    }

    // ---- Acceptance 5：artifact 结构只含引用/枚举/序数，无 domain 载荷 ----

    [Fact]
    public void Artifact_ShapeHoldsNoDomainPayload()
    {
        var allowed = new HashSet<Type> { typeof(string), typeof(int) };
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
            if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                                     || t.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)))
                return IsAllowed(t.GetGenericArguments()[0]);
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

    private sealed class ThrowingRunTrace : IRunTrace
    {
        public ITraceOperationScope StartOperation(
            SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
            => throw new InvalidOperationException("recorder exploded (test double)");
    }
}
