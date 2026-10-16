# TRAJECTORY-001 — Event Stream / Heartbeat / 事件触发咨询（Agent 介入判断基础设施）

lifecycle_state: planned · review_state: **grilled-decision-complete** · disposition: none · depth: decision-heavy · base: 56334655

## Intent（WHAT/WHY）

RUN-006 真机实证（F1-F11）：系统局部每步成功与全局零推进并存（46× 同目标
重派 + 45× 同弹窗清除），Agent 看不见轨迹、机械判据漏报、无介入通道。
本 change 把 v3.2 设计定稿落成实现：

> **当系统不知道下一步怎么走时，帮助 Agent 判断是否介入、如何介入。**

设计输入（唯一真相源）：docs/analysis/agt019-followup-optimization-
and-research-2026-10-17.md（v3.4）——术语纪律、五裁决（D1-D5）、
Heartbeat/Case Packet/Pull 边界、P25 身份纪律和实现 Acceptance 已录。

## Scope（按 v3.2 待办顺序）

1. **Event Stream 正式化**：存量记录流补 seq（RuntimeRunEvent 只做
   Runtime 外层索引，五类事实归各自 Owner）+ 新增 TargetCoverageMarked
   （ReachedOverlay/RouteEntered/Skipped 三态；Pending 是未标记的进行态，
   不是一条 Marked 事件）；Agent Skip 必须先形成
   RUN-004 D3/D7 typed union member，并与 structural-skip 分账；
2. **矩形工具箱**（融合片前置）；
3. **融合片**：Overlay/BasePageAnchor（D1 约束）/ 几何遮挡（D5：只
   发信号）/ 路由连续性；
4. **Derived Signals + Heartbeat**（三组状态字段，Agent 友好原则：
   文本语义为主、id/数值为锚；HeartbeatTick 与咨询携带解耦；M5
   `stagnationWindow` 与 Heartbeat `recentEventWindow` 分离；附带
   push-only Context Reference Index 目录，不实现 Agent-driven Pull）；
5. **trigger=event 的 Agent Decision Consultation + Case Packet**
   （复用既有 P25；每次新的语义咨询生成新的 DecisionId，transport
   correlation 保持 realization-private；rearm 由新观察和证据变化决定）；
6. **D2/D3/D4 落账**：覆盖四态、进度轴组合修法、Skip 事件化双指标。

## Out of Scope

Context Pull / resume 完整语义 / 信任阶梯实现（M6 v2 承载）；
跨 run 反思记忆；键盘浮层（无 buyer）。

## Decisions

- 五裁决 D1-D5 + M6 v2 立项：见 v3.4 §八；
- RuntimeRunEvent 只做 Runtime 外层事件索引，Observation/Popup/Receipt/
  Verification/Coverage 五类事实由各 Owner 持有，经显式投影进入咨询上下文；
- Heartbeat v1 固定包含 World Model、Kernel、Traversal 三组状态；字段
  语义化，id/数值为锚；Heartbeat、Case Packet、Context Reference Index
  都是投影/目录，不是事实 Owner；
- 每次新的语义咨询使用新的 DecisionId；transport retry 复用
  realization-private correlation；不新增 Product ConsultationId；
- `CoverageStagnantSignal` 使用 AGT-019 M5 双零 `stagnationWindow`；
  `recentEventWindow=12` 只控制 Heartbeat 事件增量；K/R/N 保持可配置；
- Agent Skip 必须是 RUN-004 D3/D7 的 typed union member，带 DecisionId、
  target、reason、evidenceRefs；零 Effect，写入 TargetCoverageMarked，
  与 structural-skip 分账；
- 本 Change 只实现 push-only Context Reference Index 目录（semanticLabel、
  authority、epistemic、capability、availability 等边界元数据）；不实现
  Agent-driven Pull。完整 Pull 语义属于 M6 v2，届时仍须通过 profile/host/
  protocol 三层 capability gate，且不改 World Model/Coverage、不触发新咨询；
- HardCap、ExecutionContract、RenewalWindow 优先于所有 Signal、Heartbeat
  和 Pull；任何新字段必须走 RUN-004 D3/D7、AGT-001 F5 和生成工件同步。

## Acceptance（实现前定稿）

1. RUN-006 重复派发回放：同一 signalKey 在同一冻结窗口只生成一次语义
   咨询；新语义咨询使用新的 DecisionId；transport retry 不重复扣账；
2. 新 ObservationAccepted 改变相关证据才 rearm；证据不变则累计无推进，
   不重复刷屏；
3. Heartbeat、eventDelta、Case Packet 使用同一 eventSeq cutoff，真实 DSH
   请求实际收到三组语义可读状态；
4. Agent Skip 走 typed union，零 Effect，写 TargetCoverageMarked，与
   structural-skip 分账并可回放；
5. push-only Context Reference Index 目录中的每条引用都有 semanticLabel、
   authority、epistemic、capability、availability 和 lineage；截图/
   judgment evidence 不得单独驱动超 Grant 的 Effect。Agent-driven Pull
   不在本 Change 的 Acceptance 内，归 M6 v2；
6. Event Stream、Heartbeat cutoff、P25 journal 在重启/回放中可恢复，不能
   重复 Effect；
7. 全量回归绿 + 场景库随动 + H7/桥审计 + 真机一轮留证。

## Status log

- 2026-10-17 · created · v3.2 定稿 + owner 裁决 A（AGT-019 profile
  行关闭）后立项；初始状态为 draft-not-grilled。
- 2026-10-17 · RESOLVE → PERSIST · v3.4 协议闭包复核完成：修正 P25
  DecisionId/transport correlation 边界、M5 W 与 Heartbeat 窗口混用、
  Skip typed union 前置条件、Pull capability gate 和实现 Acceptance；
  下一步 = PLAN。
- 2026-10-17 · PERSIST → PLAN · plan.md 按协议 → Owner Event Stream → Kernel
  trigger → H7 evidence 的垂直切片落定；完整 Agent-driven Pull 保留 M6 v2。
