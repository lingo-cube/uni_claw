# AGT-019 后续优化：Event Stream / Heartbeat / 事件触发咨询——设计与裁决定稿（v3.4，2026-10-17）

> Status: DRAFT / CANDIDATE
> Authority: NONE（设计定稿 + owner 裁决已录；实现须经 change 通道）
> 谱系：RUN-006 真机六轮暴露弹窗死循环 → 多轮讨论与对抗审查（v1→v2→
> v3 → 术语审计 v3.1 → 内容复核 v3.2 → grill 裁决 v3.3 → 协议闭包
> 复核 v3.4：Heartbeat
> 三组字段具象化 / Context Pull 补引用目录与批量查询 / Heartbeat 调度
> 语义消歧 / Agent 友好载荷与双层信号身份冻结）。
> **术语纪律**：只使用仓库 canonical term 或明确标注的候选新增；
> 不引入新总容器名。既有：Agent Decision Consultation /
> AgentDecisionContext / ConsultationProgress / consultation-turn /
> interruption plane（AGT-001 F1）/ RuntimeRunEvent（Host Runtime）/
> ConsultAgent。候选新增（本设计造）：Case Packet、Context Pull、
> Context Reference Index、Heartbeat。
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

**Agent 友好原则（决策层载荷，横切规则）**：进入咨询上下文的信息以
文本与语义为主、id 与数值为锚；机器可判的枚举名优先于数字；任何引用
都必须附带一句“这是什么”的短语义标签。数值可以留在感知和内部计算层，
跨层进入 Agent 决策面时必须语义化。它与“事实无判语”正交：后者禁止
把推断写成事实，前者要求事实被 Agent 读懂。

**与现有实现的关系（边界钉死）**：本文是 AGT-019 的后续设计输入，仍是
`DRAFT / CANDIDATE`，不是当前协议或实现依据。现行实现继续以 AGT-019
M1–M5、RUN-004 P25 和生成的 `AgentDecisionContext` schema 为准。Heartbeat、
Case Packet、Context Reference Index、事件触发咨询若进入实现，必须分别
经 RUN-004 D3/D7 的协议修订、AGT-001 F5 handshake 和生成工件同步；本文
不得假设只加一个字段就能上线。任何 Signal、Heartbeat 或 Pull 都不能扩展
P25 HardCap、ExecutionContract 或 RenewalWindow 的预算。

**P25 身份纪律**：每一次新的语义咨询生成新的 `DecisionId`，即使仍处在
同一个 decision boundary；同一事件触发的传输重试沿用 realization-private
transport correlation 并回放原结果。共享 Product 协议不新增
`ConsultationId` 或 transport correlation 字段。这样事件升级不会破坏
咨询账本的幂等、计数和恢复语义。

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
事件总线。**RuntimeRunEvent 承载边界按 §十三已裁决**：它只做 Runtime
层外层运行事件索引，不承载五类 Owner 事实。

四类投影：完整 Event Stream（审计/回放/验证）、Heartbeat（咨询附页）、
Case Packet（trigger=event 载荷）、CoverageReport（任务结果）。

## 三、Derived Signals（派生信号）

信号是派生事实，不是权威状态：

```text
RepeatedTargetSignal / PopupReappearanceSignal / CoverageStagnantSignal
ViewportUnchangedSignal / ScrollZeroDisplacementSignal / KernelUnknownSignal
```

`CoverageStagnantSignal` 沿用 M5 的双零定义：在 `stagnationWindow` 内
同时没有新发现、没有新完成验证；同 route 重复只作辅助信号。其余信号的
阈值见 §八参数。信号只触发咨询与诊断，不改覆盖事实（裁决 D5）。

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

**内容 = 三组状态（字段级，落入 `AgentDecisionContext.heartbeat`，
各字段映射既有载体，不造新容器）**：

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

复用既有 P25 咨询机制（M1 未知页强制咨询的推广）：使用同一选项集词汇和
同一 Kernel 执法链；每次新的语义咨询使用新的 `DecisionId`，只有 transport
retry 复用 realization-private transport correlation。

**触发流程（机械）**：

```text
冻结 eventSeq（Heartbeat / eventDelta / Case Packet 同一截止点）
→ 生成 Heartbeat snapshot → 组装 Case Packet
→ 暂停下一步 Effect（interruption plane）→ 强制咨询
```

**触发清单**：同目标反复重派 / 同弹窗复现 / 覆盖长期不变 / 滚动无
位移 / 未知路由 / 材料不足 / 策略冲突 / 预算将尽 / 即将 bounded-stop。
（"验证结果互相矛盾"待机械定义；基础设施异常走 bounded-stop 旁路，
不进咨询通道——桥死则咨询不可达。）

**双层信号身份与 rearm**：Host 以稳定字段形成机械 `signalKey`：
`signalType + targetId + popupIdentity + route/container + evidenceFingerprint`。
Host 只用它做同信号合并、去重和基础防抖；Agent 结合 Heartbeat、Case
Packet 与 Pull 证据判断是否属于同一个介入问题，并决定再观察、换策略、
Skip、Blocked 或接管。Host 无法机械确认时，生成需要 Agent 判断的歧义
介入案例，不强行归并。

