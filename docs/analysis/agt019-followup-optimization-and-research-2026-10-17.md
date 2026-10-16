# AGT-019 后续优化：Event Stream / Heartbeat / 事件触发咨询——设计与裁决定稿（v3.2，2026-10-17）

> Status: DRAFT / CANDIDATE
> Authority: NONE（设计定稿 + owner 裁决已录；实现须经 change 通道）
> 谱系：RUN-006 真机六轮暴露弹窗死循环 → 多轮讨论与对抗审查（v1→v2→
> v3 → 术语审计 v3.1 → 内容复核 v3.2：Heartbeat 三组字段具象化 /
> Context Pull 补引用目录与批量查询 / Heartbeat 调度语义消歧）。
> **术语纪律**：只使用仓库 canonical term 或明确标注的候选新增；
> 不引入新总容器名。既有：Agent Decision Consultation /
> AgentDecisionContext / ConsultationProgress / consultation-turn /
> interruption plane（AGT-001 F1）/ RuntimeRunEvent（Host Runtime）/
> ConsultAgent。候选新增（本设计造）：Case Packet、Context Pull、
> Context Reference Index、Heartbeat data。
> 证据锚点：changes/RUN-006/report.md + evidence/run-006/。

---

## 一、总目标

本机制的消费者不是循环检测器，而是：

> **当系统不知道下一步怎么走时，帮助 Agent 判断是否介入、如何介入。**

循环检测只是机械前置条件。主链：

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

`trigger = event` 是描述性修饰，不是新协议类型——与现有 P25
Agent Decision Consultation 不冲突、不新增第二套咨询协议。

## 二、Event Stream（事实事件流）

事件 = 事实源。五种事实事件：

```text
ObservationAccepted    看到了什么（含 bounds/digest 字段）
PopupStateObserved     弹窗状态
EffectReceiptProduced  点了什么（回执）
PostActionVerified     验证结果
TargetCoverageMarked   目标标记（ReachedOverlay / RouteEntered / Skipped）
```

实现约束（钉死）：四类已有现成载体（Kernel effect receipts、
PostActionEffectVerification、账本弹窗记录、账本观测）；实现 =
存量记录流正式化（补 seq）+ 新增 TargetCoverageMarked，不造第二条
事件总线。**RuntimeRunEvent 承载边界 = grill 前置裁决项**（见 §十二）。

四类投影：完整 Event Stream（审计/回放/验证）、Heartbeat（咨询附页）、
Case Packet（trigger=event 载荷）、CoverageReport（任务结果）。

## 三、Derived Signals（派生信号）

信号是派生事实，不是权威状态：

```text
RepeatedTargetSignal / PopupReappearanceSignal / CoverageStagnantSignal
ViewportUnchangedSignal / ScrollZeroDisplacementSignal / KernelUnknownSignal
```

（阈值见 §八参数。）信号只触发咨询与诊断，不改覆盖事实（裁决 D5）。

## 四、Heartbeat（状态投影与调度概念）

**定位**：AgentDecisionContext 内的一类状态投影 + 调度概念，非协议
对象、非新总容器。

**调度语义（消歧，钉死）**：

```text
HeartbeatTick  = 投影刷新动作，按 heartbeatEveryConsults 节奏刷新
Agent Decision Consultation = 每次咨询携带"当前最近一次可用的
                               Heartbeat 投影 + 自上次咨询以来的事件增量"
```

即：刷新节奏与咨询节奏解耦；咨询永远带最新可用快照，不因未到 Tick
而空手，也不因每问必答就强制每问重算。

**内容 = 三组状态（字段级，落入 AgentDecisionContext 的 Heartbeat
data，各字段映射既有载体，不造新容器）**：

```text
① World Model 导航状态（载体：WorldModel/ContainerBelief 投影）
   current revision           当前世界修订号
   current container          当前容器
   current route              当前路由
   BasePageAnchor / Overlay   底页锚点与叠加层引用（D1 裁决约束下）
   last accepted observation  最近一次入账观测（eventSeq/captureId）

② Kernel 执行状态（载体：KernelRunDriver 状态面）
   current phase              当前决策相位（AgentDecisionPhase 既有）
   last effect / receipt      最近效应与回执
   last verification          最近验证结果（PostActionEffectVerification）
   pending obligations        未决义务（PendingObligations 既有）
   next Effect blocked?       下一步效应是否被暂停（interruption plane 置位）

③ Traversal 状态（载体：Coverage Ledger 投影）
   current target             当前目标
   frontier                   前沿集合
   reached / skipped / pending 覆盖三数
   recent route/container transitions  近期路由/容器迁移
   recent target attempts     近期目标尝试（含重复计数）
```

增量机制：`fromEventSeq / toEventSeq / eventDelta / omittedCount`；
首次咨询带全量种子。窗口语义（钉死）：筛选只发生在词表策展层（五类
事实事件），窗口内按序全量、丢最旧、不做二次语义筛选。

参数：`heartbeatEveryConsults`（Tick 节奏，可调待标定）；
`recentEventWindow = 12`（事实事件数，非触发步数）。不用 maxSteps/8。

## 五、事件触发的 Agent Decision Consultation（trigger = event）

复用既有咨询机制（M1 未知页强制咨询的推广）：同一 DecisionId 纪律、
同一选项集词汇，附件换为 Case Packet。

**触发流程（机械）**：

```text
冻结 eventSeq → 生成 Heartbeat snapshot → 组装 Case Packet
→ 暂停下一步 Effect（interruption plane）→ 强制咨询
```

