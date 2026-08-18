# U2 Open-World Settings Traversal — Graduation Decision

> Status: GRADUATED (independent review) | Decision: `GRADUATE_U2_OPEN_WORLD_SETTINGS_TRAVERSAL` | Date: 2026-08-16
> Gate: `PROJECT_LEADER_U2_OPEN_WORLD_SETTINGS_GRADUATION_REVIEW`
> MODE: `INDEPENDENT_U2_CAPABILITY_GRADUATION_REVIEW` (no production/test/spec mutation during review)
> Maturity: `U2_BOUNDED_OPEN_WORLD_SETTINGS_TRAVERSAL_GRADUATED`
> Change: `openspec/changes/u2-open-world-settings-traversal/`

## 0. Scope Discipline

This graduation proves ONLY the exact U2 purchased capability: a bounded production
execution seam that runs a **resolved open-world type-level Settings traversal** (no
pre-enumerated route/work inventory) through the existing Agent control plane with
verified parent return, sibling continuation, and evidence-gated traversal-shaped
completion. It does NOT prove: full open-world object discovery, Settings inventory
completeness, Settings navigation-candidate perception, physical Android reality,
Phase3 bounded discovery, viewport exploration, Recovery, or generic navigation.

## 1. Original Buyer (§4)

| Field | Value |
|---|---|
| Change ID | `u2-open-world-settings-traversal` |
| OriginalBuyer | **CC-04** — high-level open-world Settings traversal specification (preserved by `TypeLevelTraversalSpecification`) that production Runtime could not yet execute through verified parent return and sibling continuation |
| OriginalScenario | `SC-U2-MUS-001 — Bounded Open-World Settings Traversal` |
| ConcreteFailureBeforeU2 | Runtime could execute only closed-world concrete `Plan` (`Agent.RunAsync(Goal, Plan, ...)`); the opt-in branch-discovery path could enter fresh child Containers but could not verify a parent return, continue a sibling, or gate fresh Goal evaluation on verified bounded traversal completion; the S0 Capstone showed the composition only in a test-side orchestrator, not as a production path |
| DesiredCapability | Execute a resolved `OPEN_WORLD_TYPE_LEVEL` envelope without a pre-enumerated concrete route: runtime-discovered A/B siblings, verified child terminal evidence, unique authorized parent return, fresh exact parent reconciliation, sibling continuation, and final existing fresh GoalEvidence — completion only via `VerifiedBoundedTraversalCompletion` + satisfied `GoalEvidence` |
| BuyerClass | **E (authorization of a bounded execution mechanism for an already-resolved representation) + B (unknown page traversal within declared bounds)** — NOT A (object discovery), NOT D (fixture-only) |

## 2. Capability Boundary (§5)

**Before U2:** Runtime could not execute the authoritative open-world type-level
Settings representation end-to-end — no production path existed to traverse
runtime-discovered in-scope children with verified parent return and sibling
continuation, and no production completion gate tied traversal completion to fresh
GoalEvidence.

**After U2:** `Planning/IntentExecution.RunOpenWorldAsync` (one public static seam)
accepts only a resolved `OPEN_WORLD_TYPE_LEVEL` envelope, rejects closed-world input
before Runtime activity, and forwards primitive/model inputs to the internal
`Agent.RunOpenWorldAsync`. Agent then performs bounded depth-first traversal: derives
complete required-branch inventory from accepted fresh Container evidence (validated
source sequences), selects at most one pending required child after positive existing
candidate authorization, delegates local selection/dispatch/fresh-observation to
Container/Traversal, verifies child terminal evidence, requires a unique authorized
parent-return target, reconciles exactly to the expected parent, preserves completed
sibling evidence, and only after deriving `VerifiedBoundedTraversalCompletion` invokes
the existing `Goal.EvidenceEvaluator` on the fresh root Observation. No concrete
Plan/route/inventory is manufactured; closed-world `RunAsync` is unchanged.

CapabilityBoundaryTruthful = PASS.

## 3. Task Truth (§3)

**4/4** — TotalTasks=4, CheckedTasks=4, UncheckedTasks=0 (matches reported 4/4).
TaskTruth = PASS. Tasks: 1.1 deterministic fixture; 2.1 production seam + bounded
Agent traversal; 3.1 formal Scenario proof; 4.1 independent validation.

## 4. Production vs Test Artifacts (§6-8)

