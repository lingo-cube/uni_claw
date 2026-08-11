# Semantic Core Consolidation Review

> 2026-08-11 | Status: FREEZE_READY
> Baseline: 661/661 tests pass · 6 phases · 14/14 invariants respected
> Scope: Freeze boundaries. No refactor. No new capability.

---

## 1. Current Semantic Responsibility Map

### 1.1 Agent — Run-Level Semantic Decision Authority

| Responsibility | Boundary | Contract | Owner Correct? |
|---|---|---|---|
| **Closed-loop semantic execution** (`RunSemanticGoalAsync`) | Receives `SemanticGoalInput` + domain catalog. Runs READ→DECIDE→ACT→OBSERVE→UPDATE loop. Manages RunState, TraceEvent, lifecycle. | `(goal, objects, capabilities, runId) → SemanticRunResult` | **YES** — Agent is sole run-level authority (I-3). Loop termination decisions (Satisfied, BudgetExhausted, StateEvidenceRequired) belong here. |
| **Capability selection** (inline in RunSemanticGoalAsync) | Given object + desired state dimension, selects Capability from catalog by `ApplicableToCategory + StateDimension`. Exactly 1 required. | `(obj, goal, capabilities) → Capability \| null` | **YES** — capability selection is a semantic decision. Agent owns it. |
| **Action authorization** (`AuthorizeAction`) | Validates: category match, dimension declaration, well-formedness. Returns null if valid, Invalid result if not. | `(action, obj, capability) → SemanticActionResult?` | **YES** — static pure validation. Agent is the authority. |
| **Plan execution** (`RunAsync`, `RunOpenWorldAsync`) | Closed-world: executes pre-compiled PlanStep sequence. Open-world: type-directed branch dispatch. | `(goal, plan/context) → RunState` | **YES** — legacy compatibility + OpenWorld. |
| **Drift detection** (`IsAgentScopeDrift`) | Foreground≠baseline + !IsStillMine + page=null → drift. | `(obs, container, belief) → bool` | **YES** — pure predicate. |
| **Recovery orchestration** (`RecoverFromDriftAsync`) | Begin → dispatch recipe → observe → verify → reconcile → rebind → resume. | RecoveryAnchor → RunState | **YES** — Agent owns recovery decisions (HG-4). |

**Agent coupling:** Agent depends on Container, Traversal, Recovery, Startup, World, Model. This is correct per I-1 (Agent→Container→Traversal→Environment). Agent is the top of the spine — it MUST know about all lower layers.

### 1.2 Container — Page-Local State Owner

| Responsibility | Boundary | Contract | Owner Correct? |
|---|---|---|---|
| **Page belief** (`EvaluatePageBelief`) | Fuses LOCAL_IDENTITY + external SemanticEvidence → SemanticBeliefState. Stores in `_localPageBeliefState`. | `(obs, evidence[]) → SemanticBeliefState` | **YES** — Container is sole mutable owner (I-2). Evidence fusion is a pure operation. |
| **Object bindings** (`UpdateBindings`) | Holds `ImmutableArray<ObjectBinding>` — which ObservedElements currently instantiate which SemanticObjects. Observation-local. | `bindings → void` (sets `_objectBindings`) | **YES** — Container owns this state. |
| **Object state belief** (`RefreshObjectStateBeliefs`) | Reads bound elements' SwitchState. Exactly 1 PerceptionType="toggle" element → belief=true/false. 0 or >1 → null (unknown). | `obs? → void` (sets `_objectStateBeliefs`) | **YES** — pure evidence operation. Container owns the state. |
| **Continuity gates** (`TryVerifyLocalContinuity`, `TryVerifyViewportContinuity`) | Fresh sequence + compatible foreground + IsStillMine + reconciled page match → accept observation. | `(obs, page, foreground) → bool` | **YES** — protocol gates, not adjudication. Agent decides consequence. |
| **Identity rule** (`IsStillMine`) | Delegates to injected lambda. | `obs → bool` | **PARTIAL** — still caller-injected. Target: evidence-backed, not single oracle. Not this review's scope. |

**Container state consolidation:** Container now holds 7 mutable fields. All are page-local, observation-local, evidence-backed. No duplication. Each has a single mutation path. I-2 respected — cross-owner exposure is immutable snapshots.

**Note:** Container is both a "page state holder" and an "object state holder." These are co-located because both are observation-local and page-scoped. Separating them would create a second mutable owner for the same observation scope — violating I-2. Correct as-is.

### 1.3 Traversal — Grounding + Lowering

