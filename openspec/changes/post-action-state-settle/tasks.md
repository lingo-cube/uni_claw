# Tasks: post-action-state-settle

> System of record. THIS GATE IS BASELINE ONLY (proposal/design/spec/tasks +
> validation). Implementation tasks are pending the APPLY gate
> (`PROJECT_LEADER_APPLY_POST_ACTION_STATE_SETTLE`).

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — Verified source baseline (Traversal Verify phase has no settle;
      B4/SC-P2-002 step-scope retry exists; NavigationTransitionSettle precedent;
      StateEvidenceRequired terminal when currentBelief is null)
- [x] Slice 2 — Owner freeze (Traversal execution-verification mechanics; NOT Agent
      semantic code, NOT Environment)
- [x] Slice 3 — Settle semantics (dispatch → fresh observe → evidence? yes →
      verify; no but eligible → bounded settle; no → fail-closed; strict freshness
      on every retry)
- [x] Slice 4 — Eligibility generic predicate (7 conjuncts; no action-type string
      match; no `if action == X { sleep }` policy style)
- [x] Slice 5 — Stopping rule D. HYBRID (immediate observe + bounded retry until
      valid evidence or budget; first-valid-frame stop; opposite-state stops
      truthfully)
- [x] Slice 6 — COMPOSITION_POLICY budget (max re-observe 3; evidence-evaluating
      delay 200–400ms initial; max duration ≈1.2s; no unbounded retry; no
      MaxAssistanceConsults interaction)
- [x] Slice 7 — Action scope B (state-changing actions with missing post-action
      state evidence only)
- [x] Slice 8 — Failure semantics (budget exhausted → same truthful
      StateEvidenceRequired; never success/contradiction/consultation/guessed)
- [x] Slice 9 — Freshness (SequenceNumber strictly advances per retry)
- [x] Slice 10 — L1 freeze (L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED; L0 closes
      locally; no Assistance coupling)
- [x] Slice 11 — Test matrix T1–T12 (APPLY) + falsifiers F1–F10
- [x] Validation — openspec validate --strict, check-consistency.sh, buyer-doc
      cross-check

## Implementation plan (APPLY gate — EXECUTED 2026-08-17)

- [x] A1 — Traversal Verify-phase settle hook (owner: execution-verification
      mechanics): `ExecuteLoweredActionAsync` post-Observe evaluation point;
      eligible state-changing actions get bounded fresh re-observation before the
      step result returns to the Agent. Dispatch happens EXACTLY ONCE; settle only
      delays + observes (T13).
- [x] A2 — Eligibility predicate (generic, truthful, 7 conjuncts): typed
      `DeviceAction.SetSwitch` variant (narrowest existing internal signal —
      carries TargetState; NOT a protocol-token string match), target control
      still identifiable in fresh observation, `SwitchState is null`, no
      opposite/terminal evidence, budget remains.
- [x] A3 — Settle loop (D. HYBRID): immediate observe → bounded delay → fresh
      ObserveAsync → re-evaluate; stop on FIRST valid fresh state evidence
      (True/False, incl. opposite — A4); budget exhausted → return last fresh
      observation → Agent's existing StateEvidenceRequired path (A8).
- [x] A4 — Opposite state: stop settling immediately, pass real evidence into the
      existing reconciliation path; no locally manufactured SemanticContradiction.
- [x] A5 — COMPOSITION_POLICY budget: max re-observation 3 (ctor param, default),
      delay default 300ms (within measured 200–400ms; ctor param), max duration
      ≈0.9s ≤ 1.2s; no unbounded retry; no MaxAssistanceConsults interaction.
- [x] A6 — Freshness: every retry calls the real ObserveAsync; SequenceNumber must
      strictly advance (else stop); no prior SwitchState/binding/GoalEvidence reuse.
- [x] A7 — Cancellation interrupts settle delay + pending re-observation
      (cancellationToken threaded through all 4 Agent call sites); retry count
      observable via `TraversalJournalEntry.PostActionSettleCount`.
- [x] A8 — Failure semantics: all eligible fresh observations unknown → SAME
      truthful StateEvidenceRequired; never success/contradiction/consultation/
      guessed state/stale fallback/extra physical action.
- [x] A9 — Tests T1–T15 (PostActionStateSettleTests: 15 pass) incl. T13
      dispatch-once invariant (DispatchCount == 1 while ReObservationCount > 0).
- [x] GRADUATION REPAIR (OBSERVATION_SCOPED_TARGET_IDENTITY): TargetElementIndex is
      observation-scoped (裁决 3 — "当前 Observation 内的稳定序位"); the settle must
      NOT carry the numeric index across observations. `IdentifyTargetToggle` now
      re-identifies the target toggle in EVERY fresh observation via existing
      SPATIAL_RELATION evidence (bounds overlap with action TargetBounds +
      PerceptionType toggle) — unique overlap = target; zero/ambiguous = no settle
      (existing fail-closed). T16 (index shifts between observations → settle still
      re-identifies by bounds) + T17 (control gone → no settle, fail-closed) added
      and passing (17/17).
