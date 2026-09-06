# UniClaw

The repo for UniClaw development: the UniFlow development harness (workflow,
skills, delegation contracts) and the GREENFIELD product line — the Uni
Kernel of the Target Architecture v0.1 — living together, separated by
structure (harness mechanism layers vs `src/`/`tests/` product code), not by
branch. This glossary defines both: how development work is described and
controlled, and the product's canonical Evidence/Belief domain language.

## Language

### Workflow control

**UniFlow**: The single development workflow control plane — owns WHEN and
WHAT NEXT across a task's lifecycle.
_Avoid_: pipeline, process framework, orchestration skill

**Semantic Gate**: One of four readiness checkpoints (Explore Resolution,
Execution Readiness, Verification, Completion) a task must pass on
evidence; a semantic contract, not a form.
_Avoid_: phase gate, checklist, ceremony

**Pre-UniFlow Explore**: The stage that makes the problem clear — feature
grilling or bug diagnosis — before controlled execution starts.
_Avoid_: research phase, discovery sprint

**Outcome token**: The normative state label emitted by a gate or rule:
`EXPLORE_RESOLVED`, `STAY_IN_EXPLORE`, `STAY_IN_DIAGNOSIS`,
`EXECUTION_READY`, `VERIFIED`, `VERIFICATION_FAILED`, `COMPLETE`.
_Avoid_: ad-hoc status strings

**Human Gate**: The escalation point reserved for genuinely human
decisions — unresolved product meaning, major architecture choice,
authority conflict, irreversible action.
_Avoid_: approval step, sign-off

### Roles and execution

**Leader**: The context that owns the workflow — evaluates gates, decides
routing, holds the Plan, and declares completion.
_Avoid_: orchestrator, main agent

**SubAgent**: A fresh, disposable execution context that completes one
delegated WorkItem and returns result plus evidence.
_Avoid_: worker, thread, minion

**Direct**: Execution by the Leader inside its own current context; no
WorkItem is created.
_Avoid_: inline mode

**Delegate**: Routing a bounded, self-contained objective to a SubAgent
through a transient WorkItem.
_Avoid_: fan-out, dispatch-and-forget

**WorkItem**: A transient Leader→SubAgent delegation contract compiled
from the current Plan; never an issue tracker, backlog, or persistent
store.
_Avoid_: ticket, task, issue, spec

**Plan**: The Leader's current execution intent, persisted only when a
context boundary requires later resumption.
_Avoid_: spec, proposal, design doc

### Evidence and provenance

**Evidence**: Artifacts that prove completion — test output, review
conclusions, reproduction records. The only basis for VERIFIED and
COMPLETE; self-reports never qualify.（Harness 义；产品域的观察依据见
**Evidence Record**。）
_Avoid_: logs, claims, evidence record

**Evidence packet**: The structured return of the UniClaw debug extension —
evidence summary, failure class, First Divergence Point, owner, root-cause
support, remaining uncertainty, escalation.
_Avoid_: bug report, findings

**Change State**: The durable WHAT/WHY/ACCEPTANCE record of one change
(`changes/`), persisted at variable depth — never a plan, ticket, or ADR.
_Avoid_: ticket, spec, work item

**Smart Zone**: The early, sharp part of a session (~150K tokens, working
value). Past it, work splits into persisted change state plus fresh
subagents instead of stretching the session.
_Avoid_: long context, infinite chat

**NO_REAL_BUYER**: The marker recorded when a required element has no
genuine real-world vehicle, so it is waived honestly rather than fabricated.
_Avoid_: skip, N/A

**Owner / Authority**: The project-declared responsibility for a state or
decision. UniFlow identifies and validates it; it never invents or
overrides it.
_Avoid_: controller

**Vendored skill**: A third-party skill kept verbatim from its upstream
repository, updated only through the installer.
_Avoid_: fork, copy

**Skill provenance**: A skill's origin record — `UPSTREAM_MATT`,
`UPSTREAM_HUMANLAYER`, `LOCAL_UNIFLOW`, `LOCAL_UNICLAW` — carried by
skills-lock.json (upstream) or SKILL.md frontmatter (local).
_Avoid_: attribution file

## Product Domain — Uni Kernel（Evidence & Belief · Control & Effect）

Target Architecture v0.1 的产品域语言（已由 E2B-001 / C2E-002 在
`src/UniClaw.Kernel` 落地的部分）。注意与本仓 Harness 层的
**Evidence**（完成证明工件）区分：产品域的观察依据一律称
**Evidence Record**。

**Evidence Record**: Evidence Ledger admission 通过后形成的不可变
canonical 观察依据记录（EvidenceId + claim + provenance）。
_Avoid_: raw artifact、producer claim、数据、observation

**Admission**: Evidence Ledger 对输入是否具备成为 canonical Evidence
Record 条件的结构性判定（integrity/provenance/来源/时间/scope/lineage），
只判 eligibility，不判真值、相关性或充分性。
_Avoid_: validation、truth judgment、acceptance

**Provenance**: 一条观察「谁产生、何时、声明何 scope、经历何种转换」
的不可变溯源记录。
_Avoid_: metadata、source info

**Belief Relevance**: World Model 对单条 accepted Evidence Record 是否
影响 Current WorldBelief 的独立判定（与 Admission 是两个产出）。
_Avoid_: interest、matching、admission

**Reconciliation**: World Model 将 accepted Evidence 转换为新
WorldBelief revision 的唯一路径；必须显式处理冲突并保留完整 basis。
_Avoid_: update、merge、sync

