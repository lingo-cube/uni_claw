# AGT-019 后续优化：Event Stream / Heartbeat / 事件触发咨询——设计与裁决定稿（v3.1，2026-10-17）

> Status: DRAFT / CANDIDATE
> Authority: NONE（设计定稿 + owner 裁决已录；实现须经 change 通道）
> 谱系：RUN-006 真机六轮暴露弹窗死循环 → 多轮讨论与对抗审查（v1→v2→v3
> → owner 术语审计 → 本 v3.1）。
> **术语纪律（v3.1 修订原因）**：只使用仓库 canonical term 或明确标注
> 的候选新增；不引入新总容器名。既有：Agent Decision Consultation /
> AgentDecisionContext / ConsultationProgress / consultation-turn /
> interruption plane（AGT-001 F1）/ RuntimeRunEvent（Host Runtime）/
> ConsultAgent。候选新增（本设计造，非既有）：Case Packet、Context Pull。
> 证据锚点：changes/RUN-006/report.md + evidence/run-006/。

---

## 一、总目标

本机制的消费者不是循环检测器，而是：

> **当系统不知道下一步怎么走时，帮助 Agent 判断是否介入、如何介入。**

循环检测只是机械前置条件。主链（全部用既有术语 + 标注的候选面）：

```text
Event Stream
  ↓
Derived Signals
  ↓
interruption plane
  ├─ Agent Decision Consultation（scheduled 形态）+ Heartbeat
  ├─ Agent Decision Consultation（trigger = event）
  │    + Heartbeat snapshot
  │    + Case Packet（候选协议面）
  └─ bounded-stop 或带标记 Skip
```

**注意**：`trigger = event` 是**描述性修饰**，不是新协议类型——与现有
P25 Agent Decision Consultation 不冲突、不新增第二套咨询协议。

## 二、Event Stream（事实事件流）

事件 = 事实源（对齐 Host Runtime 的 RuntimeRunEvent 既有设施）。五种
事实事件：

```text
ObservationAccepted    看到了什么（含 bounds/digest 字段）
PopupStateObserved     弹窗状态
EffectReceiptProduced  点了什么（回执）
PostActionVerified     验证结果
TargetCoverageMarked   目标标记（ReachedOverlay / RouteEntered / Skipped）
```

**实现约束（钉死）**：四类已有现成载体（Kernel effect receipts、
PostActionEffectVerification、账本弹窗记录、账本观测；Host Runtime 的
RuntimeRunEvent 事件存储是现成地基）——实现 = 存量记录流正式化（补
seq）+ 新增 TargetCoverageMarked 一类，不造第二条事件总线。

四类投影（同一事实源派生）：

```text
完整 Event Stream → 审计、回放、验证
Heartbeat         → Agent Decision Consultation 的常驻附页
Case Packet       → trigger=event 咨询的附加材料
CoverageReport    → 任务结果
```

## 三、Derived Signals（派生信号）

信号是派生事实，不是权威状态：

```text
RepeatedTargetSignal         同目标重派 ≥K
PopupReappearanceSignal      弹窗清除后复现 ≥R
CoverageStagnantSignal       覆盖进度指纹不变 ≥W
ViewportUnchangedSignal      XML 视口摘要重复 ≥N
ScrollZeroDisplacementSignal 滚动零位移（绑定同元素/坐标空间/容差）
KernelUnknownSignal          未知路由
```

信号只触发咨询与诊断，不改覆盖事实（裁决 D5）。

## 四、Heartbeat（状态投影与调度概念）

**定位**：行为/调度概念 + AgentDecisionContext 内的一类周期性状态
投影——**不冒充为仓库既有协议对象，v1 不新增协议类型**。

每次 Agent Decision Consultation（scheduled 形态）自动附带，内容：

```text
当前 Run 状态 / 当前路由与弹窗
reached / skipped / pending
frontier / 剩余预算
Derived Signals 计数 / 最近变化趋势
上次咨询之后的新增事件（增量）
```

增量机制：`fromEventSeq / toEventSeq / eventDelta / omittedCount`；
首次咨询带全量种子。语义闭环（钉死）：窗口内筛选只发生在词表策展层
（五类事实事件即策展结果），窗口内按序全量、丢最旧、不做二次语义
筛选——防"筛选即偏见"（viewportDigest 教训）。

参数：`heartbeatEveryConsults`（可调待标定）；`recentEventWindow=12`
（最近 12 个事实事件，非"每 12 步触发"）。不使用 maxSteps/8。

## 五、事件触发的 Agent Decision Consultation（trigger = event）

复用既有咨询机制（M1 未知页强制咨询的推广）：同一 DecisionId 纪律、
同一选项集词汇，附件换为 Case Packet。

**触发流程（机械）**：

```text
冻结 eventSeq → 生成 Heartbeat snapshot → 组装 Case Packet
→ 暂停下一步 Effect → 强制 Agent Decision Consultation
```

**触发清单**：同目标反复重派 / 同弹窗复现 / 覆盖长期不变 / 滚动无
位移 / 未知路由 / 材料不足 / 策略冲突 / 预算将尽 / 即将 bounded-stop。
（"验证结果互相矛盾"待机械定义后再入列；slow/bridge/device 基础设施
异常不进此通道——桥死则咨询不可达，走 bounded-stop 带原因旁路。）

**consultation cooldown**：同一信号触发咨询后，若 Agent 选"再观察"，
计数器重置并冷却 N 轮（防咨询自身成环）。

