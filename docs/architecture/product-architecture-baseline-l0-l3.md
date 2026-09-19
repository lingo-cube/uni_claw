# UniClaw Target Product Architecture Baseline — L0–L3

> DocumentType: `TARGET_PRODUCT_ARCHITECTURE_BASELINE_V0_1`
>
> Status: `CANDIDATE_FOR_ADOPTION / L0-L3_CLOSED`
>
> Authority: `NONE`
>
> Version: `v0.1`
>
> Date: `2026-09-05`
>
> Scope: `UniClaw Product Architecture / L0–L3`
>
> Cardinality: `1 Session / 1 Primary Goal / 1 Primary Run`
>
> Forbidden Boundary: 本文不讨论 Development Harness，不授权实现，不锁定类、模块、存储、Provider、模型、算法或部署结构。

---

## 0. 文档目的

本文只定义 UniClaw 未来 Target Product Architecture：系统是什么、各层承担什么职责、谁拥有 canonical state、谁拥有哪一类判断权威，以及哪些边界在实现替换后仍必须成立。

本文不解释任何既有系统，也不规定迁移过程。

# Part I — Product 定位与 L0

## 1. Product 定位

UniClaw 是运行在真实 GUI / Device Environment 上的智能执行 Product。

它接收用户希望现实世界达到的目标，在不完全可观测、持续变化且不能完全信任的环境中：

- 理解并保持 Primary Goal；
- 建立有边界的 Execution Contract；
- 维护一个 Primary Run 的现实执行闭环；
- 把环境产出转化为 canonical Evidence；
- 基于 accepted Evidence 形成和修正 WorldBelief；
- 分离 control intent、assurance judgment 与 external effect delivery；
- 独立验证动作 Effect 与 terminal Outcome；
- 产出可追溯的 Runtime Outcome；
- 最终评价 Primary Goal satisfaction。

Product 的核心不是特定模型、Provider 或算法，而是稳定的目标、证据、世界判断、执行控制、效果验证和完成证明体系。

## 2. 当前范围

```text
1 Product Session
  └── 1 Primary Goal
        └── 1 Primary Run
```

Session 是 Product correlation root。它关联 Goal、Contract、Run、Evidence、Outcome 与 Goal Evaluation，但不是共享可变状态、WorldBelief、Run State、事件总线或控制器。

一个 Primary Run 可以包含多个 observe / decide / act / verify cycle。cycle 数量不改变 Run cardinality。

以下语义保留，不属于当前范围：多 Primary Goal、多 Primary Run、并行 Run、terminal 后 continuation、跨 Session world identity 和多执行内核协作。

## 3. L0 一等概念

### 3.1 UniAgent

**Definition**

面向用户的完整智能主体。负责理解 Primary Goal、形成全局策略、创建 Execution Contract，并评价最终 Goal satisfaction。

**Owner / Authority**

- Primary Goal；
- Goal interpretation；
- global strategy；
- Execution Contract authoring；
- Goal Evaluation。

**Lifecycle**

- 随 Product Session 建立；
- 在 Session 内保持监督；
- 在 Goal 完成、放弃、不可继续或 Session 关闭时结束。

**Boundary**

- 不拥有 Current WorldBelief；
- 不产生现实动作授权；
- 不写入 Run State、Effect judgment 或 Outcome Proof；
- 不以模型输出或执行回执代替 Goal satisfaction。

### 3.2 Primary Goal

**Definition**

用户希望现实世界达到的结果。Goal 不是动作列表、页面路径、计划、Run State 或证明材料。

**Owner / Authority**

UniAgent 唯一拥有 Goal 语义、优先级、约束及最终满意度解释。

**Lifecycle**

- Session 内创建一次；
- 允许通过显式澄清形成新 revision；
- 完成、放弃或不可继续时终止；
- revision 不得静默改变已接受 Execution Contract 的含义。

### 3.3 Execution Contract

**Definition**

UniAgent 交给 Uni Kernel 的稳定执行语义边界。它定义 objective、scope、constraints、proof criteria、allowed / forbidden effects、adaptation permissions 与 escalation conditions，但不规定具体执行路径。

**Owner / Authority**

- UniAgent 拥有 contract authoring authority；
- Run Model 拥有已接受 `Execution Contract View` 的 canonical representation；
- Control Loop 只能在 contract 边界内形成 Tactical Hypothesis；
- 任何下层责任域不得重写 Goal 或扩大 contract scope。

**Lifecycle**

- Primary Run 启动前创建；
- 接受后按 version 固定；
- 小范围策略变化不改变 contract；
- objective、scope、permission 或 proof criteria 的实质变化必须形成显式新 version，不能静默覆盖当前 view。

### 3.4 Primary Run

**Definition**

一个已接受 Execution Contract 在真实环境中的一次完整执行生命周期。

**Owner / Authority**

- Run Model 唯一拥有 Uni Kernel 内的 canonical Run State；
- Control Loop 拥有 Control Intent Authority；
- Assurance 拥有 Runtime Assurance Judgment Authority，包括 Outcome Proof Authority；
- Effect Boundary 拥有 canonical target binding 与 effect delivery；
- Uni Kernel 聚合这些责任域并作为唯一外部执行与 Runtime Outcome emission boundary。

**Lifecycle**

- Execution Contract 被接受后开始；
- 可经历 active、uncertain、recovering、blocked 或 paused 等非终态；
- 只能形成一个 terminal Outcome State；
- terminal 后不得产生新的现实动作。

### 3.5 Uni Kernel

**Definition**

Primary Run 的 aggregate execution boundary。它组合六个 L2 责任域，维持现实执行闭环，并向外发出 immutable Runtime Outcome。

**Owner / Authority**

Uni Kernel 不作为六类 canonical state 或 judgment 的模糊兜底 Owner。具体 ownership 必须落在 Evidence Ledger、World Model、Run Model、Control Loop、Assurance 或 Effect Boundary。

Uni Kernel 唯一拥有：

- execution boundary integrity；
- L2 之间的合法协作边界；
- 保证所有 external effects exclusively pass through Effect Boundary；
- terminal Runtime Outcome emission boundary。

**Lifecycle**

- 随 Primary Run 建立；
- 在 Run 内协调闭环；
- terminal Runtime Outcome 发出后，保证不再接受新的 effect command；Effect Boundary 关闭 external effect delivery。