**触发清单**：同目标反复重派 / 同弹窗复现 / 覆盖长期不变 / 滚动无
位移 / 未知路由 / 材料不足 / 策略冲突 / 预算将尽 / 即将 bounded-stop。
（"验证结果互相矛盾"待机械定义；基础设施异常走 bounded-stop 旁路，
不进咨询通道——桥死则咨询不可达。）

**consultation cooldown**：同一信号触发后 Agent 选"再观察"，计数器
重置并冷却 N 轮。

**Case Packet（候选协议面，按介入动作分）**：

```text
skip-target / replan / blocked / takeover
（各包内容见 v3：目标尝试史、全局覆盖、失败链、+指令意图与约束）
```

**resumeToken**：v1 不透明关联号；完整语义随 M6 v2。

## 六、interruption plane 职责边界

只做：是否暂停下一步 / 是否触发咨询 / 选 Case Packet / 限制可选动作。
不做：是否 skip / blocked / 完成——Agent 返回后照旧过 Kernel →
Grounding → Policy → Assurance → Effect Boundary → 验证全链。

## 七、AgentDecisionContext 组成（不新增总容器）

```text
AgentDecisionContext
  ├─ existing fields
  ├─ LocalObservationView        （AGT-019 B3 已有）
  ├─ ConsultationProgress        （RUN-004 已有，B4 已扩）
  ├─ Heartbeat data              （§四三组状态，候选面）
  ├─ Context Reference Index     （§九引用目录，候选面）
  └─ Case Packet                 （仅 trigger=event 时在场，候选面）
```

## 八、裁决记录（owner 已裁，2026-10-17）

- **D1** BasePageAnchor：通过（背景依据；不可直接点击；无 fresh
  occurrence 不可 Grounding；不改 CurrentContainer；不证明页面进入）。
- **D2** 到达即完成分账：ReachedOverlay / RouteEntered / Skipped /
  Pending 四态。
- **D3** M5 进度轴组合：清障验证不算 Coverage Progress + 独立
  CoverageProgressFingerprint；StepsVerified 保留为事实字段。
- **D4** Skip 事件化：TargetCoverageMarked{state,target,reason,
  source(agent|policy-auto),decisionId,evidenceRefs}；零 Effect；双
  指标 taskCoverage=reached+skipped、verifiedReachRate=reached/total；
  预算耗尽不得静默 Skip；允许重试。
- **D5** 几何与 digest 第一阶段只做信号/诊断/慢感知输入，不改
  Coverage Ledger；参与覆盖判定另立 Promotion 决策。
- **M6 v2 正式立项**：抽象计划/具体指令信任阶梯。
- **参数初值**：K=3、R=2、W=5、N=3、recentEventWindow=12；
  heartbeatEveryConsults 可调待标定。

## 九、Context Pull（候选协议面）+ Context Reference Index

**设计形态 = 先看目录、再按引用批量拉取**（不是按类别裸查）。

**Context Reference Index**（随 AgentDecisionContext 常驻的引用目录，
候选面）：

```text
每条引用：
  refId          引用号（Pull 的句柄）
  kind           world-model | observation | event | trace | screenshot
               | evidence | coverage | signal | budget
  owner          事实属主（WorldModel / Kernel / Ledger / Pipeline）
  eventSeq / captureId   定位锚
  freshness      新鲜度（产生时刻 + 有效界）
  lineage        溯源（provenance 摘要）
  availability   可用性（在库 / 需重取 / 已过期 / 不可用）
```

**批量查询形态（Pull 请求）**：

```text
queries[]       引用号列表（refId×N）或 scope 过滤
limit           条数上限
byteBudget      字节上限
cursor          分页游标
```

**纪律**：结果只进咨询上下文（判断级），永不进 World Model、不产生
覆盖事实；像 Defer 一样计轮封顶；scope 枚举（target/popup/events/
signal/coverage/budget，见 v3）继续有效，作为按类检索的补充形态。
**发运 M6 v2。**

## 十、先行核查（与任何实现并行）

- [ ] H7：真实 DSH 请求边界确认装饰后 Context 送达；
- [ ] 桥审计修复：双记装饰前后两态 Context。

## 十一、业界参照

（"参考启发"级；链接全文见 git 历史 v2 版 a8cdd808。）

## 十二、grill 前置裁决项（进入 grill 的第一批追问）

1. **RuntimeRunEvent 承载边界**：它是 Host Runtime 的外层运行事件
   索引，还是要承载 Observation/Popup/Receipt/Verification/Coverage
   五类 Owner 事实？若只做索引，Event Stream 的 Owner 侧落点需要
   另定；
2. **Heartbeat 最小字段集**：§四三组字段哪些进 v1、哪些候补；
3. **Context Reference Index 的 authority**：目录条目由谁铸造、
   freshness 由谁判、availability 的四态语义。

## 待办汇总

- 补齐后进入 grill（owner 主审 + grilling/domain-modeling 双轨，
  第一轮从 §十二三项开始）；
- 实现（顺序）：存量事件流正式化 + TargetCoverageMarked → 矩形
  工具箱 → 融合片 → Derived Signals + Heartbeat → trigger=event
  咨询 + Case Packet → D2/D3/D4 落账；
- 先行核查：H7 + 桥双记；
- M6 v2：信任阶梯 + Context Pull + resume 完整语义；
- RUN-006 旧挂账与留观项（同 v3）。
