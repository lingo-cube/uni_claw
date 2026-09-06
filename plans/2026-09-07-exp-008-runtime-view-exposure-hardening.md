# Plan — EXP-008 Runtime View Exposure Hardening

> PlanType: exp-008-runtime-view-exposure-hardening / Status: ADOPTED /
> References: changes/EXP-008/state.md · ADR-0011 · 协议基线 P5/P11 ·
> commit 79519a43

## 垂直切片：三 view + Owner 派生 + 消费者签名收缩，一次闭环

```text
World Model（Owner，表达面扩展）
  DeriveBindingView(subject)        → BindingView
  DeriveActionAssuranceView(target) → ActionAssuranceView
  DeriveOutcomeAssuranceView(subjects) → OutcomeAssuranceView
  （每次消费前从 Current 即时派生；无 current → fail-closed）
        ↓ one operation
Effect Boundary（Bind / IsBindingValid / Dispatch ← BindingView）
Assurance Judge（← ActionAssuranceView）
Assurance EvaluateObligations / JudgeOutcome（← OutcomeAssuranceView）
        ↓ 判定权不动：四态拒绝 / checks 从 Owner fact 推出

Control（P5）：SelectIntent(view, slice)——RunState 移除
```

正交不变量：view 只携带 Owner-owned fact（D4）；check 名与判定语义
零漂移（CBA-005 D3 检查集、FRS-007 三态）；E2B 冻结面零触碰。

## Before / After

### Before

- `ControlLoop.SelectIntent(view, slice, runState)`：runState 整传、
  零字段消费；`ControlInputs` 同病（ContractView 三重暴露）
- `EffectBoundary.Bind/IsBindingValid/Dispatch(intent|binding, …,
  WorldBeliefRevision current)`：整聚合入签名，六成员 carried-but-
  never-read（WorldGraph / EvidenceBasis / conflicts / uncertainty /
  freshness / parent）
- `RuntimeAssurance.Judge(intent, binding, view, WorldBeliefRevision)` /
  `EvaluateObligations / JudgeOutcome(…, WorldBeliefRevision)`：同一
  整聚合喂两条不相交消费画像

### After

| 文件 | 变化 | Owner |
|---|---|---|
| `World/ConsumerViews.cs`（新） | `ScopedClaim(string Value, string EvidenceId)` · `BindingView(string RevisionId, int RevisionNumber, bool HasTargetSubjectClaim)` · `ActionAssuranceView(string RevisionId, int RevisionNumber, FreshnessBasis FreshnessBasis, bool HasConflictOnTarget)` · `OutcomeAssuranceView(string RevisionId, int ConflictingClaimCount, IReadOnlyDictionary<string, ScopedClaim> Claims, IReadOnlyList<Conflict> Conflicts, IReadOnlySet<string> BasisEvidenceIds)`；doc 注明 ADR-0011 语义（immutable / ephemeral / Owner fact only） | World Model |
| `World/WorldModel.cs` | + 三个具名派生方法（scope：candidate subject / intent target（null→无冲突 fact）/ obligation subjects）；从 `Current` 派生，无 current → throw（镜像 DeriveSlice）；`Reconcile` / `DeriveSlice` / `IsSliceValid` 零改动 | World Model |
| `Control/ControlLoop.cs` | `SelectIntent(view, slice)`——RunState 参数与 null 检查移除；其余零改动 | Control |
| `Control/ControlPolicy.cs` | `ControlInputs(ExecutionContractView ContractView, Slice Slice)` | Control |
| `Effects/EffectBoundary.cs` | `Bind(intent, candidate, BindingView)`（stale: candidate.SourceRevisionId != view.RevisionId；UnknownTarget: !view.HasTargetSubjectClaim；CanonicalBinding 锚 view.RevisionId/RevisionNumber）· `IsBindingValid(binding, BindingView)` · `Dispatch(binding, judgment, BindingView)`（binding-stale: binding.RevisionId != view.RevisionId）；四态拒绝与 gate 逻辑零语义改动 | Effect Boundary |
| `Assurance/RuntimeAssurance.cs` | `Judge(intent, binding, contractView, ActionAssuranceView)`（freshness 输入 = view.FreshnessBasis + view.RevisionId；no-unresolved-conflict = target null ∨ !view.HasConflictOnTarget；no-blind-retry = view.RevisionNumber > failedAt；currentness 两查 = ×.RevisionId vs view.RevisionId）· `EvaluateObligations(obligations, OutcomeAssuranceView, canonical)`（claim 查找走 view.Claims；membership 走 view.BasisEvidenceIds；conflicts 走 view.Conflicts）· `JudgeOutcome(contractView, obligations, OutcomeAssuranceView, canonical)`（proof id / BasisEvidenceIds / UnresolvedUncertainty 走 view）；check 名集合零改动 | Assurance |
| `UniKernel.cs` | `SelectIntent`：去掉 runState 取用（IsTerminal / View 门保留）· `Act`：Bind 前 `DeriveBindingView(candidate?.TargetSubject)`（同实例传 Dispatch）+ Judge 前 `DeriveActionAssuranceView(intent.TargetSubject)` · `EvaluateTerminal`：`DeriveOutcomeAssuranceView(state.ProofObligations.Obligations.Select(o => o.Subject))` | 组合缝 |
| tests | 新增 `RuntimeViewExposureTests`（下表）+ seam 直调机械迁移（台账见下） | —— |

## 关键语义（固化，防漂移）

1. **判定权不动**：四态拒绝 / no-unresolved-conflict / no-blind-retry /
   gate 全部在 consumer 内从 view fact 推出；check 名、RejectionReason
   惯例（首失败项）、gate reason 词汇零改动。
