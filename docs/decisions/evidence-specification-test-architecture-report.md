# PROJECT_LEADER_EVIDENCE_SPECIFICATION_TEST_ARCHITECTURE_REPORT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Independent Reviewer: DeepSeek-V4-Pro
> Scope: Test architecture migration only — NO Runtime production code change,
> NO Agent/FSM/Traversal/GoalEvidence authority change, NO Semantic Capability
> change, NO new authority ownership.

---

## 1. Current Test Architecture Problems

Audit of `tests/UniClaw.Runtime.Tests/` (111 Scenario files + 21 fakes) classified
assertions into generic-capability (A) vs scenario-implementation (B):

| Category | Count | Examples |
|---|---|---|
| A — generic capability assertions | **853** | Trace/Evidence/Belief/Coverage/GoalEvidence/ContainerComplete/authorization |
| B — ActionHistory sequence assertions | **446** | `Assert.Equal(8, ActionHistory.OfType<Tap>().Count())`, exact action arrays |
| B — scenario-label assertions | **46** | `Assert.Contains("Battery", ...)`, "Wi‑Fi"/"Location"/"Developer Options" |

**Core problems found:**

1. **Tests validate "Runtime can replay a known Settings script" instead of
   "Runtime can reason and complete based on evidence and specifications."**
   The most overfit assertions pin exact click counts and action sequences
   (e.g. `Assert.Equal(8, ...Tap().Count())` in `OpenWorldTypeDirectedScenarioTests`
   Proof5) — these encode the scenario's execution path, not the Runtime's
   generic capability.

2. **Hidden execution plans in test-side evaluators:** many OpenWorld tests
   embed page→children maps (`Page()` recognizers + `BranchInventoryEvaluator`
   switch statements) that are scenario knowledge wearing the guise of
   inventory evidence. The Runtime's *expected* behavior is correct; the test
   fixture should declare the world, not the answer key.

3. **Scenario vocabulary leaks into assertion surfaces:** 46 label assertions
   and numerous reason-string assertions tie green/red to Settings-specific
   strings, so the same generic capability cannot be re-proven on another
   scenario without rewriting the test.

4. **Mixed concerns:** a single test class interleaves genuine mechanism
   proofs (authorization respected, fail-closed dispatch, identity safety) with
   scenario-script assertions, making the generic proofs hard to reuse.

5. **Fragile real-device plumbing:** 7 RealDevice classes hardcoded a personal
   machine adb path and fixed serials (resolved separately via
   `RealDeviceTestConfiguration`), and 5 evidence dumps indexed
   `StructuredElements[SourceElementIndex]` unconditionally although
   `SourceElementIndex` is per-source (primary→`Elements`, auxiliary→
   `StructuredElements`) — an IndexOutOfRange crash once the OCR channel is live.

## 2. New Generic Model

```text
EvidenceFixture (world + observable evidence)
        +
ExpectedSpecification (scope / required coverage / completion criteria)
        |
        v
EvidenceRuntimeHost (wires fixture into the REAL Runtime: Startup + Traversal
                     + Container factory + Agent + Goal + type-level spec)
        |
        v
Produced Trace / BranchProgress / GoalEvidence / Belief / TerminalState
        |
        v
EvidenceEvaluator (generic, scenario-neutral comparison)
        |
        v
EvaluationResult (Pass/Fail + discovered/covered containers + failures)
```

Model components live under `tests/UniClaw.Runtime.Tests/Evidence/` and are
test-only — no production Runtime model was added or modified.

## 3. EvidenceFixture Design

`EvidenceFixture.cs` — scenario-neutral external world + evidence declaration:

- **Screens** (`EvidenceScreen`): container identities, launch target, elements,
  foreground app, optional **container identity override** so OFF/ON variants of
  one container share a semantic identity (switch state never changes container
  identity).
- **Elements** (`EvidenceElement`): text, switch state, transition target +
  action + target state (navigation / state-change evidence), bounds,
  perception type.
- **ChildRelations** (`EvidenceRelation`): declared navigable child relations —
  the world's topology, not an execution path.
- **GoalSignals** (`EvidenceGoalSignal`): which container shows which element as
  goal-evidence signal.
- `ToScriptedEnvironment()` maps the fixture onto the deterministic fake world.

A fixture supplies ONLY the world and its observable evidence. It NEVER supplies
an execution path, click sequence, expected route, or hidden answers.

