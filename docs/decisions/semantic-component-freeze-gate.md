# Semantic Component Freeze Gate

> 2026-08-11 | Status: FREEZE_READY
> Baseline: SEMANTIC_COMPONENT_EXTRACTION_READY · 661/661 tests · 14/14 invariants
> Purpose: Freeze ownership, dependency, and naming rules. Not a refactor mandate.
> Scope: Architecture gate. No code changes.

---

## 1. Frozen Ownership Rules

### 1.1 Agent — Run-Level Semantic Decision Authority

```
Freeze: Agent is the sole authority for:
  - Semantic goal execution (closed loop)
  - Capability selection
  - Action authorization
  - Goal satisfaction adjudication
  - Recovery orchestration
  - RunState lifecycle

Agent MUST:
  - Own RunState, TraceEvent, BranchProgress (I-2)
  - Read Container belief; never duplicate it
  - Escalate via Trap (I-8)
  - Terminate only on GoalEvidence (I-10)

Agent MUST NOT:
  - Own page-local state
  - Own element-level grounding
  - Execute physical actions directly
  - Own Capability or SemanticObject definitions (declarative, immutable)
```

### 1.2 Container — Page-Local State Owner

```
Freeze: Container is the sole mutable owner of:
  - Page belief (_localPageBeliefState)
  - Object bindings (_objectBindings)
  - Object state beliefs (_objectStateBeliefs)
  - Current observation (_observation)
  - Executed steps, viewport observations
  - Local completion flag

Container MUST:
  - Expose state as immutable snapshots only (I-2)
  - Escalate unresolved/contradicted belief via Trap (I-8)
  - Reset state on Bind()

Container MUST NOT:
  - Make business-semantic decisions (I-3: Agent owns that)
  - Reference Agent, Environment, or Traversal (I-1)
  - Select capabilities or authorize actions
  - Own Capability or SemanticObject definitions
```

### 1.3 Traversal — Grounding + Lowering

```
Freeze: Traversal is the sole authority for:
  - Candidate selection (Text + SwitchState grounding)
  - Protocol token → DeviceAction compilation
  - SemanticAction → ExecutionAction lowering
  - Post-action sequence verification
  - Step-scope retry

Traversal MUST:
  - Lower only AUTHORIZED SemanticActions
  - Enforce safety rules: unknown state, already satisfied, ambiguous surface
  - Carry Bounds through to DeviceAction

Traversal MUST NOT:
  - Select business capabilities (Agent owns that)
  - Choose semantic targets (Agent authorizes via binding)
  - Adjudicate world state
  - Reference Agent or Container (I-1)
```

### 1.4 Environment — Physical Boundary

```
Freeze: Environment is the sole authority for:
  - Producing Observation (ObserveAsync)
  - Executing DeviceAction (ExecuteAsync)
  - Mapping Bounds → physical coordinates

Environment MUST:
  - Populate ObservedElement fields from perception
  - Report dispatch outcome only (never world effect)

Environment MUST NOT:
  - Perform semantic interpretation
  - Select targets
  - Authorize actions
  - Adjudicate world state
```

### 1.5 Stateless Evidence Producers

```
Freeze: The following are stateless pure functions:
  - PageAnalysis.Analyze
  - ElementAnalysis.Analyze + ReconcileBindings
  - SemanticReconciliation.FuseBelief
  - Reconcile.FromObservation (legacy)

Each MUST:
  - Produce immutable output from immutable input
  - Own no mutable state
  - Depend only on Model types
  - Be deterministic (same input → same output)

Each MUST NOT:
  - Own mutable state
  - Reference Agent, Container, Traversal, or Environment
  - Make business decisions
```

### 1.6 Domain Model Types

```
Freeze: The following are immutable declarative records:
  - SemanticObject, Capability
  - SemanticAction, SemanticGoalInput
  - SemanticEvidence, SemanticEvidenceStance, SemanticBeliefState
  - ObjectBinding, ElementBindingCriteria, PageAnalysisCriteria
  - DeviceAction variants
  - Trap, GoalEvidence, WorldBelief

Each MUST:
  - Be immutable (sealed record)
  - Carry no mutable state
  - Have no mutable owner

Each MUST NOT:
  - Contain UI execution details in domain types
  - Contain domain semantics in execution types
```

---

## 2. Frozen Dependency Direction

