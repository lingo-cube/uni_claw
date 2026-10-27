# 运行中生效：Agent 介入面扫描、ONES 案例复演与「运行内经验」设计

> DocumentType: `ANALYSIS`
> Status: `COMPLETE / EVIDENCE-BOUNDED`（代码扫描 + 案例复演 + 候选设计，未授权实现）
> Authority: `NONE`（分析产物；与 ADR / baseline 冲突以后者为准）
> 扫描基线：`dab47788`（uni-harness）
> 前序：`self-improving-ui-automation-ones-worklog-case-2026-10-27.md`（跨运行的
> 加速机制）。本文只回答**「同一次运行内，刚学到的纠正怎么生效」**。
> 相关候选设计：`agt019-followup-optimization-and-research-2026-10-17.md`
> （Event Stream / Heartbeat / 事件触发咨询 / Context Pull，未实现）。

---

## 0. 结论先行

1. **三条通道里，只有「运行时反馈建议」能在运行中生效。** Skill 与静态提示词
   在 attach 时被哈希钉死（`artifact-fingerprint-mismatch` 直接拒绝接入），
   运行中改不动。能每轮变的只有**决策上下文**。
2. **但只补通道没用。** 复演 ONES「只读日期 + 必须按 Enter」这个案例，拦住
   它的是两个更前面的断点：**元素投影里没有「只读」这个事实**，**动作词表里
   没有「按键」这个原语**。经验灌进去也没有可执行出口。
3. **落地顺序应是：补投影 → 补词表 → 补通道。** 通道恰好是最便宜的一段，
   因为 interruption / CasePacket / ContextReferenceIndex 已经存在且在跑。

---

## 1. 扫描：当前 Agent 介入面（7 条）

| # | 机制 | 触发 | 载体 | 谁能改 | 今天是否存在 |
|---|---|---|---|---|---|
| 1 | P25 决策边界咨询 | 每个语义决策边界（6 相：InitialPlanning / StepVerified / StepRejected / VerificationFailed / DeferRoundsExhausted / PolicyInvalidated） | `AgentDecisionContext` → `ConsultAgent` | Kernel 组装 | **在跑** |
| 2 | 失败上下文 | StepRejected / VerificationFailed | `Phase` + `FailureReason` + `FailedStepIndex`（原文不净化） | Kernel | **在跑**，且一次性消费即清 |
| 3 | 进度与预算 | 每次咨询 | `Progress`（RoundsUsed / StepsDispatched / StepsVerified / StagnantConsultations / SameRouteConsultations）、`BudgetRemaining`、`RenewalWindow` | Host 装饰层写停滞计数 | **在跑** |
| 4 | 局部观察视图 | 每次咨询 | `LocalView`（容器结构 / 视口 / 观察诊断 / 近 3 周期事实行） | Host 装饰层 | **在跑** |
| 5 | 事件触发咨询 | 事件（如停滞） | `RunDriverInput.Interruption` → `Heartbeat` + `ContextReferenceIndex` + `CasePacket` | Host 生产，Kernel 合并进下一次上下文 | **在跑，只有一个生产者** |
| 6 | Director 注入 + 计划符合性校验 | 每次咨询 | `context.Objective += "COVERAGE DIRECTIVE (authoritative for this turn)"`；偏离则有界重试，仍偏离 fail closed | Host 装饰层包 `ConsultAgent` 缝 | **在跑**（`SettingsCoverageDirector`） |
| 7 | 动作策略守卫 | 咨询返回后、dispatch 前 | `SettingsActionGuard` 判 Allow/Reject/NoAction，可把 Act 改写成 NoAction | Host | **在跑** |

另有一条常被忽略的**度量原料**：`IConsultationJournal` 在每个边界写一条耐久
记录（key = runId / boundary / decisionId，payload = 上下文 SHA256 指纹 +
最终 decision）。这意味着「这次咨询里 agent 看到了什么、答了什么」是可机械
回放的，不需要新建埋点。

---

## 2. 三条「知识通道」的钉死证据

### 2.1 Skill：attach 时钉版，运行中不可变

- `UniAgentProfile.AllowedSkillRefs` 是 `SkillReference(Name, Revision, Path,
  Sha256)`，加载时校验 revision 与逐文件哈希（`UniAgentProfileYaml.VerifySkill`）；
