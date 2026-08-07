# UniClaw Agent Runtime — Greenfield 构建宪章（按职责分类）

> 版本: v1.0（分类重组版）| 日期: 2026-08-07 | 状态: Active
> 来源: 《UniClaw Agent Runtime — Greenfield 构建主提示词》（60 节原始版），按职责分类重组，**内容未删改**。
> 定位: 新 Runtime 的**完整行为指导**——回答"Agent Runtime Greenfield 应该怎样被构建和运行"。
> 关系: [constitution/runtime-architecture-contract.md](constitution/runtime-architecture-contract.md) = 从本宪章提炼的 **12 条不可违反边界契约**（硬约束子集，机械 Guard 验证）；本宪章 = 完整行为指导（含生命周期、场景、路线）。
> 读者: 所有参与 Agent Runtime Greenfield 的 AI Coding Agent / 开发者。
> 导航: AGENTS.md「Agent Runtime（新）— Greenfield」段；OpenSpec: `openspec/changes/greenfield-agent-runtime/`

## 分类总览（原 § 号 → 分类映射）

| 分类 | 职责（本类回答什么） | 原节 |
|------|---------------------|------|
| [Part I](#part-i--使命与第一原则) 使命与第一原则 | 系统为什么存在、以什么信念运转 | §1-3, §55 |
| [Part II](#part-ii--核心运行职责) 核心运行职责 | Agent → Container → Traversal → Environment 各自负责什么、如何连成脊柱 | §4-8, §56 |
| [Part III](#part-iii--世界模型与语义判断) 世界模型与语义判断 | 系统如何认识外部世界、证据与判断如何分层 | §9-16 |
| [Part IV](#part-iv--生命周期与执行协议) 生命周期与执行协议 | 状态机与阶段协议如何推进、谁在何时被建立 | §17-20 |
| [Part V](#part-v--故障模型与恢复) 故障模型与恢复 | 假设失效如何被表达、上报、恢复并验证 | §21-25, §44 |
| [Part VI](#part-vi--智能与可观测性) 智能与可观测性 | AI 如何增强确定性执行、系统为何每一步都能被解释 | §26-28 |
| [Part VII](#part-vii--运行期约束) 运行期约束 | 取消 / 暂停 / 并发边界在哪 | §46-47 |
| [Part VIII](#part-viii--架构治理与编码纪律) 架构治理与编码纪律 | 状态 owner、决策 authority、依赖方向、类与接口纪律 | §29-32, §45, §48-51 |
| [Part IX](#part-ix--验证体系) 验证体系 | 架构如何被自动验证、完成如何被证据证明 | §41-43 |
| [Part X](#part-x--场景规范) 场景规范 | 必须通过的 5 个 Scenario 及其仿真前提 | §33-38 |
| [Part XI](#part-xi--建设路线与工作方式) 建设路线与工作方式 | 分阶段建设顺序、项目结构、完成标准、AI 工作方式 | §39-40, §54, §57, §60 |
| [Part XII](#part-xii--文档与架构决策) 文档与架构决策 | 文档体系如何保持对 Coding Agent 友好、什么必须记 ADR | §52-53 |
| [Part XIII](#part-xiii--设计自由度与最终原则) 设计自由度与最终原则 | 哪些可以偏离、哪些永不偏离 | §58-59 |

---

## Part I — 使命与第一原则

> 本类回答：系统为什么存在、以什么信念运转。

### 1. 系统目标

UniClaw 是一个运行在真实 GUI / Device Environment 上的智能执行 Runtime。

系统接收一个用户 Intent，例如：

"打开系统设置中的 WiFi"

"找到某个联系人并发送消息"

"进入某个 App，完成一系列页面操作"

然后持续执行：

Intent
→ Plan
→ Establish Environment
→ Observe
→ Understand
→ Decide
→ Execute
→ Verify
→ Update State
→ Continue

直到：

Completed

或者：

Failed / Terminated

真实设备环境不是可靠的内部程序状态。

设备可能发生：

- 页面加载延迟；
- 动画；
- Popup；
- Scroll；
- 页面结构变化；
- 元素位置变化；
- App 被关闭；
- 跳转到错误页面；
- 回到 Launcher；
- 外部事件改变当前页面；
- 操作已经执行但系统不知道是否成功。

因此 UniClaw 不是普通 Workflow Engine。

它必须能够持续解决：

1. 我想完成什么？
2. 我当前认为自己在哪里？
3. 现实世界实际上是什么？
4. 我的执行状态是否仍然可信？
5. 下一步应该执行什么？
6. 如果状态失配，应该在哪里恢复？
7. 恢复完成以后，如何验证并继续？

### 2. 核心控制闭环

Runtime 的核心不是：

Plan → Execute

而是：

Observe
   ↓
Reconcile
   ↓
Decide
   ↓
Execute
   ↓
Observe
   ↓
Verify
   ↓
Update
   ↓
Continue

如果：

Expected World
≠
Observed World

则：

Detect Trap
→ Determine Scope
→ Recover
→ Observe
→ Verify Recovery
→ Reconcile
→ Resume

任何架构设计都必须服务于这个闭环。

### 3. 第一原则：External World 不可信

设备是一个外部、弱状态甚至可以视为无状态的执行环境。

程序内部记录：

CurrentPage = WiFi

不能证明真实设备仍然在 WiFi 页面。

程序内部记录：

ActionExecuted = true

不能证明操作真实生效。

因此：

Internal Runtime State
≠
External World State

必须通过 Observation 对现实重新确认。

禁止设计：

"FSM 处于某状态，所以现实一定处于对应状态。"

Runtime 必须允许：

Observe
→ Discover Mismatch
→ Correct Belief
→ Correct Runtime
→ Continue / Recover

### 55. Greenfield 最大优势

当前不存在历史代码。

因此：

不要为不存在的兼容性设计 Adapter。

不要提前建立 Legacy layer。

不要模拟未来可能存在的迁移问题。

不要因为某种常见框架习惯就引入复杂架构。

每一个复杂度都必须由当前 Requirement 支付成本。

Greenfield 的目标不是：

第一天拥有完整 Agent Framework。

而是：

第一天拥有正确的 Architecture Spine。

---

## Part II — 核心运行职责

> 本类回答：Agent / Container / Traversal / Environment 各自负责什么、不允许承担什么、如何连成一条脊柱。

### 4. 核心架构

第一阶段只定义四个核心运行职责：

Agent
→ Container
→ Traversal
→ Environment

另外存在若干支持能力：

Startup
World Model
Planning
Memory
Recovery
AI
Observability

这些支持能力不是新的"核心层"。

不要未经必要性证明继续创造：

TaskContainer
ExecutionContainer
PageAgent
AgentFSM
TraversalAgent
WorldAgent

等概念。

优先保持概念数量少而清晰。

### 5. Agent

Agent 是一次 Run 范围内的最高控制者。

它拥有整个执行过程的目标和最高语义判断权。

Agent 负责：

- Intent；
- Goal；
- Plan；
- Run-level lifecycle；
- World Belief；
- 当前 Container 管理；
- Container 切换；
- Container Rebind / Invalidate；
- Trap Scope 判断；
- Agent-level Recovery；
- Re-plan；
- Memory 协调；
- AI Decision；
- 高层完成条件；
- 最终成功 / 失败判断。

Agent 回答：

"为了完成当前目标，现在应该做什么？"

Agent 不应该直接承担：

- OCR；
- 点击实现；
- Scroll 实现；
- 坐标转换；
- 单步 Traversal 状态推进；
- 大量页面元素 bookkeeping；
- 每一种具体 App 的特殊规则。

AgentRuntime 是 Controller / Control Plane。

禁止将 AgentRuntime 设计成新的 God Object。

Agent 可以编排能力，但具体能力必须由明确组件提供。

### 6. Container

Container 是 UniClaw 中一个核心 Runtime 概念。

定义：

"一个语义页面范围内的局部运行状态域。"

Container 不是：

- UI Control；
- screenshot；
- Traversal Node；
- Task；
- Frame；
- FSM；
- App；
- 单纯页面 DTO。

例如：

Android Settings Main 页面：

顶部：

Settings Main
WiFi
Bluetooth
Apps

向下 Scroll：

Settings Main
Accessibility
Security
About

视觉内容发生了巨大变化。

但语义页面仍然是：

Settings Main

因此仍然属于：

同一个 Container。

而：

Settings Main
→ Network & Internet

通常意味着进入新的语义页面，因此进入新的 Container。

Container 负责：

- Semantic Identity；
- 当前 Observation；
- 当前页面局部状态；
- Local Progress；
- 当前可见元素；
- visited / failed / scroll 等局部 bookkeeping；
- Local Traversal Graph；
- Local Grounding；
- Traversal Runtime；
- 页面范围内的局部恢复；
- 判断当前 Container 是否完成；
- 判断当前 Observation 是否仍可能属于自己。

Container 回答：

"在当前这个语义页面范围内，我应该如何继续完成局部执行？"

Container 的判断属于：

Local Belief。

Agent 拥有更高语义 Authority。

因此：

Agent 可以：

Rebind Container
Invalidate Container
Correct Container Identity
Switch Active Container

Container 不得反过来修改 Agent 的全局目标或世界真相。

### 7. Traversal

Traversal 是局部、确定性的执行 Kernel。

它负责：

"已经知道当前要执行一个局部步骤以后，如何可靠执行这一小步。"

典型执行协议：

Select
→ Check
→ Execute
→ Verify
→ Branch / Complete

具体命名可以根据实现调整。

Traversal 可以负责：

- candidate selection；
- precondition check；
- target resolve；
- operation execute；
- result verify；
- retry；
- re-resolve；
- re-observe；
- step bookkeeping；
- action result；
- structured failure / trap emission。

Traversal 不负责：

- 世界级语义理解；
- Agent Goal；
- App 是否已经退出；
- 当前 Plan 是否应该重写；
- Container Semantic Identity 最终裁决；
- Agent-level Recovery；
- 直接调用 LLM 做所有决策。

Traversal 的最终目标是：

Deterministic
Testable
Observable
Replayable as much as practical

它应该成为 execution kernel，而不是"聪明的大脑"。

### 8. Environment

Environment 是外部世界能力边界。

Environment 至少包括：

Observation capabilities
和
Action capabilities。

例如：

Vision
OCR
YOLO
Screenshot
Device Controller
Tap
Swipe
Back
Launch App
External API

Environment 不拥有任务决策。

Environment 回答：

"我现在能看到什么？"

以及：

"请让我对世界执行这个动作。"

Environment 不回答：

"为了完成任务下一步应该做什么？"

### 56. Architecture Spine

第一阶段必须建立并稳定的是：

                Agent
                  │
                  ▼
             World Belief
                  │
         ┌────────┴────────┐
         │                 │
      Decide          Active Container
                           │
                           ▼
                       Traversal
                           │
                           ▼
                       Environment
                           │
                           ▼
                      Observation
                           │
                           └──────────────→ Reconcile

异常路径：

Traversal / Container / Environment
              │
              ▼
             Trap
              │
       Determine Scope
              │
              ▼
           Recovery
              │
              ▼
           Observe
              │
              ▼
            Verify
              │
              ▼
           Reconcile
              │
              ▼
            Resume

这条 Spine 比目录、类数量和 Pattern 更重要。

---

## Part III — 世界模型与语义判断

> 本类回答：系统如何认识外部世界——Observation 是证据、World Belief 是可修正的判断、Runtime State 是内部执行簿记，四者必须严格分层。

### 9. Observation

Observation 是：

"某一时刻从现实世界采集到的证据。"

例如：

Observation
{
    Screenshot
    Elements
    OCR
    DetectedControls
    ForegroundApp
    PopupSignals
    Fingerprint
    Timestamp
}

Observation 是事实证据的载体。

但 Observation 本身也可能：

- 不完整；
- 有识别错误；
- 有延迟；
- 有低置信度。

所以：

Observation
≠
Semantic Truth

它只是 World Belief 的输入。

### 10. World Model / World Belief

World Belief 表示：

"Agent 根据当前 Observation、历史、Memory 和语义推断后，对现实世界形成的当前最佳判断。"

例如：

WorldBelief
{
    ForegroundApplication
    SemanticPage
    ActiveContainer
    Confidence
    Evidence
    DriftStatus
}

World Belief 必须允许：

Unknown
Uncertain
Conflicting

不要强迫系统在证据不足时给出假确定答案。

推荐所有重要语义判断都可以携带：

Confidence
Evidence
Source
Timestamp / Freshness

World Belief 可以被新的 Observation 修正。

### 11. Runtime State

Runtime State 与 World Belief 必须严格分离。

Runtime State 表示：

程序为了执行维护的内部状态。

例如：

CurrentTraversalStep
SelectedNode
RetryCount
VisitedCandidate
LastAction
ActionJournal
LocalProgress

World Belief 表示：

程序当前认为现实是什么。

禁止将二者混成一个巨大 Context。

### 12. Memory

Memory 表示：

过去积累的知识。

例如：

- 某种页面的语义模式；
- 某种 Container Identity；
- 某 App 页面结构经验；
- 某些元素匹配经验；
- Recovery 成功路径；
- AI 分析结果。

但：

Memory is not truth.

Memory 只能提供：

Prior / Advice / Evidence

新的 Observation 可以否定 Memory。

高置信度的当前现实证据优先于历史 Memory。

### 13. Plan

Plan 表示：

"为了完成 Goal，目前预计可以采取的执行结构。"

Plan 不是现实世界。

Plan 是：

Executable Hypothesis
/
Execution Prior

因此：

Plan
≠
World Model

如果世界发生变化：

系统可以重新 Ground、修正甚至重新规划。

禁止：

因为 Plan 里存在 Node A，
所以系统默认现实一定存在 Node A。

### 14. Graph

Graph 可以表示：

- Plan；
- Container 内部局部执行结构；
- 已发现的导航关系。

但必须明确每一种 Graph 表示什么。

不要创建一个 Graph 然后同时让它表示：

Plan
+
Reality
+
History
+
Navigation
+
Execution Stack

一个 Graph 必须拥有明确语义。

第一阶段建议：

Local Traversal Graph belongs to Container.

Container 之间可以首先只使用：

Active Container Stack

当真实需求证明有价值后，再增加：

Container Navigation Graph。

不要假定整个 GUI World 是一棵永久稳定的树。

### 15. Dynamic Grounding

静态 Plan 与当前真实页面之间需要 Grounding。

例如：

LocalPlan：

Find "WiFi"

Current Observation：

[
    "Internet",
    "WiFi",
    "Bluetooth"
]

Grounding：

Plan Requirement
+
Observation
+
Rules
+
Memory
→
Grounded Candidate

Dynamic Match 的本质应该是：

Grounding

而不是：

永久生成新的世界事实。

Grounding Result 必须允许在新的 Observation 后重新计算。

### 16. Semantic Identity

必须严格区分：

Semantic Identity
Snapshot
Fingerprint

Semantic Identity：

"这是哪个语义页面 / Container？"

Snapshot：

"当前时刻看到了什么？"

Fingerprint：

"当前观察内容是否发生明显变化？"

因此：

FingerprintChanged
≠
ContainerChanged

FingerprintChanged
≠
NavigationOccurred

FingerprintChanged
≠
ShouldPressBack

Fingerprint 只能作为廉价 Observation Evidence。

禁止将它作为强页面 Identity。

---

## Part IV — 生命周期与执行协议

> 本类回答：状态机与阶段协议如何推进——FSM 只做 protocol transition；Run 生命周期、Startup、Recovery Anchor 分别在何时被建立。

### 17. FSM

FSM 在本系统中的定义：

"用于表达有限生命周期或执行协议，并约束合法状态转换的确定性机制。"

FSM 的职责：

- current phase；
- legal transition；
- lifecycle；
- protocol；
- deterministic progression；
- observability。

FSM 不负责：

- Semantic Reasoning；
- Planning；
- World Model；
- Memory；
- AI Decision；
- Page Identity；
- Container Identity；
- 高层 Recovery Strategy。

原则：

State belongs to Runtime.
Truth belongs to World Model.
Decision belongs to Agent / Policy.
Transition belongs to FSM.

不要因为系统有多个层，就为每层机械创建 FSM。

只有在：

- 生命周期清晰；
- 状态有限；
- 转换值得约束；
- 状态变化值得测试和 Trace；

时才创建 FSM。

### 18. Global Lifecycle

整个 Run 需要一个非常简单的生命周期。

可以设计类似：

Idle
Initializing
Running
Paused
Completed
Failed
Terminated

具体枚举可以讨论。

Global Lifecycle 只回答：

"这个 Run 当前处于什么生命周期？"

它不承担世界判断。

它不承担页面恢复。

它不承担 Agent Intelligence。

### 19. Startup

正式执行之前，必须建立一个可信工作环境。

Startup 是明确的一次生命周期阶段。

它可能包括：

Attach Device
→ Initialize Capabilities
→ Launch / Bind Application
→ Wait Until Observable
→ Observe
→ Resolve Initial Semantic World
→ Establish Initial Container
→ Establish Recovery Anchor
→ Ready

Startup 成功以后，才允许 Runtime 进入正式执行状态。

### 20. Recovery Anchor

Recovery Anchor 是 Startup 建立的重要产物。

它不是 Traversal Root Node。

它表示：

"当 Agent 完全迷失时，至少可以恢复到这里重新建立可信世界。"

例如：

RecoveryAnchor
{
    ApplicationIdentity
    EntryStrategy
    ExpectedSemanticEntry
    RestoreRecipe
    VerificationCriteria
}

例：

Application:
Android Settings

RestoreRecipe:

ColdLaunch Settings
→ Wait
→ Observe
→ Verify Settings Main

如果 Runtime 后续进入：

Desktop
Unknown App
Unknown Page

最坏情况下应该可以：

Current World
→ Recovery Anchor
→ Reconstruct Expected Container
→ Resume

---

## Part V — 故障模型与恢复

> 本类回答：执行假设失效如何被表达（Trap）、按什么 Scope 上报、如何恢复并验证——以及为什么 Recovery 不是单个动作。

### 21. Trap

Trap 是一等 Runtime 模型。

Trap 不等于 Exception。

Exception 通常表示：

技术执行失败。

Trap 表示：

"当前执行过程所依赖的状态假设可能失效，继续按照原控制流执行已经不可靠。"

例如：

ActionFailed
TargetLost
StateMismatch
UnexpectedPage
ContainerMismatch
WorldLost
PlanInvalid

Trap 至少应该携带：

Source
Scope
Kind
Expected
Observed
LastAction
Evidence
Timestamp
Recoverability

推荐 Scope：

Step
Container
Agent

### 22. Trap Scope

Step Scope：

局部动作问题。

例如：

click timeout
coordinate stale
temporary target missing

允许 Traversal 尝试：

Retry
ReResolve
ReObserve

Container Scope：

问题仍处于当前语义页面范围。

例如：

Popup
页面局部结构改变
Scroll 状态异常
当前 Candidate 消失
需要重新 Ground

由 Container 处理。

Agent Scope：

当前局部页面模型已经无法可信控制现实。

例如：

Desktop
App exited
Other application
Unknown semantic page
Plan invalid
无法确认如何返回当前 Container

由 Agent 处理。

原则：

Lower Scope may recover locally.

如果不能证明恢复成功：

Escalate Upward.

低层不得偷偷执行高层恢复。

### 23. Recovery

Recovery 不是：

执行 PressBack()

Recovery 是完整协议：

Detect
→ Diagnose
→ Plan Recovery
→ Execute
→ Observe
→ Verify
→ Reconcile
→ Resume

Recovery 成功必须经过 Observation + Verification。

不能：

Recovery Action returned success
→ assume recovered

如果无法验证：

Recovery 仍然未完成。

### 24. Recovery Mechanism

不同 Scope 不需要三套完全不同的 Recovery Framework。

可以使用统一机制：

RecoveryRequest
→ RecoveryPlanner
→ RecoveryPlan
→ RecoveryRuntime
→ RecoveryResult

其中：

RecoveryPlan 可以不同。

例如 Container Scope：

Dismiss Popup
Reobserve
Reground
Retry

Agent Scope：

Go Home
Cold Launch
Restore Recovery Anchor
Navigate
Rebind Container

Mechanism 可以共享。

Authority 不共享。

### 25. Action Safety

真实 UI Action 是有副作用的。

必须考虑：

- action 是否已经发送；
- action 是否可能执行但响应丢失；
- retry 是否安全；
- action 是否幂等；
- 是否需要重新 Observe 再决定。

因此应该存在：

Action Intent
Action Dispatch Record
Action Result
Post-action Observation

不要简单：

catch timeout
→ retry click

因为第一次 click 可能已经成功。

高风险的非幂等操作必须：

Observe first
→ determine actual state
→ decide retry

### 44. Error、Trap、Failure 必须区分

Error：

技术问题。

例如：

JSON parse failed

Vision provider unavailable

Trap：

执行假设失效。

例如：

Expected Network Page
Observed Launcher

Failure：

某个执行范围最终无法完成。

例如：

Container recovery exhausted

Run unable to restore environment

不要使用：

catch(Exception)
→ ErrorHandling

处理所有情况。

---

## Part VI — 智能与可观测性

> 本类回答：AI 如何作为可插拔能力增强确定性执行（而不是替换 Runtime 架构），以及系统如何做到每一步都可被解释。

### 26. AI / LLM / VLM

AI 是可插拔能力。

不是 Runtime 核心流程的唯一实现。

优先路径：

Fast Vision
+
Deterministic Rules
+
Memory

用于高频运行。

LLM / VLM 适合：

- Startup 首次语义识别；
- Unknown Page；
- Container Identity 低置信度；
- Grounding 无法可靠完成；
- Recovery 需要复杂判断；
- Plan 修复；
- 新页面语义学习。

不要：

每一个 Step 都同步调用大模型。

系统必须允许：

AI unavailable

此时核心确定性 Runtime 仍然可以运行到合理程度。

AI 输出不能直接成为世界事实。

AI Output
→ Semantic Evidence
→ Agent Decision
→ World Belief

### 27. AI 的异步能力

对于不阻塞当前安全执行的语义判断，可以允许：

Background Semantic Analysis

例如：

Fast path 判断：

"当前大概率仍属于当前 Container，可以继续进行安全观察。"

后台：

VLM 对当前页面进行更深语义分析。

结果返回后：

如果与当前 Belief 一致：

update Memory

如果发生冲突：

emit reconciliation signal

但异步 AI 结果必须携带 Observation Identity / Timestamp。

禁止旧 Observation 的 AI 结果覆盖更新的 World State。

### 28. Observability

Observability 是一等能力，不是后补日志。

至少需要能够追踪：

Run
Startup
Observation
WorldBelief changes
Container lifecycle
Traversal Step
Action
Verification
Trap
Recovery
AI Decision
Completion

推荐统一：

RunId
ContainerId
StepId
ObservationId
ActionId
RecoveryId

便于形成完整因果链。

必须能够回答：

"为什么系统做了这个动作？"

而不仅仅：

"系统做了什么动作？"

---

## Part VII — 运行期约束

> 本类回答：取消 / 暂停 / 关停的边界在哪，什么可以并发、什么必须串行。

### 46. Cancellation / Pause / Shutdown

Runtime 从第一阶段就应该正确考虑：

CancellationToken
Pause
Resume
Shutdown

但是 Pause 属于 Run lifecycle。

不要让暂停逻辑渗透到每个业务 Handler。

Run Controller 负责：

"现在是否允许继续推进。"

局部组件应该响应 cancellation，而不是自行管理整个 Run pause 状态。

### 47. Concurrency 原则

当前假设：

一个 Device
=
一个 Active Run

不要为了未来假想需求设计：

Multi-task scheduler
Multi-agent arbitration
Concurrent UI actions

真实 Device Action 应保持序列化。

允许并发的是：

非破坏性的后台工作。

例如：

Semantic analysis
Trace persistence
Memory enrichment

但异步结果必须考虑：

Observation Version
Freshness
Cancellation

禁止旧结果覆盖新状态。

---

## Part VIII — 架构治理与编码纪律

> 本类回答：可变状态与决策的归属铁律、依赖方向、接口纪律、类与接口的准入标准、被禁止的架构味道、编码原则。

### 29. 一个 Mutable State 只能有一个 Owner

这是不可突破的原则。

任何可变状态必须有唯一 Owner。

例如：

WorldBelief
→ Agent / WorldStateManager

Container local progress
→ Container

Traversal step state
→ TraversalRuntime

Action Journal
→ Execution component

不能出现：

AgentRuntime
Container
TraversalFSM
Handler

同时各维护一份"当前页面"。

其他组件只能：

read snapshot
request change
emit event
return result

不能跨边界直接修改。

### 30. 一个 Decision 只能有一个 Authority

例如：

"是否应该重新启动 App？"

Authority：

Agent

Traversal 只能报告：

WorldLost

不能自行：

LaunchApp()

例如：

"是否重新 Ground 当前页面候选？"

Authority：

Container

Agent 不应该管理每一个元素匹配。

例如：

"是否 retry 当前 target resolve？"

Authority：

Traversal

上层不应该微操 Traversal 每个内部状态。

### 31. Dependency Direction

概念依赖方向：

Agent
↓
Container
↓
Traversal
↓
Environment

低层不能反向依赖高层。

禁止：

Traversal → Agent

Environment → Container business logic

Vision → Agent Runtime

低层通过：

Result
Observation
Event
Trap

向上报告。

共享纯模型、接口、Observability 可以作为基础能力。

不要通过 Service Locator 或巨大 IServiceProvider 绕过依赖方向。

### 32. Interfaces Before Implementations

对于外部能力优先定义 Port：

IVisionProvider
IDeviceController
IActionExecutor
IAISemanticResolver
IMemoryStore
IClock

具体实现属于 Adapter。

核心 Runtime 测试不应依赖真实手机。

必须支持：

FakeEnvironment
ScriptedVision
FakeDevice

从而确定性模拟完整运行。

### 45. Result 类型应该表达语义

优先返回明确结果。

例如：

TraversalStepResult
ContainerResult
RecoveryResult
StartupResult

不要依赖大量：

bool
null
magic string
mutable flags

结果应该可以明确表达：

Success
Incomplete
Retryable
Trap
Failed
Completed

但不要为了类型丰富过度创建几十种 wrapper。

保持模型最小而明确。

### 48. 每个核心类必须回答的问题

新增核心类之前，必须说明：

Purpose
它为什么存在？

Owns
它唯一拥有哪份可变状态？

Does Not Own
哪些职责明确不属于它？

Inputs
它消费什么？

Outputs
它产生什么？

Authority
它允许做哪些决策？

Lifecycle
谁创建？什么时候销毁？

Failure
失败产生 Exception、Result 还是 Trap？

Dependencies
允许依赖什么？

如果回答不了：

不要创建这个类。

### 49. 每个接口必须证明自己有价值

不要为了"Clean Architecture"创建：

IXxxService
IXxxManager
IXxxProvider

然后只有一个实现且不存在真正的边界。

优先对以下情况创建接口：

- 外部能力；
- 可替换策略；
- AI Provider；
- Device；
- Vision；
- Storage；
- Clock；
- nondeterministic environment。

纯内部实现如果没有替换需求，可以保持简单。

### 50. 不允许的架构味道

发现以下情况必须停止并重新评估：

AgentRuntime.cs 持续膨胀；

一个 Context 包含所有 Runtime 状态；

多个组件都能 PressBack；

多个组件维护 CurrentPage；

FSM handler 里开始调用 LLM；

Fingerprint 决定 Page Identity；

Traversal 可以 Launch App；

Container 可以 Replan Goal；

Vision Provider 修改 Runtime State；

Graph 被当成真实 UI；

Memory 被当成当前事实；

大量特殊 case 被塞入 Engine 主循环；

一个 bug fix 需要同时修改五层共享 flag。

这些都是架构正在重新失控的信号。

### 51. 编码原则

优先：

small cohesive types
explicit ownership
immutable observations
structured results
async cancellation
deterministic core
dependency injection at boundaries
scenario-first testing
observability by design

避免：

God Object
Global mutable state
Service Locator
hidden side effects
magic flags
bool-driven orchestration
deep inheritance
reflection-driven core behavior
unbounded generic abstraction
premature plugin framework

---

## Part IX — 验证体系

> 本类回答：架构规则如何被自动验证、为什么 Scenario 测试优先、完成如何被证据证明。

### 41. Architecture Tests

不要只用文档约束架构。

能够自动验证的规则必须加入 Architecture Tests。

例如：

Traversal namespace 不得依赖 Agent。

Environment 不得依赖 Agent。

Domain / Model 不得引用 Runtime implementation。

核心 contracts 不得引用具体 Android Adapter。

如果某条 Architecture Invariant 能通过测试锁定，就不要只写 README。

### 42. Scenario Tests 优先级高于大量 Unit Tests

Unit Test 验证组件。

Scenario Test 验证架构。

Agent Runtime 最重要的是：

不同组件协同后，控制权是否正确。

因此第一阶段重点 Scenario：

NormalExecution
AgentRecovery
ContainerRecovery
ScrollIdentity
UncertainAction
StartupFailure
RecoveryFailure

每一个架构 Bug 最好最终变成一个新的 Scenario Test。

### 43. Completion 必须有 Evidence

禁止：

"Traversal graph 遍历完了"
=
"任务完成了"

任务完成条件必须最终与 Goal 对齐。

例如：

Goal:
Enable WiFi

Completion Evidence：

Observed WiFi State = ON

而不仅仅：

Node visited.

Container Completion 和 Goal Completion 是不同概念。

Agent 对最终 Goal Completion 拥有 Authority。

---

## Part X — 场景规范

> 本类回答：哪些 Scenario 必须通过——它们锁定本宪章的核心原则（Observation ≠ Semantic Identity、非幂等 Action、低 Scope 本地恢复）。

### 33. 第一阶段不要连接真实手机

第一阶段首先创建一个可模拟环境。

原因：

如果 Runtime Architecture 只能通过真实设备调试，无法建立确定性的架构测试。

先实现：

Simulation Environment

能够配置：

Screen A
Click X → Screen B
Click Y → Popup
Unexpected event → Launcher

然后验证 Runtime 生命周期。

等 Runtime Kernel 稳定后再接入：

Android Adapter
Vision Adapter
真实 AI Provider

### 34. 第一条必须通过的 Normal Scenario

建立最小 Scenario：

Goal:

Enable WiFi

World：

Screen 1:
Settings Main

元素：
Network & Internet

执行：
Click Network

Screen 2:
Network Settings

元素：
WiFi

执行：
Click WiFi

Screen 3:
WiFi Settings

元素：
WiFi Switch = OFF

执行：
Enable

Screen 3':
WiFi Switch = ON

Expected lifecycle：

Run Initialize
→ Startup
→ RecoveryAnchor established
→ Bind Settings Container
→ Traverse
→ Navigate
→ Bind Network Container
→ Traverse
→ Bind WiFi Container
→ Execute
→ Verify
→ Goal Completed
→ Run Completed

这个 Scenario 首先使用 Fake Environment。

### 35. 第二条必须通过的 Recovery Scenario

仍然执行：

Enable WiFi

执行到 Network Container 后：

External Environment unexpectedly changes to:

Launcher

此时：

Expected:
Network Settings

Observed:
Launcher

系统必须：

Detect mismatch
→ emit Agent-scope Trap
→ Agent Recovery
→ restore RecoveryAnchor
→ verify Settings Main
→ recover expected execution position
→ rebind / reconstruct Network Container
→ continue
→ Enable WiFi
→ Completed

不能：

直接从任务头重新执行一切。

也不能：

Traversal 私自 PressBack 猜测恢复。

### 36. 第三条必须通过的 Scroll Scenario

进入一个可滚动页面。

Observation 1：

Items:
A B C

Fingerprint = F1

Scroll

Observation 2：

Items:
D E F

Fingerprint = F2

要求：

F1 != F2

但：

ContainerIdentity remains the same.

系统不得仅因为：

FingerprintChanged

就：

创建新 Container
PressBack
判定 Navigation

这个 Scenario 用来锁定：

Observation != Semantic Identity

这一核心原则。

### 37. 第四条必须通过的 Uncertain Action Scenario

执行 Click。

设备实际上完成了页面跳转。

但 Action transport 返回：

Timeout

系统不得直接再次 Click。

正确流程：

Action result uncertain
→ Observe
→ discover target world already reached
→ mark action effectively successful
→ continue

这个 Scenario 用来锁定真实世界 Action 的非幂等处理原则。

### 38. 第五条必须通过的 Popup Scenario

当前 Container：

Network Settings

突然出现系统 Popup。

Popup 不改变底层页面语义。

要求：

Container-level Trap
→ Local Recovery
→ dismiss popup
→ Observe
→ verify Container still valid
→ continue

不得无条件升级 Agent Recovery。

---

## Part XI — 建设路线与工作方式

> 本类回答：按什么顺序建设（Vertical Slice）、目录如何表达架构、第一阶段完成标准、AI Coding 的工作方式。

### 39. 建设顺序

不要一次构建整个系统。

严格按照 Vertical Slice 逐步推进。

Phase 0 — Architecture Skeleton

建立：

- project structure；
- core contracts；
- core models；
- dependency rules；
- test infrastructure；
- fake environment；
- architecture docs。

不要实现复杂业务。

Phase 1 — Deterministic Runtime

实现：

- Run lifecycle；
- Startup；
- Observation；
- World Belief；
- RecoveryAnchor；
- Container；
- Traversal；
- basic action/verify；
- Normal Scenario。

Phase 2 — Trap & Recovery

实现：

- Trap；
- scope escalation；
- RecoveryRequest；
- RecoveryRuntime；
- Recovery verification；
- Recovery Scenario。

Phase 3 — Robust Execution

实现：

- uncertain action；
- idempotency handling；
- Popup；
- Scroll；
- Dynamic Grounding；
- local history。

Phase 4 — Real Environment

接入：

- real screenshot；
- YOLO；
- OCR；
- device action；
- application lifecycle。

Phase 5 — Semantic Intelligence

增加：

- semantic page resolution；
- Container identity；
- Memory；
- LLM/VLM fallback；
- async semantic enrichment。

Phase 6 — Advanced Agent

根据真实需求再考虑：

- dynamic re-plan；
- richer memory；
- container navigation graph；
- adaptive recovery；
- learning from successful runs。

不要提前实现 Phase 6。

### 40. 项目结构原则

不要首先追求漂亮目录。

目录应该表达 Runtime Architecture。

可以从类似结构开始：

src/
  UniClaw.Core/

    Agent/
    Startup/
    Container/
    Traversal/
    Recovery/
    World/

    Planning/
    Memory/

    Capabilities/
      Vision/
      Device/
      AI/
      External/

    Model/
      Observation/
      Graph/
      Actions/

    Observability/

tests/
  Unit/
  Architecture/
  Scenario/
  Integration/

docs/
  system/
  decisions/
  scenarios/

具体目录允许调整。

但必须保持：

高层概念清晰
+
依赖方向可验证。

### 54. AI Coding 工作方式

实现功能时，不要：

Prompt
→ immediately code

应该：

Requirement
→ Scenario
→ Responsibility
→ Authority
→ State Owner
→ Interfaces
→ Implementation
→ Verification

面对复杂问题时：

先给出设计判断。

如果需求不足：

明确 Deferred Decision。

不要为了显得完整猜未来设计。

### 57. 第一阶段完成标准

第一阶段不以：

"写了多少类"

作为完成标准。

必须满足：

1. Normal Scenario 可以完全在 Fake Environment 中运行；
2. Agent Recovery Scenario 可以运行；
3. Scroll 不会因为 Fingerprint 改变导致 Container Identity 错误；
4. uncertain Action 不会盲目重复执行；
5. Popup 可以在 Container Scope 恢复；
6. Startup 能建立 RecoveryAnchor；
7. Recovery 成功必须经过 Observation + Verify；
8. Global lifecycle 与 Traversal protocol 职责分离；
9. 一个 mutable state 只有一个 owner；
10. Dependency direction 有自动 Guard；
11. Scenario Trace 可以解释系统为什么做每一步；
12. 不依赖 LLM 也能跑完确定性测试。

满足以后，再开始扩大真实设备能力。

### 60. 你的第一项工作

不要立即实现完整系统。

首先完成：

A. Architecture Proposal

输出：

- Runtime component model；
- ownership table；
- dependency diagram；
- runtime state model；
- normal lifecycle；
- trap / recovery lifecycle；
- minimal project structure；
- deferred decisions。

B. Minimum Contracts

只定义第一条 vertical slice 真正需要的 contracts。

C. Fake Environment

建立可以确定性驱动页面变化的 simulation。

D. Normal WiFi Scenario

实现完整正常生命周期。

E. Recovery WiFi Scenario

实现 Launcher drift + Agent Recovery。

F. Architecture Review

完成之后检查：

- 是否出现 God Object；
- 是否出现重复 authority；
- 是否混淆 Runtime State / World State；
- 是否将 Plan 当 Reality；
- 是否产生不必要 FSM；
- 是否能够解释每一个状态 owner；
- 是否可以不用真实手机和 LLM 完成核心测试。

通过以后再继续下一阶段。

---

## Part XII — 文档与架构决策

> 本类回答：文档体系如何保持对 Coding Agent 友好、哪些决策必须记录 ADR。

### 52. 文档原则

项目必须对 Coding Agent 友好。

根目录应提供简洁 AI Entry Point。

例如：

AGENTS.md

但 AGENTS.md 不应该成为几百行知识仓库。

它主要提供：

- 项目目标；
- 不可突破原则；
- 文档路由；
- 开发流程；
- 构建测试入口。

详细知识进入：

docs/system/
docs/decisions/
docs/scenarios/

每个核心 Runtime 模块建议存在简洁设计文档：

Purpose
Responsibility
State Ownership
Dependency
Lifecycle
Failure / Trap
Examples

### 53. Architecture Decisions

影响长期结构的决策必须记录 ADR。

例如：

为什么 Container 以 Semantic Page 为边界？

为什么 Fingerprint 不是 Page Identity？

为什么 RecoveryAnchor 属于 Startup？

为什么 Traversal 不允许依赖 Agent？

ADR 记录：

Context
Decision
Consequences

不要记录每一个普通代码选择。

---

## Part XIII — 设计自由度与最终原则

> 本类回答：哪些内容允许你根据实现分析调整、哪些原则永不偏离。

### 58. 设计自由度

Architecture Invariants 不允许随意破坏。

但以下内容允许你根据实现分析：

- Container 是否需要独立 FSM；
- TraversalFSM 具体有哪些状态；
- Global lifecycle 具体枚举；
- Recovery 是否内部采用 FSM；
- WorldBelief 具体模型；
- Plan Graph 数据结构；
- Container Navigation 是否使用 Graph；
- Semantic Identity 算法；
- Memory backend；
- AI Provider 接口；
- DI 框架；
- namespace；
- project assembly boundaries。

如果发现：

某项本文建议导致不必要复杂度，

请：

1. 明确指出；
2. 解释当前真实 Requirement；
3. 区分 Architecture Invariant 和 Implementation Suggestion；
4. 给出更小的方案。

不要机械执行文档。

### 59. 最终架构原则

请始终遵守以下原则：

External world is authoritative.

Observation is evidence, not semantic truth.

World Belief is revisable.

Plan is hypothesis, not reality.

Memory is prior knowledge, not truth.

Fingerprint is evidence, not identity.

Agent owns global semantic authority.

Container owns page-local runtime state.

Traversal owns deterministic step execution.

Environment owns interaction with the external world.

FSM owns protocol transitions, not intelligence.

Lower scope can escalate; it cannot steal higher-scope authority.

One mutable state has one owner.

One decision has one authority.

Recovery is not an action; recovery is a verified process.

Completion requires evidence against the Goal.

AI augments deterministic execution; it does not replace Runtime architecture.

Do not optimize architecture for hypothetical future complexity.

Build the smallest correct system, then grow from real scenarios.