同一 `signalKey` 触发后，只有在出现新的 `ObservationAccepted`，且相关
目标、弹窗或 Coverage 证据发生变化后，才重新具备触发资格（rearm）。
如果新观察证明状态没有变化，不重复发送相同咨询，而是累计一次无推进，
达到 N 次后升级为 bounded-stop 或要求 Agent 改变策略。rearm 不是固定
时间等待，也不与 K/R/W/N 复用同一含义。

**Case Packet（按介入案例分，不等于 AgentDecision kind）**：

```text
skip-target / replan / blocked / takeover

固定骨架：
  triggerSignals
  heartbeatSnapshot
  eventDelta
  relevantRefs
  availableActions
  constraints
```

其中 `availableActions` 是允许的动作集合，不能直接写成“应该 Skip”或
“已经 Blocked”。这些案例类型不能绕过现有的封闭回答 union；v1 的
`Reobserve`、`Replan`、`Blocked`、`Act`/`Plan`/`Policy` 必须继续使用已有
类型。`Skip` 若要成为 Agent 的主动回答，必须通过 RUN-004 D3/D7 增加一个
带 `DecisionId`、`target`、`reason`、`evidenceRefs` 的 typed union member；
在该协议修订完成前，不得用字符串 action 或 Case Packet 字段模拟 Skip。
Case Packet 只提供证据，不替 Agent 下结论。

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
  ├─ heartbeat                   （§四三组状态，Heartbeat 投影）
  ├─ Context Reference Index     （§九引用目录，候选协议面）
  └─ Case Packet                 （仅 trigger=event 时在场，候选协议面）
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
  预算耗尽不得静默 Skip；允许重试。Agent Skip 必须与配置或结构性
  `structural-skip` 分开记账，不得把两者合并成同一种跳过理由；Agent
  主动 Skip 需要在 RUN-004 D3/D7 中形成 typed union member，不能靠字符串
  或 Case Packet 字段伪造。
- **D5** 几何与 digest 第一阶段只做信号/诊断/慢感知输入，不改
  Coverage Ledger；参与覆盖判定另立 Promotion 决策。
- **M6 v2 正式立项**：抽象计划/具体指令信任阶梯。
- **Q1 RuntimeRunEvent 边界**：保持 Runtime 层外层运行事件索引；五类
  事实事件归各自 Owner，经显式投影进入咨询上下文，不由 Runtime 持有。
- **Q2 Heartbeat v1**：World Model、Kernel、Traversal 三组字段全部保留；
  字段按 Agent 友好原则提供语义文本，id/数值作为锚。
- **Q3 Context Reference Index authority**：Owner 铸造 refId 与 lineage，
  Store 管可用性与读取，Source Owner 判 freshness，Index 只读不拥有事实；
  availability 四态为 available / refresh_required / expired / unavailable。
- **Q4 术语**：`HeartbeatTick` 是内部投影刷新动作；`Heartbeat` 是
  AgentDecisionContext 中的状态概览；不另造总容器。
- **Q5–Q7、Q9**：触发信号分机械升级/语义不确定/运行边界；同一冻结窗口
  合并；Case Packet 固定骨架；事件触发的厚 Push 必带 Heartbeat snapshot、
  event delta 与 Case Packet。
- **Q8、Q12、Q13**：Pull 优先 ref、同时允许受控语义查询；只能访问当前
  Run 的登记 kind 与范围；新结果先进入当前咨询的临时 Index，持久化由
  Owner 决定；Pull 不改 World Model/Coverage/Signal，也不触发新咨询。
- **Q10**：Host 判断机械 `signalKey` 并负责去重；Agent 判断语义上的同一
  介入问题并决定动作；无法机械确认时交给 Agent；同信号以新观测和证据
  变化作为 rearm 条件。
- **Q11、Q16 参数治理**：K=3（同目标重复派发）、R=2（弹窗复现）、
  W=5（“新发现 + 新完成验证”双零的停滞判定窗口）、N=3（连续无推进
  升级次数）；含义
  冻结、数值可配置可标定，每个 Run 固定 thresholdProfileId、参数快照、
  rearmPolicyVersion 与 effectiveEventSeq；Agent 不直接改写当前 Run 参数。
- **Q14–Q15**：Agent 的“同一问题”判断不写入领域事实；Pull 结果支持
  ok / partial / budget_exhausted / unavailable / expired / invalid_scope /
  not_authorized / cursor_exhausted，并逐条携带 refId、kind、semanticLabel、
  availability、lineage。
- **参数初值**：K=3、R=2、W=5、N=3、recentEventWindow=12；其中 W
  属于 M5 `stagnationWindow`，recentEventWindow 只控制 Heartbeat 事件
  增量，不得互换；
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
  semanticLabel  短语义标签（说明“这是什么”）
  sourceAuthority  来源权威（只读投影的事实 Owner）
  epistemic      observed | partial | ambiguous | judgment
  audience       agent | host | audit
  redaction      脱敏与可见性约束
  capability     读取所需能力与宿主绑定要求
  maxBytes       单条材料上限
  availability   available | refresh_required | expired | unavailable
