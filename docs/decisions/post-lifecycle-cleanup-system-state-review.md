# Post-Lifecycle Cleanup — System State Review (UniClaw System Baseline)

> Status: SYSTEM_STATE_TRUTH_ESTABLISHED | Date: 2026-08-16
> Gate: `PROJECT_LEADER_POST_LIFECYCLE_CLEANUP_SYSTEM_STATE_REVIEW`
> MODE: `SYSTEM_CAPABILITY_BASELINE_AND_BUYER_REASSESSMENT`
> This is the Project Leader's actual system baseline — organized by semantic/runtime
> responsibility, NOT by OpenSpec change names. OpenSpec state is supporting metadata.

## 0. Lifecycle Baseline (verified)

- Active OpenSpec directories (3): `greenfield-agent-runtime` (LONG_LIVED_BASELINE),
  `open-world-container-inventory-completeness` (PROPOSED_NO_CURRENT_BUYER),
  `trace-capture-scenario-catalog-foundation` (PROPOSED_NO_CURRENT_BUYER).
- PENDING_GRADUATION queue: EMPTY (phase1/phase2/u2 graduated + archived this flow).
- Parallel stream (observed, not this flow's action): `settings-navigation-candidate-evidence`
  graduated (`SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE`) + archived.
- No contradiction found in lifecycle truth; this gate does not re-audit history.

## 1. System Capability Map (12 layers, production semantics)

### A. INTENT / GOAL ENTRY
| Entry | Status |
|---|---|
| `Goal` + injected evidence evaluator | PRODUCTION (Phase1 frozen; completion gate) |
| Concrete `Plan` (closed world) | PRODUCTION (`Agent.RunAsync(Goal, Plan, ...)`, frozen) |
| Type-level traversal spec (`OPEN_WORLD_TYPE_LEVEL` envelope) | PRODUCTION (`IntentExecution.RunOpenWorldAsync` — U2, bounded depth/navigation-only/explicit-parent-target) |
| BusinessIntent / TaskSpec | DESIGN ONLY (no production type; zero src/ references) |

### B. AGENT DECISION
Agent decides (production): branch inventory acceptance (from injected criterion + accepted evidence), candidate authorization (injected criterion), one-child-at-a-time selection, completion/failure (GoalEvidence consumption), recovery initiation/continuation/termination (Phase2), bounded traversal completion derivation (U2), capability selection (SemanticRun), semantic action authorization.
Still precompiled from caller/config: Goal criteria, Plan steps, inventory/authorization evaluators, RecoveryAnchor recipe, type-level world constraints (U2), capability/object catalogs for SemanticRun.

### C. OBSERVATION / PERCEPTION
| Evidence | Status |
|---|---|
| Elements / ForegroundApplication / SequenceNumber | PRODUCTION (frozen) |
| SwitchState? / Index grounding evidence | PRODUCTION (frozen) |
| `Observation.StructuredElements` (from UIAutomator hierarchy) | PRODUCTION (settings-nav) |
| `InteractionAffordanceEvidence` (NAVIGATION_CANDIDATE / LOCAL_CONTROL / UNKNOWN) | PRODUCTION (settings-nav analyzer, caller-independent, Settings-scoped) |
| Toggle candidate / type / bounds (Perception pipeline) | PRODUCTION (perception-actionable-toggle-evidence + reality-repair, graduated) |
| Outside scope: raw-image VLM semantics, generic-app affordance classification (settings-nav explicitly NO generic claim) | NOT PURCHASED |

### D. BINDING / IDENTITY
- Observation-local: `ObservedElement.Index`, `StructuredElementEvidence.SourceElementIndex` (observation-local ordinal — NOT long-lived semantic identity).
- Durable semantic association: perception pipeline object bindings (`ObjectBinding`, `BindingAnalysis`/`BindingReconciler`, graduated perception) and `SemanticObject` catalogs for SemanticRun.
- Identity claim boundary: explicit-rule page identity (Container), exactly-one parent-return target matching (U2), state-bearing switch grounding (SC-P1-005). No coordinate identity, no array-index-as-truth, no expected-plan identity.

### E. STATE BELIEF
- `WorldBelief(SemanticPage, Confidence, Evidence, SourceObservationSequence)` — belief from `Reconcile.FromObservation` only; Unknown/Uncertain/Conflicting allowed (§10).
- Container-local state beliefs (`ObjectStateBeliefs`, page beliefs) via graduated perception integration.
- Cannot infer: full Settings-tree topology, unobserved child presence, semantic meaning beyond injected criteria, raw-image semantics.

### F. CONTAINER
Knows (production): current accepted Observation, visible candidates, accepted same-Container observation sequence (viewport), local progress, local-complete, object bindings/state beliefs (when perception criteria injected), still-mine rule.
Does NOT know: complete subtree / full Settings inventory (explicitly NOT claimed — U2/inventory boundary), historical branches beyond retained progress evidence, world truth.

### G. TRAVERSAL
| Semantics | Status |
|---|---|
| Local child traversal (Select→Check→Execute→Observe→Verify→Branch) | PRODUCTION (frozen) |
| Step-scope retry (re-observe/re-resolve, bounded, zero dispatch) | PRODUCTION (Phase2) |
| Multi-level navigation (bind/traverse/navigate) | PRODUCTION (frozen) |
| Branch discovery + parent return + sibling continuation (bounded, depth≤N, explicit parent target) | PRODUCTION (U2, `RunOpenWorldAsync`) |
| Viewport scroll + identity continuity + bounded exhaustion | PRODUCTION (Phase3 sc-p3-003 / CAND-007 composed into Agent) |
| Unexpected navigation reconciliation | PRODUCTION (graduated) |
| Uncertain-action verification (post-dispatch timeout) | PRODUCTION (Phase3 sc-p3-001) |
| Cross-page discovery (inventory-based, bounded) | PRODUCTION (Phase3 CAND-008 composed) |
| Runtime-verified full-container exhaustion | **NOT PRODUCTION** (see §10 gap) |

### H. TRAP / RECOVERY
- Trap: 7-field immutable evidence (Kind/Scope/Expected/Observed seq refs/Source/Evidence/LastAction); Agent emits (Agent-scope), Agent owns, Agent consumes. Trap != decision.
- Recovery: independent component (mechanism: recipe list/cursor; Agent: all decisions); Guard 7 no Container/Traversal reverse dep; no RecoveryRequest/Planner/Runtime.
- Flow: drift → Trap → Begin(recipe) → dispatch → fresh Observe → Verify(criteria) → Verified: Reconcile/Rebind/Resume | Failed: Agent Fail. Restore != success; Verify != Agent decision; RecoveryResult != GoalEvidence; single attempt (bounded, no recursion).

### I. PHYSICAL ACTION
| Action | Physical proof |
|---|---|
| Launch / Tap / SetSwitch (bounds-center translate) | REAL (physical-wifi-slice2: full REAL chain incl. ADB `input tap`); multi-level Settings navigation REAL (physical-settings-to-wifi); scroll mechanism REAL (physical-scroll-container-semantic-traversal); popup obstruction REAL (semantic-run-popup-obstruction); unexpected navigation REAL (semantic-run-unexpected-navigation) |
| Open-world type-level traversal dispatch | DETERMINISTIC/FIXTURE ONLY (U2 on Fake; no physical open-world traversal run) |

### J. VERIFICATION / GOAL EVIDENCE
- `GoalEvidence(Satisfied, Reason, SourceObservationSequence)` — kernel Runtime side; evaluator reports evidence, Agent decides Completed/Failed. Completion requires fresh post-action Observation evidence (I-10).
- Physical loop proven: `GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP` (all edges REAL: Goal → Agent belief → capability select → authorize → lower → PhysicalEnvironment → translator → ADB → fresh Observe → perception → GoalEvidence → Agent).
- Traversal-shaped completion (U2): `VerifiedBoundedTraversalCompletion` + fresh GoalEvidence — production, fixture-proven.

### K. OBSERVABILITY / CONTROL PLANE
- DSH can observe (read-only, frozen wire methods): run snapshot, run events (real RuntimeEvent stream — graduated), trap, evidence, run list, control support.
- Strictly read-only: no control-plane mutation of Runtime; client plugin GUI (commands + native console) is an observer.
- Shadow cognition (DSH-side, graduated) is host-side intelligence, not Runtime semantics.

### L. COGNITION / OUTER INTELLIGENCE
- Design only: `docs/decisions/outer-intelligence-integration-architecture.md` (IntelligenceSeam at adjudication points, TaskSpec/AgentProfile). ZERO production consumer; no `IntelligenceSeam`/`TaskSpec` symbols in src/.

## 2. Maturity Levels (SEMANTIC_MODEL vs DETERMINISTIC_MECHANISM vs INTEGRATED_REALITY)

| Capability | Semantic model | Deterministic mechanism | Reality proven |
|---|---|---|---|
| Toggle evidence | YES | YES (perception pipeline) | YES (Wi-Fi loop, reality-repair) |
| Bounded open-world Settings traversal | YES | YES (U2, fixture) | NO (no physical open-world run) |
| Container inventory completeness | PARTIAL (caller-injected `BranchInventoryEvidence` model) | NO (no Runtime-verified exhaustion) | NO |
| Navigation-candidate evidence | YES | YES (UIAutomator → StructuredElements → analyzer) | YES for evidence source (real hierarchy); NO for physical open-world traversal |
| Trap/Recovery | YES | YES | YES (launcher-drift on Fake; physical recovery not separately graduated) |
| Popup recovery | YES | YES | YES (semantic-run-popup-obstruction physical) |
| Scroll identity | YES | YES | YES (physical-scroll) |

## 3. Strongest Current Chains

- **Strongest deterministic chain**: Goal/type-level spec → fresh Observation → evidence → binding/state belief → Agent decision → Container/Traversal → fresh verification → GoalEvidence (U2 bounded open-world traversal: caller provides type-level constraints, Runtime discovers in-scope branches one at a time, verified parent return, sibling continuation, completion gated on VerifiedBoundedTraversalCompletion ∧ fresh GoalEvidence). Status: DETERMINISTIC_ONLY (fixture world).
- **Strongest physical reality chain**: `GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP` — SemanticGoal `WifiConnectivity.Enabled=true` → Agent belief → capability select → authorize → lower → PhysicalEnvironment → DeviceActionTranslator → ADB → fresh Observe → perception → GoalEvidence → Agent Completed, ALL edges REAL on emulator. Plus multi-level Settings navigation, scroll, popup, unexpected-nav physical loops.

## 4. Core Runtime Invariants (system-wide, not governance)

1. Runtime belief ≠ World truth — belief only from `Reconcile.FromObservation`; observation is the bridge; no cached state becomes reality by assertion.
2. Plan ≠ Reality — Plan/steps/spec are hypotheses from caller; fresh evidence governs.
3. Grounding ≠ Semantic identity authority — index/bounds are observation-local evidence, not long-lived identity.
4. Agent = sole semantic decision authority — completion, failure, recovery continuation, branch authorization, GoalEvidence consumption.
5. Environment = external-world observation/dispatch boundary — dispatch outcome ≠ world success.
6. Trap ≠ decision; Recovery ≠ Agent; Restore ≠ success; RecoveryResult ≠ GoalEvidence.
7. Completion only from satisfied GoalEvidence on fresh evidence (I-10).
8. One mutable state one owner; one decision one authority (I-2/I-3).

## 5. U2 Semantic Frontier (frozen boundary)

Graduated: bounded open-world Settings traversal — caller provides type-level world constraints; Runtime receives NO precompiled concrete next steps; fresh evidence determines in-scope branches; Agent authorizes one child at a time; verified parent return; sibling continuation; bounded traversal completion; fresh GoalEvidence.
Explicitly NOT proven by U2: unknown-world discovery; full Settings-tree inventory completeness; navigation-candidate perception (fixture supplies visible elements); physical reality.

## 6. Settings Navigation Candidate Evidence (new graduated fact)

Production capability: `Observation.StructuredElements` (from real Android UIAutomator hierarchy) → `InteractionAffordanceAnalyzer` → `InteractionAffordanceEvidence` classified NAVIGATION_CANDIDATE / LOCAL_CONTROL / UNKNOWN, Settings-scoped, caller-independent discovery, clickable≠navigation (distinguishes navigation rows from local actionable controls), generic-app claim NO, architecture/authority delta NONE.
- Adds: structured production evidence sufficient to distinguish navigation vs local-control candidates in real Settings hierarchy.
- Does NOT add: physical open-world traversal, inventory completeness, or a consumer that feeds this evidence into U2 traversal (no production link to `RunOpenWorldAsync`).

## 7. U2 × Settings-Navigation Composition (audit)

Production layers exist separately: U2 seam (`IntentExecution.RunOpenWorldAsync` + `Agent.RunOpenWorldAsync`) and settings-nav evidence (`StructuredElements` → `InteractionAffordanceAnalyzer`). There is **no production or test link** between them: `RunOpenWorldAsync` consumes a caller-injected `Goal.BranchInventoryEvaluator`; the settings-nav analyzer output is not wired into any inventory criterion, and no test composes them.
Classification: **INVENTORY_SEMANTIC_GAP** — even with real navigation-candidate evidence, the Runtime cannot truthfully assert "all required in-scope children discovered" because `RunOpenWorldAsync` trusts the caller's completeness declaration and owns no deterministic viewport-exhaustion loop (`OPEN_WORLD_VIEWPORT_EXHAUSTION_MECHANISM_MISSING`, per open-world-container-inventory-completeness proposal). This is the earliest missing semantic link in the future composition, NOT a current failure.

## 8. Physical Reality Frontier

Highest semantic capability crossed into real emulator/device world:
- **Minimum physical semantic loop** (Wi-Fi off→on, all edges REAL): `GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP`.
- Multi-level Settings navigation (physical-settings-to-wifi), scroll mechanism (physical-scroll-container-semantic-traversal), popup obstruction (semantic-run-popup-obstruction), unexpected navigation (semantic-run-unexpected-navigation), actionable toggle evidence + reality repair (perception-actionable-toggle-evidence + reality-repair).
- NOT yet physical: open-world bounded traversal (U2 fixture-only), inventory completeness (not built), recovery on physical launcher-drift (Fake only).
DeterministicVsPhysicalGap: deterministic semantics (U2 bounded open-world traversal, bounded exploration, cross-page discovery) exceed current physical proof (single-route WiFi/multi-level loops). This is a REALITY_INTEGRATION_GAP as a classification, but NOT a current buyer (no failing physical scenario today; §14 forbids manufacturing a device scenario to catch reality up).

## 9. Deferred Architecture

- Outer Intelligence (IntelligenceSeam adjudication, TaskSpec/AgentProfile): design only, zero consumer.
- BusinessIntent → autonomous capability selection: no consumer (U2 seam takes resolved type-level spec; callers select goals/specs today).
- Control plane expansion: current graduated read-only observability + event stream satisfies operator workflow; no blocked workflow.
- Trace-capture scenario catalog: infrastructure enhancement (durable capture, canonical catalog, catalog-driven replay) with zero Runtime semantic delta; consumers (testing/control-plane/run-start) currently satisfied by fixtures + ad-hoc golden replay — no blocked workflow.

## 10. Buyer Assessment (rule of 5: concrete consumer / current system cannot satisfy / exact earliest missing link / not aesthetics / no smaller mechanism)

| Candidate | Consumer | Current failure | Verdict |
|---|---|---|---|
| A. OPEN_WORLD_CONTAINER_INVENTORY_COMPLETENESS | None today — U2 runs on fixture (caller-injected inventory is honest there); settings-nav has no traversal consumer | No running scenario blocked | NO |
| B. U2_PRODUCTION_SETTINGS_EVIDENCE_INTEGRATION | None — no production open-world Settings traversal scenario exists | No running scenario blocked | NO |
| C. PHYSICAL_OPEN_WORLD_SETTINGS_REALITY_INTEGRATION | None — physical frontier is single-route loops | No failing physical scenario | NO |
| D. TASKSPEC_INTENT_ENTRY | None — callers supply resolved Goal/spec; no autonomy request | No consumer | NO |
| E. OUTER_INTELLIGENCE_ADJUDICATION | None — zero production symbols | No consumer | NO |
| F. TRACE_CAPTURE_SCENARIO_CATALOG | Testing/control-plane — currently satisfied by fixtures + ad-hoc replay | No blocked workflow | NO |
| G. CONTROL_PLANE_EXPANSION | Operator — satisfied by graduated read-only observability | No blocked workflow | NO |
| H. NO_IMMEDIATE_SYSTEM_EXPANSION | — | — | **SELECTED** |

## 11. Decision

- **SelectedBuyer = NO_IMMEDIATE_SYSTEM_EXPANSION** (SelectedBuyerType = NO_BUYER).
- EarliestMissingSystemLink (recorded, NOT purchased): `OPEN_WORLD_VIEWPORT_EXHAUSTION_MECHANISM` — Runtime-verified container inventory completeness for the future U2×settings-nav composition. It becomes a buyer only when a real scenario fails without it (e.g., a production Settings traversal that must truthfully terminate).
- NextAction: **FREEZE_CURRENT_SYSTEM_BASELINE_AND_WAIT_FOR_REAL_BUYER**.
- No new OpenSpec change; no reopened graduated capability; no new buyer manufactured by an active proposal (open-world-container-inventory-completeness stays PROPOSED_NO_CURRENT_BUYER; trace-capture stays PROPOSED_NO_CURRENT_BUYER).

---

# Baseline Freeze (2026-08-16)

> Gate: `PROJECT_LEADER_FREEZE_CURRENT_SYSTEM_BASELINE` | MODE: `SYSTEM_BASELINE_FREEZE`
> Status: `NO_IMMEDIATE_SYSTEM_EXPANSION` | CurrentBuyer: NONE
> Implementation/New-OpenSpec/Runtime/Perception/DSH: all FROZEN.

## Frozen Frontiers

| Frontier | Maturity | Meaning |
|---|---|---|
| Deterministic | `U2_BOUNDED_OPEN_WORLD_SETTINGS_TRAVERSAL_GRADUATED` | caller supplies type-level constraints; Runtime receives no concrete precompiled next steps; fresh evidence determines in-scope branches; Agent authorizes one child at a time; verified parent return; sibling continuation; bounded traversal completion; fresh GoalEvidence |
| Physical Reality | `GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP` + physical multi-level Settings navigation / scroll / popup / unexpected-navigation | real emulator loops with all edges REAL |
| Perception | `PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED` + `SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE` | toggle evidence + navigation-candidate evidence (UIAutomator → StructuredElements → InteractionAffordanceEvidence) |
| Trap/Recovery | `PHASE2_DETERMINISTIC_TRAP_RECOVERY_BASELINE_GRADUATED` | Trap evidence / independent Recovery mechanism / Agent decision authority |
| Design-only (deferred) | Outer Intelligence (IntelligenceSeam), TaskSpec | DESIGN_FROZEN_DEFERRED; zero production symbols |

## Known but Unbought Pressure

- `OPEN_WORLD_VIEWPORT_EXHAUSTION_MECHANISM` — production Runtime cannot independently prove from real Settings evidence that all required in-scope children are discovered/exhausted.
- Classification: **KNOWN_ARCHITECTURAL_PRESSURE**, NOT ACTIVE_BUYER. Do not implement until a concrete runtime consumer is blocked.

## Reentry Rule

System expansion resumes ONLY when a new concrete consumer/failure exists. A candidate buyer must satisfy ALL:
1. A real current workflow/scenario exists.
2. Current UniClaw cannot satisfy it.
3. The failure is reproducible or durably evidenced.
4. The earliest missing system link can be identified.
5. Existing graduated capabilities cannot already solve it.

## Reentry Triggers (do NOT pre-arm)

- **A — Open-World Inventory**: only when a production traversal over real Settings evidence requires truthful termination and fails because Runtime cannot establish all required in-scope children discovered/exhausted → candidate `OPEN_WORLD_CONTAINER_INVENTORY_COMPLETENESS`, earliest link `OPEN_WORLD_VIEWPORT_EXHAUSTION_MECHANISM`. Not activated merely because the proposal exists.
- **B — Physical Reality**: only when an actual physical/emulator scenario needs an already-deterministic capability and fails at the reality integration layer; classify the exact failure (perception/binding/state belief/navigation/inventory/dispatch/verification/environment). No synthetic device scenarios to raise maturity.
- **C — TaskSpec/Intent**: only when a real caller has business intent but cannot reasonably provide the currently required Goal / type-level traversal specification / capability selection, blocking a real workflow → candidate `TASKSPEC_INTENT_ENTRY`.
- **D — Outer Intelligence**: only when Agent reaches a real state where deterministic evidence exists, deterministic rules cannot truthfully adjudicate, continuation is required by a current workflow, and an advisory answer has a defined consumer → candidate `OUTER_INTELLIGENCE_ADJUDICATION`. Authority: Intelligence = advice; Agent/Kernel = final adjudication.
- **E — Control Plane**: only when a real operator workflow is blocked by a missing control/observation capability (cannot start required run / inspect required evidence / perform required deterministic control). Select the narrow missing capability; no completeness-driven UI expansion.
- **F — Scenario Catalog**: only when an actual consumer requires an authoritative scenario catalog and current fixtures/ad-hoc replay cannot satisfy it. Until then `trace-capture-scenario-catalog-foundation` = DEFERRED_NO_BUYER.

## Active OpenSpec Semantics (directory presence ≠ buyer state)

- `greenfield-agent-runtime` → LONG_LIVED_BASELINE_BY_DESIGN
- `open-world-container-inventory-completeness` → DEFERRED_NO_BUYER
- `trace-capture-scenario-catalog-foundation` → DEFERRED_NO_BUYER

## Project Leader Rule

On the next new product/runtime request: DO NOT resume the oldest deferred change; DO NOT implement the known earliest missing link automatically; DO NOT follow roadmap order automatically. Instead run a fresh `PROJECT_LEADER_SELECT_NEXT_SYSTEM_BUYER` against the concrete new consumer/failure.

## State

```text
SYSTEM_BASELINE_FROZEN
NextGate: NONE
NextAction: FREEZE_CURRENT_SYSTEM_BASELINE_AND_WAIT_FOR_REAL_BUYER
```
