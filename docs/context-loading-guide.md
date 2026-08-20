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