2. **无 buyer 不加字段**：三个 view 字段集 = D6/D7/D8 白名单，实现时
   不得"顺手"补字段（allowlist 测试即执法）。
3. **不偷解 Deferred**：无 currentness validation API、无 view 缓存、
   无 Session/correlation、无 L4 / WorldGraph schema 触碰。
4. **P5 移除不等于改 Run 语义**：RunModel / RunState / typed
   transitions 零改动；UniKernel terminal 门（IsTerminal）语义不变。
5. **E2B 零触碰预期**：EvidenceToBeliefTests 零 diff；若实现中发现
   触碰 → 回 Leader 显式解冻，不得静默改断言。

## TDD 次序（RED → GREEN → REVIEW → VERIFY）

1. RED（`RuntimeViewExposureTests` 先行 + 目标签名 stub）：
   - N1 shape allowlist：三 view public member 集合精确等于白名单
     （多/少任一成员即失败）
   - N2 负向结构：三 view 无 WorldBeliefRevision / RunState /
     WorldGraph 型别成员、无 mutable collections（IList/IDictionary/
     ISet 可写接口）
   - N3 签名零 aggregate：ControlLoop 公开签名无 RunState 参数；
     RuntimeAssurance / EffectBoundary 公开签名无 WorldBeliefRevision
     参数
   - N4 无持久持有：ControlLoop / RuntimeAssurance / EffectBoundary
     实例字段（含 private）无三 view 型别、无元素为三 view 的
     collection 型别
   - N5 correlation mismatch（stale view）：world 推进 rev-2 后，用
     rev-1 的 BindingView + rev-2 candidate/binding → Bind
     stale-revision / Judge binding-revision-currentness / Dispatch
     binding-stale 各面 fail-closed
   - N6 fact 派生正确性：HasTargetSubjectClaim（有/无 claim × graph
    -only subject 对照）、HasConflictOnTarget（冲突/无冲突/异 subject
     对照）、ScopedClaim/basis/uncertainty scoped 派生
   - N7 P5 收缩：SelectIntent(view, slice) 编译契约 + 反射（N3 已含）
     + 既有 C2E/TOT SelectIntent 用例迁移后行为不变
2. GREEN：按 After 表最小实现 + seam 机械迁移
3. REVIEW：fresh SubAgent（轴：白名单无越界字段 / 判定权未移动 /
   ephemeral 未被破坏（无缓存/字段持久化）/ Deferred 未偷解 / E2B
   零触碰 / 迁移台账属实 / 意外改动）
4. VERIFY：全量 dotnet test（≥70）；验收 10 条逐条；grep：Control
   命名空间零 `RunState`、Assurance/Effects 公开签名零
   `WorldBeliefRevision`；基线 P5/P11 改写 + Conflict 升格注记 + §5
   清单更新；CONTEXT/ADR 一致性核对；evidence 落盘

## 验收 ↔ 用例映射

| 验收 | 用例 |
|---|---|
| 1 Control 零 RunState | N3 + N7（迁移后 GREEN） |
| 2 EB 零 aggregate | N3 + C2E 直调迁移 GREEN |
| 3 Assurance 零 aggregate | N3 + TOT/ING 直调迁移 GREEN |
| 4 shape allowlist | N1 + N2 |
| 5 ephemeral 证明 | N5 + N4 |
| 6 CBA-005 回归 | C2E 全量迁移 GREEN（Bind→Judge→Gate 序 + 三元组断言原样） |
| 7 FRS-007 回归 | FreshnessEnforcementTests 迁移后 GREEN（Scenario 14 原样；FreshnessBasis 经 view） |
| 8 上层回归 | OUT/ING/GEV GREEN；E2B 零 diff；Agent 项目零改动（git diff 证） |
| 9 全量 GREEN | dotnet test ≥70 + 迁移台账核对 |
| 10 文档同步 | VERIFY 文档核对（基线改写 / ADR-0011 / CONTEXT.md） |

## 迁移台账（VERIFY 时按 grep 实测校正）

```text
签名迁移（seam 直调，行为断言不变；grep 实测 27 处 derive 调用点 + 1 处实参移除）：
  ControlToEffectTests（19 处）：IsBindingValid ×3（:149 e2. 前缀 + :210/:216）；
    Accepted4 Bind ×1 + Dispatch ×4；Accepted10 Bind+Judge ×2；Scenario13
    case1/case2 各 Bind+Judge+Dispatch ×6；产者侧 correlation
    Bind+Judge+Judge(null) ×3
    （world.Current! → world.DeriveBindingView / DeriveActionAssuranceView）
  TerminalOutcomeTests（7 处）：DeriveOutcomeAssuranceView ×3
    （EvaluateObligations ×2 + JudgeOutcome ×1）；DeriveBindingView ×3
    （Bind ×1 + Dispatch ×2）；SelectIntent 直调 ×1（:435 去 run.State!）
  FreshnessEnforcementTests（1 处）：IsBindingValid ×1
  ObservationIngressTests（1 处）：EvaluateObligations ×1
新增：RuntimeViewExposureTests N1-N7
删除：无（全部迁移，无静默删除）
零改动：E2B 8 断言（EvidenceToBeliefTests 零 diff）、UniKernel 公开
  API 签名（SelectIntent(slice)/Act/EvaluateTerminal）、RunModel /
  RunState / WorldModel 既有方法、Agent 层全部
（草稿估算 C2E 24 处，实测 19 处：Bind×5 / Dispatch×6 / Judge×5 /
  IsBindingValid×3；已校正。）
```