| Responsibility | Boundary | Contract | Owner Correct? |
|---|---|---|---|
| **Semantic action lowering** (`LowerAction`) | Authorized SemanticAction → ExecutionAction. Safety rules: unknown state, already satisfied, ambiguous surface. | `(action, binding, obs) → SemanticActionResult` | **YES** — static pure function. Traversal lowers; does not decide business semantics. |
| **Step execution** (`ExecuteStep`) | Select→Check→Execute→Verify→Branch. Text+SwitchState grounding. | `(step, obs, candidates) → TraversalStepResult` | **YES** — existing execution kernel. Legacy text grounding path. |
| **Candidate selection** (`Select`, `SelectGrounded`) | Text match + SwitchState disambiguation + authorization receipts. | `(step, candidates) → int?` | **YES** — existing grounding mechanism. |
| **Action construction** (`BuildAction`) | Protocol token → DeviceAction. Includes Bounds passthrough. | `(description, index, bounds) → DeviceAction?` | **YES** — mechanical token→action mapping. |

**Traversal coupling:** LowerAction is a static method that depends only on Model types. ExecuteStep depends on IEnvironment. These are correctly separated — LowerAction has no environment coupling.

### 1.4 Stateless Evidence Producers

| Capability | File | Contract | Owner |
|---|---|---|---|
| **Page evidence** | `World/PageAnalysis.cs` | `Analyze(Observation, PageAnalysisCriteria) → ImmutableArray<SemanticEvidence>` | Stateless |
| **Object binding evidence** | `World/ElementAnalysis.cs` | `Analyze(Observation, ElementBindingCriteria) → ImmutableArray<SemanticEvidence>` | Stateless |
| **Binding reconciliation** | `World/ElementAnalysis.cs` | `ReconcileBindings(evidence, objects) → ImmutableArray<ObjectBinding>` | Stateless |
| **Belief reconciliation** | `World/Reconcile.cs` | `FromObservation(Observation, Func) → WorldBelief` | Stateless |
| **Evidence fusion** | `Model/SemanticReconciliation.cs` | `FuseBelief(SemanticEvidence[]) → SemanticBeliefState` | Stateless |
| **Spatial predicate** | `World/ElementAnalysis.cs` | `SameRow(ElementBounds, ElementBounds) → bool` | Stateless |

All follow the same pattern: static pure function, no state, deterministic. Correct. No coupling issues.

### 1.5 Immutable Domain Model

| Concept | Type | File |
|---|---|---|
| Domain object | `SemanticObject` (record) | `Model/SemanticObject.cs` |
| Domain capability | `Capability` (record) | `Model/Capability.cs` |
| Semantic action | `SemanticAction` (record) | `Model/SemanticAction.cs` |
| Semantic goal | `SemanticGoalInput` (record) | `Model/SemanticGoalInput.cs` |
| Run result | `SemanticRunResult` (discriminated union) | `Model/SemanticRunResult.cs` |
| Action result | `SemanticActionResult` (discriminated union) | `Model/SemanticActionResult.cs` |
| Evidence | `SemanticEvidence` + `Stance` + `BeliefState` + `Reconciliation` | `Model/` |
| Binding | `ObjectBinding` + `ElementBindingCriteria` | `Model/` |
| Page knowledge | `PageAnalysisCriteria` | `Model/PageAnalysisCriteria.cs` |
| Goal | `Goal` + `GoalEvidence` | `Model/` |
| Execution | `DeviceAction`, `Plan`, `PlanStep`, `TargetGroundingCriterion` | `Model/` |
| Escalation | `Trap`, `TrapKind`, `TrapScope` | `Model/` |
| World belief | `WorldBelief` | `Model/WorldBelief.cs` |

All immutable records. No mutable state. No singleton services. Correct.

---

## 2. Candidate Component Boundaries (Readable Domain Names)

These are NOT proposed refactors. They are NAMES for the existing boundaries — to be used in documentation, architecture discussions, and future module tasks. The code stays where it is.

| Domain Name | What It Names | Current Location |
|---|---|---|
| **SemanticGoalRunner** | Closed-loop semantic execution | `Agent.RunSemanticGoalAsync` |
| **PlanExecutor** | Plan-step execution | `Agent.RunAsync` |
| **ActionAuthorizer** | Capability→Object→Action validation | `Agent.AuthorizeAction` |
| **CapabilitySelector** | Select capability for object+dimension | Inline in `Agent.RunSemanticGoalAsync` |
| **ActionLowering** | SemanticAction → ExecutionAction | `Traversal.LowerAction` |
| **StepExecutor** | Select→Execute→Verify kernel | `Traversal.ExecuteStep` |
| **PageEvidenceProducer** | Page identity evidence | `PageAnalysis.Analyze` |
| **ObjectBindingProducer** | Element→Object binding evidence + reconciliation | `ElementAnalysis.Analyze` + `ReconcileBindings` |
| **PageBeliefFusion** | Evidence→Container belief | `Container.EvaluatePageBelief` |
| **ObjectStateTracker** | Binding + state belief management | `Container.UpdateBindings` + `RefreshObjectStateBeliefs` |
| **WorldBeliefProducer** | Observation→WorldBelief | `Reconcile.FromObservation` |
| **EvidenceFusion** | Multi-source evidence→belief state | `SemanticReconciliation.FuseBelief` |
| **DomainCatalog** | SemanticObject + Capability definitions | `SemanticObject` + `Capability` records |
| **GoalEvaluator** | Goal satisfaction evidence | `Goal.EvidenceEvaluator` (caller-injected) |