| Artifact | Class |
|---|---|
| `src/UniClaw.Runtime/Planning/IntentExecution.cs` (seam; originally `IntentSemanticEnvelopeExecution.cs` — see §13 drift) | **PRODUCTION_MECHANISM** |
| `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` → `RunOpenWorldAsync` (internal bounded Agent path) | **PRODUCTION_MECHANISM** |
| `tests/UniClaw.Runtime.Tests/Scenario/Fakes/U2OpenWorldSettingsFixture.cs` | **TEST_FIXTURE** |
| `tests/UniClaw.Runtime.Tests/Scenario/U2OpenWorldExecutionTests.cs` (3) + `U2OpenWorldSettingsFormalScenarioTests.cs` (5) + `OpenWorldTypeDirectedScenarioTests.cs` (6) | **TEST_HARNESS** |
| `openspec/changes/u2-open-world-settings-traversal/` artifacts | **SPEC_ONLY / GOVERNANCE** |

ProductionCapabilityDelta = one public static seam type (`IntentExecution.RunOpenWorldAsync`) + one internal Agent bounded traversal path; `Goal.cs` and all existing evidence models unchanged; zero new enum/interface/engine/manager/mutable field/state owner; existing `Agent.RunAsync(Goal, Plan, ...)` unchanged.
TestFixtureDelta = `U2OpenWorldSettingsFixture` (root siblings A/B, explicit parent returns, dangerous candidate, beyond-depth candidate, unresolved inventory, ambiguous/rejected return, wrong parent, stale Observation, A-only progress, replay worlds).
ProductionVsFixtureBoundary = PASS — fixture is referenced only from tests; no fixture leaks into production (verified: `src/` contains no `UniClaw.Runtime.Tests` / fixture references; only csproj comments, AGENTS.md docs, and standard `InternalsVisibleTo`).

## 5. "Open-World" Meaning in U2 (§9)

"open-world" in U2 means: **executing a caller-resolved open-world type-level Settings
specification without a pre-enumerated concrete route or work inventory** — the
specific sibling nodes (A/B), their order, and the concrete navigation steps are NOT
preselected by the caller; the Runtime discovers and authorizes one in-scope child at
a time from fresh accepted evidence within the declared bounded scope (depth ≤ N,
navigation-only, explicit parent-target Settings world).

NOT: unknown object identities discovered at runtime (identity uses existing
binding/grounding); world topology not precompiled (the type-level specification IS
supplied); complete Settings tree traversal; whole-world exhaustion (depth cutoff is
explicitly non-exhaustion — spec Requirement 5).

## 6. World Knowledge Boundary (§12)

Caller/test fixture supplies the **type-level specification** (application identity,
semantic entry, max depth, navigation-only scope, safety boundary) and the **world
state** (Fake visible elements/transitions/observations). Runtime supplies the
**traversal** — which nodes are in scope, in what order, and whether traversal is
complete — from fresh evidence. This is **runtime traversal over a supplied
type-level world model with runtime-discovered concrete branch selection**, NOT
runtime discovery of an unknown world topology. WorldKnowledgeBoundary = TRUTHFUL
(declared).

## 7. Authority & Invariant Preservation (§13-19)

- **PlanAsRealityRegression = NONE**: U2 manufactures no concrete Plan; inventory is
  derived from accepted fresh evidence, never from caller expectation; `Plan` stays a
  hypothesis, never world truth.
- **ObservationRemainsWorldEvidence = YES**: current world state comes only from
  Observation/Environment evidence (`Reconcile.FromObservation`); fixture expectations
  never become execution-time truth.
- **AgentAuthorityRegression = NONE**: Agent remains sole semantic decision authority —
  derives inventory, authorizes selection, derives `VerifiedBoundedTraversalCompletion`,
  consumes GoalEvidence, decides completion/failure.
- **FixtureDecisionAuthority = NONE**: fixture defines only visible world state,
  transitions, dispatch outcomes, and Observations; it encodes no Container identity,
  branch completion, traversal completion, Goal success, or action authority (task 1.1
  Required Semantic).
- **TraversalAuthorityRegression = NONE**: Traversal retains local select/dispatch/fresh
  verify mechanics only; no independent semantic goal progression.
- **ContainerCapabilityClaim**: Container (under U2) provides current **visible in-scope
  children from accepted fresh evidence** for the declared bounded scope; it does NOT
  prove complete subtree / complete Settings inventory. `CONTAINER_INVENTORY_COMPLETE`
  is NOT claimed.
