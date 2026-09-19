# Runtime 核心模型与 Interface 归档骨架 v0.1

> DocumentType: `RUNTIME_CORE_MODEL_INTERFACE_ARCHIVE_SKELETON_V0_1`
>
> Status: `DRAFT / REVIEW_REQUIRED`
>
> Authority: `NONE`
>
> Date: `2026-09-13`
>
> Scope: `archive rules, templates, and frozen-model index only`
>
> Companion: `docs/design/runtime-perception-world-simulation-replay-roadmap-v0.1.md`

> Stateful Grill: `Human-selected directions recorded in companion roadmap; no new model or Interface promoted by this file`

---

## 1. 用途与当前边界

本文件是未来“核心模型与核心 Interface 归档”的**空骨架**。当前只做三件事：

1. 定义模型与 Interface 的晋升规则；
2. 提供归档模板；
3. 索引已由冻结产品/协议/UIWorld/组件基线或已接受 ADR 确认的现有模型。

当前明确不做：

- 不从 Runtime、Simulation、Replay、Asset、Observation Session、Supervision、World Presentation 等名词推导新模型或 `Ixxx`；
- 不复制 `docs/design/runtime-flow-simulation-core-model-v0.1.md` 中未经 Human 裁决的候选接口；
- 不冻结签名、字段、namespace、transport、storage、timeout 或 retry policy；
- 不把现有 P1–P23 typed protocol edges 等同为某个语言级 Interface；
- 不修改 `CONTEXT.md`、ADR、Architecture Baseline 或产品实现。

只有 companion roadmap 的对应 Phase 完成 tracer bullet、Acceptance 与 Human Gate 后，本骨架才允许追加候选条目。本次 Stateful Grill 的选择解决设计方向问题，但不替代 Phase 的执行证据、正式上位文档裁决或独立实施授权。

## 2. 真相源与优先级

归档条目必须引用唯一拥有语义的来源，不得在本文件重新解释：

1. `docs/architecture/product-architecture-baseline-l0-l3.md`：L0–L3 Owner / Authority / Lifecycle / invariants；
2. `docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`：P1–P23 typed protocol edges；
3. `docs/architecture/uworld-protocol-baseline-l4.md`：Container、Occurrence、LogicalItem、Slice 等 UIWorld L4 语义；
4. `docs/architecture/perception-provider-baseline-v0.1.md`：当前 live provider component boundary；
5. `docs/adr/`：已接受且未 supersede 的决策；
6. `src/` 与 `tests/`：当前 realization 与 executable evidence，不能单独覆盖上层语义。

冲突处理：上位冻结语义优先；代码差异记为 Gap；未裁决 draft 只能形成待验证问题。

## 3. 晋升状态

| 状态 | 含义 | 允许进入产品实现 |
|---|---|---|
| `FROZEN_REFERENCE` | 已有冻结真相源确认；本文件只做索引 | 仅按原真相源，不由本文件授权 |
| `EVIDENCE_BACKED_CANDIDATE` | tracer 已证明需要，完整模板已填，但 Human 未接受 | 否 |
| `HUMAN_ACCEPTED` | Human 接受语义与 seam，是否需 ADR 已判断 | 需独立实施 Change 授权 |
| `IMPLEMENTED_NOT_VERIFIED` | 已实现但 Acceptance 未完整证明 | 否 |
| `VERIFIED` | 相关 Phase 的 verification 四元组完备 | 可作为后续依赖，但不扩大原 scope |
| `SUPERSEDED` | 被新版本取代，保留历史引用 | 否 |

状态不能由代码存在、测试 green 或 draft 命名自动跃迁。

## 4. 核心模型晋升规则

候选模型必须同时满足：

1. **领域问题真实**：至少一个可执行场景证明现有模型无法无损表达问题；
2. **买方真实**：至少两个场景或一个独立长期 buyer 使用其语义，而非只为序列化/测试方便；
3. **Owner 唯一**：谁创建、更新、失效和终止明确；若非权威，必须写明它引用哪些 Owner records；
4. **Authority 不重叠**：不得复制 WorldBelief、Run State、Evidence、Binding、Assurance、Effect、Outcome 或 Trace authority；
5. **Lifecycle 闭合**：创建、更新/append、currentness/freshness、失效、terminal/supersede 有明确语义；
6. **Identity 与 correlation 分离**：domain identity、revision-local ref、technical id、test id 不混用；
7. **不变量可否证**：至少一个正向场景与一个 fail-closed 负向场景；
8. **相邻模型可区分**：能说明为什么不并入现有模型，以及禁止承载什么；
9. **Human 接受**：未经 Human 接受，不得写入 frozen glossary / protocol / ADR 或 public code。

