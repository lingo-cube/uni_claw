# AGT-001 — UniAgent Runtime Architecture：DSH 作为 Product UniAgent Realization（设计稿 v0.2 · post-grill revision）

> Status: DESIGN DRAFT v0.2（第一次正式 adversarial grill：PASS_WITH_FINDINGS；
> F1-F7 已按 owner 裁决 GQ1=A / GQ2=B-DEFER / GQ3=YES / GQ4=YES 修订；
> 待 focused re-grill）
> Authority: NONE（本文档不修改任何 baseline；上游顺序见 §0）
> 输入谱系：product-architecture-baseline-l0-l3（§24.2/§24.5/§24.7/不变量 43-47）·
> uniagent-realization-baseline-v0.1（FROZEN，ADR-0022）·
> consultation-protocol-v0.1 · decision-granularity-scenarios-v0.1 ·
> RUN-004（CLOSED）· GEV-004（CLOSED）· DSH checkout 机制核查 ·
> grill-2026-09-24（F1-F7 findings + GQ1-GQ4 owner 裁决）
> 日期：2026-09-24 · 修订：v0.2（v0.1 → grill → 本版；修订日志见文末）

---

## 0. Authority Order 与本文档位置

```text
Target Product Architecture（L0-L3，FROZEN）
  └─ Inter-Component Protocol Baseline
      └─ ADR-0019（Kernel self-drive）· ADR-0022（双 realization）
          └─ UniAgent Realization Baseline v0.1（FROZEN）
              └─ 本设计稿 v0.2（填 baseline §9 Deferred 槽位）
                  └─ 后续：RUN-005（Kernel 侧 Policy 展开与词汇表冻结）· 实现 change
```

核心原则（贯穿全文）：

> **UniAgent 是产品角色，DSH 是这个角色的实现机制。**
> **Kernel owns WHEN；DSH-UniAgent owns WHAT。**
> DSH 的 session/turn/step/tool/event/transcript 全部是 realization-private，
> 不取得任何 Product Authority。

---

## 1. Q1 — Runtime Boundary：.NET 监管的 local sidecar（维持 v0.1 裁决）

**B：local sidecar + stdio 版本化 JSON-RPC**。A（embedded）因 cordis/Node 绑定
与崩溃隔离丧失被拒；C（standalone service）多租户买家不存在（ADR-0026）。
.NET Product Host 是 sidecar 的 **supervisor**（启动/健康/关闭/带外中断，见 §3.2）。

Adapter 位置：新程序集 `src/UniClaw.Agent.Dsh`，实现现行缝
`Func<AgentDecisionContext, AgentDecision?>`（RUN-004 D6 冻结形态**不变**——
transport 异步、缝语义同步）。

---

## 2. Q2 — Session Mapping（v0.2 修订：吸收 F6 / GQ2）

1 Product Session : 1 DSH session；adapter 创建/关闭；DSH sessionId 为
realization-private correlation（≠ Product Session / Run Model / World Model）。

**跨重启语义（GQ2=B 正式冻结）**：

```text
同一 KernelRunDriver / runtime lifecycle 内（D1 收窄至此）：
  已 adopted decision（_adoptedDecision）→ 同一 semantic boundary 不重复 consult

process restart：
  canonical product owner records
    ↓
  fresh observation / reconcile
    ↓
  reconstruct valid runtime state
    ↓
  UniAgent re-evaluate
    ↓
  新 semantic decision boundary → 允许新的 consultation / DecisionId
  （这不是违规 duplicate）
```

**禁止**：从 DSH transcript 恢复现实 · 从旧 Agent session 恢复 Run truth ·
盲目 replay 旧 AgentDecision。DSH session resume 只是策略记忆的 realization
便利，不是产品恢复路径。

**不提前创建的 authority（GQ2 裁决）**：Kernel consultation journal ·
consultation adoption canonical owner · durable adopted AgentDecision owner ·
cross-process exactly-once consultation——全部 DEFER；未来要求出现时独立开
**Recovery / Agent Continuation change** 再裁决（AgentDecision durable owner /
consultation durable owner / resume semantics）。AGT-001 不新建任何 Kernel
owner record。

