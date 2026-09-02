# Spec Delta: container-runtime-v2-evidence-foundation

## Purpose

定义 Container Runtime V2 的 Evidence Foundation：Fast 瞬态感知输出与 Runtime accepted evidence 之间的验收边界，以及 accepted stable Slice、SpatialRegion、accepted visual Occurrence、SliceRelation 等基础证据模型的 observable 行为，使不可靠感知的错误在进入 Runtime 世界前被显式接受、降级或拒绝。

## ADDED Requirements

### Requirement: Runtime Acceptance 边界（三职责 + 原子提交）

Runtime MUST 在瞬态感知候选与 accepted evidence 之间维持显式验收边界，内部职责分离为：viewport 稳定性验收（这一眼是否有资格成为 accepted Slice）、跨源对应（structured evidence 是否对应某 accepted visual candidate）、实例化（哪些 accepted visual candidate 成为 Occurrence）。`FAST_RESULT != ACCEPTED_RUNTIME_EVIDENCE`。`Slice + Occurrence[] + bound FastAssessment[]` MUST 作为一次原子提交进入 Runtime state；系统 SHALL NOT 产生先提交 Slice 引用、后补 Occurrence 实体的部分接受状态。

#### Scenario: 稳定观察被原子接受
- **WHEN** 一次 fresh Observation 通过稳定性验收（settle 证据满足）
- **THEN** Runtime 在一次原子提交中产生恰好一个 Slice、其 accepted visual Occurrence[] 与绑定的 FastAssessment[]，且不存在任何 dangling 引用

#### Scenario: 半稳定帧不被接受为 Slice
- **WHEN** 一次 fresh Observation 处于 settling / transient 状态（动画、加载中）
- **THEN** Runtime 不为该 Observation materialize 任何 Slice；该 Observation 保留为 raw capture，其稳定性证据进入验收输入与诊断记录

#### Scenario: 验收拒绝留下诊断而非实体
- **WHEN** 一个感知候选被验收拒绝或降级
- **THEN** 系统通过既有 Observability/Trace 通道记录（ObservationRef、候选摘要、拒绝原因、validator 决策），且不创建新的 Runtime domain 诊断实体

### Requirement: Slice = accepted stable fresh viewport

Slice SHALL 定义为 Runtime accepted 的 stable fresh viewport evidence，与 Observation 的基数恒为：每个 accepted stable viewport Observation materialize 恰好一个 Slice；rejected / transient Observation materialize 零个 Slice。分屏/多分区界面 MUST 由同一 Slice 内的多个 SpatialRegion 表达，SHALL NOT 拆分为多个 Slice。

#### Scenario: 分屏仍是单一 Slice
- **WHEN** 一次 accepted Observation 的画面包含多个独立分区（如导航面板 + 媒体列表 + 常驻控制条）
- **THEN** Runtime 产生一个 Slice，携带多个 SpatialRegion，而不是多个 Slice

### Requirement: Occurrence = accepted visual occurrence（structured 仅佐证）

Occurrence SHALL 定义为一次 accepted Slice 中的 accepted primary viewport visual occurrence。Structured evidence MAY 通过确定性对应关系佐证一个 Occurrence（状态提示、证据引用），SHALL NOT 独立铸造 Occurrence，SHALL NOT 创造 visual occurrence truth。`MULTI_SOURCE_CORROBORATION != OCCURRENCE_IDENTITY`；`SOURCE_AUTHORITY IS CLAIM_SPECIFIC`（vision 为 fresh visibility/grounding 主证据；structured 为 state/结构佐证）。

#### Scenario: structured 佐证 visual occurrence
- **WHEN** structured 节点（clickable/checkable 等）与某 accepted visual candidate 建立确定性对应
- **THEN** 该 structured evidence 作为 StateHints 与证据引用并入对应 Occurrence

