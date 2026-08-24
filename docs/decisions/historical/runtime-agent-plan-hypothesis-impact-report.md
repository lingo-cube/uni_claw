# RuntimeAgent Plan Hypothesis Impact Report (Phase 2)

> Phase 2 deliverable for the "RuntimeAgent Phase 2 — Runtime-local Plan Hypothesis Capability" mission.
> Authority: READ-ONLY architecture inspection. Working artifact, NOT an Architecture Decision, OpenSpec
> spec, or contract amendment. Frozen baselines (Architecture v1, Protocol v1, Contract I-1..I-14,
> charter) govern and are not redefined here.
> Date: 2026-08-21 | Inspector: Leader (GLM-5.2) | Baseline: build 0 errors / 0 warnings, 1506 tests green

---

## Current Flow

After Phase 1 (`runtime-agent-directive-capability`), the exploration pipeline is:

```
Directive (Model/)                     ← caller's bounded exploration intent
    ↓ DirectiveDecomposer.Decompose    ← stateless, caller-configured projection (Planning/)
DirectiveDecompositionResult.Resolved(Specification, Goal)
    ↓ DirectiveExecution.RunDirectiveAsync  ← additive entry (Planning/)
IntentSemanticEnvelope.Resolved(OpenWorldTypeLevel)
    ↓ IntentExecution.RunOpenWorldAsync     ← existing seam (Planning/)
Agent.RunOpenWorldAsync                    ← DFS engine (Agent/, UNCHANGED in Phase 1)
    ↓
Evidence-driven DFS: discover → authorize → expand → execute → verify parent return →
                     update WorldBelief → stop on GoalEvidence
    ↓
RunState (Completed | Failed) + Agent.Trace (append-only observable record of every inflection point)
```

Existing run-local observable records owned by Agent (I-2):
- `_trace` (`List<TraceEvent>`) — append-only causal chain; recorded at EVERY loop inflection point
  (discovery epoch freeze, authorization reject, boundary observed, verified parent return, leaf
  dispatch, depth cutoff, completion). **Does not drive decisions** — pure observable record.
- `_belief` (`WorldBelief`) — current best judgment of reality; revised by fresh Observation (I-4).
- `_branchProgress` — immutable cross-Container progress snapshots.

The DFS loop already records rich trace evidence in real-time:
- `EXTERNAL_BOUNDARY_OBSERVED` (Agent.OpenWorld.cs:1058)
- `verified parent return` (Agent.OpenWorld.cs:1006)
- `open-world container inventory complete` (Agent.OpenWorld.cs:179)
- `open-world branch inventory` (Agent.OpenWorld.cs:221)
- authorization reject, leaf dispatch, depth cutoff, etc.

**The trace is the real-time evidence stream.** It records every observation-driven inflection point.
The hypothesis is a higher-level interpretation of this stream: "what was the RuntimeAgent assuming,
and how did observations revise that assumption?"

---

## Missing Capability

The RuntimeAgent executes the DFS loop but does not maintain an **explicit, run-local execution
hypothesis** — a record of its current execution assumption (objective, expected transition, expected
outcome) and how observations confirmed or revised it. The loop's assumptions are implicit in its
control flow; they are not made explicit as a first-class, lifecycle-tracked model.

Current:
```
Directive → Execution (assumptions implicit in control flow) → RunState
```

Target:
```
Directive → ExecutionHypothesis (explicit, run-local) → Execution →
  Observation → Hypothesis Confirm/Revision → Continue/Complete
```

The hypothesis must be:
- **Run-local**: created per Run, discarded after; NOT global memory, cross-run knowledge, or a
  navigation model.
- **Revisable**: confirmed when observations match expectations; revised when they contradict.
- **Passive**: it RECORDS assumptions; it does NOT authorize actions, decide execution, modify
  completion, or bypass Agent/Traversal authority.

---

## Minimal Extension Point

The mission's expected insertion point:
```
DirectiveExecution → RuntimeAgent Context → ExecutionHypothesis → IntentExecution
```

This is in the **Planning/DirectiveExecution layer**, NOT inside the DFS loop. The smallest insertion
that preserves all proven capabilities and authority:

