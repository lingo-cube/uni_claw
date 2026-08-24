## Context

See `proposal.md` for motivation. Architecture v1 already freezes UniAgent supervisory autonomy, RuntimeAgent bounded autonomy, Session correlation, and a single Primary Run. Protocol v1 already freezes Directive, Outcome, Capability, Session, and `Escalation != TerminalOutcome`. The alignment draft shows that the proposed Agent vocabulary is mostly additive, but several names would collide with existing ownership if copied without qualification.

The change is documentation-only. It must preserve the single active Architecture v1 and Protocol v1 authority hierarchy and must not imply that a documented concept already has a DTO, database model, transport, or production implementation.

## Goals / Non-Goals

**Goals:**

- Ratify one subordinate Agent Concept Model v1.
- Resolve the nine collisions recorded by the alignment draft.
- Make DSH / UniClaw object mappings explicit and source-linked.
- Validate the model against three end-to-end semantic scenarios.
- Amend current architecture/protocol text without changing wire or Runtime behavior.

**Non-Goals:**

- Task semantics, Session/Run database schemas, Data Plane, event sourcing, Memory persistence, DSH UI, or Agent Hook implementation.
- New Run states, retry APIs, Plan DTOs, escalation transport, cancellation, multi-run, SubRun, BranchRun, or generic multi-agent orchestration.
- A second top-level architecture or protocol baseline.

## Decisions

### Decision 1: Subordinate amendment, not parallel baseline

The final `agent-concept-model-v1.md` is governed by Architecture v1 and Protocol v1. The canonical index lists it as a frozen subordinate semantic model after ratification. Architecture and Protocol receive short amendments so the new document cannot be interpreted as independent authority.

Alternative rejected: leave the draft as a free-standing top-level baseline. That would violate the one-architecture / one-protocol rule.

### Decision 2: Two goal levels

Use `Primary Goal` for the Session-level user expectation owned and evaluated by UniAgent. Use `Execution Goal` for the bounded target inside a Runtime Directive. Existing code names remain unchanged; the distinction is conceptual.

Alternative rejected: equate `SemanticGoalInput` with the entire Primary Goal. It cannot represent satisfaction, broader constraints, or a multi-decision supervisory context.

### Decision 3: Plans remain on their owning side

UniAgent may maintain a Supervisory Plan. RuntimeAgent may maintain a Runtime-local Plan. Only the bounded Directive crosses current Surface A. Plans do not acquire truth status and do not add wire fields.

Alternative rejected: serialize the Abstract Plan now. There is no buyer for a Plan contract and Protocol v1 explicitly keeps strategy-alteration messages reserved.

### Decision 4: Three result layers

Keep request acceptance, Runtime Outcome, and Goal Evaluation separate. Current terminal Runtime outcomes remain `Completed` and `Failed`; `Cancelled` and `Interrupted` remain conceptually valid future outcomes but are not claimed as realized. UniAgent Goal Evaluation uses Completion and Satisfaction dimensions and cannot modify Runtime truth.

Alternative rejected: one boolean or one shared `Result` enum. It loses request/run/evaluation ownership and makes contradictory-but-valid outcomes impossible to express.

### Decision 5: AssistanceRequired is non-terminal

Map `AssistanceRequired` to the already-frozen non-terminal Escalation semantic. The current fail-closed `RunFailed` path stays truthful, but it is not renamed to non-terminal assistance. Transport remains reserved.

Alternative rejected: add AssistanceRequired as a terminal RunState now. That collapses Protocol invariant PI-9 and invents lifecycle behavior without implementation evidence.

### Decision 6: Fact history and belief projection are different

Facts and producer records are append-oriented. WorldBelief and latest summaries are current projections that may change when fresh evidence arrives. Observation authorship stays with Environment/perception; reconciliation stays with RuntimeAgent.

Alternative rejected: a generic mutable Fact object. It erases evidence history and creates an ambiguous second state store.

### Decision 7: Correlated trace domains, not shared mutable trace

RuntimeAgent Trace and UniAgent Trace are producer-scoped. They correlate through Session and reference identities. The model freezes Trace/Span/Event meanings but no persistence, sampling, replay, or reference schema.

Alternative rejected: define a universal Trace store in this change. That belongs to the Data Plane and the separately gated trace-capture work.

### Decision 8: Agent Loop is not Run

Agent Loop describes bounded agent activity; Run is RuntimeAgent's authoritative execution lifecycle and identity. Repeated pre-terminal internal cycles do not create new Runs. After `Completed` or `Failed`, UniAgent may record a Retry Decision and candidate revised Directive, but executing it requires a separately authorized new Run model. The current v1 cardinality remains `1 Session / 1 Primary Goal / 1 Primary Run`.

Alternative rejected: interpret each loop activation or retry as an independent Run. That would silently open Multi-Run and SubRun extensions.

### Decision 9: Specialist SubAgent is a fixed relationship

RuntimeAgent may be described as UniAgent's specialist execution SubAgent only for the fixed architecture relationship. The term does not authorize general spawning or agent graphs.

Alternative rejected: ban the SubAgent description entirely. The existing DSH run-entry buyer already uses the useful specialist relationship, and the authority boundary can be stated without opening general orchestration.

## Scenario validation design

### SC-A: Runtime Completed, Goal Unsatisfied

The Runtime Run ends `Completed` from GoalEvidence. UniAgent evaluates broader Primary-Goal constraints and records `Completion = Completed`, `Satisfaction = Unsatisfied`. Falsifier: changing RunState or GoalEvidence to match the evaluation.

### SC-B: Runtime Failed, revised Directive

The original `Failed` outcome remains immutable. UniAgent may record a supervisory Retry Decision and candidate revised Directive serving the same Primary Goal, but current v1 does not dispatch it after the terminal outcome. Executing the candidate requires a separately authorized new Run model. Falsifier: deleting the failure, dispatching the candidate as a second Run without a gate, or supplying physical actions.

### SC-C: AssistanceRequired with operator judgment

RuntimeAgent signals a semantic need for supervisory adjudication. The signal is not terminal, no completion is fabricated, and RuntimeAgent retains grounding and final outcome authority. Current transport remains `SEMANTICALLY_FROZEN_NOT_YET_REALIZED`. Falsifier: claiming the existing capability-assistance poll/resolve path already implements UniAgent non-terminal escalation.

## Risks / Trade-offs

- **Risk: terminology freeze is mistaken for implementation completion** → Every mapping is classified as aligned, partial, missing contract, or reserved; projections cite the source.
- **Risk: Goal Evaluation becomes a second Runtime truth** → Architecture and Protocol amendments explicitly preserve Runtime-owned RunState and GoalEvidence.
- **Risk: retry language opens Multi-Run** → Scenario SC-B allows recording the Retry Decision but explicitly reserves post-terminal dispatch for a new Run gate.
- **Risk: document duplication drifts** → The concept document is the detailed subordinate source; Architecture/Protocol amendments are short ownership anchors and the index declares the hierarchy.
- **Trade-off: no GoalEvaluation DTO yet** → Semantic ownership is frozen first; implementation waits for the separately proposed minimum-contract buyer.

## Migration Plan

1. Finalize and rename the alignment draft as `agent-concept-model-v1.md`.
2. Add bounded amendments to Architecture v1 and Protocol v1.
3. Update canonical navigation and source-linked projections.
4. Run OpenSpec strict validation and documentation consistency checks.
5. Graduate and sync the new capability spec only after the documentation assertions match the frozen sources.

Rollback is documentation-only: revert the amendment and retain this OpenSpec change as the evidence record. No production state or wire migration is involved.
