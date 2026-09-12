# UniClaw Legacy → Target Architecture Mapping

> DocumentType: `LEGACY_TO_TARGET_ARCHITECTURE_MAPPING`
>
> Status: `CANDIDATE / NOT_ADOPTED`
>
> Authority: `NONE`
>
> Date: `2026-09-05`
>
> Scope: 现行及历史 UniClaw 概念到 Target Product Architecture L0–L3 的语义映射
>
> Target: [Target Product Architecture — L0–L3](../architecture/product-architecture-baseline-l0-l3.md)
>
> Forbidden Boundary: 本文不定义 Target Architecture，不修改现行权威、active change、实现、测试、Owner 或生命周期，也不授权迁移。

---

## 0. 文档目的

本文只回答：现行或历史概念怎样映射到 Target Product Architecture。

Target Architecture 必须能够脱离旧结构独立成立；本文只能建立 translation、compatibility 与 conflict visibility，不能把旧名称重新带回 Target canonical model。

## 1. 现行权威关系

本文是 `Authority: NONE` 的 mapping。现行语义由以下来源控制：

1. [Architecture Index](../architecture/README.md)；
2. [UniAgent Architecture v1](../architecture/uniagent-architecture-v1-core-development-guide.md)；
3. [UniAgent Protocol v1](../architecture/uniagent-protocol-v1-consolidation-design.md)；
4. [Agent Concept Model v1](../architecture/agent-concept-model-v1.md)；
5. [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md)；
6. 已批准的 change-local specifications 与 Decisions。

若本 mapping 对现行语义的描述与上述来源冲突，以原权威来源为准。Target 候选也不能通过本 mapping 静默改变现行 Owner、Authority 或 lifecycle。

## 2. Naming boundary

### 2.1 Target L1 canonical terms

```text
L1
├── UniAgent
├── Uni Kernel
├── Capability Plane
└── Memory System
```

### 2.2 Legacy/current-only terms

以下名称只用于描述来源系统或建立映射，不是 Target canonical terms：

- `RuntimeAgent`；
- `Autonomy Kernel`；
- `Capability Boundary`；
- `Durable Memory Capability`。

### 2.3 核心校正

`RuntimeAgent → Uni Kernel` 不是简单类改名。

现行 RuntimeAgent 聚合表达的 state 与 authority，在 Target 中必须按类别落到六个 L2 Owner：

```text
RuntimeAgent aggregate responsibilities
  ├── evidence semantics/admission ──► Evidence Ledger
  ├── current belief/reconciliation ─► World Model
  ├── canonical run state ───────────► Run Model
  ├── control intent ────────────────► Control Loop
  ├── runtime assurance judgment ────► Assurance
  └── binding/effect delivery ───────► Effect Boundary

Uni Kernel
  = target aggregate execution boundary
  != new catch-all mutable owner
```

## 3. L0 概念映射

| Current / legacy concept | Target concept | Relation | Compatibility boundary |
|---|---|---|---|
| supervisory `UniAgent` | `UniAgent` | semantic continuity | 继续拥有 Primary Goal、global strategy、Execution Contract 与 Goal Evaluation；不取得执行侧 current-state authority |
| `Primary Goal` | `Primary Goal` | semantic continuity | 与 execution goal、plan、Run State 和 GoalEvidence 分离 |
| `Directive` | `Execution Contract` | bounded protocol realization | Directive 是一次有界请求；Target contract 补齐 objective、scope、constraints、proof criteria 与 permissions 的上位语义 |
| `StrategyDirective` | `Execution Contract` | typed start-time realization | 保留 typed、bounded、start-time admission；不得直接携带 action route、FSM command 或 completion fact |
| current `Run` | `Primary Run` | lifecycle continuity | 仍是一个 contract 的一次现实执行生命周期；control cycles 不等于多个 Run |
| `RuntimeAgent` / `Autonomy Kernel` | `Uni Kernel` + six L2 owners | aggregate decomposition | 保留执行闭环，但不把所有 state/judgment 继续归给 aggregate 名称 |
| `Observation / Fact / EvidenceRef / artifact` | `Evidence` + `Evidence Ledger` records | typed normalization | 保留 producer provenance 与类型专有语义；canonical EvidenceId/admission 归 Evidence Ledger |
| `WorldBelief` | `Current WorldBelief` | semantic continuity | Target 唯一 Owner = World Model；Fact history 不与 current projection 合并 |
| `GoalEvidence` | Evidence Record + Proof Obligation State + Completion Proof | responsibility decomposition | 证据记录归 Evidence Ledger；proof obligation/outcome 归 Run Model；completion judgment 归 Assurance |
| `RunCompleted / RunFailed / RunState` | Outcome State + Runtime Outcome | state/emission split | Run Model 记录 canonical Outcome State；Uni Kernel 发出 immutable Runtime Outcome |
| `Goal Evaluation` | `Goal Evaluation` | semantic continuity | UniAgent 评价 Primary Goal satisfaction，不回写 Run State 或 Completion Proof |

