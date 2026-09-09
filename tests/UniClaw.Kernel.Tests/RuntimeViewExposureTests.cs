using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using Xunit;

using UniClaw.Kernel.Trace;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// EXP-008 验收 —— Consumer View 曝射面收敛（ADR-0011：consumer-specific
/// immutable projection / Owner-only derivation / projection ≠ second truth /
/// 不承载 consumer-owned judgment）。N1-N4 结构断言（反射 allowlist，
/// 新增任何 public member 即失败）；N5/N6 行为断言；N7 P5 收缩契约。
/// 纯内存（level: DETERMINISTIC）。
/// </summary>
public sealed class RuntimeViewExposureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 7, 10, 5, 0, TimeSpan.Zero);

    private static readonly Type[] ViewTypes =
        { typeof(BindingView), typeof(ActionAssuranceView), typeof(OutcomeAssuranceView) };

    private static readonly Type[] ConsumerTypes =
        { typeof(ControlLoop), typeof(RuntimeAssurance), typeof(EffectBoundary) };

    // ---- 测试替身（与其他套件同构） ---------------------------------------

    private static ObservationProposal Observation(
        string subject,
        string value,
        DateTimeOffset captureTime,
        string producer = "provider.scripts") =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance(producer, captureTime, $"scope:{subject}",
                new[] { "raw://capture", "encode:v1" }));

    private static ExecutionContract Contract() => new(
        Version: "c1",
        Objective: "verify-home-screen",
        Scope: new HashSet<string> { "screen.home" },
        AllowedEffects: new HashSet<string> { "tap" },
        ForbiddenEffects: new HashSet<string> { "swipe" },
        ProofCriteria: new[] { "home-screen-observed" });

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

    /// <summary>组装六 L2 kernel；relevance scope 含 screen.home + screen.header
    ///（screen.header 供构造无冲突的 rev-2，用于 stale-view 场景）。</summary>
    private static (UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run,
        ControlLoop Control, RuntimeAssurance Assurance, EffectBoundary Effects) NewKernel()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home", "screen.header" }, new SeedContainerAssociationStrategy());
        var run = new RunModel();
        var control = new ControlLoop(new ScriptedPolicy());
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var effects = new EffectBoundary(new ScriptedDriver());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, run, control, assurance, effects);
        return (kernel, ledger, world, run, control, assurance, effects);
    }

    // ---- N1：shape allowlist（EXP-008 D6/D7/D8 白名单） -------------------

    [Fact]
    public void N1_ViewPublicShapesMatchAllowlistExactly()
    {
        AssertShape(typeof(BindingView),
            ("RevisionId", typeof(string)),
            ("RevisionNumber", typeof(int)),
            ("HasTargetSubjectClaim", typeof(bool)),
            ("HasTargetOccurrence", typeof(bool)));   // UIW-004 解锁：UI 通道 owner fact

        AssertShape(typeof(ActionAssuranceView),
            ("RevisionId", typeof(string)),
            ("RevisionNumber", typeof(int)),
            ("FreshnessBasis", typeof(FreshnessBasis)),
            ("HasConflictOnTarget", typeof(bool)));

        AssertShape(typeof(OutcomeAssuranceView),
            ("RevisionId", typeof(string)),
            ("ConflictingClaimCount", typeof(int)),
            ("Claims", typeof(IReadOnlyDictionary<string, ScopedClaim>)),
            ("Conflicts", typeof(IReadOnlyList<Conflict>)),
            ("BasisEvidenceIds", typeof(IReadOnlySet<string>)));

        // ScopedClaim = claim 粒度协议表示（WorldClaim 不跨边界）
        AssertShape(typeof(ScopedClaim),
            ("Value", typeof(string)),
            ("EvidenceId", typeof(string)));
    }

    /// <summary>public shape allowlist：属性集（名 + 类型）精确等于白名单，
    /// 新增 / 改名 / 删除 / 改类型任何 public member 即失败（ADR-0011）。</summary>
    private static void AssertShape(Type type, params (string Name, Type PropertyType)[] expected)
    {
        var actual = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType))
            .OrderBy(t => t.Name)
            .ToArray();
        var want = expected.OrderBy(t => t.Name).ToArray();
        Assert.Equal(want, actual);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    // ---- N2：负向结构 —— 无 owner-internal aggregate / 无 mutable 集合 ----

    [Fact]
    public void N2_ViewsExposeNoOwnerInternalAggregatesOrMutableCollections()
    {
        var banned = new[]
        {
            typeof(WorldBeliefRevision), typeof(RunState), typeof(WorldModel), typeof(RunModel),
            typeof(EvidenceLedger), typeof(ControlLoop), typeof(RuntimeAssurance), typeof(EffectBoundary),
        };

        foreach (var viewType in ViewTypes)
        {
            foreach (var property in viewType.GetProperties())
            {
                var propertyType = property.PropertyType;
                Assert.All(banned,
                    b => Assert.NotEqual(b, propertyType));
                Assert.All(propertyType.GetGenericArguments()
                        .Concat(propertyType.IsArray && propertyType.GetElementType() is { } e
                            ? new[] { e }
                            : Array.Empty<Type>()),
                    g => Assert.All(banned, b => Assert.NotEqual(b, g)));

                // mutable collections：只允许只读接口形态（allowlist 之外的
                // IList / IDictionary / ISet&lt;&gt; 实现即失败）
                Assert.False(
                    typeof(System.Collections.IList).IsAssignableFrom(propertyType)
                    || typeof(System.Collections.IDictionary).IsAssignableFrom(propertyType)
                    || propertyType.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>)),
                    $"{viewType.Name}.{property.Name} 不得暴露 mutable collection：{propertyType.Name}");
            }
        }
    }

    // ---- N3：签名零 canonical aggregate（P5 / P11 收缩） ------------------

    [Fact]
    public void N3_ConsumerPublicSignaturesAcceptNoCanonicalAggregates()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // P5：Control 公开面零 RunState
        foreach (var method in typeof(ControlLoop).GetMethods(flags))
            Assert.All(method.GetParameters(),
                p => Assert.NotEqual(typeof(RunState), p.ParameterType));
        foreach (var property in typeof(ControlLoop).GetProperties(flags))
            Assert.NotEqual(typeof(RunState), property.PropertyType);

        // P11：Assurance / Effect Boundary 公开面零 WorldBeliefRevision
        foreach (var consumer in new[] { typeof(RuntimeAssurance), typeof(EffectBoundary) })
        {
            foreach (var method in consumer.GetMethods(flags))
                Assert.All(method.GetParameters(),
                    p => Assert.NotEqual(typeof(WorldBeliefRevision), p.ParameterType));
            foreach (var property in consumer.GetProperties(flags))
                Assert.NotEqual(typeof(WorldBeliefRevision), property.PropertyType);
        }
    }

    // ---- N4：view 是 ephemeral projection，不是可缓存的第二 truth ---------

    [Fact]
    public void N4_ConsumersNeverPersistConsumerViewsInInstanceFields()
    {
        foreach (var consumer in ConsumerTypes)
        {
            foreach (var field in consumer.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.False(ViewTypes.Contains(field.FieldType),
                    $"{consumer.Name} 实例字段 {field.Name} 直接持久持有 {field.FieldType.Name}");
                var elementTypes = field.FieldType.GetGenericArguments()
                    .Concat(field.FieldType.IsArray && field.FieldType.GetElementType() is { } e
                        ? new[] { e }
                        : Array.Empty<Type>());
                Assert.All(elementTypes,
                    e => Assert.False(ViewTypes.Contains(e),
                        $"{consumer.Name} 实例字段 {field.Name} 经 collection 持有 {e.Name}"));
            }
        }
    }

    // ---- N5：旧 view 不能冒充当前 projection（correlation mismatch） ------

    [Fact]
    public void N5_StaleViewFailsClosedAgainstCurrentCorrelationAnchors()
    {
        var (kernel, _, world, run, _, assurance, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        kernel.Process(Observation("screen.home", "idle", T0));      // rev-1

        // 消费前派生的 rev-1 view（world 随后演进）
        var staleBindingView = world.DeriveBindingView("screen.home");
        var staleActionView = world.DeriveActionAssuranceView("screen.home");

        kernel.Process(Observation("screen.header", "ok", T1));      // rev-2（无冲突）

        // Bind：current candidate（rev-2）+ 旧 view → stale-revision
        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));   // basis rev-2
        var stale = effects.Bind(intent, new CandidateBinding("screen.home", "idle", "rev-2"), staleBindingView);
        Assert.Null(stale.Canonical);
        Assert.Equal(BindingRejectionReason.StaleRevision, stale.RejectionReason);

        // Judge：current canonical binding（rev-2）+ 旧 view → binding-revision-currentness
        var canonical = effects.Bind(
            intent, new CandidateBinding("screen.home", "idle", "rev-2"),
            world.DeriveBindingView("screen.home")).Canonical!;
        var staleJudgment = assurance.Judge(intent, canonical, run.View!, staleActionView);
        Assert.False(staleJudgment.IsAdmissible);
        Assert.Equal("binding-revision-currentness", staleJudgment.RejectionReason);

        // Dispatch：admissible rev-2 judgment + 旧 view → binding-stale（gate 执法）
        var authorized = assurance.Judge(
            intent, canonical, run.View!, world.DeriveActionAssuranceView("screen.home"));
        Assert.True(authorized.IsAdmissible);
        var (gate, receipt) = effects.Dispatch(canonical, authorized, staleBindingView);
        Assert.False(gate.Allowed);
        Assert.Equal("binding-stale", gate.Reason);
        Assert.Null(receipt);

        // IsBindingValid：current binding + 旧 view → 不 valid
        Assert.False(effects.IsBindingValid(canonical, staleBindingView));
        // 对照：current view 派生下同一 binding 有效（dispatch 未发生）
        Assert.True(effects.IsBindingValid(canonical, world.DeriveBindingView("screen.home")));
    }

    // ---- N6：Owner 派生 facts 与 canonical revision 语义一致 ---------------

    [Fact]
    public void N6_OwnerDerivedFactsMatchCurrentRevisionSemantics()
    {
        var (kernel, _, world, _, _, _, _) = NewKernel();
        kernel.AdmitContract(Contract());

        // 无 revision → 无法派生（fail-closed，镜像 DeriveSlice）
        Assert.Throws<InvalidOperationException>(() => world.DeriveBindingView("screen.home"));
        Assert.Throws<InvalidOperationException>(() => world.DeriveActionAssuranceView("screen.home"));
        Assert.Throws<InvalidOperationException>(() => world.DeriveOutcomeAssuranceView(new[] { "screen.home" }));

        var first = kernel.Process(Observation("screen.home", "idle", T0));   // rev-1
        var rev1 = world.Current!;

        // BindingView：revision 锚 + claim 存在性 fact（graph-only subject 不算）
        var home = world.DeriveBindingView("screen.home");
        Assert.Equal(rev1.RevisionId, home.RevisionId);
        Assert.Equal(rev1.RevisionNumber, home.RevisionNumber);
        Assert.True(home.HasTargetSubjectClaim);
        Assert.False(world.DeriveBindingView("screen.header").HasTargetSubjectClaim);
        Assert.False(world.DeriveBindingView(null).HasTargetSubjectClaim);

        // ActionAssuranceView：revision 锚 + FreshnessBasis + 冲突存在性 fact
        var action = world.DeriveActionAssuranceView("screen.home");
        Assert.Equal(rev1.RevisionId, action.RevisionId);
        Assert.Equal(rev1.RevisionNumber, action.RevisionNumber);
        Assert.Equal(rev1.FreshnessBasis, action.FreshnessBasis);
        Assert.False(action.HasConflictOnTarget);
        Assert.False(world.DeriveActionAssuranceView("screen.header").HasConflictOnTarget);
        Assert.False(world.DeriveActionAssuranceView(null).HasConflictOnTarget);

        // 冲突 revision（screen.home idle→active）
        var second = kernel.Process(Observation("screen.home", "active", T1));   // rev-2
        var action2 = world.DeriveActionAssuranceView("screen.home");
        Assert.Equal("rev-2", action2.RevisionId);
        Assert.True(action2.HasConflictOnTarget);
        Assert.False(world.DeriveActionAssuranceView("screen.header").HasConflictOnTarget);

        // OutcomeAssuranceView：scoped claims / conflicts + 全量 basis + 总冲突计数
        var outcome = world.DeriveOutcomeAssuranceView(new[] { "screen.home", "" });
        Assert.Equal("rev-2", outcome.RevisionId);
        Assert.Equal(1, outcome.ConflictingClaimCount);            // 全 revision 冲突计数（非 scoped）
        var claim = Assert.Single(outcome.Claims);
        Assert.Equal("screen.home", claim.Key);                    // 空 subject 占位不入 scope
        Assert.Equal("idle", claim.Value.Value);                   // established 保留
        Assert.Equal(first.Admission.EvidenceId, claim.Value.EvidenceId);
        var conflict = Assert.Single(outcome.Conflicts);
        Assert.Equal("screen.home", conflict.Subject);
        Assert.Equal("active", conflict.ChallengingValue);
        Assert.Equal(second.Admission.EvidenceId, conflict.ChallengingEvidenceId);
        Assert.Equal(2, outcome.BasisEvidenceIds.Count);           // 全量 basis（proof buyer）
        Assert.Contains(second.Admission.EvidenceId!, outcome.BasisEvidenceIds);
    }

    // ---- N7：P5 收缩 —— Run State 退出 Control 输入 ------------------------

    [Fact]
    public void N7_P5RunStateDependencyRemovedFromControl()
    {
        // ControlInputs 输入二元组：ContractView + Slice（RunState 退出）
        Assert.Equal(
            new[] { "ContractView", "Slice" }.OrderBy(n => n),
            typeof(ControlInputs).GetProperties().Select(p => p.Name).OrderBy(n => n));

        // SelectIntent 只收 (ExecutionContractView, Slice)
        var select = typeof(ControlLoop).GetMethod(nameof(ControlLoop.SelectIntent))!;
        Assert.Equal(
            new[] { typeof(ExecutionContractView), typeof(Slice) },
            select.GetParameters().Select(p => p.ParameterType).ToArray());

        // 行为：intent 签发与 run 侧状态记录解耦（dependency 暂时消失，
        // P5 deferred；Run 记录面照常由 Kernel 编排）
        var (kernel, _, _, run, control, _, _) = NewKernel();
        kernel.AdmitContract(Contract());
        kernel.Process(Observation("screen.home", "idle", T0));
        var i1 = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var i2 = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        Assert.Equal(2, run.State!.Progress.Cycles);
        Assert.Equal(2, control.IntentLog.Count);
        Assert.All(new[] { i1, i2 }, i => Assert.Equal("rev-1", i.BasisRevisionId));
    }
}
