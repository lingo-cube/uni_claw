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

The Session-level Primary Goal is distinct from the bounded Execution Goal in a
Directive. Supervisory Plan remains UniAgent-local, while RuntimeAgent may
maintain a revisable Runtime-local Plan. The protocol preserves the frozen
four-field `run.start` Directive and additively admits one typed, bounded
StrategyDirective at Run start through `run.strategy.start`; this does not carry
the Supervisory Plan or permit mid-Run redirection. Agent Loop is not Run; a
post-terminal retry dispatch requires a separately authorized new Run model.
Runtime Outcome and UniAgent Goal
Evaluation are separate layers. GoalEvaluation and UniAgent Decision have a
frozen semantic contract but no current DTO/store representation; UniAgent Trace
remains a missing contract.

Sources: [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Decision + Goal Evaluation minimum contract](uniagent-decision-goal-evaluation-minimum-contract.md).

## Container

Containers own page-local runtime state and organize local traversal. The Agent
retains global semantic authority, including active-container management and
agent-level recovery.

Source: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## Evidence

Observation is evidence rather than truth. RuntimeAgent rebuilds WorldBelief
from fresh observation, and completion is decided from GoalEvidence.

Sources: [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

Observation is produced through the Environment boundary and consumed by
RuntimeAgent. Facts are append-only records; WorldBelief is a revisable current
projection rather than a replacement of Fact history.

Source: [Agent Concept Model v1](agent-concept-model-v1.md).

Harness now provides an external, failure-isolated capture and replay path:
ordered public environment evidence can be frozen and atomically persisted,
while reviewed Scenario assets are resolved explicitly through a validated
catalog. This path does not own Runtime belief, action authorization,
GoalEvidence, completion, or Scenario selection from intent.

Finalized hierarchical traces are also available through a bounded in-process
read model for one explicitly registered `runId`: callers may inspect trace
metadata and stable cursor-paged spans without creating Runtime truth. Harness
can independently read one explicitly named published capture and its optional
TraceRun through a fail-closed validation boundary. Neither capability adds a
DriverHost wire operation, inferred lookup, replay authority, or persistence
mutation.

Sources: [Trace Capture architecture gate](../decisions/trace-capture-scenario-catalog-architecture-gate.md), [capture foundation change](../../openspec/changes/trace-capture-scenario-catalog-foundation/), [trace/span read-model change](../../openspec/changes/trace-span-read-model/).

## Vision

Vision is the primary perception path. Screenshot-derived observation is the
primary grounding authority for candidate discovery, DFS progress, and
verification. ADB UI hierarchy is an optional auxiliary observation source: it
may corroborate a primary Vision occurrence or carry structural metadata, but it
is never a required dependency, never an equivalent primary channel, and can
never independently authorize, verify, complete, or trigger a lifecycle
transition.

## External Semantic Capability

External Scenario Knowledge Packages and Semantic Capability Bindings own
scenario interpretation (page classifiers, locale rules, preference-row and
relation recognition) and emit typed candidate evidence only. They hold no
execution, authorization, completion, FSM, Traversal, GoalEvidence, or
Run-start authority.

Runtime owns evidence admission, fusion, and reconciliation: canonical
occurrence normalization, source-tier preservation, contradiction handling, and
WorldBelief proposal. The Agent retains authorization, verification, completion,
and terminal-state ownership. RuntimeAgent keeps bounded reasoning and
reconciliation support only.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md),
[Protocol v1](uniagent-protocol-v1-consolidation-design.md),
[semantic-perception-layer-baseline decision](../decisions/semantic-perception-layer-baseline.md),
[runtime-external-semantic-capability-boundary change](../../openspec/changes/runtime-external-semantic-capability-boundary/).

## DSH

DSH is an implementation framework / composition host. It hosts implementation
surfaces without becoming an independent execution-truth authority.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Governance

Canonical architecture and protocol sources remain the authority for their
respective levels. This file is only a current-state projection and introduces
no gate, invariant, or lifecycle conclusion.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md).
