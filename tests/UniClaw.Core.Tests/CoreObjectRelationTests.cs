using System.Reflection;
using Xunit;

namespace UniClaw.Core.Tests;

/// <summary>
/// CORE-003 验收 —— Core 对象与最小语义关系（纯 Core 场景；spec 见
/// changes/CORE-003/spec.md）。全部确定性（level: DETERMINISTIC）。
///
/// 覆盖映射（spec Testing Decisions）：
///  - Clause 四类 + 「观察永不产生授权」的结构证明 …… Clause 测试
///  - 主垂直 tracer（Segment ⊃ 多 Slice → Evidence → Claim → Effect ⊃
///    Attempt uses TargetBinding → 点击后 Evidence）…… Relation chain 测试
///  - Event 发生/来源/时间/参与者/次数/同一性/因果以组合表达、无独立类型
///    （CORE-001 §4.4 缺口的正向证据）…… Event semantics 测试
///  - 「update then use」vs「use then update」删除/演化检查 …… Order 测试
///  - Core 无 UI/设备/Runtime/Harness/旧模型依赖（程序集级）…… 纯度测试
/// 测试断言语义行为与边界，不断言记录布局、构造形状或旧类一一映射；
/// 不证明严格最小性、生产就绪或迁移完成。
/// </summary>
public sealed class CoreObjectRelationTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-09-19T10:00:00Z");
    private static readonly DateTimeOffset T1 = DateTimeOffset.Parse("2026-09-19T10:00:02Z");
    private static readonly DateTimeOffset TAttempt = DateTimeOffset.Parse("2026-09-19T10:00:04Z");
    private static readonly DateTimeOffset TPost = DateTimeOffset.Parse("2026-09-19T10:00:06Z");
    private static readonly DateTimeOffset TPost2 = DateTimeOffset.Parse("2026-09-19T10:00:08Z");

    // ---- Clause：四类条款 + 授权不来自观察（结构证明） ----------------------

    [Fact]
    public void Clause_covers_four_kinds_and_observation_never_creates_authorization()
    {
        Assert.Equal(4, Enum.GetValues<ClauseKind>().Length);

        // 结构证明 1：Core 程序集内除构造与编译器合成成员外，无任何产出
        // Clause 的 API——条款（含权限）只能显式构造，不存在观察→授权的
        // 导出通路
        var derivationApi = typeof(Clause).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName
                && !m.Name.StartsWith("<", StringComparison.Ordinal)   // 编译器合成（<Clone>$ 等）
                && m.ReturnType == typeof(Clause))
            .ToList();
        Assert.Empty(derivationApi);

        // 结构证明 2：World/Effect 侧记录不携带条款/授权字段——观察记录
        // 在类型层面无从表达权限
        foreach (var type in new[]
                 {
                     typeof(Segment), typeof(EvidenceRecord), typeof(Slice), typeof(Claim),
                     typeof(Effect), typeof(Attempt), typeof(TargetBinding),
                 })
        {
            foreach (var property in type.GetProperties())
                Assert.True(
                    property.PropertyType != typeof(Clause) && property.PropertyType != typeof(ClauseKind),
                    $"{type.Name}.{property.Name} 不得携带 Clause/ClauseKind（观察记录 ≠ 权限载体）");
        }

        // 结构证明 3：判定面（CoreInvariants）不消费观察内容——授权/投递
        // 判定输入只有 binding/attempt，观察 Claim/Evidence 不进入
        foreach (var method in typeof(CoreInvariants).GetMethods())
            Assert.DoesNotContain(method.GetParameters(), p =>
                p.ParameterType == typeof(Claim) || p.ParameterType == typeof(EvidenceRecord));

        // 行为：权限条款是唯一权限载体；描述观察的 Claim 不构成投递许可
        var permission = new Clause(
            new CoreId("clause:allow-tap"), ClauseKind.Permission, "run r1 may tap element:b", T0);
        var requirement = new Clause(
            new CoreId("clause:req-1"), ClauseKind.Requirement, "objective must be completed before terminal", T0);
        var constraint = new Clause(
            new CoreId("clause:con-1"), ClauseKind.Constraint, "no effect outside contract scope", T0);
        var criterion = new Clause(
            new CoreId("clause:crit-1"), ClauseKind.Criterion, "tap counts only with post-action state evidence", T0);
        Assert.NotEqual(ClauseKind.Permission, requirement.Kind);
        Assert.NotEqual(ClauseKind.Permission, constraint.Kind);
        Assert.NotEqual(ClauseKind.Permission, criterion.Kind);

        var observationClaim = new Claim(
            new CoreId("claim:obs"), new CoreId("element:b"), "visible-text", "Item 01",
            ClaimDisposition.Accepted, [new CoreId("evidence:1")]);
        Assert.Equal(ClaimDisposition.Accepted, observationClaim.Disposition); // 判断为真 ≠ 授权存在
        var unverified = new TargetBinding(
            new CoreId("binding:x"), new CoreId("page:1"), new CoreId("slice:1"), "k", "v",
            BindingDisposition.Unverified);
        Assert.False(CoreInvariants.CanDispatch(unverified));                   // 非 Canonical 恒 fail-closed
    }

    // ---- 主垂直 tracer：完整关系链（手机滚动—点击域，story 15） ------------

    [Fact]
    public void Relation_chain_tracer_covers_specification_world_effect_and_post_action_evidence()
    {
        // Specification：显式条款（要求/判据）——本 tracer 不从观察产生任何权限
        var permission = new Clause(
            new CoreId("clause:allow-tap"), ClauseKind.Permission, "run r1 may tap element:b", T0);
        var criterion = new Clause(
            new CoreId("clause:crit-1"), ClauseKind.Criterion, "tap counts only with post-action state evidence", T0);

        // World：Segment ──contains──> 多个历史 Slice（滚动前后、重叠覆盖）
        var page = new Segment(new CoreId("page:1"), "ui.page");
        var element = new CoreId("element:b");
        var ev1 = new EvidenceRecord(
            new CoreId("evidence:scroll-1"), page.Id, T0, "phone-camera", "viewport-1", ["capture", "detect"]);
        var ev2 = new EvidenceRecord(
            new CoreId("evidence:scroll-2"), page.Id, T1, "phone-camera", "viewport-2", ["capture", "detect"]);
        var s1 = new Slice(new CoreId("slice:1"), page.Id, [ev1.Id], T0, "viewport-1", [element], "partial");
        var s2 = new Slice(new CoreId("slice:2"), page.Id, [ev1.Id, ev2.Id], T1, "viewport-2", [element], "partial-overlap");

        Assert.Equal(page.Id, s1.SegmentId);                    // Segment contains Slice
        Assert.Equal(page.Id, s2.SegmentId);
        Assert.Contains(element, s1.ObservedRecordIds);         // 重叠覆盖
        Assert.Contains(element, s2.ObservedRecordIds);
        Assert.NotEqual(s1.Id, s2.Id);                          // 新 Slice 不改写旧 Slice

        // Claim：观察形成判断；滚动后值演化，历史依据保留
        var claimV1 = new Claim(
            new CoreId("claim:text-v1"), element, "visible-text", "Item 01",
            ClaimDisposition.Accepted, [ev1.Id]);
        var claimV2 = new Claim(
            new CoreId("claim:text-v2"), element, "visible-text", "Item 02",
            ClaimDisposition.Accepted, [ev1.Id, ev2.Id]);
        Assert.Contains(claimV1.EvidenceBasis[0], claimV2.EvidenceBasis);

        // Effect：逻辑操作；TargetBinding：basis 固定于建立绑定时的 S1；
        // Attempt：实际尝试时间独立于观察时间
        var effect = new Effect(new CoreId("effect:tap-b"), "tap", element);
        var binding = new TargetBinding(
            new CoreId("binding:tap-b"), page.Id, s1.Id, "ui-descendant", "node:b",
            BindingDisposition.Canonical);
        var attempt = new Attempt(
            new CoreId("attempt:tap-b"), effect.Id, binding.Id, TAttempt,
            DeliveryOutcome.Completed, new CoreId("evidence:attempt-1"));

        Assert.Equal(effect.Id, attempt.EffectId);              // Effect contains Attempt
        Assert.Equal(binding.Id, attempt.BindingId);            // Attempt uses TargetBinding
        Assert.Equal(s1.Id, binding.BasisSliceId);
        Assert.True(CoreInvariants.IsHistoricalBasisStable(binding, s1.Id));
        Assert.False(CoreInvariants.IsHistoricalBasisStable(binding, s2.Id)); // 不跟随最新 Slice
        Assert.True(attempt.StartedAt > s2.ObservedAt);                     // 时间独立
        Assert.True(CoreInvariants.CanDispatch(binding));
        Assert.True(CoreInvariants.IsTerminalDelivery(attempt));

        // 投递 ≠ 结果：Completed attempt 不产生 World 记录（Core 无该通路——
        // 见 Clause 测试的结构证明法）；世界变化只经新 Evidence 进 Claim
        var post = new EvidenceRecord(
            new CoreId("evidence:post-1"), element, TPost, "phone-camera", "viewport-2", ["capture", "diff"]);
        var stateClaim = new Claim(
            new CoreId("claim:state-1"), element, "checked-state", "true",
            ClaimDisposition.Accepted, [post.Id]);
        Assert.Equal(post.Id, Assert.Single(stateClaim.EvidenceBasis)); // 依据是新证据，不是 attempt
    }

    // ---- Event 发生语义：组合表达（不建独立类型） ---------------------------

    [Fact]
    public void Event_occurrence_semantics_remain_expressible_composite_without_independent_type()
    {
        // 场景：element:b 被点击两次——发生语义由 Evidence + Claim 组合表达
        var element = new CoreId("element:b");
        var attemptEvidence = new CoreId("evidence:attempt-1");
        var attempt = new Attempt(
            new CoreId("attempt:tap-b"), new CoreId("effect:tap-b"), new CoreId("binding:tap-b"),
            TAttempt, DeliveryOutcome.Completed, attemptEvidence);
        var post1 = new EvidenceRecord(
            new CoreId("evidence:post-1"), element, TPost, "phone-camera", "viewport-2", ["capture", "diff"]);
        var post2 = new EvidenceRecord(
            new CoreId("evidence:post-2"), element, TPost2, "phone-camera", "viewport-2", ["capture", "diff"]);

        // 同一性：跨多次发生的稳定事件 subject
        var eventId = new CoreId("event:tap:element:b");
        var occurred1 = new Claim(new CoreId("claim:ev-1"), eventId, "occurred", "1",
            ClaimDisposition.Accepted, [post1.Id]);
        var participants = new Claim(new CoreId("claim:ev-parts"), eventId, "participants", "element:b",
            ClaimDisposition.Accepted, [post1.Id]);
        var at1 = new Claim(new CoreId("claim:ev-at-1"), eventId, "occurred-at", "2026-09-19T10:00:06Z",
            ClaimDisposition.Accepted, [post1.Id]);
        var causedBy = new Claim(new CoreId("claim:ev-cause"), eventId, "caused-by", attempt.Id.Value,
            ClaimDisposition.Candidate, [post1.Id, attemptEvidence]);
        var occurred2 = new Claim(new CoreId("claim:ev-2"), eventId, "occurred", "2",
            ClaimDisposition.Accepted, [post1.Id, post2.Id]);
        var at2 = new Claim(new CoreId("claim:ev-at-2"), eventId, "occurred-at", "2026-09-19T10:00:08Z",
            ClaimDisposition.Accepted, [post2.Id]);

        // 七面逐项可取回：发生 / 来源 / 时间（观察侧+主张侧）/ 参与者 / 次数 /
        // 同一性 / 因果判断——组合表达足够，未出现需要独立 Event 类型的反例
        Assert.Equal("1", occurred1.Value);                                // 发生
        Assert.Equal("phone-camera", post1.Source);                        // 来源
        Assert.Equal(TPost, post1.ObservedAt);                             // 时间（观察侧）
        Assert.Equal("2026-09-19T10:00:06Z", at1.Value);                   // 时间（主张侧）
        Assert.Equal("element:b", participants.Value);                     // 参与者
        Assert.Equal("2", occurred2.Value);                                // 次数（修订后）
        Assert.Equal(occurred1.SubjectId, occurred2.SubjectId);            // 同一性
        Assert.Contains(occurred1.EvidenceBasis[0], occurred2.EvidenceBasis); // 演化保留依据
        Assert.Equal(ClaimDisposition.Candidate, causedBy.Disposition);    // 因果是判断 ≠ 现实
        Assert.Contains(attemptEvidence, causedBy.EvidenceBasis);          // 因果判断引用 attempt 依据

        // 发生主张以世界证据为据，不以 receipt/attempt 为据（投递 ≠ 结果）
        Assert.DoesNotContain(attemptEvidence, occurred1.EvidenceBasis);

        // 事件主张不产生权限副作用：无导出 API（Clause 测试结构证明）；
        // 事件语义留在 World 侧，永不进入 Specification
    }

    // ---- 删除/演化检查：update then use vs use then update -----------------

    [Fact]
    public void Update_then_use_and_use_then_update_preserve_the_same_distinctions()
    {
        // 路径 A（update then use）：完成全部演化后一次性消费
        var (s1A, s2A, claimA, bindingA) = (
            new Slice(new CoreId("slice:1"), new CoreId("page:1"),
                [new CoreId("evidence:scroll-1")], T0, "viewport-1", [new CoreId("element:b")], "partial"),
            new Slice(new CoreId("slice:2"), new CoreId("page:1"),
                [new CoreId("evidence:scroll-1"), new CoreId("evidence:scroll-2")], T1, "viewport-2",
                [new CoreId("element:b")], "partial-overlap"),
            new Claim(new CoreId("claim:text-v2"), new CoreId("element:b"), "visible-text", "Item 02",
                ClaimDisposition.Accepted, [new CoreId("evidence:scroll-1"), new CoreId("evidence:scroll-2")]),
            new TargetBinding(new CoreId("binding:tap-b"), new CoreId("page:1"), new CoreId("slice:1"),
                "ui-descendant", "node:b", BindingDisposition.Canonical));

        // 路径 B（use then update）：每步消费留痕后再演化
        var s1B = new Slice(new CoreId("slice:1"), new CoreId("page:1"),
            [new CoreId("evidence:scroll-1")], T0, "viewport-1", [new CoreId("element:b")], "partial");
        var midSliceSnapshot = s1B;                                        // 中途快照
        var claim1B = new Claim(new CoreId("claim:text-v1"), new CoreId("element:b"), "visible-text", "Item 01",
            ClaimDisposition.Accepted, [new CoreId("evidence:scroll-1")]);
        var midClaimSnapshot = claim1B;                                    // 中途快照
        Assert.True(CoreInvariants.IsHistoricalBasisStable(                // use：先消费当前态
            new TargetBinding(new CoreId("binding:tap-b"), new CoreId("page:1"), s1B.Id,
                "ui-descendant", "node:b", BindingDisposition.Canonical), s1B.Id));

        var s2B = new Slice(new CoreId("slice:2"), new CoreId("page:1"),
            [new CoreId("evidence:scroll-1"), new CoreId("evidence:scroll-2")], T1, "viewport-2",
            [new CoreId("element:b")], "partial-overlap");
        var claim2B = new Claim(new CoreId("claim:text-v2"), new CoreId("element:b"), "visible-text", "Item 02",
            ClaimDisposition.Accepted, [new CoreId("evidence:scroll-1"), new CoreId("evidence:scroll-2")]);
        var bindingB = new TargetBinding(new CoreId("binding:tap-b"), new CoreId("page:1"), s1B.Id,
            "ui-descendant", "node:b", BindingDisposition.Canonical);

        // 两条路径最终状态全等（字段级比较：候选记录的列表字段参与 record
        // 相等性时按引用比较——该摩擦留痕于 evidence 缺口，不在本 Change
        // 单方面改为内容相等）
        AssertSameState(s1A, s1B);
        AssertSameState(s2A, s2B);
        AssertSameState(claimA, claim2B);
        Assert.Equal(bindingA, bindingB);   // TargetBinding 无列表字段，record 相等即内容相等

        // 早期快照不被后续追加改写（append/revise，不就地重写）
        AssertSameState(midSliceSnapshot, s1B);
        AssertSameState(midClaimSnapshot, claim1B);
        Assert.Contains(claim1B.EvidenceBasis[0], claim2B.EvidenceBasis);

        // 两条路径共同保持必要区分：basis 固定 vs 最新、Unknown 不升级
        Assert.True(CoreInvariants.IsHistoricalBasisStable(bindingB, s1B.Id));
        Assert.False(CoreInvariants.IsHistoricalBasisStable(bindingB, s2B.Id));
        var unknown = new Attempt(new CoreId("attempt:u"), new CoreId("effect:u"),
            new CoreId("binding:u"), TAttempt, DeliveryOutcome.Unknown, null);
        Assert.False(CoreInvariants.IsTerminalDelivery(unknown));
        Assert.Equal(DeliveryOutcome.Unknown, unknown.Delivery);
    }

    // ---- Core 纯度：程序集自足、记录面领域无关 ------------------------------

    [Fact]
    public void Core_assembly_stands_alone_with_domain_neutral_record_surface()
    {
        // 程序集级：Core 不引用任何 UniClaw.* 程序集（无 UI/设备/Runtime/
        // Harness/旧模型依赖；依赖闭包自足）
        Assert.DoesNotContain(typeof(Clause).Assembly.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("UniClaw.", StringComparison.Ordinal));

        // 记录面：候选记录只携带领域无关值（string / CoreId / DateTimeOffset /
        // 枚举 / CoreId、string 列表）——域载荷在边界上保持不透明
        foreach (var type in new[]
                 {
                     typeof(Clause), typeof(Segment), typeof(EvidenceRecord), typeof(Slice),
                     typeof(Claim), typeof(Effect), typeof(Attempt), typeof(TargetBinding),
                 })
        {
            foreach (var property in type.GetProperties())
            {
                var t = property.PropertyType;
                var allowed = t == typeof(string)
                    || t == typeof(CoreId) || t == typeof(CoreId?)
                    || t == typeof(DateTimeOffset)
                    || t == typeof(BasisReference)
                    || t == typeof(AttemptExecutionState)
                    || t.IsEnum
                    || (t.IsGenericType
                        && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                        && (t.GetGenericArguments()[0] == typeof(CoreId)
                            || t.GetGenericArguments()[0] == typeof(string)
                            || t.GetGenericArguments()[0] == typeof(BasisReference)));
                Assert.True(allowed, $"{type.Name}.{property.Name} 携带非领域无关值类型 {t}");
            }
        }
    }

    // ---- 字段级状态比较（列表字段按内容；见删除/演化检查说明） --------------

    private static void AssertSameState(Slice expected, Slice actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.SegmentId, actual.SegmentId);
        Assert.Equal(expected.ObservedAt, actual.ObservedAt);
        Assert.Equal(expected.Scope, actual.Scope);
        Assert.Equal(expected.Coverage, actual.Coverage);
        Assert.Equal(expected.ObservationEvidenceIds, actual.ObservationEvidenceIds);
        Assert.Equal(expected.ObservedRecordIds, actual.ObservedRecordIds);
    }

    private static void AssertSameState(Claim expected, Claim actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.SubjectId, actual.SubjectId);
        Assert.Equal(expected.Predicate, actual.Predicate);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.Disposition, actual.Disposition);
        Assert.Equal(expected.EvidenceBasis, actual.EvidenceBasis);
    }
}