### 3.6 Evidence

**Definition**

Evidence 是关于环境、动作、效果或历史事实的 canonical、可引用、带 provenance 的依据记录。它回答“系统依据什么”，不回答“现实必然是什么”。

**Owner / Authority**

- Provider 拥有 raw artifact production；
- Evidence Ledger 唯一拥有 canonical Evidence Record semantics、EvidenceId、Provenance、Temporal/Causal references 与 Admission Record；
- Evidence Ledger is the sole Evidence Admission Authority；
- Evidence Admission 只判断输入是否具备成为 canonical Evidence Record 所需的 record integrity 与 provenance 条件；
- World Model 唯一决定 accepted Evidence 如何影响 Current WorldBelief；
- Assurance 唯一决定 Evidence 是否足以支持某个具体 assurance claim。

```text
Evidence Ledger
  → decides whether an input may become a canonical Evidence Record

World Model
  → decides how accepted Evidence affects Current WorldBelief

Assurance
  → decides whether Evidence is sufficient for a specific assurance claim
```

```text
Admission != Truth
Admission != Belief Relevance
Admission != Proof Sufficiency
```

**Lifecycle**

- raw artifact 先产生；
- 通过 admission 后形成 immutable canonical Evidence Record；
- record append-oriented，可被后续记录 supersede、降权或声明失效；
- 原始 provenance 与历史内容不可被就地改写。

### 3.7 Belief / WorldBelief

**Definition**

WorldBelief 是系统基于 accepted Evidence 形成的、版本化、可修正的当前世界判断。

**Owner / Authority**

World Model 唯一拥有 Current WorldBelief、World Graph、Container Graph projection、World State、Slice 与 Reconciliation。

**Lifecycle**

- 首次 Reconciliation 后形成；
- 新 accepted Evidence 可产生新 revision；
- freshness 下降时必须降级为 stale、uncertain 或 unknown；
- 历史 revision 保留为只读依据，不与 current revision 并列生效。

### 3.8 Runtime Outcome

**Definition**

Runtime Outcome 是 Primary Run 的 immutable terminal 结论及其 proof references。它描述执行实际达到、失败、取消或中断了什么。

**Owner / Authority**

- Assurance judges terminal Outcome Proof；
- Run Model records Proof Obligation State and terminal Outcome State；
- Uni Kernel emits immutable Runtime Outcome；
- UniAgent 只消费 Outcome，不回写其事实。

**Lifecycle**

- terminal Outcome State 成立后发出一次；
- 发出后不可变；
- 任何 Goal Evaluation 都不得反向重写 Runtime Outcome。

### 3.9 Goal Evaluation

**Definition**

UniAgent 基于 Runtime Outcome、Primary Goal 语义和用户上下文，对最终 Goal satisfaction 作出的监督评价。

**Owner / Authority**

UniAgent 唯一拥有 Goal Evaluation Authority。

**Lifecycle**

- 收到 Runtime Outcome 后形成；
- 可得到 `Satisfied / Partially Satisfied / Unsatisfied / Needs Follow-up` 等评价；
- 不修改 WorldBelief、Run State、Outcome Proof 或 Runtime Outcome。

## 4. L0 顶层关系

```text
User
  │
  ▼
UniAgent
  │ Primary Goal / Execution Contract
  ▼
Uni Kernel
  │ aggregate execution boundary
  ├── Evidence Ledger
  ├── World Model
  ├── Run Model
  ├── Control Loop
  ├── Assurance
  └── Effect Boundary
  │
  ▼
Environment
  │ Evidence / Effect
  └──────────────────────────────► Uni Kernel

Assurance
  → judges terminal Outcome Proof

Run Model
  → records Proof Obligation State and terminal Outcome State

Uni Kernel
  → emits immutable Runtime Outcome

UniAgent
  → evaluates Primary Goal satisfaction
```

# Part II — L1 Product Architecture

## 5. L1 canonical structure

```text
L1
├── UniAgent
├── Uni Kernel
├── Capability Plane
└── Memory System
```

L1 名称是 Target Product Architecture 的 canonical terms。逻辑组件不要求一一对应进程、程序集或部署单元。

## 6. UniAgent

UniAgent 的稳定职责是 Goal interpretation、global strategy、Execution Contract 与 Goal Evaluation。

它不得取得 Current WorldBelief、Control State、Assurance Judgment、target binding、external effect delivery 或 Run State 的 ownership。

## 7. Uni Kernel

Uni Kernel 是 aggregate execution boundary，不是共享 mutable context，也不是所有状态和判断的总 Owner。

它必须：

- 保证六个 L2 Owner 互不重叠；
- 保证 control intent、assurance judgment 和 effect delivery 分离；
- 保证跨 L2 只传 typed immutable values、references 或 commands；
- 保证外部 capability 不能旁路 L2 authority；
- 保证 terminal 后不存在绕过 Effect Boundary 的 effect path，并由 Effect Boundary 关闭 external effect delivery；
- 只从 canonical Outcome State 发出 Runtime Outcome。

## 8. Capability Plane

Capability Plane 提供可替换的 typed capabilities，例如 perception、semantic understanding、reasoning、grounding candidate generation、device dispatch adapter 或其他专业能力。

Capability Plane 可以：

- 接收有界输入；
- 产生 raw artifact、typed proposal、candidate binding 或 mechanical receipt；
- 声明 provenance、confidence、uncertainty、latency、failure 与 partial result；
- 被组合、替换、降级或关闭。

Capability Plane 不可以：

- 创建 canonical EvidenceId 或 admission record；
- 成为 Current WorldBelief 或 Run State owner；
- 取得 Control Intent Authority 或 Runtime Assurance Judgment Authority；
- 形成 canonical target binding；
- 宣布 Effect、Outcome Proof 或 terminal Runtime Outcome；
- 通过能力强弱扩大权限。

## 9. Memory System

Memory System 保存和召回跨时间可能有价值的历史知识、经验和引用。

Memory System 唯一拥有：

- durable memory records；
- memory provenance；
- retention metadata；
- recall result production。

Memory System 不拥有：

- canonical Evidence admission；
- Current WorldBelief；
- canonical Run State；
- current Control Intent；
- action authorization、Effect Verification 或 Outcome Proof。