```
                    ┌──────────┐
                    │  Agent   │  semantic decisions, lifecycle
                    └────┬─────┘
                         │ reads belief, calls methods
                    ┌────▼─────┐
                    │ Container│  page-local state, belief
                    └────┬─────┘
                         │ binding + observation
                    ┌────▼─────┐
                    │ Traversal│  grounding, lowering
                    └────┬─────┘
                         │ DeviceAction
                    ┌────▼─────┐
                    │Environment│ physical dispatch
                    └──────────┘

STATELESS (depend only on Model):
  PageAnalysis ──→ Model types only
  ElementAnalysis ──→ Model types only
  SemanticReconciliation ──→ Model types only

DOMAIN MODEL (no dependencies):
  SemanticObject, Capability, SemanticAction, ...
```

### Forbidden Dependencies

```
Agent       ← Container     NO (I-1: Agent→Container, never reverse)
Container   ← Traversal     NO (I-1)
Traversal   ← Environment   NO (I-1)

Any Runtime ← Perception producing semantic verdicts  NO (I-4, I-14)
Container   ← Agent state duplication                 NO (I-2)
Traversal   ← Business capability selection           NO (I-3)
Environment ← Target selection                        NO (I-3)
Domain types ← Mutable state                          NO (immutable)
Stateless   ← Mutable state                           NO (stateless)
```

---

## 3. Approved Responsibility Names

These are documentation names for architecture discussions. They refer to existing code locations. They do NOT mandate extraction or renaming.

### 3.1 Run-Level (Agent)

| Responsibility | Name | Current Code |
|---|---|---|
| Closed-loop semantic execution | **SemanticGoalRunner** | `Agent.RunSemanticGoalAsync` |
| Capability selection | **CapabilitySelector** | Inline in Agent |
| Action authorization | **ActionAuthorizer** | `Agent.AuthorizeAction` |
| Plan execution (legacy) | **PlanExecutor** | `Agent.RunAsync` |
| Goal satisfaction evidence | **GoalEvaluator** | `Goal.EvidenceEvaluator` (caller-injected) |

### 3.2 Page-Local (Container)

| Responsibility | Name | Current Code |
|---|---|---|
| Page belief fusion | **PageBeliefFusion** | `Container.EvaluatePageBelief` |
| Object binding management | **ObjectBindingState** | `Container.UpdateBindings` |
| Object state belief | **ObjectStateBelief** | `Container.RefreshObjectStateBeliefs` |

### 3.3 Execution (Traversal)

| Responsibility | Name | Current Code |
|---|---|---|
| Semantic action lowering | **ActionLowering** | `Traversal.LowerAction` |
| Step execution kernel | **StepExecutor** | `Traversal.ExecuteStep` |
| Candidate grounding | **TargetGrounding** | `Traversal.Select` |

### 3.4 Evidence (Stateless)

| Responsibility | Name | Current Code |
|---|---|---|
| Page identity evidence | **PageEvidenceProducer** | `PageAnalysis.Analyze` |
| Object binding evidence | **ObjectBindingProducer** | `ElementAnalysis.Analyze` |
| Binding reconciliation | **BindingReconciler** | `ElementAnalysis.ReconcileBindings` |
| Multi-source fusion | **EvidenceFusion** | `SemanticReconciliation.FuseBelief` |
| Legacy world belief | **WorldBeliefProducer** | `Reconcile.FromObservation` |

### 3.5 Future Capabilities

| Responsibility | Name | Phase | Attachment Point |
|---|---|---|---|
| Intent → SemanticGoalInput | **IntentCompiler** | Phase 6 | `SemanticGoalInput` type |
| Screenshot region → ON/OFF | **StateClassifier** | Perception Bridge | `ObservedElement.SwitchState` |

---

## 4. Naming Principles (Mandatory for Future Components)

### 4.1 Must Be Immediately Understandable

A developer reading the name for the first time should understand what the component OWNS.

```
✓ IntentCompiler      — "compiles intent into something"
✓ StateClassifier     — "classifies state of something"
✓ GoalEvaluator       — "evaluates whether a goal is satisfied"
✓ ActionAuthorizer    — "authorizes actions"
✓ CapabilitySelector  — "selects a capability"
```

### 4.2 Must Be Short, Domain-Oriented, Concrete Nouns

```
✓ IntentCompiler      — two words, domain (Intent), concrete action (Compiler)
✓ TargetGrounding     — two words, domain (Target), concrete action (Grounding)
✓ PageEvidenceProducer — three words, domain (Page+Evidence), concrete role (Producer)
```

### 4.3 Must Not Use Generic Suffixes (Unless Ownership Requires It)

