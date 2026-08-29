# runtime-viewport-exhaustion-confirmation

> Status: **WITHDRAWN (2026-08-28, per Human Gate quiescence-admission proposal; hypothesis refuted by the erratum test — kept as history of the RED-first discipline working). Prior note: design/spec complete; implementation NOT authorized — separate
> Human Gate required per ruling §10).** Phase 2.6 remains STOPPED.

Human Gate `PROJECT_LEADER_RUNTIME_VIEWPORT_EXHAUSTION_CONFIRMATION_CONTRACT_GATE`
(2026-08-28): `IR-G1 = AUTHORIZE_RUNTIME_CONTRACT_CHANGE`. Core directive: the Runtime
must distinguish **"I have not found anything new yet"** from **"I have confirmed, with
fresh, stable, consistent evidence, that there is genuinely nothing new here."**

- **Capability purchased**: `VIEWPORT_EXHAUSTION_CONFIRMATION` (only this).
- **Closed semantics**: EXTENDING_WINDOW / CONSISTENT_CONFIRMATION_WINDOW /
  UNRESOLVED_WINDOW; zero-new-source alone is never exhausted.
- **Confirmation conditions**: fresh + identical-to-predecessor sequence + contiguous
  union-tail suffix + zero new sources + exact identity/no conflicts + bounded
  consecutive count (2). Any miss → unresolved.
- **Inertness**: confirmations touch no DISCOVERED/GROUNDED/CURRENTLY_VISIBLE/
  AUTHORIZED/VISITED/COMPLETED set; no GoalEvidence; no completion; no dispatch
  authority.
- **Owner choice**: Option A (normalizer owns the classification) — Option B rejected
  because a composition-provided evaluator must not pre-know the exhaustion truth only
  the Runtime proof establishes.
- **Deltas**: AuthorityDelta NONE · RuntimeBehaviorDelta PRESENT (normalization
  classification + completeness evidence recording) · ArchitectureDelta
  ADDITIVE_INTERNAL (no wire/API/surface change).
- **STOP-2 coverage**: the exact failing window sequence (extensions → identical
  terminal pair) is the deterministic reproduction the new contract must pass.
- **Phase 2.6 resume**: only after implementation + regression + independent
  graduation; fresh Stage-A reentry from the STOP-2 layer, never mid-stage.

Artifacts: `proposal.md` · `design.md` (evidence→FDP→owner, conditions, invariants,
owner analysis, spec→symbol→test mapping) · `specs/.../spec.md` (5 requirements, 10
counter-example scenarios, non-claims) · `tasks.md` (design stage complete;
implementation gated).