```text
Memory Recall
  → may provide prior / context / hypothesis / historical reference

Memory Recall
  → cannot by itself establish a current-world claim

Current-world claim
  → requires appropriate fresh accepted Evidence

Historical EvidenceRef
  → may remain historical evidence
  → does not automatically become current-world evidence
```

**Memory may influence hypothesis and observation strategy; Memory does not establish current reality by itself.**

# Part III — Uni Kernel L2–L3

## 10. L2 canonical structure

```text
L2
├── Evidence Ledger
├── World Model
├── Run Model
├── Control Loop
├── Assurance
└── Effect Boundary
```

| L2 | Canonical ownership | Sole Authority | 不拥有 |
|---|---|---|---|
| Evidence Ledger | Evidence Record、EvidenceId、Provenance、Temporal/Causal references、Admission Record | Evidence Admission Authority | Reality、Belief relevance/weight、claim sufficiency、intent、Effect/Outcome Proof |
| World Model | Current WorldBelief、World Graph、Container Graph projection、World State、Slice、Reconciliation | Belief/Reconciliation Authority | Goal、Run progress、intent、Assurance Judgment |
| Run Model | Execution Contract View、Objective State、Run/Contract-level Proof Obligation State、Progress State、Outcome State | Canonical Run State Recording Authority | WorldBelief、Control Intent、action-local assurance requirements、Outcome Proof judgment |
| Control Loop | Control State、Tactical Hypothesis、Observation Control、Traversal Control、Recovery / Reorientation | Control Intent Authority | WorldBelief、Run progress truth、Assurance Judgment、effect delivery |
| Assurance | Action Admissibility、Preconditions/Freshness、Safety/Contract Guard、Effect Verification、Outcome Proof | Runtime Assurance Judgment Authority including Outcome Proof Authority | Control Intent、Run State、target binding、dispatch |
| Effect Boundary | canonical bounded target binding、Effect Gate、Dispatch、Effect Receipt、canonical external effect delivery boundary | Canonical Binding Authority；Effect Delivery Authority | strategy、replan、recovery、Effect/Outcome Proof judgment |

## 11. Evidence Ledger

### 11.1 Canonical responsibility

Evidence Ledger 把 Provider 产生的 raw artifact 或 typed output 规范化为可引用的 canonical Evidence Record。

它唯一拥有：

- Evidence Record semantics；
- EvidenceId；
- Provenance；
- Temporal / Causal references；
- Admission Record；
- record supersession / invalidation references。

Provider 可以拥有 raw artifact production，但不得形成独立 evidence authority。Producer confidence 只能作为 record 内容，不能直接升级为 truth。

Evidence Admission 只检查：

- canonical record integrity；
- provenance completeness；
- source identity；
- capture time；
- declared scope；
- transformation lineage；
- schema / record canonicalization。

Evidence Ledger 明确不得判断：

- current-world truth；
- semantic relevance to Current WorldBelief；
- evidence weight in Reconciliation；
- claim sufficiency；
- Effect Proof、Completion Proof、Failure Proof 或 Safe-Stop / Escalation Proof。

### 11.2 L3

```text
Evidence Ledger
├── Observation Record
├── Evidence Artifact Reference
├── Provenance
├── Temporal / Causal Reference
└── Admission Record
```

#### Observation Record

记录某个 producer 在特定时间、scope 和 environment 中产出了什么观察。

最小语义包括 producer、capture time、Observation Scope、freshness、payload reference、confidence、uncertainty、failure 与 partial status。

#### Evidence Artifact Reference

引用 raw artifact 或 derived artifact，不把 bytes、producer claim、accepted Evidence、Belief 与 proof 混为一体。

```text
Raw Artifact
  != Producer Claim
  != Canonical Evidence Record
  != WorldBelief
  != Outcome Proof
```

#### Provenance

回答谁产生、使用了什么输入、何时何地生成、经历了何种转换、对哪个 claim 有效，以及有什么限制。

#### Temporal / Causal Reference

表达 sequence、correlation 与已验证 causation。时间相邻不能自动升级为因果关系。

#### Admission Record

记录输入是否可以成为 canonical Evidence Record，以及 canonical integrity、provenance completeness、source identity、capture time、declared scope、transformation lineage、schema / record canonicalization 的检查结果。

Admission 只决定 canonical record eligibility，不决定 Truth、Belief Relevance、Reconciliation Weight 或 Proof Sufficiency。

## 12. World Model

### 12.1 Canonical responsibility

World Model 唯一拥有：

- Current WorldBelief；
- World Graph；
- Container Graph projection；
- World State；
- Slice；
- Reconciliation。

包含关系：

```text
WorldBelief Revision
├── World Graph
├── World State
├── Evidence Basis
├── Freshness
├── Uncertainty
└── Conflicts
```

```text
WorldBelief
  = canonical belief aggregate

World State
  = dynamic state / claim portion of WorldBelief

Container Graph Projection
  = immutable scoped projection derived from WorldBelief

Slice
  = scoped immutable projection of a WorldBelief revision
```

WorldBelief 与 World State 不得实现为两套并列 current truth。

正式关系：

```text
Belief != Reality
Evidence != Belief
World Model owns belief, not intent
```

Reconciliation 只能依据 accepted Evidence 更新 Belief。Plan、Tactical Hypothesis、Goal、expectation 或 desired outcome 不得修改 observation interpretation。

### 12.2 L3

```text
World Model
├── World Graph
├── Container Graph Projection
├── World State
├── Slice
└── Reconciliation
```

#### World Graph

表示当前相信存在的 world entities、relations、interaction context 与可达关系。它不保存 plan、execution obligation、authorization 或 completion state。

#### Container Graph Projection

表达 screen、dialog、drawer、tab、list、viewport、nested region 等局部交互上下文及其结构关系。它是从 WorldBelief 派生的 immutable scoped projection，不是独立 mutable truth。

#### World State

表达 WorldBelief 中动态 state / claim 的组成部分。它随 WorldBelief revision 一起产生，不拥有独立 revision 或 current-truth lifecycle。

#### Slice

**Slice = scoped immutable projection of a WorldBelief revision.**

Slice 必须声明 source revision 与 scope，不得回写 WorldBelief，不得成为长期并行 owner。

原始局部视觉输入统一使用：

- Observation Scope；
- Observation Region；
- Perception Region。

这些输入不得称为 Slice。