- **IdentitySemantics = PASS**: parent-return identity is explicit visible-target
  matching with exactly-one requirement and positive authorization; no coordinate
  identity, array-index identity, or expected-plan identity as long-lived semantic
  truth; ambiguity stops before dispatch.
- **RuntimeDiscovery = YES / CallerPrecompiledNextStep = NO**: the concrete next branch
  (A vs B) is not caller-selected; Agent selects from fresh evidence after positive
  authorization.

## 8. Settings Traversal & Fixture Truth (§20-21)

Start state: root Settings (SemanticRoot == Entry.ExpectedSemanticEntry). Target:
traversal-shaped Goal satisfied only after both A and B verified. Intermediate:
runtime-discovered A and B subtrees with explicit parent-return targets. Each
transition: inventory criterion over accepted evidence → positive authorization →
transient Tap PlanStep → Container/Traversal select/dispatch/fresh Observe → fresh
reconciliation. Completion evidence: `VerifiedBoundedTraversalCompletion` (every
required child complete, all returns verified, frames empty, no unresolved in-scope
work) + satisfied existing fresh `GoalEvidence`.

Fixture truth: Fake owns only visible world state/transitions/dispatch outcomes/
Observations; deterministic transitions and negative variants (unresolved inventory,
ambiguous/rejected return, wrong parent, stale observation); replay worlds equal.
Dispatch ≠ world truth; fresh observation after every action (task 1.1/2.1 Required
Semantic).

## 9. Negative Coverage (§22)

Negative oracles proven (SC-U2-MUS-001 + tests): unresolved inventory (no Tap, no Goal
evaluation), unauthorized required branch (no dispatch), ambiguous/rejected parent
return (no return Tap, no completion), wrong parent post-return (no completion, explicit
continuity failure), stale observation (not terminal, no completion), A complete while
B pending (no early Goal evaluation), unsatisfied fresh GoalEvidence after verified
traversal (explicit fail, not mechanical success), dangerous/deeper candidates (zero
dispatch, non-exhaustion), closed-world envelope (rejected before Runtime activity).
NegativeCoverage = PASS.

## 10. Regression & Relations (§23-25)

- **Phase1Regression = NONE**: belief≠truth, Plan≠Reality, grounding≠identity,
  escalation≠authority, Traversal≠Agent, Environment boundary — all preserved (Phase1
  regression subset passes in fresh run).
- **Phase2Regression = NONE**: Trap/Recovery authority intact; U2 does not touch
  Recovery/.
