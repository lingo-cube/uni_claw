# Semantic Component Extraction Analysis

> 2026-08-11 | Baseline: SEMANTIC_CORE_FREEZE_READY · 661/661 tests · 5/6 phases validated
> Scope: Analysis only. No code changes.

---

## 1. Responsibility Graph

### 1.1 Run-Level Semantic Decision

| | |
|---|---|
| **Current owner** | `Agent` (`src/UniClaw.Runtime/Agent/Agent.cs`) |
| **Responsibility** | Closed-loop semantic execution: READ belief → DECIDE → ACT → OBSERVE → UPDATE → RE-EVALUATE. RunState lifecycle. TraceEvent recording. |
| **Input** | `SemanticGoalInput` + domain catalog (`SemanticObject[]`, `Capability[]`) + `string runId` |
| **Output** | `SemanticRunResult` (Satisfied \| StateEvidenceRequired \| BindingUnresolved \| SemanticContradiction \| BudgetExhausted \| ExecutionFailed) |
| **Depends on** | Container, Traversal, Recovery, Startup, ElementAnalysis, Model types |
| **Dependency direction** | Agent → Container → Traversal → Environment (I-1 ✓) |

### 1.2 Capability Selection + Action Authorization

| | |
|---|---|
| **Current owner** | `Agent` (inline in `RunSemanticGoalAsync` + static `AuthorizeAction`) |
| **Responsibility** | Given `SemanticGoalInput` + object, select the Capability whose `ApplicableToCategory` matches and `StateDimension` matches. Validate object declares the dimension, capability applies to category. |
| **Input** | `(SemanticGoalInput, SemanticObject, Capability[])` |
| **Output** | `Capability \| null` (selection); `SemanticActionResult?` (authorization — null = authorized) |
| **Depends on** | `SemanticObject`, `Capability`, `SemanticAction` — Model types only |

### 1.3 Semantic Action Lowering

| | |
|---|---|
| **Current owner** | `Traversal` (static `LowerAction`) |
| **Responsibility** | Ground authorized `SemanticAction` using `ObjectBinding` + `Observation` → `ExecutionAction` or safe no-dispatch. Safety rules: unknown state, already satisfied, ambiguous surface. |
| **Input** | `(SemanticAction, ObjectBinding, Observation)` |
| **Output** | `SemanticActionResult` (Dispatched \| NoOp \| StateUnknown \| Unresolved \| Invalid) |
| **Depends on** | `DeviceAction`, `ObservedElement`, Model types only. No Environment coupling. |

### 1.4 Page Identity Evidence

| | |
|---|---|
| **Current owner** | `PageAnalysis` static class (`World/PageAnalysis.cs`) |
| **Responsibility** | Stateless: `Observation` + `PageAnalysisCriteria` → `SemanticEvidence[]`. Sources: FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, SWITCH_DISTRIBUTION. |
| **Input** | `(Observation, PageAnalysisCriteria)` |
| **Output** | `ImmutableArray<SemanticEvidence>` |
| **Depends on** | Model types only. No state. No environment. |

### 1.5 Object Binding Evidence

| | |
|---|---|
| **Current owner** | `ElementAnalysis` static class (`World/ElementAnalysis.cs`) |
| **Responsibility** | Stateless: `Observation` + `ElementBindingCriteria` → `SemanticEvidence[]` about element→object bindings. `ReconcileBindings`: evidence + objects → `ObjectBinding[]`. `SameRow`: spatial predicate. |
| **Input** | `(Observation, ElementBindingCriteria)` |
| **Output** | `ImmutableArray<SemanticEvidence>` + `ImmutableArray<ObjectBinding>` |
| **Depends on** | Model types only. No state. No environment. |

### 1.6 Evidence Fusion

| | |
|---|---|
| **Current owner** | `SemanticReconciliation` static class (`Model/SemanticReconciliation.cs`) |
| **Responsibility** | Stateless pure fusion: `SemanticEvidence[]` → `SemanticBeliefState`. ≥1 Supports + 0 Contradicts → Supported; ≥1 each → Contradicted; else Unresolved. |
| **Input** | `params SemanticEvidence[]` |
| **Output** | `SemanticBeliefState` |
| **Depends on** | Model types only. |

