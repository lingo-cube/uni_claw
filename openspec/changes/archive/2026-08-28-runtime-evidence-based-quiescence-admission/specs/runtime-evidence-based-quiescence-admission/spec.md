## Purpose

Defines `EVIDENCE_BASED_QUIESCENCE_ADMISSION`: the Runtime's observation-acceptance
seam decides — from consecutive fresh observations' verifiable consistency
(multiplicity- and order-preserving, drift-bounded, ambiguity-aware) — whether a frame
may become a stable decision basis, or fails closed admitting nothing. This change
freezes the general principle and repairs the EXISTING post-scroll stability gate as
the first and only buyer. No second settle loop; no normalizer/perception/identity
change; no new owner.

## ADDED Requirements

### Requirement: Quiescence admission principles

The Runtime's post-scroll observation admission SHALL implement: (1) Fresh
Observation — every confirmation attempt consumes a strictly newer observation, never a
replayed or cached frame; (2) Multiplicity Preservation — stability evidence keeps
every occurrence, ordered, with same-signature counts intact; (3) Ambiguity-Aware
Admission — a frame containing in-frame identity ambiguity (duplicate signatures or
unmatchable occurrences) SHALL NOT be confirmable as a stable decision frame; (4)
Evidence-Based Convergence — stability is proven only when consecutive frames agree on
occurrence count, per-occurrence ordered signature, deterministic correspondence, and
bounded position drift, and neither frame contains admission-blocking ambiguity; (5)
Latest-Frame Admission — only the final confirmed frame is admitted; provisional
frames never enter decision evidence; (6) Bounded Fail-Closed — a fixed attempt budget;
exhaustion admits nothing, re-dispatches nothing, guesses nothing; (7) Traceability —
each attempt records observation sequence, occurrence count, multiplicity summary,
drift, failure reason, and the final outcome.

#### Scenario: Duplicate artifacts ultimately disappear (RED basis)

- **WHEN** scroll is followed by frames [duplicate-pair, duplicate-pair, clean, clean]
- **THEN** the duplicate frames are pending (never confirmable), the clean pair
  confirms stability, and ONLY the last clean frame is admitted

#### Scenario: Duplicate artifacts persist

- **WHEN** every post-scroll frame contains the duplicate pair until the budget is
  exhausted
- **THEN** the outcome is fail-closed: no observation admitted, no action generated or
  executed

#### Scenario: Position keeps moving then stops

- **WHEN** frames show row centers A, B, C, C
- **THEN** A/B are pending, the final C is the sole admitted decision frame

#### Scenario: Normal stable list confirms minimally

- **WHEN** two consecutive fresh frames are identical and unambiguous
- **THEN** stability confirms at the minimum attempt count with no unbounded waiting

#### Scenario: Same-signature occurrence count changes (RED basis)

- **WHEN** frame 1 shows Item ×2 and frame 2 shows Item ×1
- **THEN** the frames are NOT stable (set-equality must not mask the count change)

#### Scenario: Persistent genuinely-duplicate visible rows

- **WHEN** two real rows produce the same frozen signature in every frame
- **THEN** the gate is non-confirmable (GATE_LEVEL_NON_CONFIRMABILITY, frozen): budget
  exhausted, the Run fails closed, the Observation never becomes a decision frame, and
  SourceIdentity / the normalizer's duplicate rules are NOT modified or relaxed

#### Scenario: Candidate order changes

- **WHEN** two frames carry the same signature multiset in different occurrence order
- **THEN** they are NOT stable (unordered comparison must not mask the reorder)

#### Scenario: Left the container during confirmation

- **WHEN** a confirmation frame resolves to a different page or foreground application
- **THEN** the outcome is fail-closed; the new page is never admitted as the scroll's
  stable result

### Requirement: No parallel quiescence mechanism, no authority change

The change SHALL repair the existing post-scroll stability gate in place (same owner,
same call sites) and SHALL NOT add a second settle/quiescence loop, any time-based
pass condition, any perception/normalizer/identity modification, any Action,
Completion, or GoalEvidence production, any Memory/Planner, or any wiring of other
buyers (tap, navigation, popup, recovery, loading, relayout). Observation remains
evidence, never truth; quiescence confirmation confers no authority.

#### Scenario: Repair-in-place is verifiable

- **WHEN** the implementation diff is reviewed
- **THEN** it touches only the existing gate's comparison evidence, confirmation
  logic, and additive trace fields at the existing call sites


### Requirement: Terminal supervisory handoff on budget exhaustion

When the local quiescence observation budget is exhausted, the RuntimeAgent SHALL
remain fail-closed: the current Run enters RunFailed; no provisional Observation is
admitted; no Scroll or other Action is re-dispatched; no Completion or GoalEvidence is
fabricated. The failure SHALL be exposed to UniAgent ONLY through the existing
terminal chain (existing RuntimeEventKind.RunFailed, existing RunFailedPayload.Reason,
existing trace/snapshot/evidence projections). The reason SHALL clearly state
quiescence-admission budget exhaustion, the last Observation sequence, the number of
observation attempts, and the final failure classification (duplicate ambiguity /
multiplicity mismatch / reorder / position drift / left container), and SHALL record
that no unstable frame was admitted and no action re-dispatched. The change SHALL NOT
add any new DriverHost method, wire DTO, RuntimeEventKind, callback, mid-Run escalation
transport, pause/resume, in-Run UniAgent guidance, automatic continuation Run, or
automatic action re-execution. UniAgent consumes terminal results only and cannot alter
Runtime state, WorldBelief, GoalEvidence, or the terminal outcome.

#### Scenario: Budget exhaustion produces a terminal report

- **WHEN** every observation remains ambiguous until the quiescence budget is exhausted
- **THEN** RuntimeAgent state is Failed; exactly one existing RunFailed terminal
  projection is produced; its reason names quiescence-admission exhaustion with the
  last sequence, attempt count, and failure classification; the trace carries each
  attempt's sequence, multiplicity, drift, and rejection reason; there is no
  RunCompleted, no action re-dispatch, and no provisional Observation entering
  inventory, normalization, grounding, or decision evidence

#### Scenario: UniAgent can read but cannot intervene

- **WHEN** UniAgent/Emulator reads the RunFailed through the existing Surface B
- **THEN** it can distinguish quiescence exhaustion from success and read the terminal
  reason and existing evidence projections; it produces no same-Run continuation and
  modifies no Runtime state, WorldBelief, GoalEvidence, or terminal outcome

#### Scenario: Normal stability produces no fallback report

- **WHEN** consecutive unambiguous stable frames confirm within budget
- **THEN** the latest stable frame is admitted, no RunFailed occurs, no UniAgent
  fallback is triggered, and the existing Runtime loop continues

#### Scenario: Unavailable projection reader does not change Runtime facts

- **WHEN** the external consumer of the terminal result is unavailable or has not
  polled
- **THEN** the RunFailed fact stands unchanged, no action is re-executed, projection
  unavailability never implies permission to continue, and a later read returns the
  same idempotent terminal result