- 且**只允许一个 primary skill pin**（`profile-primary-skill-pin-count-must-be-one`）；
- attach 时把 `promptHash` + `(skill.Name, skill.Revision, skill.Sha256)` 组成
  `ArtifactFingerprint`，对端回报不一致即 `Accepted=false,
  FailureReason="artifact-fingerprint-mismatch"`（`DshOpenedHttpPeer`）。

结论：**运行中改 skill = 改指纹 = 断开接入**。skill 是「跨运行沉淀」的正解，
不是「运行中生效」的正解。

### 2.2 静态提示词：同一枚指纹

`promptHash` 与 skill 指纹同属一次 attach 的校验对象，语义同上。
写在 profile/prompt 里的规则，生命周期等于「一次 attach」。

### 2.3 运行时上下文：唯一可变面

每次边界的 `AgentDecisionContext` 都是新组装的；Host 装饰层还可以在送出去
之前改写（`SettingsCoverageDirector` 就是往 `Objective` 末尾追加指令段）。
Interruption 进入后由 Kernel 合并，并在该次咨询结束即 `_pendingInterruption
= null`（消费一次）。

结论：**「运行中生效」的唯一物理载体是决策上下文**——文本可以进
`Objective`/`FailureReason`/`InterruptionSignal.Reason`，结构可以进
`Progress`/`LocalView`/`CasePacket`/`ContextReferenceIndex`。
问题只剩：**进去的是什么形状**。

---

## 3. 复演：ONES「只读日期 + 必须按 Enter」走一遍现有管线

真实场景：开始时间是只读输入框，点击弹出时间面板，选完时/分/日之后必须再
按一次 `Enter` 才提交，失焦会回滚。用本仓现有机制逐边界走：

| 边界 | 咨询相 | Agent 看到什么 | Agent 会怎么做 | 系统会怎么回 |
|---|---|---|---|---|
| B1 | InitialPlanning | `Elements` = 当前容器 occurrence 投影：`Role/Text/Bounds/Clickable/Checkable/Enabled/Epistemic/State` | `Act{ tap "开始时间" }` | grounding → binding → assurance → gate → `EgoBrowserEffectDriver` 出 `ego click node:{id}` → DeliveryCompleted → post-action 观察：面板出现 |
| B2 | StepVerified | 新元素（时/分列单元格） | `Act{ tap "09" }` | 若单元格在滚动容器可视区外 → 接地/命中失败 → `StepRejected{reason}` |
| B3 | StepRejected | `FailureReason` 原文 | `Act{ swipe-up 容器 }` → 再 `tap "09"` | 逐步验证通过 |
| B4 | StepVerified | 同上 | `tap "00"`、`tap "19"` | 通过 |
| B5 | VerificationFailed | 期待「值已变」，实际「值回滚」 | 重试各种点击组合 | 每轮都在 `tap`，每次都「投递成功但效果没发生」 |
| B6 | VerificationFailed（第 N 次） | `Progress.StagnantConsultations` 达阈值 | —— | Host 发 `Interruption{Kind:"coverage-stagnant", CasePacket{AvailableActions:[Reobserve, Replan, Blocked, Skip]}}` |
| B7 | 同上 | `CasePacket.AvailableActions` 只有四个选项，**每一个都表达不了「按 Enter」** | 只能 Reobserve / Replan / Blocked | 预算耗尽 → bounded stop，或 Blocked 诚实终局 |

**停下来的是三个不同的东西，不是一个**：

### 断点 ①：信息断点——投影里没有「只读」

`DeriveElementSummaries` 里 `Clickable = Role is "ui.element" or "switch"`、
`Checkable = Role == "switch"`、`Enabled = true`（硬编码）。也就是说：

- agent **看不到**「这个输入框是只读的」，只能试一次才知道；
- 反过来，一旦试出来，「只读」这个事实也**没有字段可以落到下一次上下文里**。

推论：本类失败不是「模型不够聪明」，而是**感知投影丢了一个判别维度**。
在补上这个维度之前，任何「教 agent 该怎么做」的通道都只是在补一个本可以
直接观察到的事实。

### 断点 ②：动作断点——词表里没有「按键」

`EgoBrowserEffectDriver` 的支持集是 `{click, tap, set-switch}`，
且是 dry-run（真实 CDP 未接）；settings 覆盖合同 `AllowedEffects =
{tap, swipe-up}`。于是：