本轮 Human-selected、但**尚未晋升**的新语义包括 World Presentation、EffectExpectation、EffectAttempt、Agent Decision Context、Bounded Contingent Decision Package、Decision Feedback View、Agent Continuation、Capability Offer/Grant、Contract View generation、Minimal Scenario Bundle、sealed Trace / ScenarioStimulus，以及 ScriptedUniAgent test double 所需的外部 seam contract。它们先留在 companion roadmap 的场景与 Human Gate 中，不在本索引伪装为已冻结模型；若未来证明可由现有 owner record 或具体实现无损表达，应删除候选，而不是强行新增模型。

### 4.1 未晋升候选的压力记录

下表只保留 tracer 要回答的问题，状态统一是 `PROPOSED / Authority NONE`，不是归档条目：

| 候选能力 | 最近的冻结 Owner | 必须避免的 shadow authority | 最小 proving pressure |
|---|---|---|---|
| Agent Decision Context / Feedback View | 各 owner records；Kernel 仅派生投影 | canonical store dump、第二 Run/Control/Assurance state、Trace dependency | 首次 bounded full context；Anchor+Delta；broken-ref full refresh；反馈足够重规划 |
| Bounded Contingent Decision Package | UniAgent author proposal；Control owns Tactical Hypothesis/Intent | 第二 Runtime FSM、Effect batch、Driver macro、跨 revision binding | typed tri-state guards、semantic lease、one active、Unknown/invalidation/manual preemption fail closed |
| EffectExpectation / EffectAttempt | Control/Assurance/Effect Boundary 现有链 | proposal 自行授权、receipt 变 proof、旧 attempt 原地重试 | one effect→post-action Evidence→reconciliation→verification；retry 新 identity |
| Agent Continuation | UniAgent cognition + canonical refs | 复制 Run/Assurance/Effect state，以 Trace 或 Host transcript 恢复 | restart 后 fresh observe/reconcile，旧未决动作不自动续发 |
| Capability Offer / Grant / Contract generation | Human/Policy Grant；UniAgent Contract authoring；Run Model admission | Grant 直接授权动作、原地改 Contract View、旧 Grant 追溯生效 | 高影响 commit point、broader permission reject、Grant→Proposal→new View generation |
| Minimal Bundle / ScenarioStimulus | Harness asset/assertion/promotion roles | Trace Event command、Scenario 写内部 owner state、治理平台阻塞产品 | sealed Trace import、exact Runtime/UI metadata、integrity、two-run digest |
| ScriptedUniAgent test double | 外部 UniAgent seam | 第三 realization、Agent intelligence proof、test-only seam 进入 Product contract | call boundary/order/count/correlation/cancel/late/duplicate/no-response |

## 5. Interface 晋升规则

Interface 不是类型签名，而是 caller 必须知道的全部事实。候选 Interface 必须满足：

1. **先有 seam 问题**：说明行为变化需要在哪里被替换，而不是先命名 `Ixxx`；
2. **先有 buyer**：明确 caller、消费时机、为什么现有 seam 不足；
3. **至少两个真实 adapter**：production + deterministic replay 可以成立，但两个仅换数据的 mock 不算；只有一个 adapter 时保持具体实现或 `candidate only`；
4. **完整 contract**：input/output、invariants、ordering、idempotency、timeout、cancellation、partial failure、retry、backpressure/performance、version compatibility；
5. **Deep module**：Interface 小，隐藏的复杂度大；删除后复杂度会扩散到多个 caller；纯 pass-through 不晋升；
6. **Authority neutral**：adapter 不能因满足 Interface 获得 caller/Owner 的判断权；
7. **Design it twice**：至少比较两个明显不同的 seam/contract 方案，按 leverage、locality、failure containment 与测试面评价；
8. **Scenario evidence**：对应 Phase tracer 与失败注入均通过；
9. **Human 接受后再冻结**：名称、签名、字段、namespace、transport、storage 都在最后决定。

## 6. 核心模型条目模板

未来每个模型使用以下模板；缺一项不得晋升：