## 4. ExpectedSpecification Design

`ExpectedSpecification.cs` — declarative WHAT (never HOW):

- `ApplicationIdentity` + `RootContainerIdentity` (scope)
- `RequiredCoverage` (container identities that must be discovered)
- `MaximumDepth` (bound)
- `RequireGoalEvidenceSatisfied` (completion criterion)
- `IncludeStateChangingControls` (optional category + dispatch policy for
  switch-state controls)
- `ToTypeLevelSpecification()` maps onto the production
  `TypeLevelTraversalSpecification` (existing open-world execution contract).

The specification contains no plan, no click order, no route, no hidden answer.
`EvaluationResult.cs` + `EvidenceEvaluator.cs` compare the specification against
actual Runtime output (trace containers, branch progress coverage, goal
evidence receipts, belief page, terminal state).

## 5. Migration Completed

| # | Migration | Evidence |
|---|---|---|
| 1 | New `Evidence/` model: `EvidenceFixture`, `ExpectedSpecification`, `EvaluationResult`, `EvidenceEvaluator`, `EvidenceRuntimeHost` | builds clean |
| 2 | Scenario-neutral world `GenericTreeWorld` (Container A → B/C/D) — zero Settings/Android/WiFi vocabulary | 5/5 tests |
| 3 | Cross-scenario proofs `CrossScenarioEvidenceValidationTests` (diamond topology, no-goal-signal, all-NonInteractive, ghost-branch) | 4/4 tests |
| 4 | Settings demoted to external evidence fixture `SettingsEvidenceFixture` (same model, OFF/ON container variants, Wi‑Fi status goal signal) | 3/3 tests |
| 5 | Overfit assertion removal: `OpenWorldTypeDirectedScenarioTests.Proof5` — replaced `Assert.Equal(8, Tap count)` with coverage/evidence assertions (per-container approved==completed, no blind-redispatch, one authorized SetSwitch) | 6/6 tests |
| 6 | Fixed per-source index bug in 5 evidence dumps (Capstone + 4 Settings RealDevice) that crashed with `IndexOutOfRangeException` once the OCR channel was live | 4 previously-crashing tests now run |
| 7 | Fixed real-device config hardcoding (serial/adb) in 7 RealDevice classes via `RealDeviceTestConfiguration` | deterministic tests unaffected |