> 即使 agent 已经知道「必须按 Enter」，它**无法用任何合法 effect 表达这件事**。

`CasePacket.AvailableActions` 是封闭四选项（Reobserve / Replan / Blocked /
Skip，见 agt019 §五），也不承载「具体怎么做」。所以运行中「学到了」与
「能做到」之间目前是断的。

### 断点 ③：通道断点——经验没有承载物

- CasePacket 目前只有一个生产者（`CoverageStagnantSignal`），`CaseKind` 是
  自由 string，没有第二个生产者的形状；
- 没有任何「刚验证过的序列」被写回上下文的路径；
- `_pendingInterruption` 是**单槽**（`InterruptionSignal?`），新信号无条件覆盖
  旧的（`_pendingInterruption = interruption.Signal`）——如果同时要投「停滞」
  和「新经验」，后到的会静默吃掉前者。

---

## 4. 那么：skill / 提示词 / 反馈建议，选哪个

| 通道 | 运行中是否可改 | 能承载什么 | 失效方式 | 判定 |
|---|---|---|---|---|
| Skill（guidance.md） | ❌ 钉死（revision + sha256，且只允许一个） | 长程序性知识 | 改了就 `artifact-fingerprint-mismatch` | **只能做跨运行出口** |
| 静态提示词（profile prompt） | ❌ 钉死（promptHash） | 稳定指令 | 同上 | 只能做基线 |
| Director 注入（Objective 追加文本） | ✅ | 任意文本 | 无结构 / 无去重 / 无失效 / 易被判语 | 应急可用，**不可作机制** |
| 事件触发咨询（CasePacket + Reason） | ✅ | 触发信号 + 心跳 + 事件增量 + 引用目录 + 容许动作 + 约束 | 单一生产者；动作集封闭 | **正解通道，需补三件** |
| Context Pull（§9，未实现） | ✅（设计） | 按 `maxBytes` / `availability` / `epistemic` 拉有界材料 | 未实现 | 长线形态，用于复核而非首次投递 |

**判定：走「事件触发的反馈建议」通道。** 理由不是它最好，而是：
(a) 它是唯一每轮可变的；(b) 它的骨架（信号 / 心跳 / 事件 / 容许动作 / 约束）
天生就是「给 agent 判断用的材料」，不需要新立语义；(c) 它已有 `signalKey`
去重与 rearm 纪律的设计可循。

**不能只走它**，因为断点 ①② 在它上游。

---

## 5. 设计：Run-Local Lesson（运行内经验）

定位：**同一次运行内**，把「刚刚被验证过的纠正」在下一个决策边界交给
agent，并保证它可执行、可失效、可度量。不新立协议类型，复用既有三个缝。

### 5.1 形状（复用既有字段，不加新类型）

```text
RunDriverInput.Interruption(InterruptionSignal{
  SignalKey  = "lesson:{siteKey}:{controlKey}:{cause}"      // 去重键（既有字段）
  Kind       = "StrategyLearned"                             // 新的生产者约定，不是新协议类型
  EventSeq   = <触发它的那次 Verified Effect 的 seq>
  Target     = <该控件>
  Reason     = 一行事实：症状 → 判别依据 → 已验证可行的动作序列（禁判语）
  Heartbeat  = 该边界的既有投影
  ContextReferenceIndex = [ ContextReferenceEntry{
                 RefId="lesson:...", Kind="event", Owner="runtime.session",
                 Epistemic="judgment", Availability="available",
                 SemanticLabel="picker: commit requires key(Enter)",
                 Lineage="verified-effect:{receiptId}", MaxBytes=512 } ]
  CasePacket = CasePacket{
                 CaseKind="strategy-learned",
                 TriggerSignals=["StrategyLearnedSignal"],
                 AvailableActions=[<本 run 已证明可执行的 effect 集合>],
                 Constraints=["advisory only; Effect still passes Grounding/Assurance/Gate",
                              "scope=this target/container; do not generalize within this run"] }
})
```

要点：**Reason 只写事实与序列，不写「你应该」**（与 agt019「Case Packet 只提供
证据，不替 Agent 下结论」一致）；能不能做由 `AvailableActions` 限定。

### 5.2 触发：同类失败第二次，而不是等停滞

