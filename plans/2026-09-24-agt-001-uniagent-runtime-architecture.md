# AGT-001 — UniAgent Runtime Architecture：DSH 作为 Product UniAgent Realization（设计稿 v0.1）

> Status: DESIGN DRAFT（待独立 Grill）
> Authority: NONE（本文档不修改任何 baseline；上游顺序见 §0）
> 输入谱系：product-architecture-baseline-l0-l3（§24.2/§24.5/不变量 43-47）·
> uniagent-realization-baseline-v0.1（FROZEN，ADR-0022）·
> consultation-protocol-v0.1（150 场景反推）· decision-granularity-scenarios-v0.1 ·
> RUN-004（已 CLOSED：多轮咨询协议冻结）· GEV-004（已 CLOSED：确定性评价）·
> DSH checkout 机制核查（agent-loop / session / tools / concludesTurn / SDK/ACP）
> 日期：2026-09-24

---

## 0. Authority Order 与本文档位置

```text
Target Product Architecture（L0-L3，FROZEN）
  └─ Inter-Component Protocol Baseline
      └─ ADR-0019（Kernel self-drive）· ADR-0022（双 realization）
          └─ UniAgent Realization Baseline v0.1（FROZEN）
              └─ 本设计稿（AGT-001 Step 1：填 baseline §9 的 Deferred 槽位
                 —— transport / deployment / session 映射 / DSH profile 形状）
                  └─ 后续：RUN-005（Kernel 侧 Policy 展开）· 实现变更
```

本稿回答「DSH 如何成为 UniAgent 的 runtime realization」；**不**修改
AgentDecision protocol、不实现、不启动 RUN-005。

核心原则（贯穿全文）：

> **UniAgent 是产品角色，DSH 是这个角色的实现机制。**
> DSH 的 session/turn/step/tool/event/transcript 全部是 realization-private，
> 不取得任何 Product Authority（realization baseline §4/§5 冻结）。

---

## 1. Q1 — Runtime Boundary：推荐 **B（.NET 监管的 local sidecar）**

### 1.1 三案对比

| 维度 | A embedded | **B local sidecar（推荐）** | C standalone service |
|---|---|---|---|
| lifecycle | 与 .NET 进程同生共死 | **.NET Product Host 监管：按需启动、健康探测、有序关闭** | 独立 OS 服务，多会话共享 |
| crash isolation | 无（Node runtime 崩 = 产品进程崩） | **进程级隔离：DSH 崩溃 ≠ Kernel 崩溃；fail-closed 映射到咨询失败** | 有，但故障域含所有会话 |
| session persistence | 依附宿主进程 | **DSH_HOME 内 append-only session log（DSH 原生），跨 sidecar 重启可 resume** | 服务侧，但与产品生命周期解耦 |
| deployment complexity | 表面低、实际高（Node runtime 嵌入 + 版本锁定） | **一个 node 运行时 + DSH host face 发布物 + profile 清单；无端口无网络面** | 端口/认证/租户隔离/升级编排全套 |
| protocol versioning | 进程内 ABI 脆弱 | **stdio 上的版本化 JSON-RPC（显式 protocolVersion 握手）** | 同 B 但跨网络 |
| .NET↔DSH 集成 | 需内嵌 JS runtime（edge/jsrt），cordis 依赖 Node API，不可行级风险 | **child-process stdio；.NET 侧一个 adapter 类** | HTTP 客户端 + 服务治理 |
| testability | 差 | **好：adapter 缝可注入 fake sidecar（协议级 double）** | 好，但环境重 |
| future provider replacement | — | **替换发生在 DSH 内部 route（provider/model），adapter 不变** | 同 B |

### 1.2 裁决与理由

**B。** A 因 cordis/Node 绑定与崩溃隔离丧失被拒（不是「实现难」，是 authority
边界要求：realization 故障必须可隔离在咨询缝内）；C 的多会话/多租户买家尚
不存在（ADR-0026：不为想象中的需求预造），且共享服务把 realization lifecycle
抬成产品依赖。B 把 DSH 的 lifecycle 权放在 **.NET Product Host**（监管者），
DSH 保持被监管的 realization——与「DSH 不是新的 Product Authority」一致。