#### Scenario: structured 无视觉对应
- **WHEN** structured 节点与任何 accepted visual candidate 均无对应（如屏外/不可见节点）
- **THEN** 该 structured evidence 保留为 unmatched auxiliary evidence，不产生任何 Occurrence，也不获得 grounding / identity / coverage / completion 权威

#### Scenario: structured 与 visual 冲突
- **WHEN** structured 声称某区域 clickable 但视觉上不存在对应实例
- **THEN** 以 visual 证据为准；不因 structured 声明而铸造可点击的视觉真相

### Requirement: SpatialRegion 与 OccurrenceRegionBinding

Slice MUST 携带 1..N 个 SpatialRegion（分区类型 + scroll/coverage/grounding 三个独立参与标志）。每个 Occurrence 与 SpatialRegion 的关系 SHALL 由 region binding 表达为空间关联（max-overlap 判定）：非主导归属时 binding 为 ambiguous，ScreenBounds 保持有效，region-relative 坐标 SHALL NOT 作为权威 correlation 证据。`OCCURRENCE_BELONGS_TO_SLICE`；`REGION_BINDING != OWNERSHIP`；`REGION_BINDING != OCCURRENCE_IDENTITY`。

#### Scenario: 固定 chrome 的返回按钮
- **WHEN** 某 UI 元素位于 FixedChrome region（如顶部返回栏）
- **THEN** 其 Occurrence 不参与 scroll correlation 与 coverage 累积，但仍可参与 action grounding（三个参与标志相互独立）

#### Scenario: 跨区域边界的模糊归属
- **WHEN** 一个 Occurrence 与多个 SpatialRegion 的 overlap 均无主导（低于阈值）
- **THEN** binding 标记为 ambiguous；该 Occurrence 的 ScreenBounds 仍可用于 grounding，其 region-relative 坐标不参与权威 correlation

### Requirement: SliceRelation 为 region-bound 空间证据

相邻 accepted Slice 之间的空间关系 SHALL 以 pairwise SliceRelation 表达，其内部按 SpatialRegion 绑定（V1 允许只计算 Primary region 的 translation-only 关系）。领域模型 SHALL NOT 写死"整 Slice 单一位移"。`SLICE_ALIGNMENT != ITEM_IDENTITY`；alignment/overlap 证据 SHALL NOT 等同 item identity；估算位置 SHALL NOT 直接作为 action grounding。

#### Scenario: 快滚产生 gap 证据
- **WHEN** 两次相邻 acceptance 之间的位移大、有效重叠低于阈值
- **THEN** SliceRelation 产生 gap 证据，coverage 判定不得宣布该区间已覆盖

#### Scenario: 不确定度分档消费
- **WHEN** SliceRelation 的位移估算带有高不确定度
- **THEN** 其派生档位为 HIGH，下游 coverage/relocation 消费按档位降权，不将估算值当精确坐标使用

### Requirement: Evidence ordering 复用既有 run-local 原语

本能力 SHALL 复用既有 run-local ordering 原语（V2 state commit version 与 observation 单调序号）承载证据排序与乐观并发拒绝，SHALL NOT 新建 global semantic clock。`REVISION_ORDER != CAUSAL_BINDING`；`LATER_REVISION != STRONGER_EVIDENCE`。

#### Scenario: 过期候选提交被拒绝
- **WHEN** 一个基于旧 state 准备的异步候选提交与更新的提交竞争
- **THEN** Runtime 通过既有 stale 拒绝机制拒绝其提交，不产生乱序接受

### Requirement: FastAssessment 仅为 hint

Fast 的结构假说（成员角色/结构/affordance hint）MUST 以 FastAssessment 形式随 acceptance 绑定到 accepted Occurrence 组，SHALL NOT 直接成为 LogicalItem、identity 或 obligation 依据。

#### Scenario: 结构假说不铸造逻辑对象
- **WHEN** Fast 对 [标题文本, 副标题文本, 图标] 三个 Occurrence 产生 LIST_ITEM 结构假说
- **THEN** 该假说仅作为最低档 canonicalization 证据进入评估，逻辑归属由 canonical world 能力裁决
