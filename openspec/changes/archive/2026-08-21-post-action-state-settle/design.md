# Design: post-action-state-settle

> BASELINE design (no code). Source-verified baseline: 2026-08-17.
> Cross-references: `docs/decisions/state-evidence-required-real-world-buyer.md`
> (G — REOBSERVATION_POLICY_BUYER_CONFIRMED), real-device evidence.

---

## 1. Exact owner (source-verified)

| Component | Owns | Evidence |
|---|---|---|
| **Traversal** | execution-verification mechanics: Select→Check→Execute→Observe→Verify→Branch; **existing step-scope retry (B4/SC-P2-002)**: bounded re-observe + re-resolve on Select failure, `RetryCount` on `TraversalJournalEntry` | `src/UniClaw.Runtime/Traversal/Traversal.cs` (protocol comment; ExecuteLoweredActionAsync retry block) |
| Agent | semantic decision / authorization; navigation-phase settle precedent (`NavigationTransitionSettle` 500ms × 4, evidence-evaluating) | `Agent/Agent.SemanticRun.cs:20-23,367-379` |
| Environment | physical observation/action only | `IEnvironment` |

**Owner = Traversal execution-verification mechanics** — it already owns the
Verify phase and the step-scope re-observe retry precedent. Post-action state
settle is a Verify-phase extension of the SAME mechanics. Agent keeps semantic
authority; Environment stays passive (F1).

## 2. Current SetSwitch post-action flow

```
SemanticAction → grounding → dispatch (ADB tap)
  → immediate fresh Observation (Traversal ExecuteLoweredActionAsync)
  → state evidence extraction (PhysicalEnvironment toggle branch → ImageSwitchStateProvider)
  → SwitchState (True/False/null)
  → reconciliation (StateBeliefReducer)
  → currentBelief is null → StateEvidenceRequired (Agent terminal)
```

- Delay today: **none** (fresh frame consumed as-is).
- Observation retries: exist ONLY for Select-phase (B4) and navigation transition
  (Agent settle) — **none for post-action state evidence**.
- State-evidence retry: none.
- Generic verification retry: partial (B4 Select; no Verify-phase state retry).
- Earliest safe insertion point: **Traversal Verify phase, after Observe, before
  the step result is returned to the Agent** — the state evidence is still in the
  execution mechanics, no semantic decision has been made.

## 3. NavigationTransitionSettle precedent (architectural, not numeric copy)

| Aspect | NavigationTransitionSettle | Post-action state settle |
|---|---|---|
| Owner | Agent (navigation phase) | Traversal (Verify phase) |
| Retry count | 4 | bounded (initial 3–4, COMPOSITION_POLICY) |
| Delay | 500ms fixed between re-observes | evidence-evaluating; initial values from toggle-animation measurement |
| Stopping condition | **result-evaluating**: re-observe until `ProvesNavigationTransition` (fresh page identity + changed) | result-evaluating: until valid state evidence / opposite state / budget |
| Freshness | strictly fresh Observation (seq advances) | strictly fresh Observation (seq advances) |
| Cancellation | cancellation token | cancellation token |

The precedent's SEMANTIC (evaluate fresh evidence, not elapsed time; bounded;
fresh-only) is reused; the numbers are not copied (F5).

## 4. Post-action settle semantics (algorithm)

```
dispatch state-changing action
  → fresh Observation (sequence S+1)
  → state evidence available? (SwitchState True/False)
       YES → verify normally (existing path)
       NO  → transient-eligible? (eligibility §5)
               YES → bounded settle: delay + fresh Observation (sequence S+2 …)
                     → re-evaluate evidence
                     → valid / opposite → stop and use it
                     → budget exhausted → StateEvidenceRequired (existing)
               NO  → existing fail-closed immediately
```

Hard rules: NEVER assume action succeeded; NEVER synthesize SwitchState; NEVER
retain stale state; NEVER convert null→desired; NEVER treat elapsed time as
GoalEvidence (F2/F3/F4/F5).

## 5. Eligibility (minimum generic predicate)

A post-action settle may run only when ALL hold (truthful, action-agnostic):

1. an action was actually dispatched in this step (journal has a dispatch);
2. the action is state-changing / verification-sensitive (its execution outcome
   requires world-state verification — expressed via the semantic action/capability
   shape, not an action-type string match);
