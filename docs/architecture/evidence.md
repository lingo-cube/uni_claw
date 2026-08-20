# Evidence

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md)

## Evidence route

The current evidence route is Raw → Structured → Semantic → GoalEvidence.
These labels describe stages of evidence handling; this document introduces no
new evidence schema or contract.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## Observation and belief

Observation is evidence, not semantic truth. RuntimeAgent forms WorldBelief by
reconciling fresh observation and evidence; historical state does not substitute
for fresh observation.

Sources: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [Architecture v1](uniagent-architecture-v1-core-development-guide.md).

## Dispatch and truth

Physical dispatch is not truth or completion. It is followed by observation,
verification, and reconciliation within the RuntimeAgent boundary.

Source: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## GoalEvidence

GoalEvidence is RuntimeAgent-owned, kernel-only completion evidence. The Agent
decides completion from GoalEvidence; external consumers receive only
producer-derived information and do not re-originate truth.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).