### 1.7 Page-Local Belief + Object State

| | |
|---|---|
| **Current owner** | `Container` (`src/UniClaw.Runtime/Container/Container.cs`) |
| **Responsibility** | Mutable owner (I-2) of: page belief (`_localPageBeliefState`), object bindings (`_objectBindings`), object state beliefs (`_objectStateBeliefs`), observation, executed steps, viewport observations. |
| **Input** | `EvaluatePageBelief(obs, evidence[])`; `UpdateBindings(bindings)`; `RefreshObjectStateBeliefs(obs?)` |
| **Output** | `SemanticBeliefState?`, `ImmutableArray<ObjectBinding>`, `ImmutableDictionary<string, bool?>` (all immutable snapshots) |
| **Depends on** | Model + BCL only (I-1: no Agent/Environment/Traversal reference) |

### 1.8 Plan Execution (Legacy)

| | |
|---|---|
| **Current owner** | `Agent.RunAsync` + `Agent.RunOpenWorldAsync` |
| **Responsibility** | Closed-world: execute pre-compiled `PlanStep[]`. Open-world: type-directed branch dispatch. |
| **Input** | `(Goal, Plan/context)` |
| **Output** | `RunState` |
| **Depends on** | Container, Traversal, Recovery, Startup |

### 1.9 Legacy World Belief

| | |
|---|---|
| **Current owner** | `Reconcile` static class (`World/Reconcile.cs`) |
| **Responsibility** | Stateless: `Observation` + caller-injected resolver → `WorldBelief` with binary confidence. Legacy compatibility. |
| **Input** | `(Observation, Func<Observation, string?>)` |
| **Output** | `WorldBelief` |
| **Depends on** | Model types only. |

---

## 2. Component Extraction Proposal

### 2.1 SemanticGoalRunner

```
Name:        SemanticGoalRunner
Purpose:     Closed-loop semantic execution — READ→DECIDE→ACT→OBSERVE→UPDATE→RE-EVALUATE
Owns:        RunState lifecycle during semantic execution, TraceEvent recording, loop termination decisions
Does not own: Container state, Traversal lowering logic, Capability definitions, Object state
Input:       SemanticGoalInput + SemanticObject[] + Capability[] + runId
Output:      SemanticRunResult
Dependency:  Agent → Container → Traversal → Environment (I-1)
Decision:    KEEP_INLINE
Reason:      The closed loop is Agent's core responsibility — it manages RunState, TraceEvent,
             lifecycle transitions. Extracting it would create a new mutable state owner
             (violating I-2) or an awkward split of Agent's lifecycle authority.
             Name "SemanticGoalRunner" is for documentation — the code stays in Agent.
```

### 2.2 CapabilitySelector

```
Name:        CapabilitySelector
Purpose:     Given a SemanticGoalInput + object + capability catalog, select the applicable Capability
Owns:        Selection logic (filter by category + state dimension, exactly 1 required)
Does not own: Capability definitions (declarative, immutable), Agent decision authority
Input:       (SemanticGoalInput, SemanticObject, Capability[])
Output:      Capability | null
Decision:    KEEP_INLINE
Reason:      Currently 3 lines of filtering logic. Extraction would create an indirection
             for no cohesion benefit. Named for documentation only.
             If capability selection grows (priority, preconditions, multi-dimension),
             revisit as EXTRACT_COMPONENT.
```

### 2.3 ActionAuthorizer

```
Name:        ActionAuthorizer
Purpose:     Validate that a SemanticAction respects domain contracts:
             capability→category match, object→dimension declaration, action well-formedness
Owns:        Authorization validation rules
Does not own: Agent decision authority (Agent decides WHETHER to authorize), Capability definitions
Input:       (SemanticAction, SemanticObject, Capability)
Output:      SemanticActionResult? (null = authorized, Invalid = rejected)
Decision:    KEEP_INLINE
Reason:      Static pure function already. No state. No coupling beyond Model types.
             Well-placed as Agent.AuthorizeAction — it's the Agent's validation gate.
```