3. a fresh Observation exists for the step;
4. the target binding/control remains identifiable in the fresh observation;
5. the required state evidence is temporarily unavailable (null), not
   contradicting;
6. no contradiction proves failure (belief not Contradicted);
7. the retry budget remains.

**OBSERVATION-SCOPED TARGET IDENTITY (graduation repair, frozen)**: `TargetElementIndex`
is valid ONLY within the Observation from which grounding occurred (裁决 3 — Index is
"当前 Observation 内的稳定序位"; DeviceAction doc "目标元素在当前观测内的 Index").
The settle MUST NOT assume `Observation S element[index] == Observation S+1 element[index]`
merely because the numeric index is equal. Target re-identification happens in EVERY
fresh Observation via the existing SPATIAL_RELATION evidence signal (same family as
`BindingAnalysis.SameRow`): the unique `PerceptionType=="toggle"` element whose Bounds
spatially overlap the action's `TargetBounds` (grounding-observation spatial evidence).
Zero overlap or ambiguous overlap → control not identifiable → settle does NOT engage
(existing fail-closed path). No `TargetBounds` (legacy Index-only path) → no settle.

No `if action == SetSwitch { sleep(...) }` policy style (F7); the predicate is
generic over state-changing semantic actions.

## 6. Stopping condition — D. HYBRID

Chosen from real toggle-animation evidence (immediate frame null, settled frame
True/False): **immediate observe, then bounded retry until valid evidence or
budget exhausted**. Stop on the FIRST fresh observation yielding valid state
evidence (True/False) — least expensive truthful policy. Opposite-state evidence
also stops (existing contradiction/failure semantics preserved). No
stable-consecutive requirement (over-purchased temporal filtering for a ~300ms
toggle animation).

## 7. Budget / timing (COMPOSITION_POLICY)

Initial values (frozen as policy, not contract):

- maximum re-observation count: **3** (aligned with evidence budget discipline;
  real animation settles within 1 extra frame in practice);
- delay policy: small evidence-evaluating delay between re-observes (initial
  200–400ms — measured toggle animation window; NOT a copy of nav 500ms);
- maximum additional verification duration: bounded by count × delay
  (≈1.2s worst case), well inside the WireTimeout composition budget;
- no unbounded retry; **no interaction with MaxAssistanceConsults** (F8);
- values may be tuned without contract changes.

## 8. Freshness

Every retry calls ObserveAsync → `Observation.SequenceNumber` strictly advances
(Traversal Observe guarantees). No prior SwitchState / ObjectBinding state /
GoalEvidence is reused as current truth (F3/F10). World authority unchanged.

## 9. Action scope — B

State-changing SemanticActions whose post-action state evidence is missing.
Narrowest repository-evidenced scope (SetSwitch is the demonstrated case; the
generic predicate covers state-changing actions without generalizing to ALL
actions — navigation/tap-only actions keep existing behavior) (F7-adjacent).

## 10. Failure semantics

Budget exhausted → the SAME truthful `StateEvidenceRequired` (or another
legitimate terminal produced by fresh evidence). Never convert timeout into
success, contradiction, model consultation, or guessed state (F9).

## 11. Test matrix (APPLY)

| # | Test |
|---|---|
| T1 | immediate post-SetSwitch frame SwitchState=null, second fresh frame desired → run continues/verifies |
| T2 | immediate state evidence valid → zero unnecessary settle retry |
| T3 | all bounded fresh frames unknown → StateEvidenceRequired |
| T4 | fresh frame opposite state → existing contradiction/failure semantics preserved |
| T5 | Observation.SequenceNumber strictly advances on retry |
| T6 | cancellation stops settle promptly |
| T7 | retry budget exact and bounded |
| T8 | no stale SwitchState survives |
| T9 | normal navigation settle behavior unchanged |
| T10 | null assistance provider / L1 behavior unchanged |
| T11 | real ImageSwitchStateProvider path used → no synthetic state injection |
| T12 | real emulator Wi-Fi transition: animation-window null → settled True/False → truthful verification |

## 12. L1 relationship (frozen)

`L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED`. This repair occurs BEFORE any need for
external semantic consultation; expected effect: normal state transition → local
bounded re-observation → state evidence → **L0 closes locally**. Do NOT force
successful local evidence recovery through L1.

## 13. Deferred

- Perception redesign; action-specific sleep policy style; generalized temporal
  filtering; L1/L2/L3 changes; new recommendation kinds.