#### Reconciliation

将 accepted Evidence 转换为新的 WorldBelief revision。它必须显式处理冲突、缺失、过期和不确定性，并保留完整 evidence basis。

## 13. Run Model

### 13.1 Canonical responsibility

**Run Model owns canonical Run State within Uni Kernel.**

它唯一拥有：

- Execution Contract View；
- Objective State；
- Proof Obligation State；
- Progress State；
- Outcome State。

Run Model 记录 canonical Run State，不产生 Control Intent，不判断 Effect 或 Outcome Proof，不拥有 WorldBelief，也不保存 action-local assurance state。

### 13.2 L3

```text
Run Model
├── Execution Contract View
├── Objective State
├── Proof Obligation State
├── Progress State
└── Outcome State
```

#### Execution Contract View

已接受 contract version 的 immutable canonical view，保留 objective、scope、constraints、proof criteria、allowed / forbidden effects、adaptation permissions 与 escalation conditions。

#### Objective State

记录 Primary Run 对 contract objective 的执行侧状态。它不是 UniAgent 的 Goal Evaluation。

#### Proof Obligation State

**Run Model Proof Obligations = contract-level / run-level obligations.**

只记录：

- objective proof；
- material effect proof；
- completion proof；
- failure proof；
- blocked / safe-stop proof；
- escalation proof。

以下 action-local / judgment-local requirements 不进入 canonical Run State：

- action-local precondition；
- target freshness；
- action admissibility；
- one-step grounding validity。

这些属于 Assurance Requirements，只在相应 judgment lifecycle 内有效。

#### Progress State

记录 execution obligations 的推进情况。Observation count、action count、coverage、provider success 或 confidence 不能单独成为 progress truth。

#### Outcome State

记录 terminal classification、fulfilled / unfulfilled obligations、Outcome Proof reference、unresolved uncertainty 与 material effect references。

Outcome State 只记录 Assurance judgment 的结果，不重复判断 Outcome Proof。

## 14. Control Loop

### 14.1 Canonical responsibility

**Control Loop is the sole Control Intent Authority.**

它唯一拥有：

- Control State；
- Tactical Hypothesis；
- Observation Control；
- Traversal Control；
- Recovery / Reorientation。

Control Loop 不拥有：

- WorldBelief；
- Run progress truth；
- Effect Verification 或 Outcome Proof judgment；
- canonical target binding；
- external effect delivery。

### 14.2 L3

```text
Control Loop
├── Control State
├── Tactical Hypothesis
├── Observation Control
├── Traversal Control
└── Recovery / Reorientation
```

#### Control State

保存当前 control cycle 所需的最小内部状态。它不得聚合 Evidence、WorldBelief、Run State 与 Memory 形成 God Context。

#### Tactical Hypothesis

Plan / Tactical Hypothesis is disposable hypothesis。它可以被新 Evidence 废弃或修订，不是事实、authorization、Effect 或 Completion 的来源。

#### Observation Control

决定何时、以何种 Observation Scope / Region、调用哪些 capability 获取新输入。它不拥有 Evidence admission 或 observation interpretation。

#### Traversal Control

负责未知结构探索、coverage、推进方向与控制权返回。

Traversal 不拥有 Goal、Belief 或 Completion。Coverage、no-progress、stack empty 或 viewport exhaustion 都不能单独宣布 objective satisfied。

#### Recovery / Reorientation

在 divergence、trap 或 invalid hypothesis 后选择有界恢复意图。Recovery / Reorientation 必须重新进入正常的 Evidence / Belief / Assurance / Effect / Verification closed loop。

```text
Re-observe / Reconcile
  → Reorient
  → Assure
  → Act if needed
  → Observe / Verify / Reconcile
```

Recovery 不要求一定先 act。无法在当前 scope 内解决时必须升级。

**Recovery must not bypass normal authority through blind retry, stale assumptions, or unverified state transitions.**

## 15. Assurance

### 15.1 Canonical responsibility

**Assurance is the sole Runtime Assurance Judgment Authority, including Outcome Proof Authority.**

它唯一负责：

- Action Admissibility；
- Preconditions / Freshness；
- Safety / Contract Guard；
- Effect Verification；
- Outcome Proof。

```text
Run Model Proof Obligations
  = contract-level / run-level obligations

Assurance Requirements
  = action-local / judgment-local requirements
```

Action-local precondition、target freshness、action admissibility 与 one-step grounding validity 只存在于 Assurance judgment lifecycle，不进入 canonical Run State。

正式区分：

```text
Control Loop = Control Intent Authority
Assurance = Assurance Judgment Authority
```

Assurance 不拥有 Tactical Hypothesis、Run State、WorldBelief、canonical binding 或 mechanical dispatch。

### 15.2 L3

```text
Assurance
├── Action Admissibility
├── Preconditions / Freshness
├── Safety / Contract Guard
├── Effect Verification
└── Outcome Proof
    ├── Completion Proof
    ├── Failure Proof
    └── Safe-Stop / Escalation Proof
```

#### Action Admissibility

判断候选动作是否在 contract scope 内、是否满足 permissions、是否有足够 binding、是否存在 unresolved conflict，以及是否需要升级。

#### Preconditions / Freshness

对当前页面、对象、坐标、状态、权限与 Evidence 应用 claim-specific freshness policy。历史位置、缓存对象、Memory recall 或 Provider confidence 不能单独满足当前物理绑定要求。

#### Safety / Contract Guard

对 safety、contract、permission 与 effect class 作 fail-closed 判断。Guard 可以拒绝或要求升级，不能扩展权限。

#### Effect Verification

依据 post-action accepted Evidence 判断外部环境是否发生预期改变。

```text
Action Attempt != Effect
Effect Receipt != Verified Effect
```

#### Outcome Proof

Outcome Proof 统一覆盖所有 terminal outcome。它判断 Run/Contract-level Proof Obligation State 是否具备足够 accepted Evidence 支持具体 terminal claim。

##### Completion Proof

判断 Proof Obligation State 是否具备足够 accepted Evidence 支持 terminal completion claim。

```text
Effect != Completion
```

##### Failure Proof

判断 Evidence 是否足以支持 failed terminal outcome，并明确 failure scope、unfulfilled obligations、known effects 与 unresolved uncertainty。

##### Safe-Stop / Escalation Proof

