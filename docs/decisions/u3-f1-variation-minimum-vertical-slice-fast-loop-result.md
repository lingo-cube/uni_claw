# U3_F1_VARIATION_MINIMUM_VERTICAL_SLICE_FAST_LOOP_RESULT

> Date: 2026-08-10
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Scenario: `SC-U3-F1-001`
> Task family: `U3-F1 — Single-Target Desired-State Assurance`
> Status: `VALIDATED`

## Capability

**Wi-Fi Desired-State Assurance Under Reordered Similar Candidates**

```text
same authoritative Intent + Goal + exact closed-world Plan
→ current candidate layout differs from the U1 baseline
→ current semantic evidence and independent safety receipts
→ correct current Wi-Fi index or explicit ambiguity
→ existing Traversal dispatch + fresh verification
→ existing fresh GoalEvidence
→ Agent completion or honest non-completion
```

The slice proves that a U1 desired-state task is not bound to baseline candidate
index 0 or the first broad text match. `Wi-Fi Calling` is placed before a
state-bearing `Wi-Fi` row and an unrelated row is inserted between them. The
existing grounding protocol evaluates the current Observation in deterministic
order and selects index 2 only when it is uniquely supported.

## Exact Delta

```text
Production delta: 0
Model/API delta: 0
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Safety-semantic delta: NONE
Architecture-invariant delta: NONE
Evidence-maturity advance: NONE
```

Added test assets only:

- `tests/UniClaw.Runtime.Tests/Scenario/Fakes/U3F1WifiVariationFixture.cs`;
- `tests/UniClaw.Runtime.Tests/Scenario/U3F1WifiVariationScenarioTests.cs`.

No `src/UniClaw.Runtime/` file was changed by this slice. The repository
worktree contains unrelated earlier authorized changes; this receipt claims
only the bounded U3-F1 delta above and does not claim whole-worktree cleanliness.

## Formal Scenario Evidence

### Already ON / Layout Variant

- the exact caller-supplied non-empty Plan is preserved by
  `CLOSED_WORLD_CONCRETE`;
- fresh sequence 2 GoalEvidence shows Wi-Fi ON;
- zero Tap and zero SetSwitch;
- Agent completes only from that evidence.

### OFF / Reordered Similar Candidates

- safety and grounding evaluation order is exactly `[0, 1, 2]`;
- `Wi-Fi Calling` at index 0 is not selected;
- the current state-bearing `Wi-Fi` row at index 2 receives exactly one Tap;
- after fresh destination evidence, `Wi-Fi` at index 1 receives exactly one
  `SetSwitch(true)`;
- Observations are sequences `[1, 2, 3, 4]`;
- only fresh sequence 4 Wi-Fi ON GoalEvidence completes the Run.

### Reordered but Ambiguous

- two current Wi-Fi-like candidates carry state-bearing support;
- ambiguity remains explicit;
- zero Tap and zero SetSwitch;
- no default index, success, or Goal completion is fabricated.

### Deterministic Replay

Equal Intent, Goal, Plan, RunId, world variant, and scripted outcomes replay
equal safety order, grounding order, actions, semantic Observation projection,
journal, Trace, GoalEvidence, post-action evidence sequences, reason, and final
RunState.

## Evidence Asset Promotion

```text
Classification: NEW_VARIANT
Level: L2_SHORT_CHAIN_INTEGRATION
Source: SC-U3-F1-001 production Planning → Agent → Container → Traversal → Environment path
Oracle: already-satisfied zero mutation + reordered OFF success + reordered ambiguity + replay
Promotion: PROMOTED
```

The fixture owns only deterministic visible world state, ordering, transitions,
dispatch outcomes, and Observations. It does not encode target identity, safety
authority, action success, GoalEvidence, or completion.

## Validation

```text
SC-U3-F1-001 targeted tests: 4/4 PASS
U1 + CP-12 + uncertain-action + Popup + Recovery focused regression: 25/25 PASS
Architecture Guards: 9/9 PASS
Full suite: 488/488 PASS
Build: PASS — 0 warnings, 0 errors
```

Post-receipt validation:

- consistency C1–C10: PASS;
- all OpenSpec changes strict validation: 14/14 PASS;
- static diff/whitespace hygiene: PASS.

## Architecture / Ownership / Authority

```text
Architecture fit: CONFIRMED
Architecture invariants: UNCHANGED
Ownership: UNCHANGED
Authority: UNCHANGED
Dependency direction: UNCHANGED
Safety semantics: UNCHANGED
State-machine pressure: NO_STATE_PRESSURE
```

Intent projection remains upstream. Agent retains GoalEvidence and final
RunState authority. Traversal retains local target selection, dispatch, first
fresh Observation, and expected-effect verification. Environment reports only
world state and dispatch outcome.

## Deferred Boundary

The slice does not combine alternate-route selection, Popup, timeout, external
drift, observation failure, or longer-horizon Recovery. It does not enter
`U3-F2`, `U3-F3-CANDIDATE`, S1/S2/S3, emulator/device execution, or any Runtime
implementation work.

## Remaining Gap

U3 remains `SCOPED_DEFINITION_ONLY`; only its first U3-F1 structural-variation
slice is validated. The next scoped priority is U3-F2 variation and
longer-horizon pressure.

## Recommended Next Task

`PROJECT_LEADER_U3_F2_VARIATION_SLICE_GATE`

Select one minimum falsifying U3-F2 disturbance Scenario, classify its lane and
required authority, and do not pre-compose the entire disturbance matrix.