```
✗ SemanticRuntimeOrchestrationManager
✗ IntelligentDecisionProcessingEngine
✗ AbstractSemanticCoordinator
✗ GoalStateObservationVerificationManager
✗ WorldBeliefUpdateProcessor

"Manager", "Processor", "Handler", "Engine", "Controller", "Coordinator",
"Orchestrator" are banned unless the component genuinely manages/orchestrates
the lifecycle of MULTIPLE sub-components.

A component that produces evidence is a "Producer", not a "Manager".
A component that classifies is a "Classifier", not a "Processor".
```

### 4.4 Must Not Combine Multiple Responsibilities in One Name

```
✗ GoalStateObservationVerificationManager
   → Split into: GoalEvaluator + StateObserver + VerificationChecker
   (if they genuinely have independent change reasons)

✗ SemanticActionAuthorizationLoweringCoordinator
   → Split into: ActionAuthorizer + ActionLowering
   (they already exist as separate responsibilities)
```

### 4.5 Legacy Names — Historical Reference Only

```
feature/refactor names are for historical context. Do not inherit:
  - PageAnalyzer (implies AI oracle — current is evidence producer)
  - PageFingerprint (hash-as-identity — replaced by evidence-backed belief)
  - PopupDetector (regex classifier — replaced by evidence-based hypothesis)
```

---

## 5. Forbidden Future Coupling

### 5.1 Cross-Layer Dependencies

```
FORBIDDEN:
  - Traversal calling Agent methods
  - Container holding Agent reference
  - Environment performing semantic classification
  - Stateless producers accessing Container state
  - Domain model types referencing Runtime components
```

### 5.2 Shared Mutable State

```
FORBIDDEN:
  - Two components mutating the same object
  - Agent holding a copy of Container._objectStateBeliefs that it mutates
  - Container holding a copy of Agent._state that it mutates
  - Any static mutable field in evidence producers
```

### 5.3 Authority Inversion

```
FORBIDDEN:
  - Traversal selecting Capability (Agent owns)
  - Environment selecting SemanticObject (Agent + Container own)
  - Container completing a Run (Agent owns I-10)
  - PageAnalysis returning a page verdict string (evidence producer, not oracle)
```

---

## 6. Allowed Extension Points

### 6.1 New Evidence Sources

```
PageAnalysis.Analyze:
  Add new Add*Evidence methods.
  Example: AddBoundsDistributionEvidence, AddTypeDistributionEvidence.
  Does NOT change the contract (Observation → SemanticEvidence[]).

ElementAnalysis.Analyze:
  Add new binding signals.
  Example: depth-based grouping, visual similarity.
  Does NOT change the contract.
```

### 6.2 New Capabilities

```
Capability records:
  Add new Capability instances to the catalog.
  Example: Capability.Define("SetVisibility", "DisplaySetting", "Visible").
  Does NOT change the Capability type.

Capability selection:
  Enhance selection logic (priority, preconditions, multi-dimension).
  Does NOT change ownership (Agent still owns selection).
```

### 6.3 New SemanticObject Types

```
SemanticObject records:
  Add new SemanticObject instances to the catalog.
  Example: SemanticObject.Define("BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]).
  Does NOT change the SemanticObject type.
```

### 6.4 New Perception Adapters

```
IEnvironment implementations:
  Add real-device perception adapter.
  Populates ObservedElement.SwitchState from StateClassifier.
  Does NOT change the IEnvironment contract or Runtime architecture.
```

### 6.5 New SemanticRunResult States

```
SemanticRunResult variants:
  Add new terminal states if required by new capabilities.
  Does NOT change the discriminated union pattern.
```

---

## 7. Freeze Verdict

```
SEMANTIC_COMPONENT_FREEZE_READY

Frozen:
  ✓ 6 ownership rules (Agent, Container, Traversal, Environment, Stateless, Domain)
  ✓ 1 dependency direction (Agent→Container→Traversal→Environment)
  ✓ 17 approved responsibility names
  ✓ 7 naming principles
  ✓ 3 forbidden coupling categories (cross-layer, shared state, authority inversion)
  ✓ 5 allowed extension points

NOT frozen (by design):
  - Internal implementation of each responsibility
  - Method signatures (can evolve within ownership boundary)
  - Test organization
  - File/directory structure

Architecture invariants: 14/14 respected.
Test suite: 661/661 pass.

This gate is the baseline for all future capability purchases.
Any proposed change that violates a frozen rule requires an
ARCHITECTURE_GATE_REQUIRED review.

STOP.
```