判断 Evidence 是否足以证明当前 Run 必须 safe-stop、blocked terminal 或形成 terminal escalation outcome，并记录无法继续的 authority、safety、freshness 或 environment 原因。

统一终局关系：

```text
Assurance
  → judges terminal Outcome Proof

Run Model
  → records Proof Obligation State and terminal Outcome State

Uni Kernel
  → emits immutable Runtime Outcome

UniAgent
  → evaluates Primary Goal satisfaction
```

## 16. Effect Boundary

### 16.1 Canonical responsibility

Effect Boundary 唯一拥有 canonical bounded target binding 与 canonical external effect delivery boundary。

```text
Uni Kernel
  → guarantees that all external effects pass exclusively through Effect Boundary

Effect Boundary
  → owns the canonical external effect delivery boundary
```

它不拥有 strategy、replan、recovery、admissibility judgment、Effect Verification 或 Outcome Proof。

### 16.2 L3

```text
Effect Boundary
├── Grounding / Target Binding
├── Effect Gate
├── Dispatch
└── Effect Receipt
```

#### Grounding / Target Binding

```text
Grounding Provider
  → Candidate Binding

Effect Boundary
  → canonical bounded target binding
```

Binding 必须关联 current WorldBelief revision、fresh occurrence、scope、ambiguity 与 target constraints。历史 bounds 不具有当前 effect authority。

#### Effect Gate

```text
Assurance
  → admissibility / freshness / authorization judgment

Effect Gate
  → enforce authorization
```

Effect Gate 只能执行或拒绝既有 judgment，不能重新判断、改变 target 或扩大 effect。

#### Dispatch

负责已授权 effect command 的 mechanical delivery，并明确报告 accepted、rejected、failed 或 unknown。

Driver 不得自行 retry、recover、replan 或改变 strategy。

#### Effect Receipt

记录 command 是否被接收、发送或由下游确认。Effect Receipt 是 attempt evidence，不直接证明 Effect。

# Part IV — Lifecycle、Owner、Authority 与 Invariants

## 17. Canonical Lifecycle Matrix

| Canonical object | Create | Update / validity | Terminal / invalidation |
|---|---|---|---|
| Primary Goal | UniAgent 在 Session 内创建 | 只通过显式 Goal revision 澄清 | completed、abandoned、unresolvable 或 Session closed |
| Execution Contract | UniAgent 在 Primary Run 前创建 | 显式 version；accepted version 不就地改写 | 被拒绝、被显式新 version 取代或 Run terminal |
| Execution Contract View | Run Model 在 contract 被接受时建立 | 对当前 Run immutable | Run terminal 后冻结 |
| Raw Artifact | Provider 在一次 capability invocation 中产生 | Producer 不得就地改变已发布内容 | invocation 结束；后续更正产生新 artifact |
| Canonical Evidence Record | Evidence Ledger admission 时创建 | immutable；通过 supersession / invalidation reference 演化 | 保留为历史依据，不重新取得 current authority |
| WorldBelief Revision | World Model Reconciliation 时创建，内含 World Graph、World State、Evidence Basis、Freshness、Uncertainty 与 Conflicts | 每次只产生新 revision；freshness 可降级 | 被新 current revision 取代或失效为 unknown |
| Slice | World Model 从指定 WorldBelief revision 派生 | immutable；只在声明的 scope/freshness 内有效 | source revision 或 freshness 不满足时失效 |
| Canonical Run State | Run Model 接受 Contract View 后创建 | 只通过 typed legal transition 更新 | 一个 terminal Outcome State；之后不可恢复为 active |
| Control State | Control Loop 随 active Run 建立 | 每个 control cycle 通过 intent transition 更新 | Run terminal 时销毁；不得作为可执行历史计划保留 |
| Tactical Hypothesis | Control Loop 基于当前 inputs 创建 | 可随新 Evidence 废弃或替换 | invalidated、abandoned 或 Run terminal |
| Assurance Judgment | Assurance 针对特定 input revision 形成，包括 action-local judgment 与 terminal Outcome Proof | immutable；inputs/freshness 改变后必须重新判断 | 被新 judgment 取代或因 freshness 失效；terminal proof 写入 Outcome State 后冻结引用 |
| Canonical Target Binding | Effect Boundary 针对 selected intent 建立 | 仅在关联 WorldBelief revision、scope 和 freshness 内有效 | dispatch、intent change、ambiguity 或 freshness loss 后失效 |
| Effect Receipt | Effect Boundary 在 dispatch 后创建 | immutable；后续 Effect Evidence 形成独立 record | 保留为 attempt evidence，不升级为 Effect truth |
| Runtime Outcome | Uni Kernel 在 terminal Outcome State 成立后发出 | immutable，exactly once | emission 完成后，Effect Boundary 关闭 external effect delivery |
| Goal Evaluation | UniAgent 消费 Runtime Outcome 后形成 | 可产生新的监督评价记录，但不回写执行事实 | Session 关闭或 Goal lifecycle 结束 |
| Durable Memory Record | Memory System 按明确 retention contract 创建 | versioned、可 supersede、可 expire | 失效后不得作为 current-state 输入直接使用 |

## 18. Canonical Owner Matrix

| Canonical state / output | Sole Owner | 唯一写入路径 | 禁止的平行 Owner |
|---|---|---|---|
| Primary Goal | UniAgent | explicit Goal revision | Memory、Control Loop、Run Model |
| Execution Contract | UniAgent | explicit contract version | Control Loop、Capability Plane |
| Execution Contract View | Run Model | contract admission path | UniAgent side copy、Control Loop |
| Canonical Evidence Record | Evidence Ledger | Evidence admission path | Provider-local interpretation、Memory |
| Current WorldBelief，包含 World Graph / World State / Evidence Basis / Freshness / Uncertainty / Conflicts | World Model | Reconciliation | Evidence Ledger、Run Model、Control Loop、parallel World State |
| Container Graph projection / Slice | World Model | immutable derivation from WorldBelief revision | independent mutable graph/state copy |
| Canonical Run State | Run Model | typed Run State transition | Uni Kernel shared context、Control Loop |
| Control State / Tactical Hypothesis | Control Loop | Control Intent transition | Run Model、Assurance |
| Assurance Judgment | Assurance | Assurance evaluation | Control Loop、Effect Boundary、Provider |
| Canonical bounded target binding | Effect Boundary | binding path from candidate + current slice | Grounding Provider、Control Loop |
| Canonical external effect delivery boundary | Effect Boundary | Effect Gate → Dispatch | Uni Kernel、Driver、Capability Provider |
| Effect Receipt | Effect Boundary | dispatch result path | Driver strategy state、Assurance |
| Runtime Outcome envelope | Uni Kernel | terminal Outcome State emission | UniAgent、Memory、Capability Plane |
| Goal Evaluation | UniAgent | supervisory evaluation | Run Model、Assurance |
| Durable Memory Record | Memory System | memory lifecycle path | World Model、Evidence Ledger |

