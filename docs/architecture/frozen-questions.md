# Frozen Questions Registry

> DocumentType: FROZEN_QUESTION_INDEX
> Authority: `NONE`
> Scope: This registry records existing decisions for retrieval. It does not
> answer new questions, amend a baseline, or create an authority source.

## RuntimeAgent Completion Authority

**Question:** Who may complete a RuntimeAgent Run?

**Decision:** RuntimeAgent owns the terminal outcome; completion is decided
from GoalEvidence by the Agent within the RuntimeAgent boundary.

**Source:** [UniAgent Protocol v1](uniagent-protocol-v1-consolidation-design.md)
(ownership matrix: Run lifecycle, GoalEvidence, and terminal outcome); [Current
Architecture State](current-architecture-state.md).

**Reopen Requirement:** Amend the frozen Architecture v1 / Protocol v1 baseline
through its applicable approved change process.

## Vision Decision Authority

**Question:** Does Vision decide Runtime truth or execution?

**Decision:** Vision is a capability surface. Its advice or evidence is
advisory-only; RuntimeAgent retains accept, reject, and reconcile ownership.

**Source:** [UniAgent Architecture v1](uniagent-architecture-v1-core-development-guide.md)
(Vision capability boundary); [UniAgent Protocol v1](uniagent-protocol-v1-consolidation-design.md)
(capability advice/evidence ownership).

**Reopen Requirement:** Amend the frozen Architecture v1 / Protocol v1 baseline
through its applicable approved change process.

## DSH Authority Boundary

**Question:** Does DSH define architecture or acquire Runtime execution truth?

**Decision:** DSH is an implementation framework / host, not an architecture
concept or an independent Runtime execution-truth authority.

**Source:** [UniAgent Architecture v1](uniagent-architecture-v1-core-development-guide.md)
(DSH relationship); [Current Architecture State](current-architecture-state.md).

**Reopen Requirement:** Amend the frozen Architecture v1 baseline through its
applicable approved change process.

## Evidence Truth Authority

**Question:** Is external observation or capability output semantic truth?

**Decision:** Observation is evidence rather than truth. Capability advice and
evidence are advisory-only; RuntimeAgent owns reconciliation and the resulting
world-belief decisions.

**Source:** [UniAgent Protocol v1](uniagent-protocol-v1-consolidation-design.md)
(ownership matrix); [Current Architecture State](current-architecture-state.md).

**Reopen Requirement:** Amend the frozen Architecture v1 / Protocol v1 baseline
through its applicable approved change process.

## GoalEvidence Ownership

**Question:** Who owns and consumes GoalEvidence?

**Decision:** GoalEvidence is RuntimeAgent kernel evidence; the Agent decides
from it, and it is the completion basis inside the RuntimeAgent boundary.

**Source:** [UniAgent Protocol v1](uniagent-protocol-v1-consolidation-design.md)
(GoalEvidence ownership matrix); [Current Architecture State](current-architecture-state.md).

**Reopen Requirement:** Amend the frozen Architecture v1 / Protocol v1 baseline
through its applicable approved change process.
