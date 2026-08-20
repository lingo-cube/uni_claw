# Context Loading Guide

> DocumentType: CONTEXT_LOADING_GUIDE
> Authority: `NONE`
> Scope: Retrieval guidance only. This document introduces no architecture rule,
> authority, gate, lifecycle transition, or semantic interpretation.

## Default Loading Order

| Level | Load | Purpose |
|---|---|---|
| **0 — Architecture Constitution / Index** | [canonical architecture index](architecture/README.md) and the repository constitution / routing entry points it names | Establish the approved baseline hierarchy and task routing. |
| **1 — Architecture v1** | [UniAgent Architecture v1](architecture/uniagent-architecture-v1-core-development-guide.md) | Load for architecture work; it is the sole active top-level architecture baseline. |
| **2 — Protocol v1 / Domain Contract** | [UniAgent Protocol v1](architecture/uniagent-protocol-v1-consolidation-design.md) and the task-relevant domain contract | Load only the protocol or contract required by the task domain. |
| **3 — Current Architecture Projection** | [current architecture state](architecture/current-architecture-state.md) and the relevant topic projection | Obtain the current-state summary. Projections carry no independent authority. |
| **4 — Snapshot + Active Gates** | [latest snapshot](snapshots/latest.md), [current gates](work/active/current-gates.md), and task-relevant active OpenSpec artifacts | Obtain the current lifecycle view and only the active change directly relevant to the task. |
| **5 — Historical Retrieval** | [decision registry](decisions/index.md), [failure index](failures/index.md), and archived OpenSpec records | Retrieve evidence by decision, capability, scenario, gate, failure/falsifier, successor, or current reference. |

## Retrieval Boundary

The default context is Levels 0–4 plus only the active OpenSpec artifacts that
are relevant to the task. Level 5 is retrieved on demand; it is not a default
full-history load.

## Source Relationship

This guide is a retrieval projection of the [canonical architecture
index](architecture/README.md) and the approved lifecycle current-state
decision. It does not replace Architecture v1, Protocol v1, a domain contract,
or the source evidence referenced by a projection.

## Task Routing Matrix

Use the [Knowledge Map](knowledge-map.md) to locate the listed sources. This
matrix preserves the Level 0–5 order above: process skills may guide retrieval,
but have `Authority: NONE` and do not replace a project source.

| Task Type | Minimum Context | Project Sources | Historical Retrieval Trigger |
|---|---|---|---|
| Architecture | L0–L3 | [Architecture index](architecture/README.md), [Architecture v1](architecture/uniagent-architecture-v1-core-development-guide.md), [current architecture state](architecture/current-architecture-state.md) | A predecessor, conflict, or evidence citation is required. |
| Protocol | L0–L3 | [Architecture index](architecture/README.md), [Protocol v1](architecture/uniagent-protocol-v1-consolidation-design.md), task-relevant domain contract, relevant projection | A protocol decision or prior contract evidence is required. |
| Runtime Implementation | L0–L4 | Architecture authority, task-relevant domain contract, relevant projection, [current gates](work/active/current-gates.md), relevant active OpenSpec | A gate, scenario, failure, or decision is cited by the active work. |
| Bug Investigation | L0–L4 | Relevant projection, [failure index](failures/index.md), relevant active OpenSpec, available evidence | The failure index or current evidence points to a historical decision, scenario, or archive. |
| Documentation / Knowledge | L0, L3–L4 | [Context Loading Guide](context-loading-guide.md), [current architecture state](architecture/current-architecture-state.md), [latest snapshot](snapshots/latest.md), [current gates](work/active/current-gates.md) | Traceability, source verification, or a historical record is required. |
| Research | L0–L3 | Architecture authority, task-relevant protocol or domain contract, relevant projection | Existing project evidence or a prior decision is needed for comparison. |
| Project Continuation | L0, L3–L4 | [latest snapshot](snapshots/latest.md), [current architecture state](architecture/current-architecture-state.md), [current gates](work/active/current-gates.md), relevant active OpenSpec | The current sources name or require evidence from a historical record. |

Historical decisions, failure records, and archived OpenSpec are not loaded by
default for any task type. Retrieve them at Level 5 only when the listed
trigger applies.