## 19. Canonical Authority Matrix

| Authority class | Sole Authority | Inputs | Output | 不得替代它的来源 |
|---|---|---|---|---|
| Goal / Contract / Goal Evaluation Authority | UniAgent | user intent、Runtime Outcome | Primary Goal、Execution Contract、Goal Evaluation | Run Outcome、Provider result |
| Evidence Admission Authority | Evidence Ledger | raw artifact、typed output、record-integrity and provenance criteria | Admission Record / canonical Evidence Record | producer confidence、Belief relevance、proof sufficiency |
| Belief / Reconciliation Authority | World Model | accepted Evidence | WorldBelief revision | Plan、expectation、Memory recall |
| Canonical Run State Recording Authority | Run Model | Contract View、run-level obligations、typed judgments | canonical Run State revision | action-local assurance state、Control State、trace projection |
| Control Intent Authority | Control Loop | Contract View、WorldBelief Slice、Run State | selected intent | Assurance、Provider suggestion |
| Runtime Assurance Judgment Authority including Outcome Proof Authority | Assurance | intent、binding、Contract View、accepted Evidence、Proof Obligation State | action-local judgment、Effect Verification、terminal Outcome Proof | Driver receipt、action attempt、Run Model record |
| Canonical Binding Authority | Effect Boundary | selected intent、candidate binding、WorldBelief Slice | canonical bounded target binding | Provider candidate、Control Loop target hint |
| Effect Delivery Authority | Effect Boundary | canonical binding、Assurance authorization judgment | bounded command / Effect Receipt | Uni Kernel、Driver retry strategy、Provider |

## 20. Core Invariants

### 20.1 Ownership and authority

1. 每个 canonical mutable state 在任一时刻只有一个 Owner。
2. 每个 authority class 只有一个 Authority。
3. Uni Kernel 是 aggregate execution boundary，不是 L2 ownership 的兜底。
4. Uni Kernel owns boundary integrity；Effect Boundary alone owns canonical external effect delivery。
5. 跨 L2 边界只传 immutable value、reference、proposal、judgment 或 command。
6. Capability strength、confidence 或 deployment location 不改变 Authority。
7. Memory may influence hypothesis and observation strategy；Memory does not establish current reality by itself。

### 20.2 Evidence and belief

8. Provider owns raw artifact production；Evidence Ledger owns canonical Evidence semantics。
9. Evidence Ledger is the sole Evidence Admission Authority，但 Admission 只判断 record integrity、provenance、source、time、scope、lineage 与 canonicalization。
10. Admission != Truth；Admission != Belief Relevance；Admission != Proof Sufficiency。
11. Provider 不得形成独立 evidence authority，Producer confidence 不得直接升级为 truth。
12. Evidence != Belief。
13. Belief != Reality。
14. World Model owns belief, not intent。
15. Reconciliation 只能依据 accepted Evidence 更新 Belief。
16. Plan / expectation 不得修改 observation interpretation。
17. WorldBelief 是 canonical belief aggregate；World State 是其 dynamic state / claim portion，不得形成并列 current truth。
18. WorldBelief 必须表达 Evidence Basis、freshness、uncertainty 与 conflicts。
19. Container Graph Projection 与 Slice 都是从 WorldBelief revision 派生的 immutable scoped projections。
20. Slice = scoped immutable projection of a WorldBelief revision；raw observation input 只能称为 Observation Scope、Observation Region 或 Perception Region。

### 20.3 Control, assurance and effect

21. Control Loop is the sole Control Intent Authority。
22. Assurance is the sole Runtime Assurance Judgment Authority, including Outcome Proof Authority。
23. Effect Boundary is the sole Canonical Binding Authority and Effect Delivery Authority。
24. Candidate Binding != Canonical Binding。
25. Candidate Action != Admissible Action != Authorized Command。
26. Effect Gate 只 enforce authorization，不重新判断。
27. Driver 不得自行 retry、recover、replan 或改变 strategy。
28. Control State 不得形成 God Context。
29. Traversal 不拥有 Goal、Belief 或 Completion。
30. Recovery / Reorientation 必须重新进入正常 Evidence / Belief / Assurance / Effect / Verification closed loop。
31. Recovery 不得通过 blind retry、stale assumptions 或 unverified state transitions 绕过正常 authority。

### 20.4 Plan, effect and completion

32. Plan / Tactical Hypothesis is disposable hypothesis。
33. Action Attempt != Effect。
34. Effect Receipt != Verified Effect。
35. Effect != Completion。
36. Run Model Proof Obligations 只包含 contract-level / run-level obligations；action-local requirements 属于 Assurance。
37. Assurance judges terminal Outcome Proof，统一覆盖 Completion、Failure 与 Safe-Stop / Escalation。
38. Run Model records Proof Obligation State and terminal Outcome State。
39. Uni Kernel emits immutable Runtime Outcome。
40. UniAgent evaluates Primary Goal satisfaction。
41. Goal Evaluation 不得回写 Runtime Outcome、Run State、WorldBelief 或 Outcome Proof。
42. terminal Runtime Outcome 发出后，Effect Boundary 必须关闭 external effect delivery。

## 21. Replaceability Contract

任何 L1 或 L2 实现都可以被替换，前提是替换前后同时保持：

- canonical term 与语义；
- Sole Owner；
- judgment Authority；
- lifecycle 与 terminal rules；
- accepted input / output contract；
- provenance、freshness 与 uncertainty；
- fail-closed behavior；
- effect 与 terminal Outcome Proof separation；
- observable evidence continuity。

### 21.1 Capability replaceability

替换 Provider、模型、算法或设备 adapter，只能改变 capability quality、cost、latency 或 coverage，不能改变 Evidence、Belief、Control、Assurance、Binding、Run State 或 Goal Evaluation 的 ownership。