现状是 `stagnationWindow`（W=5）才发停滞信号——那时 agent 已经烧了 5 轮。
「刚学会」应当更早触发：

```text
同一 targetKey + 同一 failureCause 在第 2 次出现，且第 1 次之后
出现过一次 Verified Effect（= 真学到了东西）→ 一条 StrategyLearned
```

去重与 rearm 沿用 agt019 Q10：同 `signalKey` 在冻结窗口只发一次；
出现新的 `ObservationAccepted` 且证据变化后才重新具备资格。

### 5.3 作用域：只在学到的那个控件上有效

必须绑 `siteKey + controlKey`（如 `ones / picker.open-time`），不得在同一 run
内泛化到别的控件——「按 Enter 提交这个选择器」不能变成「凡是面板都按 Enter」。
跨控件泛化属于晋升决策，不在运行内做。

### 5.4 消费与失效：用一次、验一次、失败即废

```text
注入一次（消费即清，沿用 _pendingInterruption 语义）
→ agent 采纳与否都留痕
→ 采纳后仍走 Grounding/Assurance/Gate（经验不授权 Effect）
→ post-action 验证成功 = lesson 有效；失败 = 同 run 内标记 invalid，不再注入
```

这一条是「自改进」与「自我催眠」的分界：**经验必须被验证才能留存**。

### 5.5 回写：运行结束才晋升，入口是 skill

跨运行沉淀仍走既有出口：run 结束后，把「被使用且验证成功」的 lesson 汇总成
候选，经人工/change 审阅后写进 skill 的 guidance/references——也就是
`ones-worklog` 那条路。**运行内不写 skill**（会破 attach 指纹）。

### 5.6 度量（用现成账本，不新增埋点）

| 指标 | 怎么算 | 判据 |
|---|---|---|
| 投递率 | 有 `StrategyLearned` 的 run / 发生同类二次失败的 run | 越高越好 |
| 采纳率 | 下一边界 agent 按 Reason 提出的 Act 被 grounding 通过的比例 | 低于 30% 说明形状不对 |
| 有效率 | 采纳后 Verified Effect 成功 / 采纳次数 | 这是唯一真正的「学到了」证据 |
| 假收益 | 采纳后失败的次数 | 必须单列，防「看起来学会」 |
| 覆盖 | 同一 `signalKey` 在窗口内重复投递次数 | 应为 1 |

（数据源：`IConsultationJournal` 的边界记录 + Effect receipt + post-action 验证。）

---

## 6. 实现落点与优先序

**P0-① 补投影**（少一次试错，不是其他改动的前提）

- `ElementSummary` 增 affordance 维度（`ReadOnly` / `Selected` / `Submits`），
  或在 `CurrentWorldClaims` 增 `control.readonly` 一类 subject；
- `DeriveElementSummaries` 里 `Enabled: true` 的硬编码需要有来源或显式
  `Unknown`（三态，不要用 `true` 冒充 observed）；
- 判据：只读控件在 B1 就能被 agent 看见，不需要先撞一次墙。

**P0-② 补词表**（决定「学到了能不能做」）

- 在 `AllowedEffects` 与 driver 支持集里加入提交类原语（`key` / `set-text`，
  或复合原语 `commit-picker`）；
- 注意 `EgoBrowserEffectDriver` 目前是 dry-run，真实 CDP 未接——词表先于
  真实执行器落地时，必须同时标明该 effect 的 `DeliveryFailed` 语义，不能让
  agent 以为「有词 = 能做」；
- 判据：`key(Enter)` 能走到 dispatch 并以 `DeliveryCompleted` 或明确
  `unsupported-effect` 二态报告。

**P1-③ 补通道**（本文第 5 节）

- 新增 `StrategyLearned` 生产者（Host 装饰层，与 stagnation 同级）；
- `_pendingInterruption` 单槽改有界队列（至少 2），或明确「lesson 优先于
  stagnation」的合并规则——现状是后者静默覆盖前者；
- `CasePacket.AvailableActions` 的语义从「封闭四选项」扩为「本 run 已证明
  可执行的 effect 集合」时，要区分「过程选项」（Reobserve/Skip）与「动作
  选项」（effect primitives），不能混在一个字符串数组里。

**P2-④ 度量与晋升**：第 5.6 节 + 第 5.5 节的 run 末汇总。

