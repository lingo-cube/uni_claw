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

## Product Domain — Uni Kernel（Evidence & Belief）

Target Architecture v0.1 的产品域语言（已由 E2B-001 在
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