## 4. L1 映射

| Current / legacy boundary | Target L1 | Mapping statement |
|---|---|---|
| supervisory UniAgent | UniAgent | 保留监督自主、Goal ownership 和最终评价职责 |
| RuntimeAgent execution boundary | Uni Kernel | 保留 aggregate execution boundary；内部 ownership 改用 L2 canonical 分工解释 |
| Brain / Vision / OCR / VLM / semantic provider / driver adapter | Capability Plane | 统一为 typed capability outputs；不取得 current state、control intent 或 completion authority |
| runtime-local or cross-run memory proposals | Memory System | 统一为 durable records 与 advisory recall；不作为 current truth |
| Composition Host / AgentHost / DriverHost | hosting and integration concerns | 不成为 Target L1 canonical component；只保留 composition、identity coordination、routing 或 hosting 职责，不获得 Product semantic authority |

## 5. L2–L3 映射

### 5.1 Evidence

| Current / legacy concept | Target owner | Target semantic |
|---|---|---|
| raw screenshot、XML、OCR payload、detector output | Provider in Capability Plane | raw artifact production |
| `Observation` | Evidence Ledger | canonical Observation Record after admission |
| `Fact` | Evidence Ledger | append-oriented claim record；不等于 Current WorldBelief |
| `EvidenceRef` | Evidence Ledger | canonical EvidenceId / artifact reference |
| provider confidence | Evidence Record field | 输入质量声明；不是 truth 或 authority |
| GoalEvidence record | Evidence Ledger | claim-specific completion evidence record；judgment 不在 Ledger |
| producer-local caches | no canonical target owner | 只能作为 replaceable internal cache；不得形成独立 evidence authority |

### 5.2 World and Container

| Current / legacy concept | Target owner | Target semantic |
|---|---|---|
| `WorldBelief` | World Model | Current WorldBelief |
| page/container-local world state | World Model | scoped Container Graph projection or World State projection |
| `Container` frame / subtree / interaction context | World Model | Container Graph projection；不等于 target module requirement |
| `ActiveContainerContext` observed location | World Model | accepted Evidence 驱动的 current context belief |
| execution obligations associated with a container | Run Model | 不得混入 observed world projection |
| `ContainerTransition` observation | Evidence Ledger → World Model | transition Evidence 先 canonicalize，再 Reconciliation |
| `LocalModel` / `NodeLocalModel` | World Model internal scoped model | container-local canonical projection；不得获得 plan、action、completion 或 cross-run identity authority |
| current-screen subset / page subset | World Model `Slice` only if derived from WorldBelief revision | raw observation region 不得映射为 Slice |
| raw crop / visual area | Observation Scope / Observation Region / Perception Region | perception input terminology，不是 WorldBelief projection |

### 5.3 Run and completion

| Current / legacy concept | Target owner | Target semantic |
|---|---|---|
| `RunState` | Run Model | canonical Run State |
| admitted Directive / StrategyDirective | Run Model | immutable Execution Contract View |
| execution goal state | Run Model | Objective State |
| completion requirements | Run Model | Proof Obligation State |
| admitted / pending / completed obligations | Run Model | Progress State |
| terminal state and reason | Run Model | Outcome State |
| RuntimeAgent completion decision | Assurance | Completion Proof judgment |
| RuntimeAgent terminal outcome production | Run Model + Uni Kernel | Run Model records terminal Outcome State；Uni Kernel emits Runtime Outcome |
| UniAgent satisfaction decision | UniAgent | Goal Evaluation |