```text
Model:
Status:
Source / owning decision:
Problem solved:
Owner:
Authority: <exact authority | NONE / non-authoritative>
Buyers / proving scenarios:
Creation:
Update / append semantics:
Invalidation / supersession:
Termination:
Identity:
Correlation:
Invariants:
Adjacent-model distinction:
Must not contain:
Failure semantics:
Positive evidence:
Negative evidence:
Human decision:
Verification tuple: method | expected | actual | evidence ref
```

## 7. Interface 条目模板

未来每个 Interface 使用以下模板；当前没有新条目：

```text
Interface:
Status:
Owning decision / Phase:
Seam location:
Caller / buyer:
Adapter 1 + evidence:
Adapter 2 + evidence:
Complete caller contract:
  inputs:
  outputs:
  invariants:
  ordering:
  idempotency:
  timeout:
  cancellation:
  partial failure:
  retry:
  performance / backpressure:
Hidden complexity:
Deletion test:
Why deep, not pass-through:
Authority constraints:
Alternative A:
Alternative B:
Trade-off comparison:
Rejected leakage:
Human decision:
Verification tuple: method | expected | actual | evidence ref
```

## 8. 已冻结核心模型索引

以下条目均为 `FROZEN_REFERENCE`。本表不取代来源文档，不新增字段，也不表示 companion roadmap 的新能力已获授权。

### 8.1 Goal、Contract 与 Run

| 模型 | 解决的问题 | Owner / Authority | 生命周期与 identity | 相邻区别 / 禁止承载 | 来源 |
|---|---|---|---|---|---|
| Primary Goal | 表达用户希望现实世界达到的结果 | UniAgent / Goal semantics | Session 内创建；显式澄清才 revision；完成/放弃/不可继续终止 | 不是动作列表、页面路径、Run State 或 proof | Product baseline §3.2 |
| Execution Contract | 给 Kernel 稳定、受限的执行语义 | UniAgent authoring；Run Model 拥有 accepted View | Run 前创建；accepted version 固定；实质变化需显式新 version；同 Run 新 View generation 是待正式裁决候选 | 不规定路径；下层不得扩 scope 或改 Goal | Product baseline §3.3；Protocol P1；roadmap H15 |
| Primary Run | 一个 accepted Contract 的完整现实执行生命周期 | Run Model owns canonical Run State；Kernel owns execution boundary | legal activation 后开始；多 cycle；单 terminal；RunId 为 correlation root | 不是 Host turn/thread；terminal 后零新 effect | Product baseline §3.4；ADR-0019 |
| Run State | Contract View、Objective、Proof Obligations、Progress、Outcome 的 canonical aggregate | Run Model / sole recording authority | 只经 typed legal transition；terminal 不可恢复 | 不含 WorldBelief、Control Intent、action-local Assurance 或 Trace | Product baseline §13；current `RunModel` realization |

### 8.2 Perception 与 Evidence

| 模型 | 解决的问题 | Owner / Authority | 生命周期与 identity | 相邻区别 / 禁止承载 | 来源 |
|---|---|---|---|---|---|
| Raw Artifact | 保存 provider / environment 产生的原始或明确派生字节 | Provider owns production；无 Evidence authority | 内容寻址；capture 与 derived artifact lineage 明确 | 不是 Observation、Evidence、Belief 或 proof | Product baseline §11；provider baseline；ADR-0020 |
| ObservationProposal | 把 producer claim 放到 P2 admission 门前 | Observation producer；Authority NONE | 每次观察产生；provenance/capture/scope/lineage 关联；可被拒 | 不拥有 EvidenceId，不直接改 WorldBelief | Protocol P2；PER-004；current code |
| Evidence Record | 保存 canonical、immutable、可引用的依据 | Evidence Ledger / sole admission authority | admission 后创建；append-oriented；可 supersede/invalidate 但历史不改 | admission ≠ truth/relevance/reconciliation/revision/proof sufficiency | Product baseline §3.6/§11；Protocol P3 |
| Admission Record | 记录 proposal 是否满足 canonical record eligibility | Evidence Ledger | 每次 admission attempt append；accepted 关联 EvidenceId | 不判断 reality、belief weight 或 effect | Product baseline §11.2；current code |

### 8.3 WorldBelief、Container 与消费投影