### 2.4 ActionLowering

```
Name:        ActionLowering
Purpose:     Lower an authorized SemanticAction to ExecutionAction using Container binding + Observation
Owns:        Lowering rules: toggle selection via PerceptionType, state safety checks,
             already-satisfied detection, ambiguous-surface rejection
Does not own: Business capability selection (Agent), Semantic target selection (Agent via binding),
             Physical dispatch (Environment)
Input:       (SemanticAction, ObjectBinding, Observation)
Output:      SemanticActionResult (Dispatched | NoOp | StateUnknown | Unresolved | Invalid)
Decision:    KEEP_INLINE
Reason:      Static pure function. No Environment coupling. No mutable state.
             Well-placed as Traversal.LowerAction — lowering IS Traversal's responsibility.
             The name "ActionLowering" distinguishes it from Traversal's existing
             "ExecuteStep" (execution kernel for legacy plan steps).
```

### 2.5 PageEvidenceProducer

```
Name:        PageEvidenceProducer
Purpose:     Produce multi-source SemanticEvidence about page identity from an Observation
Owns:        Evidence production rules: FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, SWITCH_DISTRIBUTION
Does not own: Page identity verdict (Container belief owns that), Page recognition criteria (caller provides)
Input:       (Observation, PageAnalysisCriteria)
Output:      ImmutableArray<SemanticEvidence>
Decision:    KEEP_INLINE
Reason:      Stateless pure function. Already well-named as PageAnalysis.
             Follows the Reconcile pattern. Scales to new evidence sources by adding methods.
```

### 2.6 ObjectBindingProducer

```
Name:        ObjectBindingProducer
Purpose:     Produce SemanticEvidence about which ObservedElements bind to which SemanticObjects,
             and reconcile evidence into ObjectBinding proposals
Owns:        Binding evidence rules: TEXT_IDENTITY, SPATIAL_RELATION, PERCEPTION_TYPE.
             Binding reconciliation: evidence → ObjectBinding[].
             Spatial predicates: SameRow.
Does not own: Object binding state (Container owns that), SemanticObject definitions (declarative)
Input:       (Observation, ElementBindingCriteria) → evidence; (evidence, objects) → bindings
Output:      ImmutableArray<SemanticEvidence> + ImmutableArray<ObjectBinding>
Decision:    KEEP_INLINE
Reason:      Stateless pure function. Already well-named as ElementAnalysis.
```

### 2.7 EvidenceFusion

```
Name:        EvidenceFusion
Purpose:     Fuse multiple SemanticEvidence stances into a single SemanticBeliefState
Owns:        Fusion rules: Supports+Contradicts→Contradicted, ≥1 Supports→Supported, else Unresolved
Does not own: Evidence production (PageAnalysis, ElementAnalysis), Belief state (Container)
Input:       params SemanticEvidence[]
Output:      SemanticBeliefState
Decision:    KEEP_INLINE
Reason:      Stateless pure function. Already well-named as SemanticReconciliation.FuseBelief.
             Claim-aware fusion is a separate future purchase.
```

### 2.8 WorldBeliefProducer

```
Name:        WorldBeliefProducer
Purpose:     Produce WorldBelief from Observation using caller-injected page resolver.
             Legacy compatibility bridge.
Owns:        Binary confidence assignment (resolver returns non-null → 1.0, null → 0.0)
Does not own: Page identity semantics (caller-injected resolver owns that)
Input:       (Observation, Func<Observation, string?>)
Output:      WorldBelief
Decision:    KEEP_INLINE
Reason:      Legacy compatibility. Will be deprecated when PageAnalysis + Container belief
             fully replace the resolver path.
```

### 2.9 IntentCompiler

