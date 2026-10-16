# TRAJECTORY-001 — Event Stream / Heartbeat / 事件触发咨询（Agent 介入判断基础设施）

lifecycle_state: designing · review_state: **draft-not-grilled** · disposition: none · depth: decision-heavy · base: 56334655

## Intent（WHAT/WHY）

RUN-006 真机实证（F1-F11）：系统局部每步成功与全局零推进并存（46× 同目标
重派 + 45× 同弹窗清除），Agent 看不见轨迹、机械判据漏报、无介入通道。
本 change 把 v3.2 设计定稿落成实现：

> **当系统不知道下一步怎么走时，帮助 Agent 判断是否介入、如何介入。**

设计输入（唯一真相源）：docs/analysis/agt019-followup-optimization-
and-research-2026-10-17.md（v3.2）——术语纪律、五裁决（D1-D5）、
参数初值、grill 前置三项均已录。

## Scope（按 v3.2 待办顺序）

1. **Event Stream 正式化**：存量记录流补 seq（对齐 RuntimeRunEvent
   索引边界 = grill Q1 裁决：Runtime 只做外层索引，五类事实归各自
   Owner）+ 新增 TargetCoverageMarked（ReachedOverlay/RouteEntered/
   Skipped 三态）；
2. **矩形工具箱**（融合片前置）；
3. **融合片**：Overlay/BasePageAnchor（D1 约束）/ 几何遮挡（D5：只
   发信号）/ 路由连续性；
4. **Derived Signals + Heartbeat**（三组状态字段，Agent 友好原则：
   文本语义为主、id/数值为锚；HeartbeatTick 与咨询携带解耦）；
5. **trigger=event 的 Agent Decision Consultation + Case Packet**
   （复用既有咨询协议，M1 推广；consultation cooldown）；
6. **D2/D3/D4 落账**：覆盖四态、进度轴组合修法、Skip 事件化双指标。

## Out of Scope

Context Pull / resume 完整语义 / 信任阶梯实现（M6 v2 承载）；
跨 run 反思记忆；键盘浮层（无 buyer）。

## Decisions

- 五裁决 D1-D5 + M6 v2 立项 + 参数初值：见 v3.2 §八（owner 已裁）；
- grill 第一轮议程：v3.2 §十二（Heartbeat 最小字段集 / Reference
  Index authority / RuntimeRunEvent 边界——Q1-Q4 已有 Leader 预答，
  待 owner 主审确认）。

## Acceptance（初稿，grill 后定稿）

1. SIM 循环复现场景：≤3 次重派即触发事件咨询，Agent 裁决生效（skip/
   blocked/继续），全程零 confirm 侧、零未授权 Effect；
2. Heartbeat 在真实链咨询载荷在场且三组字段语义可读（Agent 友好原则
   执法）；
3. TargetCoverageMarked 三态可回放，双指标（taskCoverage /
   verifiedReachRate）在 CoverageReport 分列；
4. 全量回归绿 + 场景库随动 + 真机一轮留证。

## Status log

- 2026-10-17 · created · v3.2 定稿 + owner 裁决 A（AGT-019 profile
  行关闭）后立项；下一步 = grill（owner 主审 + grilling/domain-
  modeling 双轨，第一轮从 v3.2 §十二开始）。
