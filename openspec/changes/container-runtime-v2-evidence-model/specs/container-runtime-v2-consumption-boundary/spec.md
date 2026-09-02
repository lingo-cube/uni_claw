# Spec Delta: container-runtime-v2-consumption-boundary

## Purpose

定义 Container Runtime V2 的 Consumption Boundary：Agent admission（语义条件）、执行 grounding（fresh 反向链）、canonical supersession 对 Agent progress 的显式重估、region-scoped coverage 消费与 Slow 语义修复 seam 的 observable 行为，使 canonical world 的输出被正确消费且感知错误不得无边界穿透至 Action / Graph / Progress / Completion 权威。

## ADDED Requirements

### Requirement: Agent admission 只依赖 canonical 语义

Agent obligation admission MUST 以 canonical LogicalItem 的语义判定（结构、交互语义、语义已解析）加 Goal / Scenario Policy 为条件，SHALL NOT 以"当前可 grounding"为 admission 条件。`SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE`；`CURRENTLY_GROUNDABLE != AUTHORIZED`；`LOGICAL_ITEM != TRAVERSAL_OBLIGATION`。

#### Scenario: 已滚动出屏的义务仍可成立
- **WHEN** 一个 LogicalItem 的语义判定在早先 Slice 上完成（如异步语义修复确认其为可导航项），而当前视口已滚离其位置
- **THEN** Agent 可将其 admission 为 obligation；其 grounding 延后到执行时的 relocation 完成

### Requirement: 执行 grounding 必须走 fresh 反向链

任何已授权 obligation 的执行 MUST 经由：当前或可达的 fresh Slice → fresh visual Occurrence → fresh ScreenBounds → Action Authorization。历史 bounds SHALL NOT 作为 action grounding；region-relative 坐标 SHALL NOT 作为 grounding 权威；relocation 提示 SHALL NOT 自身构成滚动授权。V1 fail-closed grounding policy：EdgeClipped（视口边缘部分可见）的 Occurrence 不参与 grounding（该条为策略而非永久不变量）。`HISTORICAL_BOUNDS != ACTION_GROUNDING`。

#### Scenario: 历史 bounds 被拒绝
- **WHEN** 执行时目标 obligation 只能以先前 Slice 的坐标定位
- **THEN** 系统通过 relocation 回到目标附近并重新 perception，以 fresh Occurrence 的 fresh ScreenBounds 完成 grounding，而非直接使用历史坐标

#### Scenario: relocation 后重新验证
- **WHEN** relocation 回到估计位置后发现 fresh visual occurrence
- **THEN** grounding 使用该 fresh occurrence 的 bounds，且 relocation 估计本身不参与最终点击坐标

#### Scenario: 无法 grounding 的义务以证据收尾
- **WHEN** 一个已 admission 的 obligation 在 coverage 穷尽后仍无法 grounding
- **THEN** 系统走显式恢复/收尾路径，以 incomplete-with-evidence 结束该义务，不产生假完成

### Requirement: Canonical supersession 触发显式 progress 重估

canonical world 的合并/拆分/重分类 MUST 产出显式 supersession（旧 canonical 引用如何被新 canonical 引用取代），并由 Agent 消费以重估 progress（authorization 有效性、重复 attribution、被取代义务）。系统 SHALL NOT 静默修改 Agent branch progress（progress 为 Agent-owned evidence aggregate，Pending 为派生视图）。supersession 契约 SHALL NOT 拥有 progress authority，SHALL NOT 将既有 container 级 correction 类型扩展为万能 correction envelope。

#### Scenario: 合并不吞没已完成进度
- **WHEN** Agent 已完成 LogicalItem M1 的义务、M2 待执行，随后 canonical 证据表明 M1 与 M2 实为同一逻辑对象 M3
- **THEN** 系统产生 supersession（M1+M2 → M3）并触发 Agent progress 重估：M2 的义务被标记为重复 attribution 而非遗留 pending，且该重估过程显式可追溯

### Requirement: Coverage 为 region-scoped 后容器聚合

