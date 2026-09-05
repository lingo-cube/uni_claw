# UniClaw Development Harness

The repository-scoped AI-coding harness for UniClaw development: one
canonical workflow (UniFlow), vendored upstream skills, and portable
delegation contracts. Product architecture lives outside this context;
this glossary only defines how development work is described and controlled.

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
COMPLETE; self-reports never qualify.
_Avoid_: logs, claims

**Evidence packet**: The structured return of the UniClaw debug extension —
evidence summary, failure class, First Divergence Point, owner, root-cause
support, remaining uncertainty, escalation.
_Avoid_: bug report, findings

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