传输面：**child-process stdio 上的版本化 JSON-RPC**（私有 by construction——
无端口、无网络暴露）。不选 HTTP：端口/认证面纯属多余攻击面。ACP 是
编辑器-Agent 协议，词表不匹配（其 lifecycle 词 ≠ 咨询协议），不采用其语义、
只借鉴「stdio JSON-RPC + 显式初始化握手」形态。

### 1.3 Adapter 位置

新程序集 `src/UniClaw.Agent.Dsh`（依赖方向 Agent.Dsh → Kernel.Runtime 缝类型；
**不改** Kernel / Agent 现有程序集）。它实现产品缝
`Func<AgentDecisionContext, AgentDecision?>`（RunDriverInputs.ConsultAgent，
RUN-004 D6 同步请求-回答形态保持不变——**transport 异步、缝语义同步**：
adapter 内部等待 turn 终结或 deadline）。

---

## 2. Q2 — Session Mapping：1 Product Session : 1 DSH session（v1）

| 问题 | 裁决 |
|---|---|
| 映射基数 | **1:1**（v1；上游 cardinality 1 session/1 goal/1 run 已冻结） |
| 谁创建 | **.NET adapter 创建**（首次咨询时 lazy 创建；Product Host 不感知 DSH 细节） |
| 谁关闭 | **.NET adapter 关闭**（Product Session 关闭时 dispose；DSH 无权单方面终结产品会话） |
| Run 重启是否复用 | **复用**（同 Product Session 内 run 重启不换 DSH session——策略记忆保留；D1 幂等触发保证不重问已答边界） |
| process crash 后 resume | .NET 重启 sidecar → 按 DSH session id **resume（log replay）**；resume 失败 → 新 DSH session（策略记忆丢失 = 诚实降级，见 §9 失败模型） |
| Session ID 归属 | Product SessionId 由产品层拥有；DSH sessionId 是 **realization-private correlation**，映射记录（sessionId、log 路径、protocolVersion）由 .NET 侧持有 |
| DSH session 是否产品持久状态 | **否**。产品持久状态 = Goal/Contract/Run/Outcome/Evaluation（owner records）；DSH session log 是 realization-private，可弃可重建 |
| 何为 realization-private | session log、派生消息历史、策略记忆、prompt/context assembly、provider 调用痕迹 |

**边界重申（realization baseline §5 逐条继承）**：

```text
DSH Session ≠ Product Session（id 显式映射，不隐式等同）
DSH Session ≠ Run Model（turn/step 不冒充 Run/cycle）
DSH Session ≠ World Model（§7 策略状态边界）
Host resume 成功 ≠ Product Session 恢复成功（产品状态只从 owner records 恢复）
```

---

## 3. Q3 — Consultation ↔ Turn：严格 1:1，turn 只经 submit_decision 终结

### 3.1 映射

```text
Kernel driver（唯一发起方，RUN-004 状态机边界）
  → ConsultAgentV2 构造 AgentDecisionContext（有界摘要，协议 §2.1）
  → DshUniAgent adapter：context 序列化 → DSH send（一条 user message）
  → DSH turn（内部任意步数推理：stream → 工具 → …；工具面 = §6 白名单）
  → 模型调用 submit_decision(AgentDecision JSON)   ← 唯一终结路径
     （DSH 机制：ToolExecutionResult.concludesTurn=true 终结 step/turn——已确认）
  → adapter：JSON-Schema 校验 + V1-V6 机械校验 → 反序列化为 AgentDecision record
  → 返回 Kernel；turn 结束；Kernel 继续（Control/Grounding/Assurance/Effect）
```

### 3.2 审查结论

