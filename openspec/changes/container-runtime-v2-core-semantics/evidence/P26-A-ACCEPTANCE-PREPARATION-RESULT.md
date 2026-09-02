# P26-A Phase 2.6 Fast-Only Acceptance Preparation Result

STATUS: `CONTAINER_RUNTIME_V2_FAST_ONLY_ACCEPTANCE_PREPARED`

ENVIRONMENT_GATE: `ENVIRONMENT_GATE_DEVICE_REQUIRED` (no online ADB device;
Task 10.2 fresh campaign auto-proceeds when a device returns, without new
Human confirmation)

## PURCHASED

Task 10.1 under the approved Fast-only acceptance stage: deterministic/stateful
acceptance-oracle fixtures (P26-F1..F12), Fast-only run metrics comparable with
the frozen old baseline, a 13-value blocker taxonomy with six mandatory
evidence fields, and read-only V2 evidence capture in the validation harness.
No Slow, no provider, no production Runtime semantic change.

## IMPLEMENTED

- `tests/UniClaw.Runtime.Tests/Scenario/Phase26FastOnlyAcceptanceFixtureTests.cs`
  — twelve production-path fixtures P26-F1..P26-F12 (r5 mismatch, multi-entry,
  path-relative return, working unknown, off-path, Fast-without-authority,
  deep Unknown, coverage+Unknown, stale bounds, wrong-child correction
  contract, stale assessment, no-duplicate-authority). All drive the real
  Agent reconciliation seam and assert public read surfaces; none
  re-instantiates the V2 reducer to re-prove R8 unit semantics; zero
  ENVIRONMENT_REQUIRED gaps.
- `src/UniClaw.Runtime.ValidationHarness/Results/Phase26FastOnlyRunMetrics.cs`
  — immutable metric schema: 19 classified count fields (including Fast
  trusted/abstained/conflict) + `BlockerCategoryCounts` + `FirstDivergence` +
  `RunTerminalDisposition`; device-only quantities honestly unavailable.
- `src/UniClaw.Runtime.ValidationHarness/Results/Phase26BaselineComparison.cs`
  — frozen old-baseline facts (19 fresh runs / 0 Completed, blocker
  distribution cited from the Phase 2.6 final report evidence path) with a
  typed per-item comparison and an explicit Completed-runs answer field.
- `src/UniClaw.Runtime.ValidationHarness/Classification/
  Phase26BlockerClassification.cs` — exactly 13 blocker categories
  (PERCEPTION, CAPTURE, SEMANTIC, FAST_RESOLUTION, CONTAINER_IDENTITY,
  TRANSITION, ENTRY_RETURN, LOCAL_MODEL, COVERAGE, AGENT_OBLIGATION,
  ACTION_GROUNDING, ENVIRONMENT, UNKNOWN) + six-mandatory-field
  `Phase26BlockerRecord` (LastGood, FirstDivergence, ExpectedReality,
  ObservedReality, Owner, EvidenceRef) with fail-closed construction, Owner
  mapped onto the existing `FailureOwner` vocabulary.
- `ResultCollector`/`ValidationResult` extension — per-run read-only V2
  capture section (current container refs, entry refs, latest occurrence,
  evidence revision, Fast availability) using the existing
  DirectPublicProjection/DerivedReadModel/Unavailable classification
  discipline.

## VALIDATED

First-hand Leader verification:

- `dotnet build src/UniClaw.Runtime.sln` → 0 errors.
- P26 fixtures → 12/12 GREEN; P26 metrics/taxonomy tests → 10/10 GREEN.
- Architecture guards → 97/97 GREEN.
- ValidationHarness suites → 197/198 (single known RACCTS-scope
  scenario-token whitelist failure, unchanged and out of scope).
- Scenario/Reconciliation/R8 regression → no new failures (only the 7 known
  RealDevice/RealEmulator environment failures).
- Full `UniClaw.Runtime.Tests` → 2609 passed / 12 failed, all pre-existing
  environmental/RACCTS/unrelated classes; zero V2 regressions.
- `scripts/check-consistency.sh` ALL PASS; strict OpenSpec valid;
  `git diff --check` clean.
- Taxonomy mechanically verified: exactly 13 enum values; six mandatory
  record fields with `ThrowIfNullOrWhiteSpace` fail-closed; metrics schema
  enumerates the full required field list.

Worker routing: both preparation WorkItems dispatched through the UniFlow
adapter (`DISPATCH_OK`, requested binding `opencode-go/deepseek-v4-flash/
high`, role `implementation_efficient`); actual worker session logs confirm
`opencode-go` / `deepseek-v4-flash`; reasoning-tier headers remain
unverifiable through the spawn channel (`ROUTING_RECEIPT_PARTIAL`, honest
limitation).

## DEFERRED

- Task 10.2 fresh real-device Fast-only campaign — gated only by
  `ENVIRONMENT_GATE_DEVICE_REQUIRED`.
- Task 10.3 falsifier review (A–J questions incl. blocker migration, r5
  absorption, Completed improvement) — after the campaign.
- Change-level final symbol map (task 9.4 final closeout) — after acceptance.
- Any Slow Shadow / AsyncAdvisory / provider experiment — Fast-only is the
  control arm and must be baselined first.

## RISKS

- P26-F8 is a deterministic micro-oracle for COVERAGE_COMPLETE !=
  SEMANTIC_RESOLVED; the full open-world traversal case remains covered by
  the existing suites, and the fresh campaign should treat it accordingly.
- The old-baseline blocker distribution mapping into the 13-category taxonomy
  is an interpretive mapping from the free-text final report; undocumented
  categories stay absent rather than zero-fabricated.
- Fixtures exercise the private Agent reconciliation seam by design; if that
  seam's signature changes in a future stage the fixtures update in lockstep.

## NEXT_WORKITEM

`PHASE_2_6_FAST_ONLY_CAMPAIGN` (Task 10.2) upon device availability:
Slow stays Disabled, scenario/buyer aligned with the old Phase 2.6, every
fresh run retaining RunRef, action evidence, Observation refs,
CurrentContainer, TransitionOccurrence, EntryContext, Graph evidence, Fast
assessment, obligation/progress, GoalEvidence, terminal result, and first
divergence. Not GRADUATED; lifecycle advancement is a separate Leader-formed
`PHASE_2_6_V2_FAST_ONLY_RESULT` decision.
