# post-action-state-settle

**Status**: IMPLEMENTED (APPLY gate 2026-08-17). **State**: design/spec/tasks
defined and executed; T1–T15 pass; real emulator multilevel Wi-Fi proof PASS
(STATE_EVIDENCE_REQUIRED_TRANSIENT_FAILURE = ELIMINATED; REAL_L0_WIFI_CLOSED_LOOP
= COMPLETED). Pending graduation review (no self-archive).

## One-line

Add a bounded post-action settle / fresh re-observation policy to the Traversal
execution-verification mechanics so a state-changing action whose post-action
state evidence is captured inside the toggle-animation window (transient null)
is re-observed truthfully within a bounded budget instead of immediately failing
closed with `StateEvidenceRequired`.

## Why

Real-device truth (`state-evidence-required-real-world-buyer.md`, G —
REOBSERVATION_POLICY_BUYER_CONFIRMED): after SetSwitch the physical effect is
CONFIRMED and `ImageSwitchStateProvider` returns True/False on stable frames, but
the immediate fresh frame is inside the toggle animation window where the
knob-position analysis returns null → state evidence unknown → truthful
fail-closed. `TRANSIENT_EVIDENCE_GAP = CONFIRMED`, `STRUCTURAL = FALSE`.

## What changes

- **Owner**: Traversal execution-verification mechanics (existing owner of
  step-scope retry B4/SC-P2-002 and the Verify phase). Not Agent semantic code,
  not Environment.
- **Semantics**: dispatch → immediate fresh observe → state evidence available?
  YES → verify normally; NO but transient-eligible → bounded settle (small
  evidence-evaluating delay + strictly fresh observation + re-evaluate); NO →
  existing fail-closed. Never assumed success, never synthesized state, never
  stale reuse.
- **Eligibility**: generic truthful predicate (7 conjuncts) — not
  `if action == SetSwitch { sleep(...) }`.
- **Stopping rule**: D. HYBRID — immediate observe, bounded retry until first
  valid evidence or budget; opposite evidence stops truthfully.
- **Budget**: COMPOSITION_POLICY (max re-observe 3; initial delay 200–400ms;
  bounded max duration; no MaxAssistanceConsults interaction).
- **Scope**: B — state-changing actions with missing post-action state evidence.
- **Failure semantics**: budget exhausted → SAME truthful `StateEvidenceRequired`.
- **L1**: `L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED` — normal transitions close
  locally (L0 closes locally).

## Artifacts

- `proposal.md` — buyer, gap, design points, non-goals, falsifiers F1–F10
- `design.md` — owner freeze, algorithm, eligibility, stop rule, budget,
  freshness, scope, failure semantics, test matrix, L1 relationship
- `specs/post-action-state-settle/spec.md` — ADDED requirements + scenarios
- `tasks.md` — BASELINE slices (done) + APPLY plan (A1–A9) + T1–T12 matrix

## Validation

- `openspec validate post-action-state-settle --strict --no-interactive`
- `bash scripts/check-consistency.sh`
- Cross-check: `docs/decisions/state-evidence-required-real-world-buyer.md`