| 项 | 裁决 |
|---|---|
| 1 consultation : 1 turn | **严格**（adapter 在缝语义上是同步的：一个 context 恰等一个 turn 的一个 submit_decision 结果；不允许一个 turn 跨多次咨询） |
| turn 内 model steps | 允许多步（推理自由度），但受 adapter 侧 **turn deadline**（默认 120s，配置项）与 DSH step 机制约束；turn 内步数不进入产品协议 |
| internal tool calls | **v1 白名单 = {submit_decision} 单工具**（推理-only；无观察工具——上下文摘要已足够，协议 §5.2 防倾倒原则）。未来 reasoning-only 扩展（如只读策略自查）需独立裁决 |
| turn 结束条件 | **仅** submit_decision（成功终结）。其余 turn 终因（no-tool-call 收尾、max-tokens、blocked、aborted、error）一律映射为 **no-response**（null）→ Kernel fail-closed |
| timeout | adapter turn deadline 到期 → 取消/放弃该 turn → 返回 null；**该次咨询计入轮次预算**（防重试无限续命，M3 诚实失败） |
| malformed result | 传输层 JSON-Schema 校验失败 → null（no-response）+ 诊断记录；**不做静默重试**；计一次失败咨询 |

**禁止项落地**：DSH 永远只在 turn 内回答；turn 之间无循环驱动权——下一次
咨询永远由 Kernel 状态机边界发起（D1-D7 纪律在 Kernel 侧执法，DSH 无权
也不能发起 consultation）。

---

## 4. Q4 — AgentDecision Model：统一四元 union（L0/L1/L2 同协议）

现状（AgentDecision.cs，RUN-004 冻结）：`Act | NoAction | Defer`。
consultation-protocol v0.1 §2.2 已预留 `Policy` 为第四元（RUN-005 落 Kernel 侧）。
本稿确认其形状（**本步不改代码**；RUN-005 以此为输入）：

```yaml
AgentDecision =                       # 封闭 union；correlation 三态同律 D2
  | Act(AgentActionProposal)          # L0/L1 共用：Steps≤16 有序、每步完整
  |                                   #   目标表达；粒度是内容差异，不是 schema 差异
  | Policy(PolicyProposal)            # L2：bounded contingent decision package
  | NoAction(AgentNoActionProposal)   # 必带 CompletionEvidence（V4）
  | Defer(DecisionId, ObserveSpec)    # 有界等待（V5/T6）
```

| 议题 | 裁决 |
|---|---|
| union schema | 上述四元封闭 union；transport 层 tagged JSON（`{"kind":"act"|policy|no_action|defer", ...}`），**产品层仍是 .NET record**（transport 形状是 realization-private 投影，consultation-protocol §4 已预留「LLM realization 立项时机械投影」） |
| versioning | 产品协议不新增 version 字段（ContractVersion 已在 context 内；协议演化走 change + 白名单执法先例 RUN-004 D7）；transport JSON-RPC 带 `protocolVersion` 握手（adapter↔sidecar 私有） |
| correlation id | DecisionId 回带三态同律（Act/NoAction 载荷内、Defer 显式字段）；V1 mismatch fail-closed |
| decision id | `decision-{runId}-{n}` 由 Kernel 铸造（D2），Agent 只回带不铸造 |
| rationale | Justification 字段保持（Act/NoAction 现有）；**不升级为正式协议必填字段**——理由属策略记忆（§7），进 DSH session log 供诊断，不进 canonical record（防 protocol 膨胀） |
| completion evidence | CompletionEvidence { Basis, Checklist }（RUN-004 D5 三层完成证明已冻结，不动） |
| defer reason | ObserveSpec { Subject?, MaxRounds≤4 }（SR-067/068，已冻结，不动） |
| policy payload | §5 |

**统一性**：L0/L1/L2 不设三套接口——L0/L1 同为 Act（步数与语义层级是内容），
L2 是 Policy 成员。Kernel 入口校验（V1-V6）对所有成员一致执法。

---

## 5. Q5 + L2 Policy Contract — bounded contingent decision package（Agent 侧）

### 5.1 三层粒度正式定义

| 层 | 是什么 | 进协议的形态 |
|---|---|---|
| **L0 Act** | 单次/极少次具体**语义**动作（"activate wifi-entry"——非物理坐标；坐标永远由 Kernel fresh Grounding 产生） | Act 的 1..n 步 |
| **L1 Plan Hypothesis** | strategy / route / next semantic objective | **不单独进 AgentDecision**：已知路径 = Act 的有序 ≤16 步 + Justification；「假设」维度（候选路线、为什么弃选）= Agent internal strategy state（§7），不进协议。理由：协议只交换**可执行建议**，不交换心理状态；Kernel 无法消费「假设」 |
| **L2 Policy** | 一次声明的规则 + 驱动器机械展开（RUN-005） | **Policy 成员（正式能力，非 optional extension）** |