- **LaterPhase3Relation = EXTENDED_COMPATIBLY**: U2 **composes** frozen Phase3 branch
  inventory/progress/candidate-safety criteria and CP-04/07/12/14 (proposal: "the
  frozen Phase 3 branch inventory, progress, candidate safety... composed by this new U2
  slice"); U2 does not contain Phase3 functionality. LaterContradiction = NONE (closed).
- **PhysicalRealityProvenByU2 = NO** — U2's buyer was deterministic Runtime
  conformance on the Fake world; acceptable and recorded as such.

## 11. Relations to Deferred Proposals (§10-11)

- **open-world-container-inventory-completeness (DEFERRED_NO_CURRENT_BUYER)**:
  InventoryCompletenessProvenByU2 = **NO** — U2 proves bounded runtime-discovered
  traversal with a complete in-scope inventory criterion within depth ≤ N; full
  Settings-tree inventory completeness is exactly the later proposal's open question.
  U2 is the narrower baseline; the later proposal exposes completeness pressure beyond
  U2's scope.
- **settings-navigation-candidate-evidence (DEFERRED_NO_CURRENT_BUYER)**:
  SettingsNavigationEvidenceGapHiddenByFixture = **YES** (explicitly recorded) — the
  U2 fixture supplies visible elements directly (Fake world defines elements/
  transitions), so the fixture bypasses the later perception problem of deriving
  navigation candidates from raw Observation evidence. This does not invalidate U2 —
  U2 never bought that layer; the gap is recorded for the later proposal.

## 12. Zero Cognition (§26)

LlmCalls = 0, VlmCalls = 0, DshCalls = 0 — U2 production files
(`IntentExecution.cs`, `IntentSemanticEnvelope.cs`, `TypeLevelTraversalSpecification.cs`,
`Agent.OpenWorld.cs`) contain no LLM/VLM/DSH/HttpClient references. Deterministic
Runtime machinery only.

## 13. Code Drift (§30) — truthful naming

- The seam was introduced as `Planning/IntentSemanticEnvelopeExecution.cs` (commit
  c70bf74) and currently lives as `Planning/IntentExecution.cs` (same public static
  class, `RunOpenWorldAsync` preserved; later refactor consolidated intent execution
  entries). The `u2-minimum-usable-agent-slice-result.md` receipt still names the old
  file. Semantic/authority/delta content is unchanged.
- CurrentCodeDrift = **COMPATIBLE_DRIFT** (documented naming drift only; no
  UNRESOLVED_CONTRADICTION).

## 14. Requirement Coverage Matrix (§27) — all FULL

| Requirement | BuyerRelation | ProductionEvidence | TestEvidence | AuthorityBoundary | Coverage | Limitation |
|---|---|---|---|---|---|---|
| Resolved open-world envelope bounded execution entry | CC-04 | `IntentExecution.RunOpenWorldAsync` (rejects CLOSED_WORLD_CONCRETE) | ClosedWorldEnvelope_IsRejectedBeforeRuntimeActivity | Seam outside `RunAsync`; no parse/observe/select/complete | FULL | entry accepts resolved envelope only |
| Agent bounded fresh-evidence branch traversal | CC-04 | `Agent.RunOpenWorldAsync` inventory/authorization loop | Positive_UsesDynamicSiblings...; UnresolvedInventory_PerformsNoTap... | Agent derives; Container/Traversal retain local mechanics | FULL | in-scope inventory criterion, not full-tree |
| Parent return & sibling continuation exact proof | CC-04 | method-local parent frames; unique authorized parent target; exact reconcile | ACompleteWhileBPending...; ambiguous/rejected/wrong-parent tests | Agent records completion only after exact proof | FULL | explicit-target Settings world only |
| Traversal-shaped completion evidence-gated | CC-04 | `VerifiedBoundedTraversalCompletion` + fresh GoalEvidence conjunction | UnsatisfiedFreshGoalEvidence_AfterVerifiedTraversalFails... | Agent consumes existing evaluator; no second evaluator | FULL | — |
| Cutoffs/failures ≠ exhaustion | CC-04 | depth/safety cutoff non-exhaustion Trace | dangerous/deeper candidate zero-dispatch tests | Trace records bounded cutoff, never world exhaustion | FULL | — |
| Deterministic replay + regressions | CC-04 | closed-world path unchanged | EqualInputs_ReplayEqual... | — | FULL | — |

## 15. Fresh Targeted Validation (§28)

U2 tests (14) + ArchitectureGuardTests (8, incl. Agent-no-Planning-dependency guard)
+ Phase1/2 regression subset (SC-P1-001/003/004/005 + SC-P2-001): **65/65 PASS**
(2026-08-16 current build). Historical receipt `u2-minimum-usable-agent-slice-result.md`
records the original independent Luna validation: U2 18/18, Guards 9/9, frozen
regressions 15/15, full suite 484/484, OpenSpec 14/14 strict, consistency ALL PASS
(2026-08-10) — used as provenance, not as this review.

## 16. OpenSpec Truthfulness (§32)

4/4 tasks complete; spec matches implementation (verified against seam + Agent path);
proposal buyer (CC-04) remains truthful; no later unimplemented proposal claimed
complete; no self-graduation; not archived at review time. OpenSpecTruthfulness = PASS.

## 17. What U2 DOES NOT Prove (explicit)

- Full Settings-tree inventory completeness (deferred: open-world-container-inventory-completeness)
- Navigation-candidate perception from raw evidence (deferred: settings-navigation-candidate-evidence)
- Physical Android Settings device path (no physical lane in U2)
- Generic navigation / arbitrary apps (explicit-target Settings world only)
- Viewport discovery, Recovery, Popup handling, generic planner/backtracking, state-changing open-world work, U3

## 18. Compliance

- FORBIDDEN list respected: zero production/test/spec mutation during review; no
  expansion of "open-world"; no inventory-completeness claim; no Phase3 capability
  imported into U2; no self-graduation; no archive before decision.
- Historical receipts (human-authorize / gate / result) were evidence; THIS review is
  the independent graduation decision.

## State

```text
U2_BOUNDED_OPEN_WORLD_SETTINGS_TRAVERSAL_GRADUATED
```

Next lifecycle action: archive `u2-open-world-settings-traversal` →
`PROJECT_LEADER_POST_LIFECYCLE_CLEANUP_SYSTEM_STATE_REVIEW`.