---

## 3. Q3 — Consultation ↔ Turn（v0.2 修订：interruption plane + late-response 保护 + 预算语义）

### 3.1 生命周期（final model，冻结形态）

```text
Kernel
  ↓ creates AgentDecisionContext + DecisionId
adapter begins one consultation
  ↓
DSH one turn
  ↓ internal model/reasoning steps
submit_decision
  ↓
adapter validates schema/correlation
  ↓
one AgentDecision
  ↓
turn terminates；DSH becomes idle
  ↓
Kernel continues
```

**禁止**：turn 结束后 DSH 继续执行产品任务 · DSH 自行发起下一次咨询 ·
DSH 直接执行 reality-changing effect。1 consultation : 1 turn 严格闭合；
**per Run：至多一个 active live consultation**（并行咨询需独立 change）。

### 3.2 Interruption plane（F1 / GQ1=A）

```text
Product cancel / human-preemption condition
        ↓  产品 lifecycle / lease / package 语义成立（owner 侧）
Host / adapter supervisor（.NET）
        ↓  AbortCurrentTurn()          ← realization-private 机械中断
DSH current turn terminated
        ↓  缝返回 null（no-response）
Kernel fail-closed 继续拥有 Run
```

**AbortCurrentTurn 的身份边界（冻结）**：

- 它**只是** DSH realization 层终止当前阻塞 turn 的机械 interruption
  mechanism；
- **≠** Cancel Run；**≠** Human Preemption Authority；
- adapter/supervisor 不拥有：cancel authority · preemption authority ·
  Run lifecycle authority · lease invalidation authority；
- 它不修改 Run state、不宣布 Cancelled、不写 Outcome、不 invalidate lease、
  不产生 AgentDecision。

**Deadline（冻结为架构要求，不冻结数值）**：

```text
turn deadline MUST be bounded
current turn MUST be externally abortable
```

default 60s 仅为 realization 默认配置，不是产品架构常量。

**Late response protection（F1+F4 联合冻结）**：abort/timeout 后旧 turn 若
最终仍产出 response——**MUST NOT re-enter Kernel**，由 transport correlation
丢弃（规则见 §3.4）。

### 3.3 预算语义（GQ3=YES）

每一次由 Kernel 正式发起的 **semantic consultation attempt** 消耗
MaxConsultations 一轮，**包括**：timeout · malformed response · no-submit ·
provider failure · adapter 返回 null——失败本身消耗 bounded resource（M3）。

**Transport retry 例外**：同一次 consultation 内的 adapter transport retry /
JSON-RPC retransmit / provider transient retry，若没有回到 Kernel 重新形成
新的 semantic consultation，**不**额外消耗 MaxConsultations。

```text
Product consultation budget ≠ Transport retry budget（两个预算，分层）
```

### 3.4 Transport correlation（F4 修订：不新增 Product identity）

**Product semantic correlation 唯一 = DecisionId**（既有）。不创建
ConsultationId——避免与 DecisionId 重叠的双 Product identity。
realization-private transport correlation（JSON-RPC request id / turn id /
request generation / transport attempt id）不进入 Product protocol。

**丢弃规则（adapter 强制执法，全部 fail-closed）**：

```text
当前无 active DecisionId            → response drop + diagnostic
response.DecisionId ≠ active        → drop + diagnostic
turn 已 aborted                     → late response drop + diagnostic
transport request generation 已过期 → drop + diagnostic
duplicate response                  → second response drop + diagnostic
```

任何被丢弃的 stale response：不得进入 Kernel · 不得生成 effect · 不得消耗
第二次 semantic consultation。

---

## 4. Q4 — AgentDecision Model（统一四元 union；维持 v0.1，schema 权威见 §6.4）

```yaml
AgentDecision =                       # 封闭 union；correlation 三态同律 D2
  | Act(AgentActionProposal)          # L0/L1 共用（粒度=内容差异）
  | Policy(PolicyProposal)            # L2（正式能力；形态见 §5）
  | NoAction(AgentNoActionProposal)   # 必带 CompletionEvidence（V4）
  | Defer(DecisionId, ObserveSpec)    # 有界等待（V5/T6）
```