L1 与 Policy 的关系：L1 是「这次走哪条路」的回答；Policy 是「以后同类局面
都按此规则」的回答。前者一次性，后者可复用带预算。

### 5.2 PolicyProposal schema（Agent 侧产出契约；Kernel 安全展开归 RUN-005）

对齐 baseline §24.2 对 Bounded Contingent Decision Package 的既有语义
（immutable advisory、typed tri-state Guard、单 active + 预算内 speculative、
每动作 fresh Grounding/Assurance、Guard fail closed 回决策边界）与粒度文档
五要素（谓词/模板/终止/守卫/预算）：

```yaml
PolicyProposal:
  PolicyId:            string            # Agent 侧唯一（policy-{sessionId}-{n}）
  Scope:               语义范围表达       # 如 "current settings list"——语义角色/容器
                                         #   签名，非坐标、非 occurrence 引用
  Match:               谓词              # 元素级判别（role/text/checkable/enabled/
                                         #   epistemic 词汇——M1：无数值置信度）
  ActionTemplate:      动作模板          # 与 AgentActionStep 同形（TargetRole/
                                         #   EffectClass/DesiredState 表达式）
  Termination:         终止条件          # 语义命题（如 "wifi.enabled == true"）
  Guards:              [守卫]            # tri-state（Satisfied/Violated/Unknown）；
                                         #   Unknown/Violated → 中止展开回决策边界
  Bounds:                                # M3：一切循环体显式预算
    MaxApplications:   int               # 模板最大适用次数
    MaxRounds:         int               # 展开轮次上限（与合同轮次预算取 min）
    MaxTraversal:      int               # 遍历类等价上限（滚动/深度的语义上界）
  ReconsultCondition:  重咨询条件         # 何种局面必须回 L0（默认 = 任一 Guard
                                         #   Unknown/Violated，或预算耗尽）
  Fallback:           枚举              # exhausted → Reconsult（默认）| FailClosed
  Justification:      string?
```

V6（协议已预留）：谓词/终止/守卫/预算**四缺一 → fail-closed 拒收**。
示例（粒度文档 A2 空调温度）：

```yaml
PolicyId: policy-s1-1
Scope: { screen: current, container: climate-panel }
Match:  { role: temp-decrease, enabled: true, epistemic: Observed }
ActionTemplate: { targetRole: temp-decrease, effectClass: tap, desiredState: null }
Termination: { claim: cabin-temp, equals: "20" }
Guards: [{ name: temp-stalled, expr: "连续 2 次观察 temp 不变", on: Violated }]
Bounds: { MaxApplications: 8, MaxRounds: 8, MaxTraversal: 0 }
ReconsultCondition: any-guard-tripped | bounds-exhausted
Fallback: Reconsult
```

**边界重申**：Policy 是 advisory proposal，不是 effect batch、不是 driver
macro、不是第二 Runtime（baseline §24.2 原文语义）；Kernel 如何安全展开
（每动作仍走完整 Grounding→Assurance→Effect 链、不变量 43 屏障逐次适用）
= RUN-005 的 scope，本稿不设计。

---

## 6. Q6 + Q7 — DSH 承担面与 Tool Surface

### 6.1 DSH 承担（不在 .NET 重造）

```text
✓ Session（append-only log、resume、fork 治理）
✓ Agent loop（turn/step、stop 条件、max-tokens、blocked/abort）
✓ Prompt assembly（system prompt 组装、context 拼装）
✓ Provider routing（ctx.llm route {provider, model}；DeepSeek/GLM/OpenAI/mock 可换）
✓ Model invocation（流式、重试、adapter defaults）
✓ Strategy history（session log 内的推理与决策史——§7 边界内）
✓ Structured decision submission（submit_decision + JSON-Schema + concludesTurn）
```

DSH 机制核查依据：`ReactLoopAgent`（turn/step 循环、`concludesTurn` 终结已
确认）、cordis 工具注册（TS types → JSON Schema 参数）、event-sourced session
（`deriveMessages` / resume replay）、`dsh-headless`/SDK 可编程驱动、
workflow 工具已示范「schema 校验的子 agent 结果」模式。