| 模型 | 解决的问题 | Owner / Authority | 生命周期与 identity | 相邻区别 / 禁止承载 | 来源 |
|---|---|---|---|---|---|
| WorldBelief Revision | 一版基于 accepted Evidence 的当前世界判断 | World Model / sole Belief and Reconciliation Authority | reconcile 产生 immutable revision；历史只读；只有 current 生效 | Belief ≠ Reality；不含 intent/plan/run progress | Product baseline §3.7/§12 |
| ContainerIdentity / Container belief | 表达有独立交互/状态/lifecycle 的世界对象及其 belief | World Model | association 以 evidence 建立/延续；不由视觉相似单独铸造 | 纯几何 region 不是 Container；不等于 page/screen | UIWorld L4 §4–§19 |
| ObservationOccurrence | 表达某 revision 观察到的 UI presentation | World Model，revision-bound belief value | 随 source revision；跨 revision 不再是 live target | provider node/bbox/OCR/DOM id 不是 identity | UIWorld L4 §35 |
| LogicalItem | 表达 Container 内 actionable logical referent 的有界连续性 | World Model | demand-gated + evidence-established；Ended 需正面 lifecycle evidence | demand/provider key/occurrence 不等于 identity；不直接承载 effect | UIWorld L4 §36–§40；ADR-0014/0015 |
| Slice | 为明确 buyer 派生一个 revision 的 scoped immutable projection | World Model；projection Authority NONE | 按需即时派生；source revision 变更后 stale；历史内容仍 immutable | admission/new revision 不自动创建 Slice；omission ≠ absence；不是 Observation、Container、Trace 或 universal DTO | Product baseline §12；Protocol P4；UIWorld L4 §20–§28/§41 |
| GroundingView | 为 grounding buyer 派生 current candidate facts | World Model；projection Authority NONE | current revision 即时派生；消费后丢弃 | 不含 CanonicalBinding、ActionAdmissible、ShouldRetry；与 Slice 同源不同 buyer | UIWorld L4 §41；ADR-0011 |

### 8.4 Control、Binding、Assurance 与 Effect

| 模型 | 解决的问题 | Owner / Authority | 生命周期与 identity | 相邻区别 / 禁止承载 | 来源 |
|---|---|---|---|---|---|
| Control Intent | 表达 Control Loop 选择的 observe/act/recovery 意图 | Control Loop / sole Control Intent Authority | 每 cycle 签发；携带 basis revision correlation | 不是 action authorization、binding、effect 或 belief | Product baseline §14；Protocol P8 |
| Tactical Hypothesis | 表达 Control 对当前局部闭环的可废弃判断 | Control Loop | 随 current WorldBelief / policy / budget 更新或失效 | 不由 UniAgent 拥有；不等于 Goal-level Plan Hypothesis、belief 或 authorization | Product baseline §14 |
| Candidate Binding | grounding provider 提出的 target candidate | Grounding capability；Authority NONE | 对 source revision 有效；可 stale/ambiguous | 不是 canonical binding，不得 dispatch | Protocol P9；ADR-0009 |
| Canonical Binding | 把 exact intent 绑定到 current bounded target | Effect Boundary / sole Canonical Binding Authority | 针对 intent + revision 建立；revision/currentness 变化后失效 | existence ≠ authorization；LogicalItem 不直接承载 effect | Protocol P10；ADR-0009；UIWorld L4 §41 |
| Assurance Judgment | 判断 exact intent + canonical binding 在当前条件下是否可行动 | Assurance / Runtime Assurance Judgment Authority | consumption-scoped；绑定 intent/binding/revision | 不签发 intent、不做 binding、不 dispatch | Product baseline §15；Protocol P13；ADR-0009/0010 |
| Effect Receipt | 记录一次机械 delivery attempt 的 immutable 结果 | Effect Boundary owns receipt / delivery record | dispatch 后创建；历史不可改 | attempt evidence ≠ Effect proof ≠ completion | Product baseline §16；Protocol P15 |

### 8.5 Terminal、Goal Evaluation 与 Trace