DecisionId 由 Kernel 铸造（D2），Agent 只回带；decision id / correlation /
completion evidence / defer reason 维持 RUN-004 冻结语义不变。

---

## 5. Q5 + L2 Policy Contract（v0.2 修订：F2 封闭语言）

### 5.1 三层粒度（维持 v0.1）

L0/L1 同为 Act（粒度=内容非 schema）；**L1 假设维度 = Agent Strategy State
不进协议**；L2 = Policy 成员。

### 5.2 Policy 语言（F2 冻结：typed closed AST / discriminated union）

**Policy 是 bounded contingent advisory decision package**——不是 workflow、
script、program、effect batch、driver macro。

**语言封闭性（架构级冻结）**：

> Policy language MUST be closed and typed。
> 禁止：arbitrary expression · source code · script · free-form executable
> predicate · embedded workflow language · general arithmetic expression
> evaluator。

**Match / Predicate**——只冻结形态（typed discriminator + typed literal
operands）；完整词汇表归 RUN-005 按真实场景冻结。示例形态：
`ClaimEquals / ClaimNotEquals / ElementRoleEquals / ElementExists /
StateEquals`（非穷尽、非冻结）。

**ActionTemplate**——typed action template + **literal values** + typed
match binding references。禁止 arbitrary expression / dynamic code /
embedded effect script。

**Guard**——closed guard kind + typed inputs + tri-state result
（Satisfied / Violated / Unknown）。自由文本（如「连续两次 temp 不变」）
**不得**直接交给 Kernel 解释；该语义若需要，RUN-005 增加正式 typed guard
（如 `ObservationUnchanged`）。

**AGT / RUN 边界（冻结）**：AGT-001 冻结「closed and typed」要求；
RUN-005 冻结完整 Policy vocabulary（predicate kinds / guard kinds /
action templates / runtime expansion semantics）。本稿不提前造完整 DSL。

### 5.3 PolicyProposal 结构（v0.2 修订）

```yaml
PolicyProposal:                       # 全部字段 typed/closed
  PolicyId:            string          # Agent 侧唯一（policy-{sessionId}-{n}）
  Scope:               typed 语义范围  # 语义角色/容器签名；非坐标、非 occurrence 引用
  Match:               [Predicate]     # §5.2 closed discriminator + literal operands
  ActionTemplate:      typed template  # §5.2：literals + match binding refs only
  Termination:         [Predicate]     # 同 Match 词汇（claim/元素判别）
  Guards:              [Guard]         # closed guard kind + typed inputs + tri-state
  Bounds:                              # M3 一切循环体显式预算
    MaxApplications / MaxRounds / MaxTraversal: int
  ReconsultCondition:  closed 枚举     # 默认 = 任一 Guard Unknown/Violated 或预算耗尽
  Fallback:            枚举           # Reconsult（默认）| FailClosed
  Justification:       string?
```

V6（协议既有）：谓词/终止/守卫/预算四缺一 fail-closed；**不可解析的
Policy 形态 → 同样 fail-closed 拒收**（Kernel 永不解释 Agent 的任意文本）。

### 5.4 Policy 的能力边界（final boundary，冻结）

**可以描述**：匹配什么语义条件 · 提议什么语义动作模板 · 何时终止 ·
最多适用几次 · 何时必须重咨询。

**不能描述**：raw click 坐标 · 任意可执行代码 · 无界循环 · 直接设备指令 ·
通用 workflow 逻辑。

**Kernel 对 Policy 的每个 reality-changing application**：fresh World →
Control → Grounding → Assurance → Effect → Verification——不因 Policy 来自
Agent 就跳过任何 authority stage（baseline §24.2 + 不变量 43；展开语义归
RUN-005）。

---

## 6. Q6 + Q7 — DSH 承担面与 Tool Surface（v0.2 修订：F3 三层机械关闭 + F5/GQ4 单源）

### 6.1 DSH 承担（不在 .NET 重造；维持 v0.1）