coverage MUST 先按 SpatialRegion 计算（覆盖证据源：SpatialRegion、SliceRelation、overlap、gap、边界/穷尽证据），再聚合为 Container coverage。单一 region 穷尽 SHALL NOT 推断 Container coverage complete。coverage SHALL NOT 依赖 Fast item 数量或"无新逻辑项"计数。`COVERAGE_COMPLETE != SEMANTICALLY_RESOLVED`；`COVERAGE_COMPLETE != TRAVERSAL_COMPLETE`。

#### Scenario: 多分区下单一区域穷尽不等于容器完成
- **WHEN** 一个多 region 容器（IVI 风格：媒体滚动区 + 导航面板 + 常驻控制条）中媒体滚动区已穷尽，而参与 coverage 的其他 region 未穷尽
- **THEN** Container coverage 判定为未完成

#### Scenario: 语义误读不影响 coverage 独立判定
- **WHEN** Fast 将分区标题误判为可导航项（语义歧义存在），但空间连续性证据表明页面已滚动穷尽
- **THEN** coverage 判定完成不受该语义歧义影响；歧义在 closure 阶段按未解析语义另行处理

#### Scenario: 快滚假完成被阻止
- **WHEN** 快速滚动导致相邻接受之间出现未覆盖区间（gap 证据）
- **THEN** coverage 不宣布完成，直至该区间被回补覆盖

### Requirement: Container 局部完成依赖三类独立条件

Container local complete MUST 至少依赖：coverage 穷尽、已 admission 义务全部解决、无 closure-critical 未解析语义。closure-critical 未解析语义 MUST 在以 incomplete-with-evidence 收尾前穷尽一个显式有界的 resolution policy（具体路线组合不在本能力冻结范围）。

#### Scenario: 深层 Unknown 不无限阻塞
- **WHEN** 页面存在未解析语义项但均不构成 closure-critical
- **THEN** 已知义务的遍历继续进行；仅当未解析语义影响 closure 时按 resolution policy 升级处理

### Requirement: Slow 为低频高精度语义修复 seam

Slow 语义修复 SHALL 仅以 typed semantic claims 形式参与确定性 reconciliation。`FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION`：首帧 SHALL NOT 自动触发 Slow；Anchor barrier 仅针对 critical initial identity ambiguity/conflict。Slow SHALL NOT 铸造 Occurrence、授权 action、宣告 completion、变更 Graph 或持有 Agent obligation。`SLOW_CORRECTION != ACTION_AUTHORITY`；`SLOW_CORRECTION != COMPLETION_AUTHORITY`；`SLOW_SEES_SOMETHING != ACCEPTED_OCCURRENCE`。

#### Scenario: Slow 发现遗漏不直接铸造实例
- **WHEN** Slow 从截图中发现 Fast 遗漏的视觉内容
- **THEN** 系统最多记录区域提示，必须经 fresh perception 与验收后才可能产生新 Occurrence

#### Scenario: 首帧不自动调用 Slow
- **WHEN** 一个新 Container 的首个 accepted Slice 到达且身份无歧义
- **THEN** 系统不触发 Slow；仅当出现 critical initial identity 歧义/冲突时 Anchor barrier 才触发

#### Scenario: Slow 错误提升被容纳
- **WHEN** Slow 的某次 typed claim 错误（如误提升成员角色）
- **THEN** 该错误不产生 action / completion / graph 权威效果，且可被更强证据经 reconciliation 推翻

### Requirement: R8 ownership 语义保持

本能力的全部行为 SHALL 保持既有 R8 语义不降级：CurrentContainer 恒为 NodeRef + CurrentSliceRef + EntryContext（`ENTRY_RELATION != RETURN_RELATION`；`RETURN_EXPECTATION != RETURN_TRUTH`）；TransitionOccurrence 只记录真实发生的转场证据（`ACTION != TRANSITION_OCCURRENCE`；期望无投票权）；ContainerGraph 不承担导航规划（`CONTAINER_GRAPH != NAVIGATION_PLANNER`；`HISTORICAL_GRAPH_PRIOR != CURRENT_WORLD_TRUTH`）；Agent 保持 action / progress / completion authority。

#### Scenario: 期望与观察脱节时以观察为准
- **WHEN** 一次局部动作后期望仍在原容器，而 fresh Observation 显示世界已回到其他容器（r5 类）
- **THEN** CurrentContainer 依据 observed transition 证据提交到真实位置，Agent 据此重估剩余义务
