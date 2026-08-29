## Why

IR-G0（Phase 2.6 STOP）与 `perception-navigation-row-composition-repair` 的 Human Gate 共同确立了两件事：

1. **点状修补不可泛化**：四锚点确定性行算子修复了"同行重复框/副标题独立成菜单"的原始缺陷（候选已证明），但锚点不足时只剩 fail-closed；三锚点放宽在真实模拟器上产生副标题误判（`Volume, vibration, Do Not Disturb` → menu），已被安全回退。继续在单一算子内调阈值是在错误的抽象层找泛化。
2. **正确的问题是**：同一类"上下文相关的感知组合规则"（行组合、角色选举、文本关系、层级佐证、模型 advisory）会反复出现在不同 系统/系统版本/应用/应用版本/设备 上，且参数随上下文漂移、需要随证据增量演化 —— 这是一个**算子 + 级联规则配置**问题，不是某个具体阈值问题。

因此本 change 提出一个泛化框架：**感知算子与级联规则（Operator & Cascading Rules）** —— 通用算子实现 + 五维+tag 的树状规则配置（CSS 式层层覆盖）+ 验证侧可增量学习的参数生命周期 + 经既有 governance receipt 的晋升通道。

## What Changes

- **算子契约（泛化）**：每个算子声明 authority class（GENERATOR / VALIDATOR / ADVISOR）、类型化输入/输出契约（带 provenance）、有界参数 schema、确定性纯函数语义、以及显式 fail-closed 契约。实现尽可能通用，场景差异全部下沉到规则配置。
- **首批算子**：
  - `uniform-list-row-grouping`（GENERATOR，v1 = 已保留的 `row_grouping.py` 四锚点语义原样移植，参数化）；
  - `row-relation-head`（GENERATOR，先行确定性版本：行组内 head/satellite 选举，结构化组合而非任意文本角色推断 —— 针对锚点不足视口，替代被回退的不安全放宽）；
  - `spacing-verifier`（VALIDATOR：几何验证/约束/fail-closed，所有 GENERATOR 输出必须通过）；
  - `text-relation-check`（VALIDATOR：只能 veto / 降置信，永不生成菜单）；
  - `structured-corroboration`（VALIDATOR：XML 仅辅助佐证，永不是菜单身份来源）；
  - `vlm-annotation`（ADVISOR：仅离线标注 / 低频 advisory，永不进入授权路径）。
- **规则级联与树视图**：选择器维度 = `system / systemVersion / app / appVersion / device` 五维 +
  `tags`（补充模式集，如 `display=triple-screen`）；匹配语义为 CSS 式 specificity 级联 ——
  规则 pin 任意维度子集，pin 数即特异性，高分覆盖低分（全 android 生效 → 某版本异化 →
  某应用特值 → 特殊模式覆盖）；维度值缺失取 `default` 且可被显式 pin（匹配"无版本应用"等情形）；
  **同特异性冲突在载入期拒绝**（要求显式交集规则），规则顺序不影响语义；"树状管理"是组织
  呈现视图，不参与匹配。解析确定性且每个值携带 provenance（ruleId + pins + specificity）。
- **配置即治理**：规则树是感知配置工件（新 config artifact 类型），内容哈希进入既有 `configId → deploymentId → CURRENT-ACTIVE receipt` 链；运行时只消费已激活配置。
- **参数学习生命周期**：验证侧 learned-parameter 存储（复用 Phase 2.6 已验证的知识纪律：provenance-gated 准入、fresh-evidence-wins、scope=选择器节点、ACTIVE/STALE/CONTRADICTED/SUPERSEDED/INVALIDATED、版本化 freeze/load）；学习永不直接改生产 —— 晋升只能经 governance 新 config manifest + receipt 切换（人工批准）。
- **IR-G0 解锁路径**（Human 批次裁决 2026-08-27：授权批次 = S1 → S2 → S4，逐阶段硬 Gate）：
  S1 框架核心 + 算子移植（与候选零行为差异，**任何差异即停止**）→ S2 确定性 relation-head
  （输入冻结为原始视觉区域与几何关系候选，禁止循环依赖；须过 v1n 反例与跨 UI 回归集，
  **不足即停止于 fail-closed 边界，不自动进入 S3**）→ S4 验证算子接线（仅 veto/降置信）。
  S3（模型版 relation-head）保持独立 Human Gate；S5（学习闭环）延后至 S2 后单独决策，
  不阻塞重入。Phase 2.6 重入条件：S2 或获授权的 S3 在回归帧集上达到"每视觉行恰一个导航候选"。

## Capabilities

### New Capabilities

- `perception-operator-rule-framework`：算子契约 + 级联规则树 + 确定性解析与 provenance + 治理化配置工件 + 学习参数生命周期。感知基础设施变更；学习与晋升不改变运行时权威（AuthorityDelta: NONE at runtime）。

### Modified Capabilities

- 无 Runtime / Strategy Contract / GoalEvidence / SourceIdentity 变更。感知融合层重构为算子组合，行为由激活配置决定。

## Impact

- 生产范围：`platforms/perception`（fusion 重构为算子 + 规则解析；governance 增加规则树工件类型）。**Large Change**（新抽象 + 新边界 + 新工件类型 + 学习生命周期）。
- 分级与 gate：本提案为 propose 阶段；实现需单独 Human Gate。S3（模型版 relation-head）再需独立 gate。
- 非目标：不修改 Runtime 归一化契约；不把 XML 提升为身份来源；不让文本语义或 VLM 生成可操作菜单；不实现自动生产参数推送（晋升永远人工）。
- 依赖：`perception-navigation-row-composition-repair` 的保留算子与其证据（v1m/v1n 实验、误判记录）作为 S1 移植输入与 S2 设计依据。
