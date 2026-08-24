# Agent Concept Model v1 Alignment — Graduation Decision

> Status: GRADUATED (documentation-only closeout) | Decision: `GRADUATE_AGENT_CONCEPT_MODEL_V1_ALIGNMENT` | Date: 2026-08-21
> Change: `openspec/changes/agent-concept-model-v1-alignment/`
> Authority: Architecture v1 and Protocol v1 remain the governing baselines.

## 1. Buyer and exact claim boundary

**Buyer:** UniAgent / RuntimeAgent concept alignment for DSH / UniClaw architecture mapping.

This receipt claims only that the Agent Concept Model v1 terminology, ownership boundaries,
lifecycle relations, layered Runtime Outcome / Goal Evaluation semantics, and minimum
Observation / Fact / Evidence / Trace distinctions have been documented and aligned as a
subordinate model. It is documentation-only. It claims no wire contract, code, DTO, Data
Plane, database schema, event-sourcing implementation, multi-run / SubRun model, or
non-terminal escalation transport implementation.

Architecture v1 and Protocol v1 remain the governing baselines; this model does not create a
second architecture or protocol authority.

## 2. Validation evidence

- Main capability spec synchronized at `openspec/specs/agent-concept-model-v1/spec.md`.
- Detailed subordinate model: `docs/architecture/agent-concept-model-v1.md`.
- Alignment design and tasks record the nine collision resolutions and the deferred scope.
- Strict OpenSpec validation: **PASS** (`openspec validate agent-concept-model-v1-alignment --strict`).
- Documentation consistency checks: **PASS** (`scripts/check-consistency.sh`).
- Formatting check: **PASS** (`git diff --check`).

## 3. Scenario receipts and falsifiers

| Scenario | Receipt | Falsifier result |
|---|---|---|
| SC-A — Runtime `Completed`, Goal `Unsatisfied` | Runtime Outcome remains RuntimeAgent-owned; UniAgent records independent Completion and Satisfaction dimensions. | **Not falsified**: no RunState or GoalEvidence rewrite is claimed or introduced. |
| SC-B — Runtime `Failed`, revised Directive | Failure remains append-only; UniAgent may record a Retry Decision/candidate Directive, but current v1 does not dispatch it after terminal failure. | **Not falsified**: executing the candidate is explicitly reserved for a new Run gate; no independent second Run or physical-action wire semantics were introduced. |
| SC-C — `AssistanceRequired` with operator judgment | Classified as non-terminal supervisory escalation; Runtime authority and truthful terminal outcome remain intact. | **Not falsified**: existing assistance polling/resolution is not claimed as UniAgent escalation transport. |

## 4. Deferred scope

The following remain outside this graduation and require separate authorization: Task user
semantics; Session / Run / SubRun persistence models; Data Plane; Fact / Trace schemas;
event sourcing; Memory storage or compression; DSH UI and Agent Hook implementation; Plan
DTOs or wire fields; cancellation/interruption lifecycle additions; generic multi-agent
orchestration; and non-terminal escalation transport.

## 5. Final lifecycle conclusion

The alignment change is **GRADUATED as documentation-only**. The main spec is synchronized,
the subordinate concept model is bounded by Architecture v1 and Protocol v1, and the three
semantic scenarios have explicit non-falsified boundaries. The change may proceed to the
normal archive step; archive is a lifecycle operation and does not authorize implementation
of any deferred scope.