### 6.2 DSH dedicated profile（"uniclaw-agent"）

```text
tools allowlist = { submit_decision }        # v1 唯一工具；concludesTurn = true
无 shell / fs / web / subagent / browser / adb / 任何 coding 工具
无 product instruction 之外的 system prompt 注入面（§6 隔离：AGENTS.md/
  coding skills 不得成为产品指令权威——realization baseline §6）
```

### 6.3 默认禁止清单（裁决：全部禁止，无一例外）

```text
× click / swipe / tap 坐标     × adb / 设备访问        × browser 效应
× 直接 Kernel mutation        × RunModel / WorldModel 读写
× Effect 类工具               × Observation 发起权
```

**为什么没有绕过 Control/Grounding/Assurance/Effect Boundary**：UniAgent 的
唯一输出通道是 AgentDecision（advisory proposal），物理效应链完整保留在
Kernel：proposal → 入口校验 → Control 签发 intent → fresh Grounding →
Assurance → Effect Boundary → 环境。Agent 无任何触碰现实的工具，连「提议
坐标」都被协议排除（AgentActionStep 只有语义目标表达）——绕过路径在
工具面与协议面双重不存在。**UniAgent 不直接执行现实 effect = 结构性成立，
不靠提示词自觉。**

---

## 7. Q8 — Strategy State Boundary（DSH session 可存什么）

| 允许（strategy state） | 禁止作为权威（reality truth） |
|---|---|
| 当前 Plan Hypothesis / 候选路线 | current World State |
| 曾拒绝的策略及原因 | current Run State |
| 推理历史（session log 原生） | effect 成功真值 |
| policy exhaustion 历史 | proof 真值（obligation/evidence 状态） |
| 语义假设（"设置页应该有 wifi-entry"） | current target binding |

**核心规则**：Agent 可以记住「我准备怎么做」「以前为什么失败」；不能把
过去观察记成当前现实。任何现实判断必须重新来自最新 AgentDecisionContext
——adapter 在每次 send 时**全量携带当次 context**（不复用 DSH 侧缓存的
世界摘要做决策依据；策略记忆只允许影响「怎么想」，不允许充当「是什么」）。

产品侧对应物：baseline §24.5 Agent Continuation（cognition summary + refs，
非 shadow state）——DSH session log 即其 realization-private 载体；恢复时
先由 owner records + fresh observe 确定现实，再由 UniAgent 重评（§24.5 原文
继承为 adapter 行为规则）。

---

## 8. 四张图

### 8.1 Authority Matrix

| Capability | UniAgent(角色) | DSH(实现) | Kernel | Control | Assurance | Effect |
|---|---|---|---|---|---|---|
| Goal 语义/评价 | **O** | r（承载推理） | – | – | – | – |
| global strategy / Plan Hypothesis | **O** | r（策略记忆载体） | – | – | – | – |
| 语义决策（Act/Policy/NoAction/Defer） | **O** | r（reasoning+提交） | – | – | – | – |
| L2 Policy authoring | **O** | r | – | – | – | – |
| Contract authoring（P1） | **O** | r（未来） | admission **O** | – | – | – |
| 咨询时机（六触发点） | – | – | **O** | – | – | – |
| 决策入口校验 V1-V6 | – | – | **O** | – | – | – |
| Run lifecycle / orchestration | – | – | **O** | – | – | – |
| Tactical Hypothesis / Control Intent | – | – | – | **O** | – | – |
| Grounding / binding | – | – | – | – | **O**（验证） | – |
| 现实 effect 执行 | – | – | – | – | – | **O** |
| World/Run truth | – | – | **O** | – | – | – |
| session/log/prompt/provider | – | **O**（realization-private） | – | – | – | – |

（O=owner，r=realization 承载；DSH 列全部为 realization-private，无 Product Authority）

### 8.2 Runtime Sequence