| 模型 | 解决的问题 | Owner / Authority | 生命周期与 identity | 相邻区别 / 禁止承载 | 来源 |
|---|---|---|---|---|---|
| Outcome Proof | 判断 Completion/Failure/Safe-Stop/Escalation 是否有足够 Evidence | Assurance / sole Outcome Proof Authority | terminal proposal 前形成；Run Model 只记录其结果 | receipt、provider report 或 Trace 不能替代 | Product baseline §15.2；Protocol P16 |
| Runtime Outcome | 发出 Primary Run 的 immutable terminal envelope | Uni Kernel / sole emission boundary | terminal Outcome State 后 exactly once；不可回写 | 不等于 Goal satisfaction；UniAgent 只消费 | Product baseline §3.8；Protocol P18 |
| Goal Evaluation | 评价 Runtime Outcome 是否满足 Primary Goal | UniAgent / sole Goal Evaluation Authority | 收到 terminal Outcome 后形成；不反写 Runtime | 不成为 Outcome Proof 或新 Runtime judgment | Product baseline §3.9；Protocol P19 |
| RunTraceArtifact | 给 one Run 提供异步结构化因果投影 | caller-owned trace scope；Authority NONE | RunId 为 root；异步 drain/seal/integrity 是候选能力；sealed 后 immutable，可 Finalized/Quarantined | Product 中不复制事实/参与决策或恢复；Simulation 也不把 Trace Event 当 command，只允许 Importer 派生 ScenarioStimulus | ADR-0013；TRC-001；roadmap §7.4 |

## 9. 当前 Interface 归档状态

```text
New core Interface entries: NONE
Reason: corresponding roadmap Phases have not completed tracer bullets and Human Gates.
```

现有代码中的 `IFastPerceptionStrategy`、`IEffectDriver`、`IRunTrace`、association/continuity strategies 继续由各自已完成 Change、组件基线与测试负责。本骨架不重新归档或扩展它们，也不以它们的存在推导“统一异步 Perception”“Simulation runner”“Replay source”“World presentation composer”或“Runtime supervisor”之类新 Interface。

Simulation Host 与 Product Host 是两个独立 composition roots，共用同一 Product Runtime modules；这可构成某些外部依赖 seam 的真实 buyer 证据，但不自动证明需要新的语言级 Interface。尤其不能为了 ScenarioStimulus 消费而把 test-only Oracle、资产加载、Trace Importer 或 replay 入口加入 Product Host / Product Runtime。

Codex-backed 与 DSH-backed 是两个完整 UniAgent realization，可作为 host-neutral 外部 seam 的真实 buyer 证据；`ScriptedUniAgent` 只是走同一 seam 的 deterministic test double，不是第三个 realization。它可以证明 ordering/idempotency/cancellation 等调用 contract，却不能单独证明 seam 足够 deep，更不能证明 Agent 智力。

P1–P23 是跨组件协议语义，不等于必须逐边建立一个语言级 Interface。未来某一 Phase 若出现独立 buyer 与两个 adapter，应重新执行 seam placement 与 deletion test，再决定是否需要 Interface。

## 10. 候选进入本归档的检查单

```text
[ ] 对应 roadmap Phase 和 tracer bullet 已完成
[ ] buyer 不是架构作者或测试便利，而是实际 caller
[ ] Owner / Authority / non-authoritative 已明确
[ ] lifecycle 与 failure semantics 已跑过负向场景
[ ] identity / correlation / revision / capture 关系已明确
[ ] 至少两个 proving scenarios
[ ] Interface 若存在，有至少两个真实 adapters
[ ] ordering / idempotency / timeout / cancellation / partial failure / retry 已定义
[ ] 两种设计方案已比较
[ ] deletion test 证明复杂度会扩散而非消失
[ ] 无第二 truth / state machine / effect / trace authority
[ ] Agent/Control 分工未漂移：Agent owns Goal-level strategy，Control owns Tactical Hypothesis/Intent
[ ] Trace 异步且非恢复输入；Simulation 只消费 ScenarioStimulus
[ ] latency / backpressure 与调用结构有基线证据
[ ] Human 已接受语义
[ ] 独立 Change 已授权实现
```

## 11. 本骨架自检

- [x] 只索引冻结模型，没有新增产品模型。
- [x] 没有填写任何未经 tracer 证明的新 Interface。
- [x] 没有把 P1–P23 机械翻译成 `Ixxx`。
- [x] 没有给 Simulation、Replay、Asset、Oracle 或 Trace 新 Authority。
- [x] 已把 Grill 决策与冻结模型索引分离；Trace Event 在隔离 Host 中也不是命令，Simulation 只消费派生的 ScenarioStimulus。
- [x] 已明确 UniAgent/Control 分工、Agent Continuation 非 shadow state、Grant/Contract admission 分离与 ScriptedUniAgent 的 test-double 地位。
- [x] 没有修改冻结文档、CONTEXT、ADR、代码或测试。
- [x] 明确了模型/Interface 晋升、Human Gate 与独立实施授权的顺序。