```
Name:        IntentCompiler
Purpose:     Compile natural-language business intent ("开启 Wi‑Fi") into structured
             SemanticGoalInput(WifiConnectivity, Enabled, true)
Owns:        Intent → SemanticGoalInput mapping
Does not own: Agent decision authority, Capability selection, Action execution
Input:       Natural language intent (string or structured intent representation)
Output:      SemanticGoalInput
Decision:    FUTURE_CAPABILITY
Reason:      Phase 6. Does not yet exist. Attachment point: SemanticGoalInput type exists.
```

### 2.10 StateClassifier

```
Name:        StateClassifier
Purpose:     Classify the ON/OFF state of a switch/toggle region from a screenshot
Owns:        Visual state classification logic
Does not own: Runtime decision authority, Container state, Object binding
Input:       (screenshot, ElementBounds) → bool?
Output:      true (ON), false (OFF), null (cannot determine)
Decision:    FUTURE_CAPABILITY
Reason:      Perception-side. Identified by Real-World State Evidence Bridge Challenge.
             Attachment point: ObservedElement.SwitchState field exists.
```

---

## 3. Naming Review

### 3.1 Existing Names — Adequate

| Current Name | File | Verdict |
|---|---|---|
| `PageAnalysis` | `World/PageAnalysis.cs` | **OK** — "Page" is domain-correct. "Analysis" follows Reconcile pattern. |
| `ElementAnalysis` | `World/ElementAnalysis.cs` | **OK** — "Element" is perception-domain. "Analysis" follows pattern. |
| `SemanticReconciliation` | `Model/SemanticReconciliation.cs` | **OK** — "Reconciliation" accurately describes evidence fusion. |
| `Reconcile` | `World/Reconcile.cs` | **OK** — legacy. Will be deprecated. |
| `SemanticObject` | `Model/SemanticObject.cs` | **OK** — domain concept. |
| `Capability` | `Model/Capability.cs` | **OK** — domain contract. |
| `SemanticAction` | `Model/SemanticAction.cs` | **OK** — domain effect. |
| `SemanticGoalInput` | `Model/SemanticGoalInput.cs` | **OK** — structured desired outcome. |

### 3.2 Documentation-Only Names (No Extraction)

| Documentation Name | Refers To | Why Documented |
|---|---|---|
| **SemanticGoalRunner** | `Agent.RunSemanticGoalAsync` | Closed-loop semantic execution |
| **CapabilitySelector** | Inline capability filtering in Agent | Named for architecture discussions |
| **ActionAuthorizer** | `Agent.AuthorizeAction` | Static validation gate |
| **ActionLowering** | `Traversal.LowerAction` | Semantic→Execution lowering |
| **PageEvidenceProducer** | `PageAnalysis.Analyze` | Page identity evidence production |
| **ObjectBindingProducer** | `ElementAnalysis.Analyze` + `ReconcileBindings` | Object binding evidence |
| **EvidenceFusion** | `SemanticReconciliation.FuseBelief` | Multi-source evidence fusion |
| **WorldBeliefProducer** | `Reconcile.FromObservation` | Legacy compatibility |

### 3.3 Names from feature/refactor — Historical Reference Only

| feature/refactor Name | Current Equivalent | Notes |
|---|---|---|
| `PageAnalyzer` (AI-vision) | `PageAnalysis` (stateless evidence) | Different architecture. Old: AI oracle. New: evidence producer. |
| `PageAnalysis` (data record) | `Observation` + `SemanticEvidence[]` | Old: single verdict record. New: observation + multi-source evidence. |
| `PageFingerprint` | `Container.LocalPageBeliefState` | Old: hash identity. New: evidence-backed belief. |
| `PopupDetector` | `Container.IsLocalObstructionHypothesis` | Old: regex classifier. New: evidence-based hypothesis. |

### 3.4 Rejected Names

| Name | Reason Rejected |
|---|---|
| `SemanticEngine` | "Engine" implies mutable state + orchestration. Stateless producers don't need it. |
| `ObjectStateManager` | "Manager" is generic. `Container.RefreshObjectStateBeliefs` is specific and accurate. |
| `BeliefProcessor` | "Processor" is generic. `SemanticReconciliation.FuseBelief` is specific. |
| `CapabilityRegistry` | Implies a mutable registry service. `Capability` is an immutable record; the "catalog" is just `ImmutableArray<Capability>`. |
| `WorldModel` | Ambiguous — conflates Container state, Agent belief, and domain definitions. |
| `IntentParser` | "Parser" implies NLP. `IntentCompiler` is more accurate for structured mapping. |