session/log · agent loop · prompt assembly · provider routing · model
invocation · strategy history · structured submission。

### 6.2 Headless 机械关闭（F3 冻结：三层）

**Layer 1 — Launch Profile**：产品 sidecar profile 不加载——webserver ·
interactive UI · approval UI · steer · inject · ask-user · todo/task
manager · workflow · subagent · shell · filesystem · browser · device
effect tools。（未来引入任何一项 = 独立架构 change。）

**Layer 2 — Static capability allowlist**：产品 UniAgent DSH profile 有明确
manifest；第一阶段产品输出面原则上只有 **submit_decision**；其他
reasoning-only capability 若存在必须逐项列入 allowlist；**禁止默认继承
DSH developer profile**。

**Layer 3 — Runtime handshake**：adapter 启动时校验 capability manifest +
protocolVersion + schemaVersion + schemaHash；发现任何未批准 capability →
**REFUSE STARTUP**。不依赖 sidecar 单向自报——**expected static manifest
vs reported runtime manifest 机械比对**。

### 6.3 现实效应排除（维持 v0.1，双层）

工具面（单工具白名单）+ 协议面（AgentActionStep 只有语义目标表达，无坐标）
双重排除；**UniAgent 不直接执行现实 effect = 结构性成立**。

### 6.4 Schema 单一权威源（F5 / GQ4=YES 冻结）

```text
.NET authoritative records（AgentDecisionContext / AgentDecision / Policy records）
        ↓ 生成
JSON Schema artifact（generation artifact = realization bridge artifact，非新 Authority）
        ├─ adapter runtime validation
        ├─ DSH submit_decision tool schema
        └─ generated TypeScript types
```

**禁止**三份手写 schema（.NET 一份 / JSON Schema 一份 / TS 一份）。
Handshake 校验 protocolVersion / schemaVersion / **schemaHash**——任何不
匹配 FAIL CLOSED，不得进入 consultation。

---

## 7. Q8 — Strategy State Boundary（v0.2：F7 边界注记）

**允许 DSH session 保存**：「上一轮 context 告诉我 X」·「之前尝试 strategy
Y 失败」·「Policy P exhausted」——以及 plan hypothesis / 弃选策略 / 推理史 /
policy 耗尽史 / 语义假设 / rationale。

**冻结规则**：

> 任何历史世界信息 MUST NOT be authoritative current reality。
> 冲突时 **latest AgentDecisionContext wins**。

不尝试把 DSH session 清洗成无任何历史世界文本——那是 reasoning quality
问题，不是 Product authority 问题（权威面由 §3.4 丢弃规则 + 下游逐动作
fresh 链保证）。每次咨询全量携带当次 context（CONTEXT.md 词条
**Agent Strategy State** 已固化此分界）。

---

## 8. 四张图（v0.2 修订）

### 8.1 Authority Matrix（+interruption 列注记）

| Capability | UniAgent(角色) | DSH(实现) | Kernel | Control | Assurance | Effect |
|---|---|---|---|---|---|---|
| Goal 语义/评价/Contract authoring | **O** | r | admission **O** | – | – | – |
| 语义决策（四元 union） | **O** | r（推理+提交） | – | – | – | – |
| L2 Policy authoring | **O** | r | – | – | – | – |
| 咨询时机/次数/预算 | – | – | **O** | – | – | – |
| 决策入口校验 V1-V6 | – | – | **O** | – | – | – |
| Run lifecycle / World/Run truth | – | – | **O** | – | – | – |
| Tactical Hypothesis / Control Intent | – | – | – | **O** | – | – |
| Grounding / Assurance 判定 | – | – | – | – | **O** | – |
| 现实 effect 执行 | – | – | – | – | – | **O** |
| session/log/prompt/provider | – | **O**（realization-private） | – | – | – | – |
| **turn 中断（AbortCurrentTurn）** | – | **机械执行**（无 authority：≠cancel/≠preemption/≠lifecycle/≠lease） | **O**（cancel/preemption 语义） | – | – | – |

### 8.2 Runtime Sequence（含中断面）