### 21.2 L2 realization replaceability

L2 可以合并部署或拆分实现，但：

- 合并部署不得合并 Owner 或 Authority；
- 拆分实现不得复制 canonical state；
- cache、index、projection 和 read model 必须从 canonical source 派生且不可回写；
- observability surface 不得成为 command surface。

### 21.3 Memory replaceability

Memory storage、index、retrieval 或 retention policy 可以替换，但 Memory Recall 只能提供 prior、context、hypothesis 或 historical reference。它不能单独建立 current-world claim；Historical EvidenceRef 保持 historical status，current-world claim 仍需要适当的 fresh accepted Evidence。

## 22. 暂不锁定

本文不锁定：

- 具体类、接口、模块与 namespace；
- 进程、线程、actor、service 或 deployment topology；
- Evidence schema 字段布局与物理存储；
- World Graph / Container Graph 的数据结构；
- FSM state enumeration；
- Traversal algorithm、frontier、depth 与 coverage implementation；
- perception、semantic、reasoning、grounding 或 driver Provider；
- Memory taxonomy、retention 与 cross-Run identity；
- 多 Goal、多 Run、parallel Run 与 continuation semantics。

开放实现选择不得改变本文已经指定的 Owner、Authority、Lifecycle、Boundary 与 Invariants。

## 23. Baseline Closure

本版本标记为：

```text
Product Architecture Baseline v0.1
Status: CANDIDATE_FOR_ADOPTION
L0-L3: CLOSED
```

自本版本起，不再继续修改 L0–L3 横向结构。后续只允许：

1. Scenario pressure test；
2. Legacy → Target mapping；
3. Migration design；
4. L4 详细设计。

只有真实 Scenario Evidence 能够证明现有 Owner、Authority 或 Boundary 无法成立时，才允许重新打开相应 L0–L3 语义。实现偏好、旧结构、Provider 限制、命名便利或局部测试失败都不足以重新打开基线。

## 24. Narrow Amendment v0.1.1 — Legal Activation、Internal Run Driver 与最小安全语义

> 修订依据：ADR-0019 / ADR-0022 + RFS-001 Human 授权（2026-09-13）。
> 性质：narrow amendment——只新增 ADR-0019 明确留给后续闭合（Deferred ⑰）的
> 最小 lifecycle / authority / 安全不变量语义，及 Phase 1 tracer 依赖项；
> 不重开 §23 已关闭的 L0–L3 横向结构，不锁定类名、字段、namespace、
> transport、storage 或 Interface 形状。各项的 epistemic 分类与理由见
> `changes/RFS-001/state.md`（D1–D17）。

### 24.1 Legal Activation（合法激活）

- Runtime 是可独立合法激活的产品组件；产品级主要目标理解与发起对象是完整
  UniAgent realization（ADR-0022）。
- accepted Execution Contract View 是合法激活的必要前置，不是激活本身
  （ADR-0019；P1 Non-Activation）。admission 建立的 Run State 不因 admission
  而开始自驱。
- 合法激活是一次性、幂等的 lifecycle command：

```text
accepted Contract View
  → legal activation（一次性 lifecycle command）
  → Kernel internal run driver self-drive（bounded execution loop）
```

- 激活幂等与 cardinality（不变量 44）：同一 accepted Contract View generation
  下 at-most-one Primary Run；重复激活不得创建第二个 Primary Run、不得重放
  Effect、不得产生第二个 Runtime Outcome，只返回同一 Run 关联。
- canonical Run State 仍只经 Run Model typed legal transition 改变；Kernel
  只持有 activation latch（composition / lifecycle coordination state，
  通则见协议 0.1.10），不新增 activation Authority class。
- 激活 seam 的唯一 Producer / Consumer、activation identity、correlation、
  并发与恢复 = OPEN GATE（协议 Deferred ⑰ / R1）；Phase 1 tracer 只实现最小
  concrete seam。§3.4 Primary Run Lifecycle 中「Execution Contract 被接受后
  开始」按 ADR-0019 与本节理解：admission 是必要前置，自驱开始于合法激活。

### 24.2 Internal Run Driver 与 UniAgent 语义决策边界

- 合法激活后，由 Uni Kernel internal run driver 编排 bounded execution loop
  直至 terminal（ADR-0019）。Host、测试、Simulation Host 与 UniAgent 都不得
  逐 cycle 驱动 Kernel 操作面；外部输入只能以 capability input 或有界
  lifecycle command 形式进入。
- 职责分工（不变量 45）：UniAgent 拥有 Goal interpretation、global strategy、
  Execution Contract authoring、Plan Hypothesis 与 Goal Evaluation；Control
  Loop 独占 Tactical Hypothesis 与 Control Intent；Kernel internal run driver
  只拥有 orchestration / lifecycle authority，不是 planner 或 intelligence
  authority，不得演化为固定业务流程。
- Kernel 只在语义 decision boundary 经外部 seam 请求 UniAgent，请求携带有界
  Decision Context。UniAgent 返回 advisory proposal（或显式 no-action /
  no-response）；proposal 不直接写 Run State、WorldBelief、Assurance、
  Binding、receipt、Effect 或 Outcome，也不携带 effect command。
- proposal 经 Kernel 机械入口校验后，仍须由 Control Loop 签发 intent 并经完整
  Grounding → Assurance → Effect Boundary 链执行。Phase 1 tracer 只锁定 schema、
  Decision/Run correlation、非空 target 与 AllowedEffects；完整 Contract scope、
  capability、risk、budget 校验因缺少 owning model 延后 Phase 6。非法 proposal、no-response、
  correlation 失配或越权请求一律 fail closed：零新 Effect，Run 保持合法非终态
  或按 contract safe-stop。
- Bounded Contingent Decision Package 只按上述外部语义理解（immutable
  advisory proposal、有限 DAG、typed tri-state Guard、单 active + 预算内
  speculative、semantic lease 绑定而非 revision number、每动作 fresh
  Grounding/Assurance、Guard Unknown / 失效 / Human preemption fail closed 回
  Agent decision boundary、Kernel 只入口校验、Control 只记 ref、Run Model 不存
  内容）。它不是 Effect batch、Driver macro 或第二 Runtime。其载荷与字段 =
  TRACER_HYPOTHESIS（roadmap Phase 6 / H14），本节不冻结。

