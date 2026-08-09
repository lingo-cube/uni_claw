## Context

The frozen Phase 2 vocabulary already represents external evidence (`Observation`), semantic page belief (`WorldBelief.SemanticPage`), foreground application, active Container identity, Container-local progress, `Container.IsStillMine`, structured `Trap` evidence with `TrapScope.Container`, `RecoveryResult`, local Execute → Observe → Verify, and Agent-owned escalation and completion decisions. Phase 2 intentionally reserved Container-scope Trap emission and Popup local recovery for Phase 3.

SC-P3-002 is a behavior-only purchase. A Popup or Overlay can block interaction while the underlying semantic page remains the same logical Container. The current Runtime has no Container-scope path that can handle that obstruction, obtain fresh post-handling evidence, preserve local progress when continuity is proven, and escalate when it is not. Ordinary local failure currently reaches Agent failure without this distinction.

The frozen Recovery component remains the owner of its existing Agent-scope recovery mechanism state, and Agent remains the authority for initiating that recovery, changing the active Container binding, evaluating GoalEvidence, and deciding final Run state. SC-P3-002 does not turn local Popup handling into a new Recovery component and does not permit Recovery to depend on Container or Traversal.

## Goals / Non-Goals

**Goals:**

- Treat supported Popup/Overlay evidence as a Container-scope obstruction hypothesis rather than immediate proof of semantic-page drift.
- Allow only bounded local handling through the existing Container-local execution direction.
- Require a fresh Observation after local handling and prove continuity from existing semantic identity evidence.
- Preserve the same Container and its existing local progress when continuity is proven.
- Escalate structured evidence to Agent when dismissal or continuity verification cannot be proven.
- Preserve deterministic ActionHistory, Observation, journal, Trace, evidence, and final-state assertions with a zero production-model delta.

**Non-Goals:**

- Introduce a Popup/Overlay production model, Popup manager, recovery engine, planner, FSM, or generic recovery framework.
- Change the frozen Recovery ownership split or the Recovery → Container/Traversal prohibition.
- Add a production type, field, enum value, interface, component, mutable state, Fingerprint, Confidence mechanism, generic retry, or generic uncertainty abstraction.
- Define Popup classification algorithms, real-device/Vision behavior, Scroll identity, multi-container progress, or SC-P3-003 behavior.
- Let Container rebind/invalidate the active Container, initiate Agent recovery, evaluate Goal completion, or terminate the Run.

## Decisions

### A local obstruction is a Container-scope hypothesis, not immediate Agent drift

Environment supplies Observation and dispatch outcomes, and Traversal supplies mechanical execution evidence. Container is the semantic authority that classifies those facts relative to its current page-local state. When the evidence is consistent with a local Popup/Overlay obstruction, Container may classify it as Container scope and authorize bounded local handling without asserting that the underlying page changed.

The obstruction Observation is still evidence rather than semantic truth. An obscured or temporarily unresolvable semantic page does not by itself authorize a global Container transition. Conversely, Popup-shaped evidence does not prove continuity; continuity must be re-established after handling.

Alternative rejected: immediately route every Popup-shaped or Unknown observation to Agent recovery. That violates the approved Scenario and the Charter rule that lower scope may recover locally.

### Local handling reuses existing execution direction without redefining Recovery

The local authority is limited to attempting approved, bounded obstruction handling through the existing Container → Traversal → Environment direction. It must not perform unbounded retries, invoke the frozen Recovery component as a Container mechanism, or execute Agent-scope restoration.

The frozen Recovery component continues to own only its existing recovery mechanism state and remains independent of Container and Traversal. Agent continues to decide whether escalated evidence requires rebind, Agent recovery, or failure.

Alternative rejected: add a PopupManager, PopupRecoveryEngine, or make Recovery depend on Container/Traversal. No approved semantic requires a new component, and the latter would violate the frozen Phase 2 boundary.

### Continuity requires fresh external evidence plus existing semantic identity evidence

After local handling, the Runtime must obtain an Observation whose sequence strictly advances beyond the obstruction evidence. Local continuity is proven only when the fresh Observation has a compatible foreground application, `Container.IsStillMine` accepts it, and reconciled semantic-page evidence does not contradict the active Container.

When those conditions hold, the same active Container remains bound and its pre-obstruction local progress is preserved. The Runtime must not call a progress-resetting rebind operation merely to represent successful local handling.

Same screenshot, same Observation object, or a visual Fingerprint is not Container identity proof. This change does not purchase Fingerprint.

Alternative rejected: treat successful dismiss dispatch as recovery success. Dispatch outcome is not world success; fresh Observation and verification are required.

### Failed local proof escalates evidence; Agent owns the higher decision

If handling cannot be performed, the post-handling Observation is absent or stale, the foreground application is incompatible, `IsStillMine` rejects the Observation, or reconciled semantic evidence remains Unknown/conflicting, Container cannot declare local success. It must preserve existing progress, avoid further blind handling, and escalate structured evidence.

Container owns the decision that its local proof is insufficient. Agent owns the separate higher-scope decision to keep or change the active binding, initiate Agent recovery, or fail the Run. Goal completion remains exclusively based on Agent-consumed GoalEvidence.

Alternative rejected: encode `LOCAL_OBSTRUCTION_HANDLED` or `ESCALATE_TO_AGENT` as a new enum. Existing results, Trap scope, evidence, and control flow are sufficient for this Scenario.

### Deterministic proof uses existing evidence surfaces

The deterministic Fake must express both a dismissible Popup over a continuous underlying Container and a branch where handling or continuity verification fails. Equal RunId, input world, and action sequence must replay to equal ActionHistory, Observation sequence, journal, Trace, evidence, preserved progress, and final Run state using existing production vocabulary.

Alternative rejected: add a production identity or confidence surface solely to simplify tests. Test convenience does not purchase production semantics.

## Risks / Trade-offs

- [Risk] The obstruction Observation may make semantic-page resolution temporarily Unknown. → [Mitigation] Treat obstruction as a bounded local hypothesis only; require unambiguous fresh evidence after handling or escalate.
- [Risk] Rebinding the same Container through the current bind operation could reset local progress. → [Mitigation] The continuity requirement preserves the same active Container and forbids silent progress reset on the verified-positive branch.
- [Risk] Local handling could grow into an implicit generic recovery framework. → [Mitigation] Limit this change to SC-P3-002 Popup obstruction behavior and reject new components, generic policy, and mutable state.
- [Risk] Existing vocabulary may prove insufficient during implementation planning. → [Mitigation] Stop and return to Semantic/Architecture Gate before tasks or production changes if a model, ownership, or authority delta becomes unavoidable.