```text
Kernel(T1..T6) → ctx+DecisionId → adapter → [DSH turn: 推理→submit_decision]
                                          ↘ （并行）product cancel/preemption 条件成立
                                             → Host/adapter supervisor
                                             → AbortCurrentTurn() → turn 终止
                                             → 缝返回 null → Kernel fail-closed
adapter 校验 schema/correlation → AgentDecision → Kernel 机械校验
  → Control intent → fresh Grounding → Assurance → Effect Boundary → 环境
  → post-action 观察 → 下一边界（或 terminal → Outcome → UniAgent.Evaluate）
```

### 8.3 State Ownership Matrix（+restart 行）

| 状态 | Owner | 持久性 | 说明 |
|---|---|---|---|
| Product Session / Goal / Contract / Run / World State | 各产品 owner | 产品持久 | 恢复唯一依据（owner records + fresh observe） |
| Agent Strategy State | UniAgent（角色） | DSH session log（realization-private，可弃） | 记「怎么做」不充当「是什么」 |
| DSH Session State | DSH | realization-private | ≠ 任何产品状态；restart 恢复**不依赖**它 |
| Goal Evaluation | UniAgent（.NET 确定性实现） | 产品持久 | GEV-004 冻结 |
| driver `_adoptedDecision` | KernelRunDriver | **进程内**（D1 收窄至 runtime lifecycle） | 跨重启不作恢复依据 |

### 8.4 Failure Matrix（v0.2：并入 GQ3 预算语义 + F4 丢弃 + F6 restart）

| failure | detector | owner | response | terminal? | 计轮? |
|---|---|---|---|---|---|
| DSH process unavailable | adapter（spawn/握手） | adapter | null（no-response）；REFUSE STARTUP 若 capability/schema 校验失败 | non-terminal | **是** |
| DSH process crash（turn 中） | adapter（exit/deadline） | adapter | null + 诊断；sidecar 标记待重启 | non-terminal | 是 |
| session not found / corrupt | adapter/DSH load | adapter | 新 session（策略记忆清零，诚实降级）；本次 null | non-terminal | 是 |
| model timeout（deadline 到期） | adapter | adapter | AbortCurrentTurn 语义 → null | non-terminal | **是**（GQ3） |
| provider unavailable | DSH（llm error） | DSH 内部（可换 fallback route，不换协议）；终不果 → null | non-terminal | 是 |
| malformed AgentDecision（schema 失败） | adapter（JSON-Schema 单源） | adapter | null，不静默重试 | non-terminal | 是 |
| unknown decision/Policy schema 或版本失配 | adapter（schemaVersion/hash 门） | adapter | null + protocol-mismatch 诊断 | non-terminal | 是（但重试无意义，需配置修复） |
| tool never calls submit_decision | adapter（deadline） | adapter | null | non-terminal | 是 |
| **abort/timeout 后 late response** | adapter（§3.4 丢弃规则） | adapter | **drop + diagnostic；永不 re-enter Kernel** | – | 否（不消耗） |
| **duplicate response** | adapter（§3.4） | adapter | second response drop | – | 否 |
| context too large | adapter（发送前尺寸门） | 协议层 OPEN | fail-closed 拒发 + null | non-terminal | 是（登记 Q-ctx） |
| budget exhausted | Kernel（RUN-004） | **Kernel** | 既有诚实失败终局 | terminal | – |
| Defer repeatedly | Kernel（T6/M1） | **Kernel** | defer-exhausted 终局 | terminal | – |
| **process restart（跨进程）** | Kernel/Host | **产品 owner records** | fresh observe/reconcile → 重建 → UniAgent re-evaluate → 新边界允许新咨询（非违规 duplicate） | – | 按 Kernel 预算语义 |

**总规则（维持）**：一切 DSH 侧失败折叠为咨询失败；DSH failure 永不升级
为 Kernel authority；Kernel 不因 realization 故障获得额外决策权。

---

## 9. Simulation / Replay（维持 v0.1 裁决）