### 24.3 现实 Effect 串行验证屏障（不变量 43）

- 每次现实 Effect dispatch 后，Runtime 必须先经合法 Observation 路径取得
  post-action accepted Evidence（P2；ObservationContext = PostActionEffectFlow），
  并由 World Model reconciliation 与 Assurance verification 处理完该次动作的
  验证义务，才允许下一次现实 Effect dispatch。
- post-action Observation 可以是 cheap / scoped / native signal，不要求全屏
  截图或 VLM，但必须经 P2 / Evidence admission；Effect receipt 与
  AttemptReport 是 attempt evidence，不是现实 Effect 证明，永不满足
  MaterialEffect 验证（不变量 33/34 的执行面细化）。
- 违例方向一律 fail closed：不得批量 dispatch、不得隐式重试、不得以 receipt
  提前放行、不得在验证义务未清偿前发出下一次现实 Effect。

### 24.4 恢复与 UnknownOutcome

- 恢复裁决只依据 canonical owner records、可靠 Checkpoint、
  Attempt / Obligation / Evidence refs 与 fresh observation；Trace 只是异步
  诊断参考，不得参与恢复裁决（ADR-0013）。
- UnknownOutcome 是恢复屏障：dispatch 结果未解清前禁止 blind redispatch
  （P15 / DSE-001）；先重新观察 / 对账，再由合法决策路径决定后续。
- Checkpoint 结构、恢复位置选择与预算 = OPEN GATE（roadmap Phase 5）。

### 24.5 Agent Continuation（非 shadow state）

- Product-level Agent Continuation 只保存 Agent cognition summary 与
  canonical refs（Goal ref、Plan Hypothesis、next objective、Run / Checkpoint /
  Attempt / Obligation / Evidence refs）。
- 不复制 Run / Control / Assurance / Effect 状态，不形成与 owner records 竞争
  的 shadow state；不依赖 Host transcript、隐藏思维链或 Trace replay。恢复时
  先由 owner records + fresh observe / reconcile 确定现实，再由 UniAgent 重评。
- 载荷与生命周期 = OPEN GATE（roadmap Phase 6）。

### 24.6 Grant 与 Contract generation 分离

- Execution Contract 可预授权低风险、task-scoped capability。
- 高影响 semantic Commit Point（发送、购买、删除、账户变更、系统权限等）
  必须使用细粒度一次性 Grant：绑定 destination / payload / scope / effect
  class / expectation。
- 授权链（不变量 46）：Human / Policy Authority 签发 bounded immutable
  Grant → UniAgent 基于 Grant author 新 Contract Proposal → Run Model
  validation / admission → 新的 immutable Contract View generation。旧 View
  与历史行为不改写，权限不追溯。
- Grant 本身不直接授权任何动作；撤销 / 过期阻断未 dispatch 动作，已投递
  Attempt 只能继续对账。
- 本节细化协议 Deferred ②（Contract version 取代语义）的方向，不改变 §17
  Contract Lifecycle。Grant 载荷、撤销与过期协议 = OPEN GATE（Phase 6 / H15
  后续裁决）。

### 24.7 Untrusted Evidence 与 Human preemption

- 页面、app、消息与网页内容都是 untrusted Evidence：可进入 Evidence / Belief
  闭环，但不得改变 Goal、Execution Contract、Capability Offer、Grant 或系统
  约束（不变量 47）。系统权限弹窗只能执行与既有 semantic Grant 等同或更窄的
  选择；出现更宽选项必须暂停并重新授权。
- Human manual input / app 切换优先抢占：检测到即失效 active execution
  lease / package，停止新 Effect，撤销未 dispatch authorization；in-flight
  Attempt 只做 reconciliation，fresh observe 后再决定是否继续，绝不与用户
  抢控制。
- 检测机制与 vocabulary = OPEN GATE（roadmap Phase 6/7）。

### 24.8 Host 隔离与同一 Runtime artifact

- Product Host 与 Simulation Host 是不同 composition root，装配同一 Product
  Runtime modules（同一 artifact identity；发布级 qualification 绑定精确
  artifact/hash）。
- Product Host 依赖闭包不得包含 ScenarioStimulus consumer、Replay、Oracle、
  Scenario Importer 或任何 Simulation 功能；不得存在可由配置打开的隐藏模拟
  路径。
- Simulation 只消费由 sealed Trace 或 reviewed assets 派生的 immutable
  ScenarioStimulus，永不把 Trace Event 当 command；Simulation 不保存第二份
  Run / World / Control / Assurance state（无 parallel Runtime FSM）。

### 24.9 Trace 异步与 seal / import 门

- Trace capture / persistence 异步、非权威：写入延迟、失败或背压不得进入
  Runtime critical path，不得改变 canonical output，不得参与恢复裁决
  （ADR-0013）。
- RFS-001 Phase 1 只验证 enabled / disabled / failing recorder 的 canonical
  行为等价与故障隔离；真正后台异步持久化 writer 明确延后 roadmap Phase 3，
  不得把 Phase 1 recorder 证据表述为 async writer 已实现。
- 只有完成 drain / seal / integrity check 的 Trace artifact 才可交给
  Scenario Importer；Importer 只派生 immutable ScenarioStimulus；未 sealed、
  Quarantined 或 integrity unknown 的 artifact 一律 fail closed。
- Phase 1 的 `IntegritySha256` 摘要可证明录制内容被篡改时 fail closed，但不证明
  artifact authenticity、签名来源或其内容为现实真相；这些仍属后续治理范围。

### 24.10 Amendment 边界

本节新增不变量编号延续 §20：

```text
43. 现实 Effect 串行验证屏障（§24.3）
44. legal activation 幂等；at-most-one Primary Run per accepted Contract View generation（§24.1）
45. UniAgent / Control / Kernel 职责分工不可漂移（§24.2）
46. Grant 不直接授权动作；授权链 Grant → UniAgent Proposal → Run Model admission（§24.6）
47. untrusted content 不得改写 Goal / Contract / Offer / Grant / 系统约束（§24.7）
```

本节不修改 §17–§20 既有条目，不锁定任何实现名称；与冻结协议（P1–P23）的
衔接见 Inter-Component Protocol Baseline 同日 narrow amendment。