```text
Kernel(driver 状态机边界 T1..T6)
  │ ConsultAgentV2(ctx)
  ▼
DshUniAgent adapter（.NET，src/UniClaw.Agent.Dsh）
  │ serialize ctx → jsonrpc send（sidecar stdio）
  ▼
DSH sidecar（uniclaw-agent profile，tools={submit_decision}）
  │ turn: prompt assembly → ctx.llm(provider route) → stream …（多步推理）
  │ submit_decision(AgentDecision JSON)  ── concludesTurn
  ▼
adapter: JSON-Schema 校验 → V1-V6 由 Kernel 入口执法 → AgentDecision record
  ▼
Kernel: 机械校验 → Control 签发 intent → fresh Grounding → Assurance
       → Effect Boundary → 环境 → post-action 观察 → 下一边界（或 terminal
       → Outcome → UniAgent.Evaluate（.NET 确定性评价，GEV-004 不变））
```

### 8.3 State Ownership Matrix

| 状态 | Owner |持久性 | Authority 说明 |
|---|---|---|---|
| Product Session State | 产品层（.NET） | 产品持久 | Goal/Contract/Run/Outcome/Evaluation 关联；DSH sessionId 映射在此 |
| Goal State（Primary Goal） | UniAgent（canonical 在产品层） | 产品持久 | 只经显式 Goal revision 变更 |
| Contract State | Run Model admission | 产品持久 | versioned immutable View |
| Run State | Kernel RunModel | 产品持久 | 唯一执行事实源 |
| World State | Kernel WorldModel | 产品持久 | 唯一世界真值 |
| **Agent Strategy State** | UniAgent（角色） | **DSH session log（realization-private，可弃）** | 「怎么做/为何败」；非 shadow state（§24.5） |
| **DSH Session State** | DSH | realization-private | ≠ 任何产品状态（§2 边界） |
| Goal Evaluation | UniAgent（.NET 确定性实现） | 产品持久 | GEV-004 冻结；不从 DSH 产生 |

### 8.4 Failure Matrix

| failure | detector | owner | response | terminal? | reconsult? |
|---|---|---|---|---|---|
| DSH process unavailable（启动失败） | adapter（spawn/握手超时） | adapter | 该咨询 → null（no-response） | non-terminal | Kernel 按预算决定（失败计一轮） |
| DSH process crash（turn 中途） | adapter（exit event/deadline） | adapter | null + 诊断；sidecar 标记待重启 | non-terminal | 同上 |
| session not found（resume 失败） | adapter | adapter | **新 session（策略记忆清零，诚实降级）**；本次咨询 null | non-terminal | 是 |
| session corrupt（log 损坏） | DSH（load 校验） | adapter | 弃 session → 新 session；本次 null | non-terminal | 是 |
| model timeout（turn deadline 120s） | adapter | adapter | cancel/放弃 turn → null | non-terminal | 是（计轮次） |
| provider unavailable | DSH（llm error） | DSH 内部 | turn error → adapter null；DSH 可内部换 fallback route（model-bindings 语义），不可静默换协议 | non-terminal | 是 |
| malformed AgentDecision（schema 失败） | adapter（JSON-Schema） | adapter | null；不静默重试 | non-terminal | 是（计轮次） |
| unknown decision schema（版本失配） | adapter（schema 版本门） | adapter | null + 显式 protocol-mismatch 诊断 | non-terminal | 否（需人工/配置修复） |
| tool never calls submit_decision | adapter（deadline） | adapter | null（等价 no-tool-call 收尾） | non-terminal | 是（计轮次） |
| context too large（超序列化预算） | adapter（发送前尺寸门） | **协议层 OPEN**（§12 Q3） | fail-closed 拒发 + null；登记 | non-terminal | 否（需扩摘要投影） |
| budget exhausted（轮次/步数） | Kernel（RUN-004 D4/D8） | **Kernel** | 既有诚实失败语义（defer-exhausted/budget 终局） | terminal | 最后一次（T6 先例） |
| Defer repeatedly（反复等待） | Kernel（T6/M1 配额） | **Kernel** | 既有 defer-exhausted 终局 | terminal | T6 最后再问一次 |

**总规则**：一切 DSH 侧失败折叠为**咨询失败**（null / AgentDecisionFailed
族），进入 Kernel 既有 fail-closed 语义（零新 effect、合法非终态或
safe-stop）；**DSH failure 永不升级为 Kernel authority**——Kernel 不因
realization 故障获得额外决策权，也不替 Agent 编造决策。

---

## 9. Simulation / Replay 策略