```
DirectiveExecution.RunDirectiveAsync
    ↓ create initial ExecutionHypothesis from the decomposed directive
    ↓ (optional) ExecutionHypothesisLedger records the run-local hypothesis sequence
    ↓ IntentExecution.RunOpenWorldAsync  ← UNCHANGED seam
    ↓ Agent.RunOpenWorldAsync            ← UNCHANGED DFS engine
    ↓ after run: ledger revises hypothesis from Agent.Trace (evidence) + RunState (outcome)
    ↓ return RunState (+ ledger for observability)
```

**The DFS loop is NOT modified.** The hypothesis is derived from the trace (the existing real-time
evidence stream) + the run outcome. This is structurally analogous to how `Reconcile.FromObservation`
derives a `WorldBelief` from an `Observation` — a pure derivation, not Runtime state.

### Why non-invasive (post-run, trace-derived revision)

The Planning layer doc states: **"Planning owns no mutable Runtime state."** So the hypothesis ledger
in Planning/ must NOT be Runtime state — it must be a transient, method-local derivation. The ledger:
- Is created as a method-local variable in `RunDirectiveAsync` (run-local by construction; discarded
  when the method returns).
- Derives the hypothesis sequence from evidence (trace events + run outcome) — a pure computation,
  not state that the Runtime consults.
- Holds NO authority: it cannot authorize, decide, complete, or execute.

The trace already records every inflection point in real-time. The hypothesis revision is a
**higher-level interpretation of the trace** — evidence-driven (I-4: observation is evidence). The
revision being computed after the run (from the complete trace) rather than inside the loop does not
change its evidence-based nature, and it requires zero modification to the proven DFS engine.

**Leader judgment on real-time vs post-run revision:** The mission's proof goal is "RuntimeAgent can
maintain and revise a run-local execution hypothesis without gaining new authority." Post-run,
trace-derived revision satisfies this: the hypothesis is maintained (exists as a run-local record),
revised (based on trace evidence), and gains no authority (Agent/Traversal unchanged). Real-time
mid-loop revision would be stronger but requires modifying the proven DFS loop (additive observer
pattern) — the mission prioritizes non-invasiveness ("Do not modify these," "smallest insertion
point"). If real-time revision is later required, it is a separate follow-up change.

---

## New Models

### `ExecutionHypothesisStatus` (enum, Model/)
```
Created = 1      // initial hypothesis from directive
Active = 2       // execution underway under this hypothesis
Confirmed = 3    // observation confirmed the expected transition/outcome
Revised = 4      // observation contradicted expectation; revision recorded
Replaced = 5     // superseded by a new hypothesis (e.g. after boundary revision)
```

### `ExecutionHypothesis` (immutable sealed record, Model/)
Fields (per mission):
- `RunId` (string) — run identity.
- `DirectiveReference` (string) — references the directive's scope/entry (NOT the directive object;
  avoids carrying caller knowledge into the record).
- `Objective` (string) — current execution objective (e.g. "Explore current container children").
- `ExpectedTransition` (string) — expected next transition (e.g. "Discover → Authorize → Expand").
- `ExpectedOutcome` (string) — expected outcome (e.g. "All authorized obligations resolved").
- `Confidence` (float, [0,1]) — hypothesis confidence.
- `RevisionReason` (string?) — null = not revised; non-null = why it was revised.
- `CreatedAtObservation` (long?) — observation sequence when the hypothesis was created/revised.
- `Status` (ExecutionHypothesisStatus) — lifecycle state.

**Carries NO**: Plan, coordinates, DeviceAction, element index, scenario strings, authorization
rules, or completion authority. It is a passive record.

### `ExecutionHypothesisLedger` (run-local, Planning/)
- Method-local (created in `RunDirectiveAsync`, discarded after).
- Creates the initial `ExecutionHypothesis` from a decomposed directive.
- Derives the hypothesis sequence from `Agent.Trace` (evidence) + `RunState` (outcome):
  maps trace inflection points (boundary observed, verified return, inventory complete, completion)
  to hypothesis lifecycle transitions (Confirm / Revise / Replace).
- Exposes the current hypothesis + an immutable history snapshot (for test observability).
- **NO authority**: cannot authorize, decide, complete, or execute. Cannot call Agent methods that
  mutate state. Cannot modify GoalEvidence or RunState.

