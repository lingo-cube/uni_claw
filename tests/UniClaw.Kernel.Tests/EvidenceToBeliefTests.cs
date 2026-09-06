using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// E2B-001 验收 1..8 —— 每条验收恰好一个用例。
/// 纯内存 fake world（level: DETERMINISTIC）；scripted Provider = 直接构造 ObservationRecord。
/// 测试验证行为，不验证实现细节。
/// </summary>
public sealed class EvidenceToBeliefTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 7, 10, 5, 0, TimeSpan.Zero);

    // ---- scripted Provider helpers -------------------------------------

    /// <summary>provenance 完整的观察（正路径输入）。</summary>
    private static ObservationRecord Observation(
        string subject, string value, DateTimeOffset captureTime, string producer = "provider.scripts") =>
        new(new ObservationClaim(subject, value),
            new Provenance(producer, captureTime, $"scope:{subject}", new[] { "raw://capture", "encode:v1" }));

    /// <summary>provenance 不完整的观察（fail-closed 输入）。</summary>
    private static ObservationRecord ObservationWithoutProducer(string subject, string value) =>
        new(new ObservationClaim(subject, value),
            new Provenance("", T0, $"scope:{subject}", new[] { "raw://capture" }));

    /// <summary>组装 kernel：relevance scope 只含 screen.home（其余 subject 判 irrelevant）。</summary>
    private static UniKernel NewKernel() =>
        new(new EvidenceLedger(), new WorldModel(new HashSet<string> { "screen.home" }));

    // ---- 验收 1：分离可观察 ---------------------------------------------

    [Fact]
    public void Accepted1_AdmissionAndRelevanceAreTwoIndependentArtifactsInOrder()
    {
        var kernel = NewKernel();

        var result = kernel.Process(Observation("screen.home", "visible", T0));

        // admission 与 relevance 是两个独立产出，accepted 输入必经两者
        Assert.NotNull(result.Admission);
        Assert.NotNull(result.Relevance);
        Assert.NotSame(result.Admission, result.Relevance);
        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        Assert.Equal(result.Admission.EvidenceId, result.Relevance.EvidenceId);

        // 各自独立留痕：ledger 记 admission，world model 记 relevance
        // （次序不可合并：rejected 输入不得产生 relevance —— 见验收 4 用例）
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var k2 = new UniKernel(ledger, world);
        k2.Process(Observation("screen.home", "visible", T0));
        Assert.Single(ledger.AdmissionLog);
        Assert.Single(world.RelevanceLog);
    }

    // ---- 验收 2：relevant accepted → 恰好一个新 immutable revision ------

    [Fact]
    public void Accepted2_RelevantAcceptedEvidenceProducesExactlyOneNewRevision()
    {
        var kernel = NewKernel();

        var result = kernel.Process(Observation("screen.home", "visible", T0));

        Assert.NotNull(result.ResultingRevision);
        var revision = result.ResultingRevision!;
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Null(revision.ParentRevisionId);
        // evidence basis 引用该 record
        Assert.Contains(result.Admission.EvidenceId!, revision.EvidenceBasis);
        // 恰好一个：history 由 0 → 1
        Assert.Single(kernel.CurrentBelief!.EvidenceBasis);
    }

    // ---- 验收 3：irrelevant accepted → 保留 record，零 revision ----------

    [Fact]
    public void Accepted3_IrrelevantAcceptedEvidenceKeepsRecordWithoutRevision()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var kernel = new UniKernel(ledger, world);

        var result = kernel.Process(Observation("device.rotation", "90", T0)); // out of scope

        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        Assert.NotNull(result.Relevance);
        Assert.False(result.Relevance!.IsRelevant);
        // canonical record 保留
        Assert.Single(ledger.CanonicalRecords);
        // 零 revision
        Assert.Null(result.ResultingRevision);
        Assert.Null(world.Current);
        Assert.Empty(world.RevisionHistory);
    }

    // ---- 验收 4：fail-closed ---------------------------------------------

    [Fact]
    public void Accepted4_IncompleteProvenanceIsRejectedWithZeroSideEffects()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var kernel = new UniKernel(ledger, world);

        var result = kernel.Process(ObservationWithoutProducer("screen.home", "visible"));

        // rejected Admission Record，且逐项检查留痕包含失败项
        Assert.Equal(AdmissionDecision.Rejected, result.Admission.Decision);
        Assert.Contains(result.Admission.Checks, c => !c.Passed);

        // 不产生 canonical Evidence Record
        Assert.Null(result.Admission.EvidenceId);
        Assert.Empty(ledger.CanonicalRecords);

        // 不进入 Relevance / Reconciliation，不修改 Belief
        Assert.Null(result.Relevance);
        Assert.Null(result.ResultingRevision);
        Assert.Empty(world.RelevanceLog);
        Assert.Empty(world.RevisionHistory);
        Assert.Null(world.Current);
    }

    // ---- 验收 5：conflict 显式表达，不静默覆盖 ---------------------------

    [Fact]
    public void Accepted5_ConflictingRelevantEvidenceProducesExplicitConflictRevision()
    {
        var kernel = NewKernel();
        var r1 = kernel.Process(Observation("screen.home", "visible", T0));
        var e1 = r1.Admission.EvidenceId!;

        var r2 = kernel.Process(Observation("screen.home", "hidden", T1));
        var e2 = r2.Admission.EvidenceId!;

        var revision = r2.ResultingRevision!;
        Assert.Equal(r1.ResultingRevision!.RevisionId, revision.ParentRevisionId);
        // 显式表达 conflict 的新 revision，不静默覆盖
        var conflict = Assert.Single(revision.Conflicts);
        Assert.Equal("screen.home", conflict.Subject);
        Assert.Equal("visible", conflict.EstablishedValue);
        Assert.Equal("hidden", conflict.ChallengingValue);
        Assert.Equal(e1, conflict.EstablishedEvidenceId);
        Assert.Equal(e2, conflict.ChallengingEvidenceId);
        Assert.Equal("visible", revision.WorldState["screen.home"].Value); // 未被覆盖
        Assert.Equal(e1, revision.WorldState["screen.home"].EvidenceId);   // 既存值仍溯源 e1
        Assert.Equal(1, revision.Uncertainty.ConflictingClaimCount);
    }

    // ---- 验收 6：派生失效（无显式 invalidation event） -------------------

    [Fact]
    public void Accepted6_SliceValidityIsDerivedFromSourceRevisionNotAnEvent()
    {
        var kernel = NewKernel();
        kernel.Process(Observation("screen.home", "visible", T0));

        var slice = kernel.DeriveSlice("screen.home");
        Assert.True(kernel.IsSliceValid(slice));
        Assert.Equal("visible", slice.Projection["screen.home"]);

        // 新 revision 取代后：旧 revision 成为历史，旧 Slice 失效 —— 纯派生判定
        kernel.Process(Observation("screen.home", "hidden", T1));
        Assert.False(kernel.IsSliceValid(slice)); // 无任何 invalidation 调用，仅 current 变化

        var fresh = kernel.DeriveSlice("screen.home");
        Assert.True(kernel.IsSliceValid(fresh));

        // 契约：Slice 表面不存在显式 invalidation event 机制
        foreach (var member in typeof(Slice).GetMembers(BindingFlags.Public | BindingFlags.Instance))
            Assert.False(member.Name.Contains("Invalidat", StringComparison.OrdinalIgnoreCase)
                      || member.Name.Contains("Event", StringComparison.OrdinalIgnoreCase),
                $"Slice 不得携带显式 invalidation event 成员：{member.Name}");
    }

    // ---- 验收 7：plan / expectation 无路径进入 Reconciliation -----------

    [Fact]
    public void Accepted7_PlanOrExpectationHasNoPathIntoReconciliation()
    {
        // Reconciliation 输入封闭于 evidence 域：Reconcile 只接受
        // EvidenceRecord / RelevanceJudgment；WorldModel 全部公共方法签名
        // 不出现 plan / expectation / goal / hypothesis / intent 类型。
        var forbidden = new[] { "plan", "expectation", "goal", "hypothesis", "intent" };

        foreach (var method in typeof(WorldModel).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var p in method.GetParameters())
            {
                var typeName = p.ParameterType.Name.ToLowerInvariant();
                Assert.False(forbidden.Any(f => typeName.Contains(f)),
                    $"{method.Name} 的参数 {p.Name}:{p.ParameterType.Name} 打开了 plan/expectation 进入路径");
            }
        }

        var reconcile = typeof(WorldModel).GetMethod(nameof(WorldModel.Reconcile))!;
        var paramTypes = reconcile.GetParameters().Select(p => p.ParameterType).ToHashSet();
        Assert.Superset(
            new HashSet<Type> { typeof(EvidenceRecord), typeof(RelevanceJudgment) },
            paramTypes);
    }

    // ---- 验收 8：幂等 ----------------------------------------------------

    [Fact]
    public void Accepted8_ResubmittingSameCanonicalRecordDoesNotDuplicateRevision()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var kernel = new UniKernel(ledger, world);
        var observation = Observation("screen.home", "visible", T0);

        var first = kernel.Process(observation);
        var second = kernel.Process(observation);

        // 同一 canonical Evidence Record：内容哈希一致，ledger 不重复建 record
        Assert.Equal(first.Admission.EvidenceId, second.Admission.EvidenceId);
        Assert.Single(ledger.CanonicalRecords);

        // 恰好一个 revision：第二次提交不重复产生
        Assert.Single(world.RevisionHistory);
        Assert.Null(second.ResultingRevision);
    }
}