**WorldBelief**: 系统基于 accepted Evidence 形成的版本化、可修正的当前
世界判断；每次 Reconciliation 产生新 revision，历史 revision 只读。
_Avoid_: world state（整体义）、cache、snapshot

**World State**: WorldBelief 中动态 state/claim 的组成部分
（subject → claim + evidence 溯源），随 revision 一起产生，无独立
current-truth 生命周期。
_Avoid_: parallel truth、belief cache

**Evidence Basis**: 一个 WorldBelief revision 的全部 Evidence Record
引用集合。
_Avoid_: inputs、sources

**Conflict**: 同 subject 的不相容 claim 并存的显式记录（双方 evidence
引用都保留）；Reconciliation 不静默覆盖。
_Avoid_: contradiction error、overwrite

**Slice**: 从某个 WorldBelief revision 派生的 scoped 只读投影；有效性由
source revision 是否仍为 current 派生判定，不存在显式 invalidation
event。原始局部观察输入（Observation Scope/Region）不得称 Slice。
_Avoid_: view、region、observation scope

### Control & Effect（C2E-002 落地）

**Execution Contract View**: Run Model 在 contract 被接受时建立的
immutable canonical view；同 version 重复 admit 幂等复用同一实例。
_Avoid_: contract copy、session config

**Run State**: Run Model 拥有的 canonical 执行状态聚合（Contract View +
Objective + Proof Obligation + Progress；OUT-003 起含 terminal **Outcome
State**）；只经 typed legal transition 更新，不含 action-local assurance
state；terminal 后冻结，不可恢复 active。
_Avoid_: god context、execution log

**Control Intent**: Control Loop 唯一签发的控制产出（observe / act /
recovery）。act-intent 只携带 target hint 与 basis revision，不是
binding，也不是 authorization。
_Avoid_: command、action、instruction

**Tactical Hypothesis**: Control Loop 内部的 disposable 假设，不是事实、
authorization 或 Completion 来源；不得进入 Assurance / Binding /
Reconciliation 的任何输入签名。
_Avoid_: plan、belief、strategy state

**Assurance Judgment**: Assurance 针对特定 input revision 形成的不可变
action-local 判定（admissibility / freshness / safety guard）；input 或
freshness 改变后必须重新判断。
_Avoid_: validation result、gate check、permission

**Candidate Binding**: Grounding Provider 产出的候选目标绑定；未经
Effect Boundary 认定不具任何 dispatch 权威。
_Avoid_: binding、target、resolved element

**Canonical Binding**: Effect Boundary 认定的唯一有效 bounded target
binding，绑定 specific WorldBelief revision；失效为派生判定（无 event）。
_Avoid_: locked target、final binding

**Effect Gate**: Effect Boundary 内只执行或拒绝既有 authorization
judgment 的执法点；不重新判断、不改变 target、不扩大 effect。
_Avoid_: validator、checker、approver

**Effect Receipt**: dispatch 后的不可变投递留痕；是 attempt evidence，
不证明 Effect。producer 前缀 `effect.boundary`、lineage 携 dispatch
引用的回流证据统称 **Attempt Evidence**——admitted 但不产生
effect-claim。
_Avoid_: effect confirmation、result、feedback

### Outcome & Terminal（OUT-003 落地）

**Proof Obligation**: contract/run-level 证明要求（objective / material
effect / completion / failure / safe-stop / escalation 六类）。Run Model
只记录；满足判定由 Assurance 执行。action-local requirements（target
freshness / one-step precondition / admissibility / grounding validity）
属 Assurance 短生命周期 judgment，永不进入 Run State。
_Avoid_: task、checklist、acceptance criteria

**Obligation Fulfillment**: 单条 Proof Obligation 的 evidence-backed 满足
状态（Assurance 判定产出，Run Model 记录）。满足 = current WorldBelief
内存在 accepted Evidence 支持的 subject=value claim（WorldState 或
Conflicts 携带该值，backing EvidenceId ∈ basis）；MaterialEffect 额外要求
backing record 的 producer 前缀 `effect.boundary`（自产观察——**receipt
永不满足 effect obligation**）。
_Avoid_: satisfied flag、done

**Outcome Proof**: Assurance 对 Run-level Proof Obligation State 是否具备
足够 accepted Evidence 支持具体 terminal claim 的终局判断；四分类：
Completion / Failure / SafeStop / Escalation，各自独立 evidence-backed。
「证据不足 / 未知」不是分类成员——由 proof absence 表达，不得伪装成功或
失败。
_Avoid_: completion certificate、result

**Terminal Outcome State**: Run Model 在终局判断接受后记录的 terminal
OutcomeState 快照（Outcome Proof ref + classification + obligation
statuses + evidence refs + unresolved uncertainty）。只记录、不重判；
exact-prior single-winner；至多成功进入一次；terminal 后不可恢复 active。
_Avoid_: result state、final status

**Runtime Outcome**: Uni Kernel 唯一产出的 immutable terminal envelope
（exactly once；Run identity / terminal classification / fulfilled /
unfulfilled obligations / Outcome Proof ref / evidence refs）。只从
Terminal Outcome State 投影；Kernel 不重判完成、不解析 Evidence、不改
classification。
_Avoid_: result、final answer

**Delivery Closure**: terminal 后 Effect Boundary 关闭 external effect
delivery 的机制；任何 dispatch 请求 fail-closed（gate reason
delivery-closed），late candidate binding / late authorization / late
driver callback 均不得恢复 Run。
_Avoid_: lockout、freeze
