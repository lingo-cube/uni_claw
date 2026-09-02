# Spec Delta: container-runtime-v2-canonical-world

## Purpose

定义 Container Runtime V2 的 Canonical World：per-Container-Node 生命周期内的 LocalModel 工作记忆、LogicalItem canonical 逻辑对象、claim-specific EvidencePolicy 与确定性 reconciliation 的 observable 行为，使累积证据能够收敛为 container-local canonical world 而不产生平行 truth owner 或穿透 Agent 权威。

## ADDED Requirements

### Requirement: LocalModel 是唯一 container-local canonical world owner

每个 Container Node SHALL 恰好有一个 LocalModel（不可变聚合：accepted evidence、assessments、canonical projection、coverage projection）。LocalModel 是 container-local canonical inventory 的唯一 owner；旧 Container page-local world state SHALL 按已批准处置表迁移（REUSE/MOVE/DERIVE/DELETE），SHALL NOT 与 LocalModel 长期并持正式 authority（迁移期 shadow 仅用于比对与分歧度量）。LocalModel SHALL NOT 持有 Agent plan、Action authorization、GoalEvidence、current physical authority、跨 run item identity 或历史 bounds 点击权威。

#### Scenario: 证据只追加、投影整体替换
- **WHEN** 新的 accepted evidence 进入 LocalModel
- **THEN** evidence 以 append-only 方式追加，canonical projection 与 coverage projection 作为派生快照被整体重算替换

#### Scenario: 迁移期不形成双正式 truth
- **WHEN** 迁移 shadow 期新旧两条路径同时运行
- **THEN** 正式 authority 只属于其一，另一路径仅产出比对/分歧度量，不产生双写正式 truth

### Requirement: LogicalItem 为 LocalModel-scoped canonical 逻辑对象

LogicalItem SHALL 表达组合语义模型（逻辑结构 × 交互语义 × 成员角色 × 状态），每个 actionable LogicalItem 恰好有一个主 interaction semantics。LogicalItem 的 identity 限于 LocalModel 生命周期内，SHALL NOT 建立跨 run 稳定 item identity，SHALL NOT 持有当前点击坐标或历史 bounds 的点击权威，SHALL NOT 等同 traversal obligation。本能力 SHALL NOT 要求 LogicalItem hierarchy（parent/children）作为 Day-1 需求；组合关系由 flat item + GROUP 结构 + membership 证据表达。

#### Scenario: 孪生文本不产生重复清单项
- **WHEN** 同一列表行的标题与副标题被识别为多个 visual occurrence，且结构/布局证据支持其同属一个逻辑对象
- **THEN** canonical world 中形成单一 LogicalItem（成员角色为 PRIMARY/SECONDARY），而非两个独立可点项

#### Scenario: 帧级类别翻转不污染逻辑对象
- **WHEN** 同一物理元素在不同帧被 Fast 判为不同视觉类别（如 text_block ↔ menu_item）
- **THEN** 其 LogicalItem 归属与语义不因帧级类别翻转而改变（类别翻转仅是最低档 hint）

#### Scenario: 标题不被误纳为可点项
- **WHEN** 分区标题在语义上被判定为 STATIC_CONTENT × NONE（无交互证据）
- **THEN** 其 LogicalItem 不具备可处理交互语义，不进入可 admission 的候选集合

### Requirement: EvidencePolicy 为 claim-specific 证据评估

canonical claim 的评估 MUST 按 claim 类型使用各自的政策（证据构成、聚合、决策边际、迟滞、冲突处理），SHALL NOT 建立全局线性证据排序，SHALL NOT 采用"同档证据永不翻转"规则。多个同档、持续一致的新证据累计后 SHALL 能够推翻旧投影（防止 sticky wrong interpretation）。`SAME_DESTINATION != SAME_LOGICAL_ITEM`：不同交互目标到达同一目的地 SHALL NOT 推断为同一逻辑对象。

#### Scenario: 同档累计证据纠正粘滞错误
- **WHEN** 旧投影基于单个 Slice 判定两个 occurrence 同属一个 LogicalItem，随后多个新 Slice 的同档证据持续一致地支持相反判定
- **THEN** canonical 投影被推翻并修正，错误解释不因"同档不可翻转"而粘滞

#### Scenario: 同目的地不推断同对象
- **WHEN** 两个不同 LogicalItem 的交互均观察到到达同一目的地容器
- **THEN** 系统不据此判定两者为同一逻辑对象

#### Scenario: 振荡受控
- **WHEN** 证据流在两种解释间往复摆动且未超过决策边际
- **THEN** 投影保持稳定或进入显式 conflict 状态，不产生高频翻转

### Requirement: Canonical reconciliation 为确定性纯函数

canonical 投影的重算 MUST 由无状态确定性 reconciler 完成，输出显式 delta（归属既有 / 新建 working / 保持 unresolved / 合并 / 拆分 / 重分类），每条归属决策可追溯到政策规则与证据。reconciler SHALL NOT 直接修改 Agent progress，SHALL NOT 直接产生 obligation，SHALL NOT 铸造 Occurrence。

#### Scenario: canonical 改写产出显式 delta
- **WHEN** 新证据导致两个既有 LogicalItem 应合并为一个
- **THEN** reconciler 输出显式 supersession delta（含旧/新引用与证据），供消费边界的 progress reevaluation 使用，而不静默改写任何 Agent 状态

### Requirement: Taxonomy 冻结组合模型而非值集

canonical 语义模型 MUST 以组合维度（视觉 primitive / 逻辑结构 / 交互语义 / 成员角色 / 状态）表达，SHALL NOT 冻结当前枚举值集为契约（值集为 V1 候选，可按真实 buyer 合并/重命名/移除/扩展），SHALL NOT 回归组合枚举（如 menu_item_with_subtitle 式单枚举爆炸）。

#### Scenario: 值集演进不构成契约变更
- **WHEN** 后续依据真实 Settings/IVI 证据对枚举值集进行合并或重命名
- **THEN** 该演进不违反本能力契约（契约只约束组合建模方式与禁止单枚举回归）

### Requirement: Runtime canonical 语义不消费 Agent 策略

canonical world 的语义判定（结构/交互语义/成员角色/状态/是否语义已解析）MUST 独立于 Goal 与 Scenario Policy。`SEMANTIC_AFFORDANCE != AGENT_ADMISSION`。

#### Scenario: 同一世界在不同目标下语义一致
- **WHEN** 同一 Container 在两个不同 Goal 的运行中被观察
- **THEN** 其 canonical 语义判定一致；是否 admission 为义务由 Agent 侧另行决定