**Case Packet（候选协议面，按介入动作分，不做通用大包）**：

```text
skip-target → 目标完整尝试史、验证史、弹窗史、覆盖分量
replan      → 全局覆盖、frontier、近期事件、剩余预算
blocked     → 失败链、尝试过的动作、失败原因、最后有效状态
takeover    → 以上全部 + 当前指令意图、约束、待完成义务
```

**resumeToken**：v1 为不透明关联号；完整恢复语义随 M6 v2 定。

## 六、interruption plane 职责边界

interruption plane（AGT-001 F1 术语）只做：是否暂停下一步 / 是否触发
咨询 / 选哪种 Case Packet / 限制 Agent 可选动作。**不做**：是否 skip /
是否 blocked / 是否完成——Agent 返回后照旧过 Kernel → Grounding →
Policy → Assurance → Effect Boundary → Post-action Verification 全链。

## 七、AgentDecisionContext 组成（不新增总容器）

```text
AgentDecisionContext
  ├─ existing fields
  ├─ LocalObservationView        （AGT-019 B3 已有）
  ├─ ConsultationProgress        （RUN-004 已有，B4 已扩停滞计数）
  ├─ Heartbeat data              （本设计：周期性状态投影）
  ├─ Context Reference Index     （后续 Context Pull 的引用目录）
  └─ Case Packet                 （仅 trigger=event 咨询时出现；候选面）
```

## 八、裁决记录（owner 已裁，2026-10-17）

- **D1 底页锚点：通过**。BasePageAnchor 入 Context：只作背景与历史
  依据；不能直接点击；无当前 fresh occurrence 不可 Grounding；不改
  CurrentContainer；不证明页面进入完成。
- **D2 到达即完成：分账**。ReachedOverlay ≠ RouteEntered；覆盖四态：
  ReachedOverlay / RouteEntered / Skipped / Pending。
- **D3 M5 进度轴：组合**。清障类验证不算 Coverage Progress + 独立
  CoverageProgressFingerprint；StepsVerified 保留为事实字段。
- **D4 Skip：事件化**。TargetCoverageMarked{state:"Skipped", target,
  reason, source(agent|policy-auto), decisionId, evidenceRefs}；零
  Effect；Agent 主动与系统自动都留同样标记；预算耗尽不得静默 Skip；
  允许重试；双指标：taskCoverage = reached+skipped、
  verifiedReachRate = reached/total。
- **D5 几何与 digest：第一阶段只做咨询/诊断信号与慢感知输入**，不改
  Coverage Ledger；参与覆盖判定须另立 Promotion 决策。
- **M6 v2 正式立项**：抽象计划与具体指令的信任阶梯（升级靠证据积累、
  判断权归 Agent；降级全机械；不可逆动作恒单步）。
- **参数初值**：K_enter=3、R_popup=2、W_stagnation=5、N_digest=3、
  recentEventWindow=12；heartbeatEveryConsults 可调待标定。

## 九、Context Pull（候选协议面，发运 M6 v2）

能力定义现在写全（对着 §二 事件分类学，查协议级类型不查 Host 内部
记录名）：

```text
scope ∈ {
  target:   某目标完整事件史（验证/弹窗/派发）
  popup:    某弹窗指纹全部出现与清除记录
  events:   按类型 + seq 窗口过滤的事实事件
  signal:   某信号的计算输入（如零位移背后的两份观测对照）
  coverage: 逐条目覆盖状态与 skip/failure 原因
  budget:   预算消耗明细
}
+ { limit, 字节上限 } 双预算；结果只进咨询上下文（判断级，永不进
  World Model、不产生覆盖事实）；像 Defer 一样计轮封顶。
```

## 十、先行核查（与任何实现并行）

- [ ] H7：真实 DSH 请求边界（FileDecisionConsult.WriteRequest / HTTP
  peer）确认是否收到装饰后 Context——不把 consult-bridge.json 空值
  直接等同于传输丢失；
- [ ] 桥审计修复：同时记录 Director 装饰前后两态 Context。

## 十一、业界参照（"参考启发"级）

MobileUse（分层反思/按需反思）、Mobile-Agent-v2（反思角色）、
Reflexion（轨迹反思）、AdaPlan-H（自适应粒度）、Plan-and-Execute
（偏差重规划）、AutoDroid/V2（脚本摊销）、auto-mobile PR#1496（浮层
保留底页）、AppAgent v2/综述（结构×视觉）、playwright-spatial-
layout-mcp（空间关系）、Web Scraper（滚动 diff）、YOLO/Appium/
Safeguard（置信度方言）。链接全文见 git 历史 v2 版（a8cdd808）。

## 待办汇总

- 实现（顺序）：存量事件流正式化（对齐 RuntimeRunEvent）+
  TargetCoverageMarked → 矩形工具箱 → 融合片 → Derived Signals +
  Heartbeat → trigger=event 咨询 + Case Packet → D2/D3/D4 落账；
- 先行核查：H7 + 桥双记（可立即开工）；
- M6 v2：信任阶梯 + Context Pull + resume 完整语义；
- RUN-006 旧挂账：deploy.sh preset 工具化、标定冻结（数据后）、slow
  落地内容的决策消费验证；
- 留观：键盘浮层、超视口列表重建、sticky 锚点、视口边缘策略、
  LocalView 空间关系族、跨 run 反思记忆。