---

## 4. Extraction Decision Summary

### 4.1 KEEP_INLINE (8)

| Component | Current Location | Reason to Keep Inline |
|---|---|---|
| **SemanticGoalRunner** | `Agent.RunSemanticGoalAsync` | Core Agent responsibility. Manages RunState + TraceEvent + lifecycle. Extraction would violate I-2. |
| **CapabilitySelector** | `Agent` (inline) | 3 lines of filtering. No independent change reason. |
| **ActionAuthorizer** | `Agent.AuthorizeAction` | Static pure function. Agent's validation gate. |
| **ActionLowering** | `Traversal.LowerAction` | Static pure function. Traversal's lowering responsibility. |
| **PageEvidenceProducer** | `PageAnalysis.Analyze` | Stateless pure function. Already correctly placed. |
| **ObjectBindingProducer** | `ElementAnalysis.Analyze` + `ReconcileBindings` | Stateless pure function. Already correctly placed. |
| **EvidenceFusion** | `SemanticReconciliation.FuseBelief` | Stateless pure function. Already correctly placed. |
| **WorldBeliefProducer** | `Reconcile.FromObservation` | Legacy compatibility. Will be deprecated. |

### 4.2 EXTRACT_COMPONENT (0)

**No component requires extraction.** All responsibilities are correctly placed. Cohesion is high within each owner. Coupling follows I-1 direction.

### 4.3 FUTURE_CAPABILITY (2)

| Component | Phase | Attachment Point |
|---|---|---|
| **IntentCompiler** | Phase 6 | `SemanticGoalInput` type exists |
| **StateClassifier** | Perception Bridge | `ObservedElement.SwitchState` field exists |

---

## 5. Dependency Graph

```
                        ┌─────────────────┐
                        │  IntentCompiler  │  (FUTURE — Phase 6)
                        │  NL → SemanticGoalInput
                        └────────┬────────┘
                                 │
                        ┌────────▼────────┐
                        │      Agent       │
                        │  SemanticGoalRunner
                        │  CapabilitySelector
                        │  ActionAuthorizer
                        └────────┬────────┘
                                 │ reads
                        ┌────────▼────────┐
                        │    Container     │
                        │  PageBelief     │
                        │  ObjectBindings │
                        │  ObjectStateBeliefs
                        └────────┬────────┘
                                 │ binding
                        ┌────────▼────────┐
                        │    Traversal     │
                        │  ActionLowering │
                        │  StepExecutor   │
                        └────────┬────────┘
                                 │
                        ┌────────▼────────┐
                        │   Environment    │
                        │  Physical dispatch
                        └─────────────────┘

       STATELESS PRODUCERS (no ownership, no state):
       PageAnalysis ──→ SemanticEvidence[] ──→ Container.EvaluatePageBelief
       ElementAnalysis ──→ SemanticEvidence[] ──→ Container.UpdateBindings
       SemanticReconciliation.FuseBelief ──→ SemanticBeliefState

       FUTURE:
       StateClassifier ──→ bool? ──→ ObservedElement.SwitchState
```

---

## 6. Conclusion

```
SEMANTIC_COMPONENT_EXTRACTION_READY

Extraction decision: KEEP_INLINE — 8 components
                     EXTRACT_COMPONENT — 0 (none needed)
                     FUTURE_CAPABILITY — 2 (IntentCompiler, StateClassifier)

All existing semantic responsibilities are correctly placed.
No component needs extraction.
Cohesion is high within each owner.
Coupling follows I-1 direction.
Documentation names provided for architecture discussions.
No code changes required.

Next:
  Phase 6 — INTENT_COMPILATION (IntentCompiler)
  OR
  Perception Bridge — StateClassifier (real-device SwitchState detection)

STOP.
```