- [x] A10 — Real emulator multilevel Wi-Fi proof: PROOF-MULTILEVEL PASS
      (satisfied, exactlyOneSetSwitch, 2 hops eachHopFreshVerified,
      sourcePointsAtFresh, perceptionSwitchOn; postRunWifiOn=1). Run #1: settle
      engaged (post-action seq 8 = settled True frame); run #2:
      postActionSettleCount=0 (immediate valid — zero unnecessary settle).
      Post-repair run: settle engaged with postActionSettleCount=1 (observation-scoped
      re-identification works on real frames).
      STATE_EVIDENCE_REQUIRED_TRANSIENT_FAILURE = ELIMINATED;
      REAL_L0_WIFI_CLOSED_LOOP = COMPLETED (L0 closes locally).
- [x] A11 — L1 freeze: zero changes to IAssistanceProvider / trigger surface /
      recommendation vocabulary / AssistanceWireProvider / AssistanceBridge /
      LlmAssistanceConsumer / MaxAssistanceConsults (git diff verified).
- [x] Regression — full .NET suite: 1246 pass / 11 fail (all 11 = pre-existing
      baseline: 5×VisionHostBehavioralProofs + 5×VisionIdentityVerificationTests
      + 1×Capstone real-emulator; REGRESSION_IMPACT = NONE_OBSERVED); OpenSpec
      strict validation PASS; check-consistency ALL PASS; git diff --check clean.

## Falsifier mapping

- [x] F1 — wrong owner → settle in Agent semantic code or Environment (spec:
      owner requirement)
- [x] F2 — assumed success → policy never treats action as succeeded without
      valid evidence, never synthesizes SwitchState (spec: settle semantics +
      failure semantics)
- [x] F3 — stale retention → no prior SwitchState/binding/GoalEvidence reused as
      current truth (spec: settle semantics scenario)
- [x] F4 — null→desired → null never converted to desired value (spec: eligibility
      scenarios)
- [x] F5 — time-as-evidence → elapsed time never treated as GoalEvidence; nav
      settle unchanged (spec: behavior-preserving scenarios)
- [x] F6 — unbounded retry → finite max re-observe + bounded duration (spec:
      COMPOSITION_POLICY scenarios)
- [x] F7 — action-specific sleeps → generic truthful predicate, no string-match
      policy style (spec: eligibility requirement)
- [x] F8 — L1 coupling → no Assistance dependency, no MaxAssistanceConsults
      change (spec: COMPOSITION_POLICY + L1 relationship scenarios)
- [x] F9 — fail-closed weakening → budget exhaustion returns the existing
      truthful terminal (spec: failure semantics scenario)
- [x] F10 — stale sequence → every retry strictly advances SequenceNumber (spec:
      freshness requirement)

## Test matrix (APPLY gate)

| # | Test | Status |
|---|---|---|
| T1 | immediate post-SetSwitch frame null, second fresh frame desired → run continues/verifies | ✅ pass |
| T2 | immediate state evidence valid → zero unnecessary settle retry | ✅ pass |
| T3 | all bounded fresh frames unknown → StateEvidenceRequired | ✅ pass |
| T4 | fresh frame opposite state → existing contradiction/failure semantics preserved | ✅ pass |
| T5 | Observation.SequenceNumber strictly advances on retry | ✅ pass |
| T6 | cancellation stops settle promptly | ✅ pass |
| T7 | retry budget exact and bounded | ✅ pass |
| T8 | no stale SwitchState survives | ✅ pass |
| T9 | normal navigation settle behavior unchanged | ✅ pass |
| T10 | null assistance provider / L1 behavior unchanged | ✅ pass |
| T11 | real ImageSwitchStateProvider path used → no synthetic state injection | ✅ pass |
| T12 | real emulator Wi-Fi transition: animation-window null → settled True/False → truthful verification | ✅ PROOF-MULTILEVEL PASS |
| T13 | physical action dispatch count remains EXACTLY ONE while re-observation count may be > 0 | ✅ pass |
| T14 | ordinary non-state-changing action does not enter this settle path | ✅ pass |
| T15 | immediate valid state evidence adds no artificial delay/re-observation | ✅ pass |
| T16 | OBSERVATION-SCOPED TARGET IDENTITY: index shifts between observations → settle re-identifies by bounds overlap | ✅ pass (graduation repair) |
| T17 | control gone across observations → no settle → fail-closed StateEvidenceRequired | ✅ pass (graduation repair) |