---

## 7. 不做什么

1. **不把 lesson 写进运行中的 skill**——会 `artifact-fingerprint-mismatch` 断链；
2. **不让 Host 下判语**（「你应该按 Enter」）——只给事实 + 可执行集合，判断权在 Agent；
3. **不新增 memory 存储层**——`IConsultationJournal` + `ContextReferenceIndex`
   已能承载；跨运行沉淀的容器是 skill 的 references，不是新服务；
4. **不用 Objective 文本注入做长期机制**——可作 P0 应急，但它没有去重、
   没有失效、也没有度量支点；
5. **不把「投递成功」当「经验有效」**——`DeliveryCompleted` 只证投递，
   有效性只能由 post-action 观察证明（与 `Verified Effect ≠ Effect Receipt` 同一条）。

---

## 8. 验收判据（实现前先写死）

| 场景 | method | expected | evidence |
|---|---|---|---|
| 只读可见性 | 复演 ONES 只读日期控件，观察 B1 上下文 | B1 即出现 `ReadOnly=true`；agent 不再需要「撞一次」 | consultation journal 的 B1 payload |
| 词表可执行 | agent 返回 `key(Enter)` | 要么 `DeliveryCompleted`，要么 `unsupported-effect`；不得静默丢弃 | Effect receipt + reason |
| 经验投递 | 同一控件第二次同类失败 | 恰一条 `StrategyLearned`；`signalKey` 在窗口内不重复 | interruption trace |
| 经验有效 | 采纳 lesson 后一次成功 | 下一次 post-action 验证通过；`有效率` +1 | receipt + verification |
| 经验失效 | 故意让 lesson 的序列失败 | 同 run 内标记 invalid，不再注入；不进入 run 末候选 | invalid 标记 + run 末汇总 |
| 不越权 | 采纳 lesson 后 Attempt 未过 Assurance | Effect Gate 拒绝，零 dispatch | gate rejection |

---

## 9. 例：把 ONES 只读日期控件压到 2 次咨询

Kernel 的耗时结构（按量级排）：

```text
总耗时 ≈ N_consult × 模型回合延迟      ← 秒级，占绝对主导
       + N_step   × (grounding→assurance→gate→dispatch→post-action 观察)  ← 毫秒级
       + N_observ × 感知周期                                        ← 毫秒~百毫秒
```

所以「让 Kernel 更快」= **减少咨询次数**，而不是减少点击次数。按现码把同一个
案例跑两遍：

### 9.1 今天（settings 风格的单步 directive）

`SettingsCoverageDirector.ValidateAdherence` 强制
`directive-requires-single-step:1`，一次咨询只能带一步：

| # | 咨询内容 | 结果 |
|---|---|---|
| 1 | `tap 开始时间` | 面板出现 |
| 2 | `tap 09` | 单元格在滚动容器可视区外 → `StepRejected` |
| 3 | `swipe-up` | 通过 |
| 4 | `tap 09` | 通过 |
| 5 | `tap 00` | 通过 |
| 6 | `tap 19` | 通过 |
| 7~10 | 各种 `tap` 试图提交 | 4 次 `VerificationFailed`（值被回滚，回车没发出去） |
| 11 | 停滞信号 → CasePacket → `Blocked` | 终局 |

**11 次咨询 = 11 个模型回合；其中 5 次是纯浪费。**

### 9.2 改完（三处改动叠加）

| # | 咨询内容 | 依赖 |
|---|---|---|
| 1 | `tap 开始时间`（B1 即可见 `ReadOnly=true`，不再先试 `fill`） | 投影补 affordance |
| 2 | 5 步一次提交：`swipe-up` → `tap 09` → `tap 00` → `tap 19` → `commit{desiredState:"2026-10-19 09:00"}` | `AgentActionProposal.Steps` 已支持多步；`commit` 是新增词表 |

**2 次咨询；0 次浪费。**

为什么不是 1 次：面板打开属于 boundary action，`AgentActionProposal` 的约束是
「boundary action 结束 Act，尾步不能进入新 scope」，所以必须切一次边界。这条
约束不是实现偷懒，是 `Act` 的作用域语义。

### 9.3 两处改动的性质不同