---

## 3. Coupling Analysis

### 3.1 Duplicated Concepts — NONE FOUND

| Check | Result |
|---|---|
| Two "goal" concepts? | `Goal` (old, caller-injected evaluators) and `SemanticGoalInput` (new, structured outcome). Different purposes — not duplicate. `SemanticRunResult.Satisfied` carries `GoalEvidence` to bridge them. |
| Two "belief" concepts? | `WorldBelief` (Agent-held, binary confidence) and `Container._localPageBeliefState` (Container-held, qualitative). Different owners, different scopes. Not duplicate. `WorldBelief` is legacy compatibility. |
| Two "action" concepts? | `SemanticAction` (domain effect) and `DeviceAction` (physical primitive). Different layers. Not duplicate. |
| Two "binding" concepts? | Only `ObjectBinding`. No duplicate. |

### 3.2 Unclear Ownership — 1 FOUND

| Issue | Severity | Detail |
|---|---|---|
| **Capability selection** has no named boundary | LOW | Currently inline in `RunSemanticGoalAsync`. Logic is simple (filter by category+dimension, require exactly 1). Does not need extraction — but should be named for documentation. Suggested name: `CapabilitySelector`. |

### 3.3 Hidden Dependencies

| Dependency | Status |
|---|---|
| Agent → Container → Traversal → Environment | **Correct** (I-1). Not hidden — explicit in constructor injection. |
| `RunSemanticGoalAsync` depends on `_traversal._environment` via `_recovery.ExecuteActionAsync` | **Indirect** — dispatch goes through Recovery component. Acceptable for Phase 5. |
| `RefreshObjectStateBeliefs` hardcodes "Enabled" as key suffix + "toggle" as PerceptionType filter | **Temporary** — Phase 5 minimum. Future: generalize to arbitrary state dimensions and control types. Not a coupling issue. |

### 3.4 Future Attachment Points

| Future Capability | Attaches To | Ready? |
|---|---|---|
| **SwitchState perception** (real device) | `Container.RefreshObjectStateBeliefs` — reads `ObservedElement.SwitchState`. When real perception provides SwitchState, this method consumes it without change. | **YES** — attachment point exists. |
| **VLM page reasoning** | `Container.EvaluatePageBelief` — accepts `params SemanticEvidence[]`. VLM evidence arrives as `SemanticEvidence(Source="VISUAL_SEMANTIC", ...)`. No code change needed. | **YES** — attachment point exists. |
| **Intent Compilation** (Phase 6) | `SemanticGoalInput` — structured desired outcome. Natural language → `SemanticGoalInput`. | **YES** — input type exists. |
| **Claim-aware evidence fusion** | `SemanticReconciliation.FuseBelief` — currently aggregates all evidence regardless of claim. Claim-aware fusion is a separate purchase. | **PARTIAL** — fusion exists, but is claim-unaware. |
| **Additional evidence sources** | `PageAnalysis.Analyze` — add new `Add*Evidence` methods. | **YES** — pattern scales. |
| **Element interaction semantics** | `ElementAnalysis.Analyze` — add new signal types. | **YES** — pattern scales. |
| **OpenWorld semantic loop** | `Agent.RunSemanticGoalAsync` — currently closed-world only. OpenWorld needs child-container management. | **PARTIAL** — loop exists, OpenWorld integration deferred. |

---

## 4. Freeze Proposal

### 4.1 Components to Freeze

