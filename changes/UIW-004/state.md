# UIW-004 — P23 缝与消费面实现（ResolveCurrent / GroundingView / Slice 重构 / binding target shape 切换）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: c6e6f1d7

## Intent（WHAT/WHY）
把 UIW-002 冻结的消费面语义落进代码：P23 单缝双模的 ResolveCurrent 出面
（GroundingView，ADR-0011 族）、Slice 重构（scope 锚定 RootContainerIdentity，
occurrence 景观 + ScopedClaim 分区）、binding target shape 切换（UI = 已解析
occurrence 引用，恒绑 CurrentOccurrenceRef；UI 字符串寻址退役，非 UI 字符串
通道兼容保留）。这是**破坏性迁移 change**：旧测试按「保护架构事实而非旧测试
本身」原则迁移（CBA-005 先例）。

## Scope
- `World/`：TargetDescriptor / CurrentCandidateSetResult 族 / CurrentGroundingView；
  WorldModel.ResolveCurrent（只读）；Slice 记录重构 + DeriveSlice 新签名；
  BindingView 增 HasTargetOccurrence fact。
- `Effects/`：UiTargetReference；CandidateBinding 增可选 UiTarget；CanonicalBinding
  增可选 TargetOccurrenceId / OwningContainerId / LogicalItemId；EffectBoundary.Bind
  UI 路径（stale / ambiguous / occurrence 不在当前投影 → 三态拒绝，无字符串
  fallback）；IsBindingValid / Dispatch 兼容扩展。
- 测试迁移 + 新 scenario 测试（执行序 Control→ResolveCurrent→candidate→Bind→
  Judge→Gate；stale occurrence；continuity re-bind）。

## Out of Scope（禁止）
- ControlLoop / ControlIntent / Assurance 判定逻辑与签名（Slice 形状变化导致的
  调用点适配除外）；Evidence / Perception / Run / Trace；EntityScopedObligation
  物理入口；Traversal View；非 UI 字符串通道语义改动。
- WorldModel belief 侧（UIW-003 已落，零改动——ResolveContinuity 已存在）。

## Decisions（Leader 固定 realization 裁决）
- ResolveCurrent(TargetDescriptor{Role, SemanticDescriptor?, OwningContainerId?})：
  机械确定性匹配（Role 相等 ∧ container 相等(若给) ∧ descriptor 相等(若给)）；
  Current null 或 Occurrences null（无 observation seam）→ ScopeProjectionUnavailable
  （fail-closed）；0/1/N 候选 → NoCandidate / UniqueCandidate / MultipleCandidates；
  纯只读（零 log、零状态）。
- Slice 新形状：SourceRevisionId / RootContainerId / FreshnessBasis /
  InScopeContainerIds / Occurrences(OccurrenceFact) / ScopedClaims(分区兼容通道)；
  DeriveSlice(rootContainerId, inScope?)——root 必须存在于 Current.Containers，
  否则 fail-closed（旧 string-prefix API 移除；旧测试经 doubles 种子容器迁移）。
- BindingView +HasTargetOccurrence（occurrence ∈ 当前投影 fact；判定权仍在 EB）；
  allowlist 测试随 public shape 更新（本 change 显式解锁该锁定面，EXP-008 原则
  不变：新增字段 = 新白名单成员）。
- UI binding 恒绑 occurrence：UiTargetReference 携 OccurrenceId（LogicalItemId
  仅 provenance 列）；CanonicalBinding UI 目标 = TargetOccurrenceId（TargetSubject
  沿载 occurrence id 字符串供 attempt 留痕约定）；无任何 string fallback。
- 既有测试迁移只改构造/读取形状，架构断言（场景期望）逐条保持。

## Acceptance
S1 ResolveCurrent 四态（Unique/No/Multiple/ScopeUnavailable）+ 只读性
S2 descriptor 匹配维度（role/container/descriptor）确定性行为
S3 Slice 新形状派生：root fail-closed / InScope / occurrence 景观 / ScopedClaims 分区
S4 UI bind 正路径：UiTarget candidate → CanonicalBinding(TargetOccurrenceId…) →
   Judge → Gate/dispatch 全通
S5 stale occurrence candidate（revision 前进）→ StaleRevision 拒绝；无 fallback
S6 occurrence 不在当前投影（view.HasTargetOccurrence=false）→ UnknownTarget
S7 ambiguous candidate → Ambiguous 拒绝（沿用四态语义）
S8 continuity re-bind：demand + revision advance → ResolveContinuity(SameReferent)
   → 新 occurrence candidate → 重绑成功（UIW-003 集成闭环）
S9 非 UI 字符串通道兼容：既有 string candidate/binding 行为不变
S10 全量回归：迁移后既有测试（架构断言不变）+ 新测试全绿

## Constraints
迁移面最小化：只动形状适配点；不重写测试语义；确定性；不改 harness 层。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ 迁移清单（文件×断言对照）
  expected: S1–S10 GREEN；全解决方案全绿；变更面仅 World/ Effects/ tests/
  actual: >
    152/152 GREEN（Kernel 135 + Agent 17；新增 S1–S9 scenario，S10 = 全量回归；
    Leader 独立复跑确认）。变更面 = World/（GroundingSeam.cs 新增；WorldModel/
    Slice/ConsumerViews 受控）+ Effects/（TargetBinding/EffectBoundary 受控）
    + UniKernel.cs 两处组合缝适配（DeriveSlice 透传签名 + Act 内 BindingView
    occurrence fact 供给——判定逻辑零触碰）+ tests/（9 文件形状迁移 + Agent
    侧 1 处编译适配 + 2 新文件）。Leader 抽审：Bind UI 通道三态拒绝序
    （Stale→Ambiguous→UnknownTarget）与恒绑 TargetOccurrenceId 合规、无字符串
    fallback；ResolveCurrent 四态纯只读；Slice root/InScope fail-closed；
    Bind→Judge→Gate 次序（G2 修正）未动。迁移清单 9 文件断言保持声明核对。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；git diff 抽审记录
```

## Status log
2026-09-08 · understanding→resolved · 权威已读（UWM-009 v0.3 §39/§41 / P23 /
  ADR-0015/0014 / ADR-0011）；breaking-change 裁决与迁移原则固定
2026-09-08 · resolved→persisted→planned · state.md 建立；切片 = 类型+缝 →
  Slice/EB 迁移 → scenario 测试 → 全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent（完整 spec）：RED →
  GroundingSeam + Slice 新形状 + BindingView fact + EB UI 通道 + 9 文件迁移 → GREEN
2026-09-08 · implemented→reviewed · REVIEW：偏离点 2 条均为组合缝必要适配
  （UniKernel 透传/fact 供给、Agent 测试编译适配），判定逻辑零触碰；allowlist
  更新 = 本 change 显式解锁面（改期望不改机制）；迁移断言保持声明逐文件核对
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 152/152 GREEN + Bind/
  ResolveCurrent/Slice 抽审合规 → P23_SEAM_AND_CONSUMPTION_FACES_ESTABLISHED；
  UIW-002 冻结模型全量落地（文档→belief 侧→缝与消费面）