同一产品缝（double 与 real 同 delegate）；deterministic regression 闭包不含
DSH；live 场景 = 冒烟/conformance 不进 TRX 真值链；replay 对象 =
**AgentDecision record**，非 token stream（realization baseline §8.3 禁止
transcript 相等判据）。

---

## 10. Provider Boundary 与 Determinism（v0.2 明细化）

### 10.1 Provider（冻结）

```text
UniAgent Product Protocol → DSH runtime → provider（DeepSeek 首个；未来
GLM / OpenAI / local model）
```

替换 provider 不得改变：AgentDecisionContext · AgentDecision · Policy
schema · Kernel consultation protocol · Product authority。产品侧不使用开发
harness 的 `.dsh/model-bindings.yaml`（§6 隔离）。

### 10.2 Determinism（明确化）

**需要确定性**：Product protocol handling · DecisionId correlation ·
schema validation · state transitions · budget accounting · transport
stale-response rejection · simulation double · replay fixtures。

**不要求**：live LLM 同 prompt → bit-identical decision（live realization
可以非确定）。Simulation regression 继续用 ScriptedUniAgent/deterministic
double，经同一 Product seam。

（RUN-004 Acceptance #8 的 amendment candidate 维持登记：其确定性表述语境
= scripted double。）

---

## 11. Trace / UI 条款（v0.2：由隐含升为显式架构条款）

**Trace Plane**：允许 DSH UI 汇合展示 Agent Trace + Product Runtime Trace；
但 **DSH session log ≠ Product Trace authority**——产品事实由 UniClaw owner
records / trace system 持有。

**UI Projection**：DSH UI 未来可显示 Goal / Run / Current Policy /
Consultation / AgentDecision / Grounding / Assurance / Effect / Outcome——
全部是 **projection**。DSH task status / todo / session state 不得成为
Product Goal / Run lifecycle truth。

**UI Commands**：Cancel / Retry / Resume / Reconsult / Approve / Human
intervention 若存在 UI 入口，**必须进入 UniClaw Product command surface**
由 Product Authority 处理；**禁止 DSH UI 直接 mutate Product state**。

---

## 12. Grill 十问（v0.2 复核，结论不变 + 增强引用）

1-10 答案维持 v0.1 §11，增强：Q7（crash 恢复权）→ §2 restart 语义 +
§8.4 restart 行；Q9（生命周期闭合）→ §3.1 final model + §3.4 丢弃规则；
Q10（旁路）→ §6.2 三层机械关闭 + §11 UI command 条款。

## 13. Open Questions（v0.2 更新）

1. Q-ctx：context 序列化字节预算（实现前裁决，维持）。
2. Q-annot：realization 标注扩展（维持）。
3. ~~失败咨询计轮~~ → **已裁决（GQ3=YES），关闭**。
4. ~~transport 信封细节~~ → 收窄为：JSON-RPC 信封具体字段设计（实现
   change 的 CONTRACT 级；correlation 分层与丢弃规则已在本稿冻结）。
5. （新增，DEFER 登记）cross-process exactly-once consultation / durable
   adopted decision / Agent Continuation → 独立 Recovery change。

## 14. Files / Interfaces To Change Later（维持 v0.1 §13）

## 15. Revision Log

- v0.2（2026-09-24）：grill F1-F7 修订——interruption plane（GQ1=A，含
  AbortCurrentTurn authority 边界与 late-response 保护）；Policy 语言
  closed/typed 冻结（F2）；三层 headless 机械关闭（F3）；transport
  correlation 分层与丢弃规则、不新增 Product ConsultationId（F4）；schema
  单一权威源 + handshake schemaHash（F5/GQ4）；D1 收窄 + restart 语义 +
  authority DEFER 清单（F6/GQ2）；strategy state 边界注记（F7）；预算语义
  （GQ3）；Trace/UI 显式条款；Determinism 明细化。
- v0.1（2026-09-24）：初稿。

## 16. Verdict

**READY_FOR_FOCUSED_RE_GRILL** —— F1-F6 闭合、F7 注记入文；focused
re-grill 范围限定为六项 finding 闭合验证 + F7 注记确认。