**Not migrated (deliberately):** mechanism-correctness assertions that happen to
count actions (e.g. "B rejected → zero B dispatch", "ambiguous parent-return →
no return action") are kept because they assert authorization/fail-closed
outcomes, not script replay. They are candidates for future expression through
the generic model.

## 6. Generic Proof Scenarios

`GenericEvidenceValidationTests` (5) + `CrossScenarioEvidenceValidationTests` (4)
+ `SettingsAsEvidenceFixtureTests` (3) — 12 generic proofs:

1. **GenericWorld_ExhaustiveCoverage_CompletesWithSatisfiedGoalEvidence** —
   Runtime discovers and completes all 4 containers; goal evidence satisfied by
   observation; belief consistent at root.
2. **GenericWorld_RootInventory_ProvesThreeAuthorizedChildren** — root proves a
   complete 3-child inventory; every dispatch authorized.
3. **GenericWorld_DeterministicReplay_SameEvidence** — same fixture+spec →
   identical trace/actions/progress (determinism without script assertions).
4. **GenericWorld_MissingChildEvidence_FailsClosed** — declared-but-unobservable
   child → no completion.
5. **GenericWorld_CorruptedSemanticEvidence_FailsClosed** — all-NonInteractive →
   zero navigation dispatch, no completion.
6. **SameRuntime_DifferentTopologies_EquivalentEvaluationSemantics** — tree and
   diamond worlds produce equivalent evaluation semantics through the same
   Runtime + evaluator (coverage proportional to declared scope, never a scripted
   count).
7. **WorldWithoutGoalSignal_FailsClosed** — removing scenario knowledge (no goal
   signal) → evidence can never be satisfied.
8. **IncorrectEvidence_AllNonInteractive_ZeroDispatchFailsClosed**.
9. **IncompleteEvidence_MissingBranch_CannotComplete** — spec requires a ghost
   container the world cannot expose → fail closed.
10–12. **Settings-as-fixture**: generic Runtime completes the Settings-shaped
   world through the same model; evaluation semantics equivalent to generic
   worlds; evaluator contains no Settings vocabulary.

## 7. Settings Fixture Status

Existing Settings tests are **preserved** (not deleted). Their role is
re-characterized:

- **Before:** "Settings logic verification" — assert exact Settings labels,
  paths, click counts.
- **After:** "Settings evidence fixture verification" — the Settings-shaped
  world is expressed through the same `EvidenceFixture` model
  (`SettingsEvidenceFixture`), and validated by the same scenario-neutral
  Runtime host + evaluator. Settings vocabulary exists only as fixture data
  (screens/relations/signals), never as an execution path or hidden answer.

The `Evidence/` model does not depend on any Settings/Android/WiFi string —
proven by `SettingsFixture_NoScenarioKnowledgeInEvaluator` running the generic
tree world (zero Settings vocabulary) to green.

## 8. Regression Results

| Suite | Result |
|---|---|
| Build `src/UniClaw.Runtime.sln` | **0 errors, 0 warnings** |
| `UniClaw.Runtime.Tests` | **1931 / 1933** (previously 1914/1921 before this migration; +12 generic evidence tests all green) |
| `Semantic.Tests` | **32 / 32** |
| `check-consistency.sh` | **ALL PASS** |
| `git diff --check` | **clean** |
| `openspec validate runtime-external-semantic-capability-boundary --strict` | **valid** |

Remaining 2 failures are REAL-DEVICE scenario issues, not part of this
test-architecture migration (and outside its STOP conditions):

1. `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete` — the
   real-device capstone runs on emulator-5554 + installed fixture APK + live
   vision service; primary OCR elements classify Unknown because the test wires
   no semantic capability (a 5.1 migration gap on the real-device path, same
   family as the previously-fixed NodeClient E2E gap; adding a capability is
   outside this task's non-goals).
2. `ExternalBoundary_RealDevice` — the real device did not reach the
   `com.android.permissioncontroller` foreground state (device/environment
   behavior, not a test-model issue).

## 9. Remaining Risks

1. **446 ActionHistory assertions remain** across ~40 test classes. Most are
   mechanism-correctness (authorization/fail-closed) and were deliberately
   kept; the generic model now exists to express them as evidence assertions in
   future migration passes. No automated guard yet prevents NEW script-style
   assertions.
2. **Real-device path semantic capability gap** (Capstone): real-device tests
   that exercise the full production pipeline still need a semantic capability
   wiring decision — belongs to the runtime-external-semantic-capability-boundary
   change review, not this migration.
3. **Settings fixture equivalence is proof-of-concept:** the Settings-shaped
   fixture demonstrates equivalent evaluation semantics but does not yet
   re-express every existing Settings-tree proof (grandchild return, sibling
   ledger, multi-level capstone) — those remain characterization tests pending a
   per-suite migration pass.
4. **Evaluator generality is bounded** to the evidence surfaces it reads
   (trace/container identity, branch progress, goal receipts, belief, terminal
   state). New evidence surfaces (e.g. future recovery evidence) would extend
   `EvidenceEvaluator`, not the fixture model.

## 10. Recommendation

**Recommendation: accept this test-architecture migration; continue in
follow-up passes.**

The migration proves the target thesis: **"Runtime can reason and complete
based on evidence and specifications"** — the generic tree/diamond worlds and
the Settings-shaped fixture all complete through the same scenario-neutral
host+evaluator, fail closed on incorrect/incomplete evidence, and reproduce
deterministically without any scripted action sequence. STOP conditions held:
no Runtime production code change, no authority change, no Semantic Capability
change, no scenario knowledge in the generic evaluator, no hidden execution
plan, no action-sequence assertions in the new tests.

Follow-up passes to consider (each separate, bounded):
1. Re-express the highest-value OpenWorld mechanism suites
   (identity-safety, parent-return resolution, completeness non-monotonicity)
   through the generic model to retire more script-style assertions.
2. Decide the real-device semantic capability wiring for Capstone/EBD as part
   of the runtime-external-semantic-capability-boundary review (Sol-gated).
3. Add a lint-style guard (e.g. in ArchitectureGuardTests) rejecting new
   `ActionHistory` count assertions in scenario tests, steering future tests to
   evidence assertions.

**This report is evidence and recommendation; it does not self-graduate.**
Independent review by DeepSeek-V4-Pro is requested before the migration is
considered closed.