### 5.4 Control, Traversal and FSM

| Current / legacy concept | Target owner | Target semantic |
|---|---|---|
| Runtime-local plan | Control Loop | Tactical Hypothesis |
| Agent decision loop | Control Loop | sole Control Intent Authority |
| observation scheduling / capture requests | Control Loop | Observation Control |
| `Traversal` / traversal strategy | Control Loop | Traversal Control |
| trap handling / local recovery | Control Loop | Recovery / Reorientation intent |
| action admissibility formerly expressed inside Agent | Assurance | Action Admissibility judgment |
| effect and completion checks | Assurance | Effect Verification / Completion Proof |
| `FSM` | protocol transition constraint across owning models | FSM 不成为 L1/L2 owner；只约束各 canonical state 的合法 transition，不产生 strategy |

### 5.5 Grounding and effects

| Current / legacy concept | Target owner | Target semantic |
|---|---|---|
| grounder result | Capability Plane Provider | Candidate Binding |
| RuntimeAgent grounding responsibility | Effect Boundary | canonical bounded target binding |
| RuntimeAgent action authorization | Assurance | admissibility / freshness / authorization judgment |
| pre-dispatch action gate | Effect Boundary | Effect Gate enforcement |
| Driver / Environment adapter | Effect Boundary | mechanical Dispatch |
| driver result / dispatch result | Effect Boundary | Effect Receipt；attempt evidence only |
| post-action verification | Assurance | Effect Verification from accepted Evidence |

## 6. 现行 Architecture v1 兼容说明

### 6.1 UniAgent / execution split

现行 Architecture v1 把 UniAgent 定义为 supervisory autonomy，把 RuntimeAgent 定义为 bounded execution autonomy。Target 保留这一语义分离，但将执行侧 canonical ownership 分解到 L2。

### 6.2 Run ownership language

现行文档中的“RuntimeAgent owns Run-local state / Run lifecycle / terminal outcome”映射为：

```text
Run Model
  owns canonical Run State and Outcome State

Control Loop
  owns Control Intent

Assurance
  owns Runtime Assurance Judgment

Effect Boundary
  owns binding and delivery

Uni Kernel
  emits the terminal Runtime Outcome
```

这是一项 Target responsibility decomposition，不代表现行实现已经完成对应拆分。

### 6.3 GoalEvidence language

现行 `GoalEvidence = KERNEL_ONLY` 的安全意图必须保留：外部 capability、UniAgent、operator note 或 Memory 都不能宣布 completion。

Target 将该 aggregate 表述细分为：

```text
Evidence Ledger
  owns canonical evidence records

Run Model
  owns Proof Obligation State

Assurance
  judges Completion Proof

Run Model
  records Outcome State

Uni Kernel
  emits Runtime Outcome
```

### 6.4 Protocol surfaces

现行 `Directive / StrategyDirective` 是 Target Execution Contract 的协议映射，不是完整 Target object model。现行只读 Snapshot、Event、Trace 和 Evidence surfaces 仍是 producer-derived projections，不能变成 canonical state 或 command surface。

## 7. Runtime Architecture Contract I-1..I-14 映射