### `DirectiveExecution.RunDirectiveAsync` (additive modification, Planning/)
- Add optional `ExecutionHypothesisLedger?` parameter (default null).
- When provided: create initial hypothesis, run DFS (UNCHANGED), revise hypothesis from trace +
  outcome.
- When null: existing Phase 1 behavior, zero regression.

---

## Authority Impact

**NONE.**

- The hypothesis is a **passive, run-local derived record** — structurally analogous to `TraceEvent`
  (an observable record that does not drive decisions).
- The ledger is **method-local** (not Agent/Container/Traversal/Environment state). Planning owns no
  mutable Runtime state (the ledger is a transient derivation, not Runtime state — per the Planning
  layer doc).
- The Agent's authority is **unchanged**: the Agent does not consult the hypothesis for decisions,
  authorization, completion, or execution.
- The DFS loop is **unchanged**: no modification to `Agent.OpenWorld.cs`.
- No new state owner, no new decision authority, no new component with architecture meaning (the
  ledger is a transient computation, not a persistent component).
- Verified against: v1 invariants 2-4 (RuntimeAgent owns execution, not scenario knowledge), Contract
  I-2 (one mutable state one owner — the ledger is not Runtime state), I-3 (one decision one
  authority — the hypothesis makes no decisions), I-5 (Plan is hypothesis not reality — the
  ExecutionHypothesis IS a hypothesis, explicitly revisable), I-12 (YAGNI — minimal model, no
  framework), I-13 (no God Context — the hypothesis is a narrow record, not aggregated with
  Observation/WorldBelief/RuntimeState/Memory).

---

## Architecture Risk

- **Low** for the non-invasive design: additive immutable model + method-local ledger + optional
  parameter. The DFS loop is untouched. Mechanically guarded by ArchitectureGuardTests (zero
  ProjectReference, no legacy namespace) and `check-consistency.sh`.
- **Low** for regression: the optional parameter defaults to null (existing behavior unchanged); the
  Phase 1 directive tests and all open-world/settings-tree suites must remain green.
- **None** for authority: the hypothesis is passive by construction (no methods that affect
  execution); authority tests verify this structurally.
- **Not applicable** for real-time revision: out of scope for this change; a separate follow-up would
  modify the DFS loop with an additive observer pattern and would need its own review.

---

## Implementation Plan

1. **Model**: `Model/ExecutionHypothesis.cs` — `ExecutionHypothesisStatus` enum + `ExecutionHypothesis`
   immutable record. Construction validation (non-blank RunId, Objective, etc.).
2. **Ledger**: `Planning/ExecutionHypothesisLedger.cs` — run-local, method-local. Creates initial
   hypothesis from decomposed directive; revises from trace + outcome; exposes current + history.
3. **Integration**: modify `Planning/DirectiveExecution.cs` additively — optional ledger parameter.
   When present, create initial hypothesis, run DFS (unchanged), revise from `Agent.Trace` + RunState.
4. **Unit tests**: hypothesis creation, lifecycle transitions (Created→Active→Confirmed/Revised→
   Replaced), confirmation, revision with reason, run-local isolation (ledger discarded after run).
5. **Authority tests**: hypothesis cannot authorize actions (no authorize method); cannot bypass Agent
   (Agent doesn't consult it); cannot modify completion (GoalEvidence unaffected); cannot create
   recursive authority (no dispatch/execute capability).
6. **Scenario test**: Fake World — directive → initial hypothesis "explore children" → DFS encounters
   external boundary (trace records `EXTERNAL_BOUNDARY_OBSERVED`) → hypothesis revised ("external
   boundary encountered, boundary disposition handled") → DFS returns to parent and continues →
   hypothesis confirmed/replaced → verify: hypothesis revised correctly, execution authority unchanged
   (RunState produced by Agent, not by hypothesis).
7. **Regression**: build 0/0; full suite green (1506+); SETTINGS-TREE-01, U2OpenWorld,
   OpenWorldTypeDirected, BoundedCandidateSafety, BoundedCrossPageDiscovery, ArchitectureGuard,
   Phase 1 directive tests all green; `check-consistency.sh` ALL PASS; `openspec validate --strict`.