| 问题 | 裁决 |
|---|---|
| 二者同一产品缝？ | **是**。ScriptedUniAgent（deterministic double）与 DshUniAgent 实现同一委托 `Func<AgentDecisionContext, AgentDecision?>`——替换不改 Kernel（缝不变量，D6 形态） |
| DSH 进 simulation 依赖闭包？ | **否**。deterministic regression 闭包 = Kernel + ScriptedUniAgent（baseline §24.8：Product Host 闭包不含 Simulation 功能；反向亦然——Simulation 不依赖 live provider） |
| `agentDecisionRealization` 标注 | 确定性回归 = `double`（现状不变）；DSH live = `real`（新增枚举值候选 `real-dsh` 或 realization 详情字段——SIM-002 G4 标注体系扩展，独立小 change） |
| live-model scenario 属确定性回归？ | **否**。live DSH 场景 = 冒烟/采样验证（conformance C1-C10 用），不进 deterministic regression 真值链（TRX 状态链） |
| DSH session 是否 replay？ | **不需要**。deterministic replay 的对象是 **AgentDecision 序列**（scripted），不是 DSH 内部 turn/token 流 |
| 记录什么？ | **AgentDecision record（canonical）**，绝不记录/重放 model token stream（realization baseline §8.3：transcript 逐字相等禁止作为判据） |

差分 conformance 预留：同一 context 序列喂 ScriptedUniAgent 与 DSH realization，
比对**语义等价**（决策 kind + 语义载荷等价，非逐字）——realization baseline
C1/C10 场景；属后续 change，本稿只留缝。

---

## 10. Provider Boundary 与 Determinism 裁决

### 10.1 Provider 边界

```text
UniAgent（角色，产品协议不变）
   ↓ AgentDecisionContext / AgentDecision / Policy / Kernel protocol（替换无关层）
DSH（route {provider, model}——ctx.llm 可插拔：DeepSeek/GLM/OpenAI/mock 已证可换）
   ↓ provider
model
```

替换 provider/model = DSH 配置变更；adapter、协议、Kernel、测试面零改动。
产品侧 **不使用** 开发 harness 的 `.dsh/model-bindings.yaml`（那是 Development
Harness context 的 adapter 绑定；Product Runtime 有自己的 realization 配置面
——realization baseline §6 隔离规则）。

### 10.2 Determinism 裁决（RUN-004 语义重申 + amendment candidate）

| 层 | 要求 |
|---|---|
| 协议确定性 | 触发序列、DecisionId 序列（D2）、幂等不重问（D1）——确定性要求成立 |
| correlation 确定性 | V1 执法确定 |
| validation 确定性 | V1-V6 机械、顺序固定 |
| state transition 确定性 | Kernel 状态机确定（输入相同决策 → 相同转移） |
| **simulation double 确定性** | ScriptedUniAgent 同输入同输出（digest 可复现，SIM-002 体系） |
| live LLM | **不要求 bit-for-bit**（realization baseline §8.3 已冻结禁止以 transcript 相等为判据；conformance = canonical 语义等价） |

**Amendment candidate（登记，本步不改）**：RUN-004 Acceptance #8「同输入两跑
决策序列与 digest 一致」当前语境 = scripted double；建议在 spec 补注一句
「live realization 的对应判据 = canonical record 语义等价（realization
baseline §8.2/8.3），非 token/决策序列逐位相等」，避免未来误读为 live
模型确定性要求。

---

## 11. Grill Preparation —— 十问预答

1. **DSH 如何证明不是第二个 Kernel？** Kernel = 唯一 lifecycle/orchestration
   owner；DSH 无 Kernel 操作面工具、无驱动循环（只在 turn 内被问而答）、无
   Run/World 写路径；其全部输出折叠为一个 advisory record 经 V1-V6 入口。
   DSH 崩溃时 Kernel 依既有 fail-closed 语义继续拥有 Run。
2. **Policy 如何证明不是 workflow program？** Policy 是 immutable advisory
   data（谓词/模板/终止/守卫/预算声明），无执行器；展开权在 Kernel 驱动器
   （RUN-005），每动作仍走完整 Grounding→Assurance→Effect 链 + 不变量 43
   屏障；Guard Unknown/Violated 即中止回决策边界——不存在「Agent 的程序
   在 Kernel 里跑」，只有「Kernel 依声明机械展开并逐次执法」。