| Current invariant | Target mapping | Compatibility result |
|---|---|---|
| I-1 Agent → Container → Traversal → Environment | Control Loop 产生 intent；World Model 提供 Container Graph projection；Traversal Control 推进；Effect Boundary 接触 Environment | `PRESERVED / RESPONSIBILITY_REFINED` |
| I-2 one mutable state owner | Evidence Ledger、World Model、Run Model、Control Loop、Assurance、Effect Boundary 各有独占 state/judgment 类别 | `PRESERVED / MADE_EXPLICIT` |
| I-3 one decision authority | Control Intent 与 Assurance Judgment 分成互不重叠的 Authority classes | `PRESERVED / CLASSIFIED` |
| I-4 Observation is evidence | Provider raw artifact → Evidence Ledger admission → World Model interpretation | `PRESERVED` |
| I-5 Plan is hypothesis | Tactical Hypothesis 属于 Control Loop，可废弃且不污染 Belief | `PRESERVED` |
| I-6 Fingerprint is evidence, not identity | fingerprint 只能进入 Evidence Record，不取得 binding 或 cross-time identity authority | `PRESERVED` |
| I-7 FSM handles protocol transition | FSM 只约束 owning model 的合法 transition，不成为 Control Intent Authority | `PRESERVED` |
| I-8 lower scope escalates | Control Loop / Assurance 在 authority 不足时显式升级 | `PRESERVED` |
| I-9 recovery loop | Recovery / Reorientation 必须 act → observe → verify → reconcile | `PRESERVED` |
| I-10 completion from Goal Evidence | Evidence Ledger records + Run Model obligations + Assurance Completion Proof | `PRESERVED / RESPONSIBILITY_REFINED` |
| I-11 no legacy control structure | Target 只保留语义，不把旧 classes/control flow 设为架构模板 | `PRESERVED` |
| I-12 no complexity without requirement | Capability Plane、Memory System 和 L2 只锁定职责，不预购实现拓扑 | `PRESERVED` |
| I-13 no God Context | Evidence、Belief、Run State、Control State、Memory 分属不同 Owner | `PRESERVED / STRENGTHENED` |
| I-14 AI is capability, not truth | AI/ML 输出留在 Capability Plane，必须经过 Evidence admission 和 Reconciliation | `PRESERVED` |

## 8. Active change compatibility boundary

以下 active work 是现行 change-local contract 的来源，不因 Target 候选或本文 mapping 被完成、替换或撤销：

- `container-runtime-v2-core-semantics`；
- `container-runtime-v2-evidence-model`；
- `runtime-active-container-context-and-transition-semantics`；
- `runtime-agent-pre-terminal-cycle-contract`；
- `runtime-external-semantic-capability-boundary`；
- `runtime-iterative-full-traversal-acceptance`；
- `uniagent-local-exploration-memory`；
- semantic perception contract / layer work。

这些 work 的 approved requirements 必须分别映射到 Target Owner。名称相似、测试通过或 target document 存在，都不能证明 change 已迁移或毕业。

## 9. 映射冲突清单

| Conflict | Source-side risk | Target resolution |
|---|---|---|
| aggregate execution owner 同时表述所有状态与判断 | owner/authority 边界模糊 | 分解到六个 L2；Uni Kernel 只保留 aggregate boundary 和 outcome emission |
| producer owns its evidence | 多 Provider 形成多 evidence authority | Provider 只拥有 raw production；Evidence Ledger owns canonical Evidence |
| Evidence Ledger 被理解为 truth store | Evidence、Belief、Reality 合并 | Ledger 只拥有 record/admission；World Model owns Belief |
| Kernel 与 Run Model 同时 owns RunState | dual mutable truth | Run Model sole owns canonical Run State |
| Control Loop 和 Assurance 都“做决策” | judgment authority 重叠 | Control Intent Authority 与 Runtime Assurance Judgment Authority 分型 |
| grounding provider result 直接可执行 | candidate binding 越过 current belief/freshness | Effect Boundary forms canonical binding；Assurance judges admissibility |
| driver success 作为效果 | attempt/receipt 冒充 reality | Receipt 进入 Evidence；Assurance 基于 post-action accepted Evidence 验证 Effect |
| GoalEvidence、completion、Outcome 混用 | proof、judgment、record、emission 多方抢权 | Ledger records；Assurance judges；Run Model records state；Uni Kernel emits；UniAgent evaluates |
| local visual crop 叫 Slice | raw input 与 belief projection 混淆 | raw uses Observation/Perception Region；Slice only means immutable WorldBelief projection |

## 10. Mapping acceptance checks

本 mapping 只有在以下问题均得到肯定答案时才可用于后续工作：

1. 每个旧概念是否明确映射到一个 Target Owner 或被明确声明不进入 Target？
2. 是否没有把 aggregate 旧 Owner 直接复制成 Target catch-all Owner？
3. 是否区分了 protocol representation、canonical state、judgment 和 immutable emission？
4. 是否保存了 Evidence、Belief、Plan、Effect、Completion 的分离？
5. 是否没有用 Target 候选反向宣称现行 active work 已完成？
6. 是否所有旧名称都只作为 source-side vocabulary 使用？