| Component | Freeze Boundary | Rationale |
|---|---|---|
| **Agent** | Sole run-level semantic decision authority. Owns: RunState, TraceEvent, semantic loop, capability selection, action authorization, plan execution, drift detection, recovery orchestration. | I-3. 14/14 invariants respected. No violation found. |
| **Container** | Sole page-local mutable state owner. Owns: observation, page belief, object bindings, object state beliefs, continuity gates, executed steps, viewport observations. | I-2. All cross-owner exposure is immutable snapshots. No duplicate owner. |
| **Traversal** | Grounding + lowering only. Owns: Select, Execute, Verify, BuildAction, LowerAction. Does NOT own: business semantics, capability selection, target object selection. | I-3. LowerAction is a static pure function with no environment coupling. |
| **PageAnalysis** | Stateless pure function. `Analyze(Observation, PageAnalysisCriteria) → SemanticEvidence[]`. | Pattern is correct. Scales to additional evidence sources. |
| **ElementAnalysis** | Stateless pure function. `Analyze(Observation, ElementBindingCriteria) → SemanticEvidence[]`. `ReconcileBindings → ObjectBinding[]`. `SameRow` spatial predicate. | Pattern is correct. Scales to additional signals. |
| **SemanticReconciliation** | Stateless pure function. `FuseBelief(SemanticEvidence[]) → SemanticBeliefState`. | Contract is correct. Claim-aware fusion is a separate future purchase. |
| **Reconcile** | Stateless pure function. `FromObservation(Observation, Func) → WorldBelief`. | Legacy compatibility. |

### 4.2 Contracts to Freeze

| Contract | Definition | File |
|---|---|---|
| **SemanticEvidence** | `{ Source: string, Claim: string, Stance: Supports\|Contradicts\|Insufficient, Reason?: string }` | `Model/SemanticEvidence.cs` |
| **SemanticBeliefState** | `Supported \| Unresolved \| Contradicted` | `Model/SemanticBeliefState.cs` |
| **SemanticObject** | `{ Identity, Category, StateDimensions }` — immutable domain concept | `Model/SemanticObject.cs` |
| **Capability** | `{ Name, ApplicableToCategory, StateDimension }` — immutable domain contract | `Model/Capability.cs` |
| **SemanticAction** | `{ ObjectIdentity, CapabilityName, StateDimension, DesiredValue }` — domain effect | `Model/SemanticAction.cs` |
| **SemanticGoalInput** | `{ ObjectIdentity, StateDimension, DesiredValue }` — structured desired outcome | `Model/SemanticGoalInput.cs` |
| **ObjectBinding** | `{ ObjectIdentity, ElementIndices, EvidenceBasis }` — observation-local | `Model/ObjectBinding.cs` |
| **ElementBounds** | `{ X1, Y1, X2, Y2 }` — normalized [0,1]×[0,1] | `Model/Observation/ElementBounds.cs` |
| **DeviceAction** | `Tap \| SetSwitch \| ScrollForward \| LaunchApp` | `Model/Actions/DeviceAction.cs` |
| **Trap** | `{ Kind, Scope, Expected, Observed, Source, Evidence, LastAction }` | `Model/Trap.cs` |

### 4.3 Ownership Rules (Frozen)

1. **Agent** is the sole semantic decision authority. No lower layer may make business-semantic decisions.
2. **Container** is the sole page-local mutable state owner. Agent reads Container state; never duplicates it.
3. **Traversal** grounds and lowers. Traversal receives authorized actions; never selects business capabilities or target objects.
4. **Stateless producers** (PageAnalysis, ElementAnalysis, SemanticReconciliation, Reconcile) produce immutable evidence. They own no state.
5. **Domain model types** (SemanticObject, Capability, SemanticAction, SemanticGoalInput) are immutable records. They are declarative knowledge — no mutable state, no singleton services.
6. **Perception** (IEnvironment) produces Observation. It performs no semantic interpretation.
7. **Environment** (adapter) executes physical actions. It selects no targets.

### 4.4 Forbidden Dependencies (Frozen)

| Forbidden | Reason |
|---|---|
| Container → Agent | I-1: Agent→Container, not reverse |
| Traversal → Agent or Container | I-1: dependency flows down |
| PageAnalysis / ElementAnalysis → Container or Agent | Stateless producers depend only on Model |
| Environment → any Runtime semantic component | Environment is the physical boundary |
| Any component → duplicate mutable state of another owner | I-2: one owner per mutable state |
| Traversal → business capability selection | I-3: Agent is sole semantic authority |
| SemanticObject / Capability → mutable state | Domain concepts are immutable |

---

## 5. Verdict

```
SEMANTIC_CORE_FREEZE_READY

No architecture changes required.
No refactoring required.
No new components required.
No ownership violations found.

Current state:
  - 661/661 tests pass
  - 14/14 invariants respected
  - 6 phases (5 validated, 1 pending)
  - All semantic responsibilities have clear owners
  - All contracts are frozen
  - All ownership rules are respected
  - Attachment points exist for all known future capabilities

One noted issue (not blocking):
  - Capability selection logic is inline in RunSemanticGoalAsync.
    Does not need extraction. Name it "CapabilitySelector" in documentation.

Next:
  Phase 6 — INTENT_COMPILATION
  ("开启 Wi‑Fi" → SemanticGoalInput(WifiConnectivity, Enabled, true))
```

STOP.