3. **DSH session 如何证明不是第二个 Run Model？** session 只存策略状态
   （§7 白名单）；Run 事实（进度/预算/义务/evidence）唯一来自 context 投影，
   每次咨询全量重发；session 丢失的后果 = 策略记忆丢失，不是 Run 状态丢失。
4. **Strategy memory 如何避免成为第二个 World Model？** 同上——记忆影响
   「怎么想」不充当「是什么」；一切现实断言的来源被协议钉死为最新 context。
5. **为什么 DSH 不能直接获得 effect 工具？** profile 白名单单工具
   （submit_decision）；效应链 authority 完整保留 Kernel/Effect Boundary
   （§6.3 双重排除：工具面无路径 + 协议面无坐标）。
6. **provider 换掉后哪些协议不变？** AgentDecisionContext / AgentDecision /
   Policy / 咨询纪律 V1-V6 / Kernel 协议全不变（§10.1 替换无关层）。
7. **DSH crash 时谁拥有恢复权？** adapter（supervisor）拥有 sidecar 生命周期
   与 session resume 决定权；产品状态恢复权永远在 owner records（§24.4）；
   Agent 不参与恢复裁决。
8. **Policy 连续展开时谁拥有现实判断权？** 每次展开动作的 Grounding/
   Assurance 判定权在 Kernel（fresh、逐次）；Agent 的 Match/Termination
   谓词只是建议输入，Unknown 一律 fail closed 回咨询。
9. **consultation 与 turn 生命周期是否严格闭合？** 是——1:1 映射、唯一
   终结路径 submit_decision、其余终因一律 no-response；无跨咨询的 turn、
   无跨 turn 的咨询。
10. **是否存在 Agent → reality 旁路？** 工具面（单工具白名单）、协议面
    （无坐标/occurrence 引用）、效应面（Effect Boundary 独占）三重不存在；
    conformance C5（terminal 后工具调用零效应）作为回归执法点。

---

## 12. Open Questions（真需架构裁决）

1. **Q-ctx：context 尺寸预算**——AgentDecisionContext 序列化上限与超限
   处理（当前有界摘要按字段设计，但无显式字节预算；协议 §5.2 证伪条款
   预留了扩投影通道）。裁决时机：AGT-001 实现前。
2. **Q-annot：realization 标注扩展**——`agentDecisionRealization` 需否
   增加 `real-dsh` 值或 realization 详情子字段（SIM-002 G4 体系演进，
   独立小 change）。
3. **Q-timeout：turn deadline 与轮次预算的耦合语义**——失败咨询计一轮
   （本稿裁决）是否需要在合同里显式声明（MaxConsultations 是否区分
   成功/失败咨询）。裁决时机：RUN-005 spec 评审。
4. **Q-transport：stdio JSON-RPC 的具体信封**（初始化握手、错误码表、
   日志通道复用与否）——实现 change 的 CONTRACT 级设计，不属本稿。

## 13. Files / Interfaces To Change Later（只列，不改）

```text
src/UniClaw.Agent.Dsh/               新程序集：DshUniAgent adapter + sidecar supervisor
                                     + submit_decision schema + transport（jsonrpc over stdio）
src/UniClaw.Host/                    组合根：ConsultAgent 缝注入 DSH realization（配置门控）
dsh-sidecar/（DSH 侧 product profile） uniclaw-agent profile：单工具白名单 + 启动入口
src/UniClaw.Kernel/Runtime/AgentDecision.cs        +Policy 成员（RUN-005 联动，非本 change）
tests/UniClaw.Dsh.Tests/             adapter 协议测试（fake sidecar）+ failure matrix 测试
scenarios/schema.json                realization 标注扩展（Q-annot 裁决后）
changes/AGT-001/                     本 change 状态（spec/plan/state）
docs/design/agent-runtime-transport-v0.1.md        Q-transport 设计稿（实现前）
```

## 14. Verdict

**READY_FOR_GRILL** —— 无缺失架构决策阻塞 grill；4 个 Open Questions
均已在文中给出倾向性裁决并标注裁决时机。