```

**批量查询形态（Pull 请求）**：

```text
queries[]       查询项列表；每项可带 refIds 或受控语义过滤
  refIds[]?     已有引用号（可选）
  kind?         允许的资料类型（必须登记）
  scope?        target | popup | events | signal | coverage | budget
  targetId?     目标范围（可选）
  fromEventSeq? 起始事件序号（可选）
  toEventSeq?   结束事件序号（可选）
limit           条数上限
byteBudget      字节上限
cursor          分页游标
```

Agent 优先使用已有 ref，也可以提出新的语义查询；Host 只解析当前 Run
授权范围内、已登记 kind 的查询。查询必须通过 profile 声明、宿主绑定和
协议门控三层 capability 检查；缺任一层即拒绝。新查询的结果生成新的
refId，并加入当前咨询的临时 Context Reference Index，供本次咨询后续 Pull
复用；是否持久化由对应 Owner 决定。未知 kind、越界 scope、无范围锚的全
历史查询或未通过 redaction 的查询拒绝。

Pull 结果只进咨询上下文（判断级），永不进 World Model、不产生 Coverage
事实；截图、视觉 trace 和 judgment evidence 只能作为判断级材料，不能单独
驱动超既有 Grant 的 Effect，也不能写入事实账本；不触发新的 Heartbeat 或
consultation。批量结果支持部分成功，统一状态为：

```text
ok | partial | budget_exhausted | unavailable | expired |
invalid_scope | not_authorized | cursor_exhausted
```

每条结果都携带 `refId`、`kind`、`semanticLabel`、`availability`、
`lineage`。Pull 计轮封顶，受 `pullCount`、`byteBudget` 和 `cursor` 限制。
**发运 M6 v2。**

## 十、实现前 Acceptance（当前未证明）

下列验收是本设计能否解决 RUN-006 的必要条件；v3.4 只定义判据，实际
结果必须进入对应 Change/evidence，不得用本设计文档的文字代替真机证据。

| 场景 | method | expected | evidence |
|---|---|---|---|
| 同目标/同弹窗重复触发 | 回放 RUN-006 的重复派发序列 | 同一 `signalKey` 在同一冻结窗口只生成一次语义咨询；transport retry 不重复扣账 | consultation journal + eventSeq 对照 |
| 新观察后的 rearm | 新增 `ObservationAccepted`，分别改变/不改变目标证据 | 证据改变才重新具备触发资格；不改变则累计无推进，不重复刷屏 | signal/rearm trace |
| P25 身份 | 同一 boundary 发起新咨询与 transport retry | 新语义咨询使用新 DecisionId；retry 复用 realization-private transport correlation 并回放旧结果 | P25 journal |
| Agent Skip | Agent 通过新增 typed Skip member 返回 target/reason/evidenceRefs | 零 Effect，写入 `TargetCoverageMarked`，与 structural-skip 分账，可重试 | RUN-004 contract + Coverage Ledger + Effect receipts |
| Pull 边界 | 请求已登记、越界、未授权、过期和超预算材料 | 分别返回 typed status；不得写 World Model/Coverage 或触发新咨询 | Pull audit + authority trace |
| 截图/判断级材料 | Pull screenshot 或 judgment evidence 后尝试 Effect | capability/epistemic/Grant 任一不满足即拒绝或重新授权 | gate rejection + zero Effect |
| 真机传输 | 真实 DSH 请求经过装饰层和桥 | Heartbeat、eventDelta、Case Packet 在 Agent 实际收到，审计记录点一致 | H7 + bridge audit + device run |
| 重启/回放 | 中断后恢复同一 Run | Event Stream、Heartbeat cutoff、P25 journal 可重放，不能重复 Effect | restart/replay evidence |

## 十一、先行核查（与任何实现并行）

- [ ] H7：真实 DSH 请求边界确认装饰后 Context 送达；
- [ ] 桥审计修复：双记装饰前后两态 Context。

## 十二、业界参照

（"参考启发"级；链接全文见 git 历史 v2 版 a8cdd808。）

## 十三、grill 裁决记录（2026-10-17）

本轮 grill 已完成。Q1–Q16 的裁决已并入 §八、§九及前述触发规则；
设计层面的前置问题已清空，但实现 Acceptance 仍须按 §十逐项证明。

裁决共同约束：Runtime、WorldModel、Kernel、Ledger、Pipeline 各自保有
事实权威；Heartbeat、Case Packet、Context Reference Index 都是面向
Agent Decision Consultation 的投影或目录，不得反向成为领域事实源。

## 待办汇总

- grill 已完成（owner 主审 + grilling/domain-modeling 双轨）；
- 实现（顺序）：存量事件流正式化 + TargetCoverageMarked → 矩形
  工具箱 → 融合片 → Derived Signals + Heartbeat → trigger=event
  咨询 + Case Packet → D2/D3/D4 落账；
- 先行核查：H7 + 桥双记；
- M6 v2：信任阶梯 + Context Pull + resume 完整语义；
- RUN-006 旧挂账与留观项（同 v3）。
