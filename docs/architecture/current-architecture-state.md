# Current Architecture State

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md)
> Scope: Current architecture only. This is a retrieval projection, not an architecture baseline or a source of normative requirements.

## Runtime

The RuntimeAgent is the bounded execution authority. It owns execution truth,
reconciliation, terminal outcome, and GoalEvidence-based completion. Its
responsibility spine is Agent → Container → Traversal → Environment.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## Agent

UniAgent supplies bounded directives and consumes RuntimeAgent-produced
outcomes. It has no physical, belief, binding, or GoalEvidence authority within
the RuntimeAgent execution boundary.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Container

Containers own page-local runtime state and organize local traversal. The Agent
retains global semantic authority, including active-container management and
agent-level recovery.

Source: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## Evidence

Observation is evidence rather than truth. RuntimeAgent rebuilds WorldBelief
from fresh observation, and completion is decided from GoalEvidence.

Sources: [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## Vision

Vision is a capability surface. Capability evidence or advice is advisory-only;
RuntimeAgent retains accept, reject, and reconcile ownership.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## DSH

DSH is an implementation framework / composition host. It hosts implementation
surfaces without becoming an independent execution-truth authority.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Governance

Canonical architecture and protocol sources remain the authority for their
respective levels. This file is only a current-state projection and introduces
no gate, invariant, or lifecycle conclusion.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md).
