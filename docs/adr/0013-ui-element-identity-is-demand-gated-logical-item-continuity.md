# 0013 — UI 元素身份是 demand-gated、evidence-established 的 LogicalItem 有界连续性，不是通用元素身份系统

UIW-002 Decision-Heavy Explore（Stateful Grill Q1–Q6，2026-09-08，G1 接受）裁决
UIWorld 的元素级身份语义：真实 buyer 不是"每个可见 UI 对象都需要长期身份"，
而是"EffectTargetCommitment 中显式的 cross-revision same-referent requirement"。
据此建立三层模型，拒绝通用元素身份系统。

## Decision

```text
ContainerIdentity        = 长期稳定 world-space identity（UWM-009 既有）
ObservationOccurrence    = revision-local observed UI presentation；
                           provider node id / bbox / OCR / DOM / UIA / detection id
                           = evidence only，永不是 identity
LogicalItem              = owning Container 内、demand-gated、evidence-established
                           的 actionable logical referent 有界连续性

晋升规则：真实跨 revision 引用需求 ∧ 充分 accepted evidence，缺一不可
（demand 只购买 tracking eligibility；identity 只能由 accepted evidence 经
reconciliation 建立——demand-gated, evidence-established，非 demand-minted）

四轴分离（绝不混用）：
Lifecycle(Established/Ended) × ContinuityAdjudication(SameReferent/Ambiguous/
Insufficient/Contradicted) × Presence(复用 UWM-009 §8 claim epistemic 词汇) ×
Maintenance(Hot/Cold，owner-internal 非 truth)

Ended 仅来自正面 lifecycle evidence：referent 明确终止/排他替代，或 owning
Container canonical lifecycle Ended（级联）。Ambiguous / Insufficient / New /
Absent / Contradicted / demand 消失——六者皆 ≠ Ended；状态变化（checked/
disabled/value/坐标/rerender/临时 offscreen）是 state claim，永不终止 continuity。

ReferentBasis = 可修订 evidence/belief basis（container + role + semantic
anchors + relations + optional platform stable key），禁实现为复合 identity key；
presentation continuity 至多是证据（RecyclerView 教训：same node/bbox/affordance
≠ same LogicalItem）；Identity never creates information——证据不能区分时
结果保持 Ambiguous，不造身份消灭歧义。

默认路径：普通 revision advance 后的 fresh re-ground（ResolveCurrent）不是
LogicalItem buyer——continuity 是例外路径，不是默认路径（Playwright Locator
思想：保存"如何重新找到"，不保存"上次找到的对象"）。
```

## Considered Options

- **通用 canonical UIEntity（屏幕元素身份系统，含统一 WorldEntityGraph / 双图并列）**：
  被拒——身份通胀（recycled list 全节点铸造）、误分类需 withdrawal 机器、
  WorldModel 滑向 DOM 镜像；且 buyer 分析证明元素级连续性只在显式 same-referent
  需求时被购买。
- **P2 affordance-minted（可交互即铸身份）**：被拒——语义错误：可交互性回答
  "能不能操作"，identity continuity 回答"是不是之前那个逻辑目标"，是两件事。
- **P1 act-minted（act/attempt 触发铸造）**：被拒——真实触发器是跨 revision 引用
  需求而非 action（异步刷新场景：dispatch 前 revision 已前进）；且 attempt 本身
  不是 identity evidence（ADR-0012 纪律）。
- **P3 双 establishment trigger（act-correlated + sufficient-affordance）**：被拒——
  正式化两个 trigger 会膨胀出 sufficient-affordance 阈值 / eager mint / demotion
  一族无 buyer 的问题。
- **全部 revision-local transient occurrence（无元素连续性）**：被拒为 universal
  policy——post-action effect verification 与显式 same-referent 需求结构性跨
  revision，纯瞬时模型退回描述寻址（双胞胎必挂）。
- **纯连续链身份 / 描述性身份 / 静默消失 / Anchor×Role 复合键**：被拒——分别
  无法执行 referent 变更裁决、双胞胎必挂、违反历史可解释性（§19）、等价
  screenshot fingerprint 2.0。

## Consequences

- UWM-009 v0.3 窄增补（§35–§38、§41；P-UW-24..31、35）；CONTEXT.md 新增词条族。
- v0.1 LogicalItem 仅覆盖 actionable logical target；ProgressBar/StatusLabel/文本/
  装饰继续 = Occurrence + Claims + Container context；推广为一般 referential
  entity 需新 buyer（Resource 重开条件同时记录）。
- LogicalItem scope ⊆ ContainerIdentity lifetime；跨 Container continuity 不设计。
- 实现（belief 侧 / 缝与消费面）另立 change 走 UniFlow。
