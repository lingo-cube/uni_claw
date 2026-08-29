# PROJECT_LEADER_RUNTIME_EVIDENCE_BASED_QUIESCENCE_ADMISSION_IMPLEMENTATION_RESULT

Gate: `..._IMPLEMENTATION_GATE_WITH_TERMINAL_UNIAGENT_HANDOFF` (2026-08-28, auto-activated
after amendment validation). Implementation via UniFlow I.1→I.5. **Not graduated, not
archived, Phase 2.6 not resumed — awaiting independent Graduation / Reentry Human Gate.**

## 1. Amendment & lifecycle (executed before implementation)

- Scenario 6 frozen **GATE_LEVEL_NON_CONFIRMABILITY**; RED set reclassified (1/2/5/6/7);
  Principle 8 **Terminal Supervisory Handoff** + Scenarios 9–12 added to spec.
- `runtime-viewport-exhaustion-confirmation` **ARCHIVED** (archive/2026-08-28-; kept as
  wrong-hypothesis + RED-first-discipline history). Projections synced (22 active).
- `unique-corroboration-admission` remains **ABANDONED_AS_PRIMARY_FIX**.
- Validation at activation: strict PASS · consistency ALL PASS · diff-check CLEAN ·
  zero wire/API/EventKind · Phase 2.6 still STOPPED.

## 2. UniFlow WorkItem record

| WI | Content | Result |
|---|---|---|
| WI-QA-1 (I.1) | RED first: S1/2/5/6/7 RED + S3/4/8 controls (new `QuiescenceAdmissionRedTests.cs`) | 5 RED failed via the exact mechanisms (TryAdd collapse / unordered-map masking / dup-pair false-confirm); controls green; existing suite untouched; leader re-verified 5红3绿 |
| WI-QA-2 (I.2+I.3) | Repair-in-place + trace + terminal handoff + S9-12 tests | All green; leader re-verified |
| WI-QA-3 (I.5) | Independent verifier (fresh, artifacts-only) | **PASS 11/11** (two literal-check confounds correctly attributed to concurrent workstreams) |

## 3. Repair summary (production scope: Agent.OpenWorld.cs + Agent.cs private field)

- `NavigationRowCenters`: `Dictionary.TryAdd` → ordered multiplicity-preserving
  `IReadOnlyList<(string Signature, float CenterY)>` (every occurrence kept; the HashSet
  in `HasDuplicateSignature` only DETECTS dups, never collapses evidence).
- `IsViewportStable`: unordered-map compare → `(bool, ScrollStabilityClassification)` —
  stable iff equal count + per-index ordered signature + per-index drift ≤ epsilon +
  neither frame has an in-frame duplicate signature (DuplicateAmbiguity /
  CountMismatch / ReorderOrSignatureMismatch / PositionDrift).
- `ConfirmScrollStabilityAsync`: unchanged budget/latest-frame/fail-closed semantics;
  per-attempt trace now carries occurrences/dup/drift/reason; exhaustion detail
  (`_lastStabilityExhaustionDetail`) threads into the existing `Fail` reason:
  "quiescence admission budget exhausted (last seq=N, attempts=K, classification=X; no
  unstable frame admitted, no action re-dispatched)". Zero new
  EventKind/wire-DTO/DriverHost-method/callback/mid-Run transport.

## 4. Verification (I.4, leader-run, fresh numbers)

- Quiescence suites: RED tests **8/8** (5 RED→GREEN + 3 controls) · Terminal handoff
  **4/4** (S9–S12) · ScrollStabilityConfirmation **2/2**.
- Targeted: OpenWorld **99/99**; stability/eligibility 17/17.
- Full regression: **2233 passed / 10 failed — all environmental** (7 RealDevice/
  RealEmulator + 3 VisionHost CORR); zero non-environmental failures.
- Architecture guards 35/35; consistency ALL PASS; `git diff --check` clean; strict
  validation valid; wire-diff grep empty.

## 5. Deltas

- **AuthorityDelta: NONE** (no GoalEvidence/FSM/Traversal/perception/normalizer change;
  UniAgent consumes terminal results read-only).
- **RuntimeBehaviorDelta: PRESENT_IF_IMPLEMENTED → implemented as specified**
  (quiescence admission stricter + ambiguity-aware; terminal exhaustion reporting).
- **ArchitectureDelta: ADDITIVE_INTERNAL_REPAIR** (existing owner, existing call sites;
  no new owner / cross-layer contract).

## 6. Status

```
Implementation:                    COMPLETE (I.1–I.5)
STOP2DeterministicReproduction:    N/A (superseded — premise withdrawn)
QuiescenceRedToGreen:              PROVEN (S1/2/5/6/7)
TerminalHandoff:                   PROVEN (S9–S12, existing Surface B only)
AuthorityDelta:                    NONE
GraduationRecommendation:          READY_FOR_INDEPENDENT_REVIEW
Phase26:                           STILL_STOPPED_PENDING_GRADUATION_AND_SEPARATE_REENTRY_GATE
```

**Stopped. No auto-graduation, no auto-archive, no Phase 2.6 reentry, no other buyers
wired.** Awaiting Graduation / Reentry Human Gate.