| 改动 | 谁在拦 | 改哪里 | 不改会怎样 |
|---|---|---|---|
| 多步提交 | Host 策略（kernel 已支持 `Steps ≤ 16`） | `SettingsCoverageDirector` 的 adherence 规则 | 咨询数 = 点击数，线性放大 |
| `commit` 原语 | 词表 + driver 支持集 | `AllowedEffects` + driver | 学到的「按回车」无处可发，只能重试到 bounded stop |

### 9.4 还有第三个收益：预算不再被提前烧穿

合同里有 `MaxConsultations`（settings 侧来自必填配置 `bounds.maxConsultRounds`，
最小值 2）。11 次咨询的流程在较小的 `maxConsultRounds` 下**根本走不到提交那一步
就会被 `max-consult-rounds` bounded stop**——所以这不是「快一点」，是「同一条
流程从跑不完变成跑得完」。这一点比省时间更重要。

### 9.5 怎么证明（可机械核对，不靠感觉）

`IConsultationJournal` 每个边界一条记录。同一案例前后各跑一遍，比较：

```text
每条记录完成的咨询数：11 / 记录  →  2 / 记录
失败相（StepRejected + VerificationFailed）占比：5/11 → 0/2
```

这是本文唯一建议纳入验收的「快」的定义：**单位成果的咨询数**。它不受模型随机
性影响，也不受机器性能影响。

### 9.6 补充：Kernel 已经内建了「序列被打断」的诚实语义

放开单步之前容易高估成本——以为要自己设计「跨 scope 怎么拼接」。实际
`KernelRunDriver.cs:1104` 已经有：

```text
多步 Act 的每一步开工前，比较 currentContainer 与 overlayState 是否仍等于
Act 起点值；不等 → act-scope-changed，StepRejected，reason 里带
  unexecuted-from={stepIndex} + remaining=[<剩余步序列>]
```

即：越界的那一步不会硬做，剩下的步骤**原样回传给下一次咨询**，并且
`FailedStepIndex` 同步给出。所以「放开」是安全的——最坏结果是多一轮咨询，
而不是做错动作。

### 9.7 但放开单独的收益是「假成功」，不是「失败」

不带 `commit{desiredState}` 时，面板内 4 步（swipe / 09 / 00 / 19）会**逐步
验证通过**——因为每步的 expected effect 是「这一步让状态变了」，而状态确实变了。
问题在提案结束后：失焦回滚，值回到 `15:00`，而 run 认为自己推进了。

这正是前序文档第 5 节的「假成功」，也是本案例最贵的一类错误。所以：

- **放开单步 = 提速**（11 → 3 左右）；
- **`commit{desiredState}` = 正确性**（让「值对不对」成为可判定的 expected
  effect，3 → 2 且不再假成功）。

两者是正交的轴，不是一个改动的两个步骤。

---

## 附：证据索引

- 咨询缝与上下文：`src/UniClaw.Kernel/Runtime/AgentDecision.cs`（`AgentDecisionContext`
  字段集）、`KernelRunDriver.cs:3067`（`ConsultAgentV2Async`）、`:3142`（interruption
  合并）、`:3975`（`_pendingInterruption = interruption.Signal` 单槽无条件覆盖）、
  `:3339`（`DeriveElementSummaries`）、`:3358`（`Enabled: true` 硬编码）
- 介入信号：`src/UniClaw.Kernel/Runtime/RunDriverInputs.cs:35`（`InterruptionSignal`）
- 案例包：`src/UniClaw.Kernel/Runtime/ConsultationTypes.cs:367`（`CasePacket`）、
  `:346`（`ContextReferenceEntry`）
- 装饰层先例：`src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs:537`（注入
  directive）、`:142`（`Consult`）
- 停滞介入：`src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs:957`（CasePacket
  组装）、`:981`（STAGNATION INTERVENTION 文本）
- 能力钉版：`src/UniClaw.Agent/Profile/UniAgentProfile.cs:47`（`SkillReference`）、
  `src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs:135`（指纹）、`:129`
  （`artifact-fingerprint-mismatch`）
- 效果词表：`src/UniClaw.Kernel/Effects/EgoBrowserEffectDriver.cs:40`
  （`{click, tap, set-switch}`，dry-run）
- 设计候选：`docs/analysis/agt019-followup-optimization-and-research-2026-10-17.md`
  §五（事件触发咨询）、§六（interruption plane 职责边界）、§九（Context Pull）
