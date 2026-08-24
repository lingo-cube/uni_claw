# Proposal: post-action-state-settle

## Buyer

Real-device truth (STATE_EVIDENCE_REQUIRED_REAL_WORLD_BUYER confirmed): after a
state-changing action (SetSwitch) the physical effect is CONFIRMED, the control
candidate exists with valid bounds, and `ImageSwitchStateProvider` correctly
returns True/False on stable frames — but the immediate fresh frame is captured
inside the toggle animation window, where the knob-position analysis returns
null → state evidence unknown → `StateEvidenceRequired` (truthful fail-closed).
`TRANSIENT_EVIDENCE_GAP = CONFIRMED`, `STRUCTURAL = FALSE`.

This change purchases ONLY a bounded post-action settle / fresh re-observation
policy (TIMING / VERIFICATION MECHANICS). It does NOT redesign perception, weaken
StateEvidenceRequired, add LLM Assistance, or invent action-specific sleeps.

## Gap (verified repository truth)

- `Traversal.ExecuteLoweredActionAsync` already owns execution-verification retry
  mechanics: B4 / SC-P2-002 step-scope retry (Select phase, bounded re-observe +
  re-resolve, `RetryCount` on the journal entry). The **Verify phase after a
  state-changing Execute has NO settle/re-observe** — the fresh frame is consumed
  as-is, so animation-window frames lose state evidence.
- `NavigationTransitionSettle` (Agent, navigation phase) is the architectural
  precedent for RESULT-EVALUATING settle: bounded re-observe until the transition
  is PROVEN (not fixed-time-only), 500ms × 4. It is nav-specific; the same
  semantic (evaluate fresh evidence, not elapsed time) should be applied to
  post-action state evidence, but with its own timing/eligibility (no magic-number
  copy).
- `StateEvidenceRequired` becomes terminal in `Agent.SemanticRun` when
  `currentBelief is null` after a dispatched semantic action.

**Earliest missing system link**: `POST_ACTION_STATE_EVIDENCE_SETTLE` in the
execution verification mechanics (Traversal Verify phase).

## What this change does (BASELINE — design/spec only, APPLY later)

1. Freezes the owner: **Traversal execution-verification mechanics** (the existing
   owner of step-scope retry / Verify), NOT Agent semantic code, NOT Environment.
2. Defines post-action settle semantics: dispatch state-changing action → fresh
   Observation → state evidence available? YES → verify normally; NO but
   transient-eligible → bounded settle + fresh re-observation; NO → existing
   fail-closed. Every retry requires a strictly fresh Observation.
3. Defines eligibility (generic predicate, not `if action == SetSwitch`):
   action actually dispatched + state-changing/verification-sensitive + fresh
   observation exists + target binding/control remains identifiable + required
   state evidence temporarily unavailable + no contradiction proves failure +
   retry budget remains.
4. Chooses the stopping rule from real toggle-animation evidence: **D. HYBRID**
   (immediate observe, then bounded retry until valid evidence or budget
   exhausted) — least expensive policy that stays truthful.
5. Freezes timing/budget as COMPOSITION_POLICY (not semantic contract): maximum
   re-observation count, delay policy (evidence-evaluating, initial values from
   toggle-animation measurement + NavigationTransitionSettle precedent), maximum
   additional verification duration. No unbounded retry; no interaction with
   MaxAssistanceConsults.
6. Fixes action scope: **B. state-changing SemanticActions with missing post-action
   state evidence** (narrowest repository-evidenced scope; not all actions).
7. Fixes failure semantics: budget exhausted → the SAME truthful
   `StateEvidenceRequired` (never converted to success/contradiction/consultation/
   guessed state).
8. Freezes L1: `L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED`; this repair closes normal
   state transitions locally (L0 closes locally).

## Non-goals

- Perception redesign; StateEvidenceRequired weakening; LLM Assistance; L2/L3;
  new recommendation kinds; action-specific sleeps as policy style; generalized
  temporal filtering; semantic authority changes.

## Required output

`PROJECT_LEADER_POST_ACTION_STATE_SETTLE_BASELINE_RESULT` with Decision
`POST_ACTION_STATE_SETTLE_READY_FOR_APPLY`, the OpenSpec change
(proposal/design/spec/tasks) created and validated, and `NEXT_GATE =
PROJECT_LEADER_APPLY_POST_ACTION_STATE_SETTLE`.

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | wrong owner | post-action settle is placed in Agent semantic code or Environment |
| F2 | assumed success | the policy treats the action as succeeded or synthesizes SwitchState |
| F3 | stale retention | any prior SwitchState/binding/GoalEvidence is reused as current truth |
| F4 | null→desired | null state is converted to the desired value |
| F5 | time-as-evidence | elapsed time is treated as GoalEvidence |
| F6 | unbounded retry | re-observation count is unbounded |
| F7 | action-specific sleeps | policy style is `if action == X { sleep(...) }` instead of a truthful generic predicate |
| F8 | L1 coupling | the repair depends on Assistance or changes MaxAssistanceConsults |
| F9 | fail-closed weakening | budget exhaustion returns anything other than the existing truthful terminal behavior |
| F10 | stale sequence | a retry reuses an Observation without strictly advancing SequenceNumber |

## Validation

- `openspec validate post-action-state-settle --strict --no-interactive`
- `scripts/check-consistency.sh`
- Cross-check against `docs/decisions/state-evidence-required-real-world-buyer.md`.
