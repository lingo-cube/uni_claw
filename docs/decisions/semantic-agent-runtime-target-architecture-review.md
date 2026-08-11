# Semantic Agent Runtime — Target Architecture Review

> Generated: 2026-08-10
> Role: Runtime Architecture Analyst
> Baseline: `docs/decisions/semantic-agent-runtime-current-state-review.md` (Current State, 2026-08-10)
> Purpose: 定义目标语义架构——Runtime 应该演进成什么，不定义如何实现。
> Scope: Architecture only. No code changes. No new classes. No implementation details.
> Constraint: 14 Architecture Invariants (I-1..I-14) remain the unviolable boundary.

---

## 1. The Missing Layer: Semantic Compilation

### 1.1 Current Collapse

```
Business Intent ("开启 Wi‑Fi")
        ↓ (caller-side compilation, OUTSIDE Runtime)
Plan = [Tap("Network&internet"), Tap("Internet"), Tap("Wi‑Fi"), SetSwitch("Wi‑Fi", true)]
        ↓
Agent.RunAsync(goal, plan)  ← Runtime receives PRE-COMPILED steps
```

The Runtime never sees the intent. It receives a pre-digested meal of action tokens. All semantic compilation — "what does '开启 Wi‑Fi' mean? what pages do I need? what elements do I interact with?" — happens outside the Runtime boundary.

### 1.2 Target Layer

```
Business Intent ("开启 Wi‑Fi")
        ↓
SEMANTIC COMPILATION (new Runtime-internal layer)
        ↓
Capability ("ToggleWiFi")
        ↓
SemanticObject ("Wi‑Fi Switch on WifiPage")
        ↓
SemanticAction (SetDesiredState, target=Wi‑Fi Switch, desired=ON)
        ↓
ExecutionAction (DeviceAction.SetSwitch(index, true, bounds))
        ↓
Environment (physical tap)
```

**The missing layer is Semantic Compilation** — the Runtime-internal translation from business intent to executable actions. This is NOT a new component or class. It is a new semantic capability within the existing Agent→Container→Traversal spine. Agent already owns global reasoning (I-3); semantic compilation is the natural evolution of that authority.

### 1.3 Why This Is a Layer, Not a Component

Semantic Compilation is a **capability**, not a **component**. It does not introduce a new mutable state owner (I-2). It does not create a new decision authority (I-3). It distributes across existing owners:

- **Intent → Capability**: Agent (global reasoning)
- **Capability → SemanticObject**: Agent + Container (world model + page-local registry)
- **SemanticObject → SemanticAction**: Container + Traversal (object grounding + action selection)
- **SemanticAction → ExecutionAction**: Traversal (protocol token → DeviceAction)

---

## 2. Semantic Concept Definitions

### 2.1 The Concept Hierarchy

```
Level 0: RAW WORLD EVIDENCE (exists)
  Observation, ObservedElement, ElementBounds, PerceptionType

Level 1: SEMANTIC EVIDENCE (exists)
  SemanticEvidence { Source, Claim, Stance, Reason? }

Level 2: SEMANTIC BELIEF (exists)
  SemanticBeliefState (Supported / Unresolved / Contradicted)

Level 3: SEMANTIC OBJECT (TARGET — does not exist)
  SemanticObject, SemanticElement, SemanticPage

Level 4: SEMANTIC ACTION (TARGET — does not exist)
  BusinessIntent → Capability → SemanticAction → ExecutionAction
```

### 2.2 Definitions

| Concept | Definition | Example | Owner | Exists? |
|---|---|---|---|---|
| **BusinessIntent** | The user's goal expressed in business/domain terms. A declarative statement of desired world state, not a procedural plan. | "确保 WiFi 已开启" | **Caller** (human, LLM, script). Runtime receives as input, does not generate. | PARTIAL — `IntentSemanticEnvelope.Intent` is a string carrier; Runtime never interprets it |
| **Capability** | What the Runtime can DO, expressed as capability semantics independent of specific UI implementation. A Capability is a named, evidence-verifiable operation on the world. | `ToggleWiFi`, `NavigateToWifiPage`, `VerifyWifiState` | **Agent** — selects which capabilities to invoke to satisfy intent. **Runtime** — defines the capability catalog. | MISSING — only exists implicitly in caller-compiled Plans |
| **SemanticObject** | A named entity in the UI world model. Has identity (what it is), category (what kind of thing), capability (what you can do with it), and observable state (what state it's in). Exists independently of any single Observation — it's part of the World Model. | "Wi‑Fi Switch" (StateChangingControl, supports SetDesiredState, current state=OFF) | **Container** — page-local registry. **Agent** — cross-page world model. | MISSING — closest analog is `TypeLevelElementCategory` (NavigableContainer/StateChangingControl) but without identity or state |
| **SemanticElement** | The Observation-level instantiation of a SemanticObject. "This specific ObservedElement at Index 6 with Bounds (0.08,0.40,0.92,0.44) and PerceptionType='toggle' IS the Wi‑Fi Switch on THIS observation." SemanticElement = SemanticObject + observation grounding evidence. | ObservedElement(Index=6, Text="Wi‑Fi", Bounds=..., PerceptionType="toggle") → IS_A → SemanticObject("Wi‑Fi Switch") | **Container** — page-local element registry. **ElementAnalysis** (future) — produces the association. | MISSING — ElementAnalysis not implemented |
| **SemanticAction** | An action with semantic meaning: target object + desired effect. Different from ExecutionAction which is the physical primitive. A SemanticAction compiles to one or more ExecutionActions. | `SetDesiredState(target=Wi‑Fi Switch, desired=ON)` → compiles to `DeviceAction.SetSwitch(index=6, state=true, bounds=...)` | **Traversal** — action compilation. **Agent** — action selection (which action for which capability). | MISSING — closest analog is `PlanStep("Wi‑Fi", "SetSwitch true")` which is pre-compiled by caller |
| **SemanticEvidence** | One source's qualitative stance on a semantic claim. Evidence ≠ Claim ≠ Belief ≠ Truth. | `SemanticEvidence { Source="TEXT_ANCHOR", Claim="page is InternetPage", Stance=Supports }` | **Stateless** — immutable value. Produced by PageAnalysis, ElementAnalysis, Perception. Consumed by Belief Fusion. | EXISTS ✓ |
| **SemanticBelief** | Fused conclusion from multiple evidence sources about a claim. | `SemanticBeliefState.Supported` — ≥1 source Supports, 0 Contradicts | **Container** — local belief state owner (I-2). **Agent** — adjudication when Contradicted. | EXISTS ✓ (but write-only in production) |
| **ExecutionAction** | Physical device primitive. The terminal action dispatched to Environment. Carries spatial evidence (Bounds) for coordinate mapping. | `DeviceAction.Tap(TargetElementIndex=6, TargetBounds=(0.08,0.40,0.92,0.44))` | **Traversal** — produces. **Environment** — executes. | EXISTS ✓ |

### 2.3 The Evidence Chain (Target)

```
Perception (external)
        ↓ Observation + Elements + Bounds + PerceptionType
ElementAnalysis (TARGET — stateless, like PageAnalysis)
        ↓ SemanticEvidence about element-object binding claims
        ↓ "ObservedElement[index=6] IS_PROBABLY SemanticObject('Wi‑Fi Switch')"
Container Object Binding
        ↓ local evidence: which objects are currently visible, with what state
        ↓ Container._objectBindings (mutable, Container-owned I-2)
Agent World Knowledge
        ↓ cross-page SemanticObjects + Capabilities (domain concepts)
        ↓ immutable declarative knowledge, not mutable state
Agent Semantic Decision (closed loop)
        ↓ reads Container belief → selects Capability → authorizes SemanticAction
Traversal Grounding + Lowering
        ↓ authorized SemanticAction → grounded ExecutionAction
Environment
        ↓ physical dispatch → fresh Observation → cycle repeats
```

---

## 3. Ownership Model

### 3.1 Core Distinction: Domain Concept vs Mutable State

**SemanticObject and Capability are domain concepts — immutable declarative knowledge — NOT mutable runtime state.**

They describe WHAT exists in the UI world and WHAT the Runtime can do. They do not change at runtime (a "Wi‑Fi Switch" is always a StateChangingControl; "ToggleWiFi" always means setting Wi‑Fi ON/OFF). What CHANGES is:

- **Binding**: which ObservedElement currently instantiates which SemanticObject (Container-owned, per-observation)
- **State belief**: what state a SemanticObject is currently in — ON or OFF (Container-owned, evidence-backed)
- **Page belief**: which page the Container currently believes it's on (Container-owned, evidence-backed)

This distinction prevents the error of duplicating domain knowledge per Container and eliminates the need for a "World Model" as a separate mutable owner.

### 3.2 The Ownership Table

| Owner | Owns (Current) | Owns (Target Additions) | Does NOT Own |
|---|---|---|---|
| **Agent** | RunState, WorldBelief instance, Trace, BranchProgress, Recovery decisions, Container switching, Completion authority (I-10) | **Capability selection** (which capability to invoke for current intent), **SemanticAction authorization** (decides desired semantic effect), **Cross-container adjudication** (Contradicted belief resolution), **Closed-loop decision** (reads belief → selects action → observes result) | Container local state, element-level grounding, physical action execution, raw perception, Capability DEFINITIONS (declarative, not owned), SemanticObject DEFINITIONS (domain concepts, not owned) |
| **Container** | _observation, _executedSteps, _viewportExplorationObservations, _isLocalComplete, _localPageBeliefState | **Object binding state** (which ObservedElement→SemanticObject bindings are currently evidenced), **Object state belief** (current state of each bound object, evidence-backed), **Page-local evidence aggregation** (SemanticEvidence about objects on this page) | Domain concept definitions, Capability definitions, Cross-page reasoning, Intent interpretation, Action authorization |
| **ElementAnalysis** (TARGET — stateless capability, like PageAnalysis) | — | **Perception→Object binding evidence**: produces SemanticEvidence about which ObservedElement corresponds to which SemanticObject. Stateless pure function. Input: Observation + SemanticObject definitions. Output: SemanticEvidence[]. | Mutable state, Object identity authority (produces evidence, not verdict), Action decisions |
| **Traversal** | Select (Text+SwitchState), Check (retry), Execute (protocol token→DeviceAction), Verify (sequence advance + post-action evaluation) | **SemanticAction → ExecutionAction lowering** (grounds authorized SemanticAction to specific ExecutionAction), **Multi-signal grounding** (Text + Bounds + PerceptionType + spatial relation) | Semantic target selection (Agent authorizes WHICH object), Capability selection (Agent selects WHICH capability), Business decision authority |
| **Perception** (external, IEnvironment port) | ObserveAsync → Observation, ExecuteAsync → ActionResult | — | Semantic interpretation, Object identity, Action decisions, Page classification |
| **Environment** (adapter) | Physical action execution, Bounds→coordinate mapping | — | Semantic target selection, Action authorization, World state interpretation |

### 3.3 Domain Concepts (Immutable Declarative Knowledge — No Mutable Owner)

| Concept | Definition | Mutable? | Owner |
|---|---|---|---|
| **SemanticObject** | A named entity in the UI domain. Has: identity (unique name), category (NavigableContainer / StateChangingControl / TextLabel / ...), declared capabilities (what you CAN do with it), and observable state dimensions (SwitchState, visibility, enabled). | **NO** — domain concept. Defined once, referenced everywhere. | **No mutable owner.** Declared as immutable data (like an enum member or record). Caller/configuration provides the catalog. |
| **Capability** | A declarative ability/contract. Has: name, target object category, desired effect, satisfaction evidence criteria. "ToggleWiFi(Wi‑Fi Switch, desired=ON)" is a Capability. | **NO** — declarative contract. | **No mutable owner.** Declared as immutable data. Agent SELECTS and APPLIES capabilities; Agent does not OWN the definitions. |

### 3.4 Authority Boundaries (Corrected)

```
                    DECISION AUTHORITY
                    ==================

BusinessIntent → Capability selection    AGENT (reads intent, selects capability)
Capability → SemanticAction              AGENT (authorizes desired semantic effect)
SemanticAction → ExecutionAction         TRAVERSAL (grounds + lowers; NO business authority)
ExecutionAction → Physical Dispatch      ENVIRONMENT (adapter)

                    STATE OWNERSHIP (mutable)
                    =========================

Run-level state                          AGENT (I-2)
Page-local belief + object bindings      CONTAINER (I-2)
Element-object binding evidence          STATELESS (ElementAnalysis produces immutable SemanticEvidence)
Page identity evidence                   STATELESS (PageAnalysis produces immutable SemanticEvidence)
World evidence (Observation)             STATELESS (immutable, produced by Perception)

                    DOMAIN KNOWLEDGE (immutable, no owner)
                    =====================================

SemanticObject definitions               DECLARATIVE (caller/configuration)
Capability definitions                   DECLARATIVE (caller/configuration)
```

### 3.5 The Semantic Compilation Closed Loop

```
                    ┌──────────────────────────────────────────┐
                    │         AGENT SEMANTIC DECISION           │
                    │                                          │
BusinessIntent ──→ Agent reads Container belief               │
Goal                │                                          │
                    ├── Belief SUPPORTED → select Capability   │
                    ├── Belief UNRESOLVED → request more evidence
                    ├── Belief CONTRADICTED → adjudicate       │
                    │                                          │
                    ↓ Capability selected                      │
                    ↓ SemanticAction authorized                │
                    └──────────────────┬───────────────────────┘
                                       ↓
                              TRAVERSAL GROUNDING
                    ┌──────────────────┬───────────────────────┐
                    │ SemanticAction → grounded ExecutionAction │
                    │ (object binding → Index + Bounds)         │
                    └──────────────────┬───────────────────────┘
                                       ↓
                              ENVIRONMENT DISPATCH
                    ┌──────────────────┬───────────────────────┐
                    │ ExecutionAction → physical dispatch       │
                    │ dispatch ≠ world effect                   │
                    └──────────────────┬───────────────────────┘
                                       ↓
                              FRESH OBSERVATION
                    ┌──────────────────┬───────────────────────┐
                    │ Perception → Observation                  │
                    │ ElementAnalysis → binding evidence        │
                    │ PageAnalysis → page identity evidence     │
                    └──────────────────┬───────────────────────┘
                                       ↓
                              CONTAINER BELIEF UPDATE
                    ┌──────────────────┬───────────────────────┐
                    │ Container._localPageBeliefState           │
                    │ Container._objectBindings                 │
                    │ (evidence-backed, revisable)              │
                    └──────────────────┬───────────────────────┘
                                       │
                    ┌──────────────────┴───────────────────────┐
                    │         AGENT READS BELIEF                │
                    │         (cycle repeats)                   │
                    └──────────────────────────────────────────┘
```

**This is NOT a one-way compiler.** It is a closed loop: Agent decides → Traversal grounds → Environment dispatches → Perception observes → Evidence updates belief → Agent reads belief → Agent decides again. The Runtime continuously reconciles its belief against fresh world evidence.

### 3.6 The Adjudication Path

```
Container._localPageBeliefState
        │
        │ if UNRESOLVED or CONTRADICTED:
        ↓
Container → Trap → Agent                    (EXISTING seam, I-8)
        ↓
Agent Semantic Decision:
  1. Read current evidence (PageAnalysis + ElementAnalysis output)
  2. Consider alternative Capabilities
  3. May invoke slow intelligence (VLM, future)
  4. May request additional observation (viewport exploration)
        ↓
Agent Adjudication:
  ADJUDICATE  — resolve ambiguity, select different Capability
  CORRECT     — revise Container belief (CreateContainer+Bind, EXISTING)
  REBIND      — reset to fresh observation
  INVALIDATE  — declare belief invalid → recovery
        ↓
Container ← revised belief / new binding
Container still owns local state (I-2)
```

---

## 4. Existing Capability Mapping

### 4.1 Where Each Purchased Capability Fits in the Target Architecture

| Purchased Capability | Current Role | Target Role | Migration |
|---|---|---|---|
| **PageAnalysis** (`World/PageAnalysis.cs`) | Observation → SemanticEvidence[] about page identity (TEXT_ATTRIBUTE level) | **KEEP** — adds STRUCTURAL_SCREEN evidence when spatial signals consumed. Becomes one of several evidence producers feeding Container belief. | ENHANCE — add spatial evidence sources (BOUNDS_DISTRIBUTION, TYPE_DISTRIBUTION) |
| **SemanticEvidence** (`Model/SemanticEvidence.cs`) | Immutable qualitative evidence contract | **KEEP** — unchanged contract. Becomes the universal evidence currency for PageAnalysis, ElementAnalysis, and future Capability verification. | KEEP AS-IS |
| **SemanticReconciliation** (`Model/SemanticReconciliation.cs`) | Pure fusion: Supports+Contradicts → Contradicted | **KEEP** — unchanged. May need claim-aware variant for multi-object fusion (future). | KEEP AS-IS (claim-aware fusion = separate future purchase) |
| **ContainerBelief** (`Container._localPageBeliefState`) | Write-only observability | **READ** by Agent for adjudication decisions. Becomes the primary signal for "does Container trust its own page identity?" | ENHANCE — Agent consumption path |
| **ElementBounds** (`Model/Observation/ElementBounds.cs`) | Normalized spatial evidence on ObservedElement | **KEEP** — becomes input to ElementAnalysis for: spatial grouping, interaction surface identification, duplicate disambiguation. | ENHANCE — consumed by ElementAnalysis |
| **PerceptionType** (`ObservedElement.PerceptionType`) | Zero production consumers | **CONSUMED** by ElementAnalysis for: object category resolution (switch/toggle ≠ menuItem/text), interaction capability inference, TypeLevelDispatch enhancement. | ACTIVATE — wire into Traversal.Select and ElementAnalysis |
| **TypeLevelDispatch** (`TypeLevelDispatchPolicy`) | Category→Handling mapping (2 categories, caller-injected) | **EVOLVE** into CapabilityDispatch: Capability→SemanticAction mapping. Object category becomes ONE signal for capability resolution, not the only signal. | REBUILD — from Category→Handling to Capability→Action |
| **Grounding** (`TargetGroundingCriterion`) | Text-only matching + post-action verification | **ENHANCE** to multi-signal: Text + Bounds + PerceptionType + spatial relation. Empty-text switch becomes groundable via PerceptionType='toggle' + same-row spatial relation to text anchor. | ENHANCE — multi-signal grounding |
| **Goal Evidence** (`Goal.EvidenceEvaluator`) | Caller-injected predicate | **EVOLVE** to Capability-specific evidence: each Capability defines its own satisfaction criteria. "ToggleWiFi" capability knows that satisfaction = SwitchState==true on the Wi‑Fi Switch SemanticObject. | REBUILD — from generic predicate to capability-aware evidence |
| **Trap** (`Model/Trap.cs`) | Container→Agent escalation | **KEEP** — unchanged seam. Enhanced to carry SemanticObject reference and evidence context for richer adjudication. | KEEP (minor enhancement) |
| **DeviceAction** (`Model/Actions/DeviceAction.cs`) | Tap(Index, Bounds?), SetSwitch(Index, State, Bounds?) | **KEEP** — unchanged primitives. SemanticAction compiles TO DeviceAction, not replaces it. | KEEP AS-IS |
| **IEnvironment** (`Environment/IEnvironment.cs`) | ObserveAsync + ExecuteAsync | **KEEP** — unchanged port. Environment remains the physical boundary. | KEEP AS-IS |

### 4.2 The Semantic Ladder

```
EXECUTION PRIMITIVES (unchanged):
  DeviceAction, IEnvironment, ActionResult
        ↑ lowered to by Traversal

SEMANTIC ACTION (TARGET):
  SemanticAction = Capability + SemanticObject + DesiredEffect
  Agent AUTHORIZES (selects capability + desired effect)
  Traversal LOWERS (grounds to ExecutionAction)
        ↑ authorized by Agent after reading belief

SEMANTIC OBJECT BINDING (TARGET):
  Container._objectBindings: which ObservedElement → which SemanticObject
  Evidence-backed, revisable per observation
  Container owns the binding state (I-2)
        ↑ evidence from

SEMANTIC OBJECT + CAPABILITY (TARGET — domain concepts):
  SemanticObject = Identity + Category + DeclaredCapabilities + ObservableStateDimensions
  Capability = Name + TargetCategory + DesiredEffect + SatisfactionCriteria
  Immutable declarative knowledge — NOT mutable state
  No mutable owner. Caller/configuration provides catalog.
        ↑ evidence from

SEMANTIC EVIDENCE (exists, enhanced):
  PageAnalysis → page identity evidence
  ElementAnalysis → object binding evidence (TARGET)
  PerceptionType + Bounds → spatial/type evidence
        ↑ evidence from

RAW EVIDENCE (exists):
  Observation, ObservedElement, ElementBounds, PerceptionType
```

---

## 5. Migration Boundaries

### 5.1 KEEP (Unchanged or Enhanced)

| Artifact | Disposition | Reason |
|---|---|---|
| **I-1..I-14** | **KEEP** — unviolated | All 14 invariants remain the architecture contract. No target capability requires weakening any invariant. |
| **IEnvironment** | **KEEP** — unchanged port | Physical boundary is correct. Environment must not gain semantic authority. |
| **DeviceAction** | **KEEP** — unchanged primitives | Tap, SetSwitch, ScrollForward, LaunchApp are the correct terminal primitives. SemanticAction compiles to them, doesn't replace them. |
| **SemanticEvidence** | **KEEP** — unchanged contract | `{Source, Claim, Stance, Reason?}` is the universal evidence currency. Scales to element evidence, capability evidence, transition evidence. |
| **SemanticReconciliation.FuseBelief** | **KEEP** — unchanged fusion | Pure function. Claim-aware fusion is a separate future purchase if multi-object fusion creates cross-contamination. |
| **Trap escalation** | **KEEP** — unchanged seam | Container→Agent escalation is correct. Enhancement: richer evidence context in Trap payload. |
| **Traversal Select→Execute→Verify** | **KEEP** — enhanced flow | The flow is correct. Grounding enhances from Text-only to multi-signal. |
| **PageAnalysis** | **KEEP** — enhanced | Adds spatial evidence sources. Remains stateless pure function. |
| **Container** | **KEEP** — enhanced | Gains SemanticObject registry. Remains sole owner of page-local state (I-2). |
| **Agent** | **KEEP** — enhanced | Gains semantic compilation capability. Remains sole adjudication authority (I-3). |

### 5.2 REBUILD (Replace semantic, not mechanical)

| Artifact | Current Form | Target Form | Reason |
|---|---|---|---|
| **resolveSemanticPage** | `Func<Observation, string?>` — caller-injected page verdict lambda | **SemanticObject registry** — Container holds known objects for current page; PageAnalysis + ElementAnalysis produce evidence; Container belief is evidence-backed | Caller lambda collapses Evidence=Claim=Belief=Truth. Target separates knowledge (registry) from evidence (analysis) from belief (fusion). |
| **identityRule** | `Func<Observation, bool>` — caller-injected still-mine verdict | **Evidence-backed page belief** — IsStillMine becomes: Container.LocalPageBeliefState == Supported AND continuity evidence from PageAnalysis | Same collapse. Still-mine should be evidence-backed, not a single oracle. |
| **CategoryClassifier** | `Func<ObservedElement, TypeLevelElementCategory?>` — caller-injected element category verdict | **ElementAnalysis** — Runtime-internal capability: PerceptionType + Bounds + Text + spatial relation → element identity/category/capability evidence | Caller lambda for element classification is the same collapse pattern. ElementAnalysis makes this a Runtime capability. |
| **Plan / PlanStep** | Caller-compiled sequence of `PlanStep("Wi‑Fi", "Tap")` — pre-digested action tokens | **Intent → Capability → SemanticAction** — Agent compiles intent to capabilities; capabilities resolve to semantic objects; objects compile to actions | Plan-as-hypothesis (I-5) is correct. But Plan-as-precompiled-steps denies Runtime any semantic understanding. Target: Plan = sequence of Capabilities, not pre-compiled action tokens. |
| **Goal.EvidenceEvaluator** | `Func<Observation, GoalEvidence>` — generic caller predicate | **Capability-specific evidence** — each Capability defines its own satisfaction criteria. "ToggleWiFi" → SwitchState==true on Wi‑Fi Switch. "NavigateToWifiPage" → PageBelief==Supported for WifiPage. | Generic predicate cannot express capability-specific semantics. Capability-aware evidence is more composable and verifiable. |
| **TypeLevelDispatchPolicy** | `Category→Handling` (2 categories, caller-injected) | **CapabilityDispatch** — Capability→SemanticAction mapping. Object category is ONE signal. Dispatch considers: object capability, current state, desired effect, safety constraints. | Category→Handling is too coarse. A StateChangingControl may need SetDesiredState (switch), Inspect (read-only state), or Forbidden (dangerous). |

### 5.3 DEPRECATE (Remove from decision path, keep for compatibility)

| Artifact | Disposition | Reason |
|---|---|---|
| **resolveSemanticPage as authoritative page verdict** | **DEPRECATE** — keep as compatibility string provider for CreateContainer naming | PageAnalysis + Container belief replace it as the semantic authority. String naming still needed for operational use (Container construction). |
| **identityRule as sole still-mine authority** | **DEPRECATE** — keep as one evidence source (LOCAL_IDENTITY) among many | Evidence-backed belief replaces single-oracle verdict. LOCAL_IDENTITY remains as one evidence channel. |
| **ClosedWorldConcrete Plan representation** | **DEPRECATE** — evolve to Capability sequence | Pre-compiled action tokens are the current mode but not the target. Keep for backward compatibility during transition. |
| **Binary WorldBelief.Confidence (0.0/1.0)** | **DEPRECATE** — SemanticBeliefState (Supported/Unresolved/Contradicted) replaces it | Binary confidence is the old model. Keep WorldBelief for backward compat but don't use Confidence for new semantic decisions. |

---

## 6. Evolution Dependency Graph

### 6.1 Phase Dependency Map

```
Phase 1: PERCEPTION EVIDENCE (DONE)
  ElementBounds ✓
  PerceptionType ✓
  PageAnalysis (TEXT_ATTRIBUTE) ✓
  SemanticEvidence contract ✓
  Belief Fusion ✓
  Container Local Belief ✓
  Spatial action mapping (Bounds → DeviceAction) ✓
        ↓ prerequisite for

Phase 2: SEMANTIC OBJECT + CAPABILITY MODEL (NEXT)
  SemanticObject = immutable domain concept (identity + category + capabilities + state dimensions)
  Capability = immutable declarative contract (name + target + effect + satisfaction criteria)
  Caller/configuration provides object + capability catalog
  NO new mutable state owner
  NO Runtime-internal object/capability inference yet
        ↓ prerequisite for

Phase 3: PERCEPTION-TO-SEMANTIC BINDING
  ElementAnalysis (stateless): Observation + Object catalog → SemanticEvidence about bindings
  Container._objectBindings: mutable evidence-backed binding state (Container-owned, I-2)
  Multi-signal grounding: Text + Bounds + PerceptionType → Object identity evidence
  PerceptionType consumed by Traversal.Select + ElementAnalysis
  Empty-text switch becomes groundable via PerceptionType + spatial relation
        ↓ prerequisite for

Phase 4: SEMANTIC ACTION
  SemanticAction = Capability + SemanticObject + DesiredEffect
  Agent authorizes SemanticAction (selects capability + desired effect)
  Traversal lowers SemanticAction → ExecutionAction (grounds object binding → Index + Bounds)
  Capability-specific evidence: each Capability defines its own satisfaction criteria
  Goal.EvidenceEvaluator → Capability.SatisfactionCriteria
        ↓ prerequisite for

Phase 5: AGENT SEMANTIC CLOSED LOOP
  Agent reads Container belief → selects Capability → authorizes SemanticAction
  Traversal grounds → Environment dispatches → Perception observes
  Evidence updates Container belief → Agent reads belief → cycle repeats
  Agent adjudication: UNRESOLVED/CONTRADICTED → alternative Capability or recovery
  NOT a one-way compiler — continuous evidence→belief→decision→action→observation loop
        ↓ prerequisite for

Phase 6: INTENT COMPILATION
  BusinessIntent → Capability selection (Agent)
  Runtime receives "开启 Wi‑Fi" and autonomously selects capabilities
  Plan = Capability sequence, not pre-compiled action tokens
  Runtime can adapt: if WifiPage is already visible, skip navigation capability
```

### 6.2 What Each Phase Unblocks

| Phase | Unblocks |
|---|---|
| **Phase 2** (Object + Capability Model) | Declarative domain knowledge enters Runtime. "Wi‑Fi Switch" and "ToggleWiFi" are named, typed concepts — not implicit in caller lambdas. No new mutable state. |
| **Phase 3** (Perception-to-Semantic Binding) | ElementAnalysis produces evidence about which ObservedElement IS which SemanticObject. Empty-text switch becomes groundable. Duplicate disambiguation via spatial+type evidence. Container owns binding state. |
| **Phase 4** (Semantic Action) | Agent authorizes "SetDesiredState(Wi‑Fi Switch, ON)" — a semantic action. Traversal lowers it to DeviceAction.SetSwitch. Capability-specific satisfaction criteria. |
| **Phase 5** (Agent Closed Loop) | Agent continuously reads belief, decides actions, observes results. Not a one-shot plan executor — a continuous evidence→decision→action loop. Adjudication when belief is contradicted. |
| **Phase 6** (Intent Compilation) | Runtime receives "开启 Wi‑Fi" and autonomously compiles to execution. No caller-side pre-compilation needed. Runtime adapts to current world state. |

### 6.3 Non-Blocking Parallel Work

These can proceed in parallel with any phase:

| Work | Why Non-Blocking |
|---|---|
| **Real device perception adapter** | Independent of semantic model. Fills Bounds + PerceptionType from real perception. |
| **Claim-aware evidence fusion** | Pure function enhancement. No ownership change. |
| **Spatial PageAnalysis sources** | PageAnalysis already supports new sources. Add BOUNDS_DISTRIBUTION, TYPE_DISTRIBUTION as new evidence channels. |
| **Container in-place identity revision** | Agent→Container revision without discard+rebuild. Preserves local progress. |
| **OpenWorld PageAnalysis integration** | Wire existing PageAnalysis into RunOpenWorldAsync path. |

---

## 7. Architecture Invariant Impact

### 7.1 All 14 Invariants Remain Unviolated

| Invariant | Target Impact |
|---|---|
| **I-1** Agent→Container→Traversal→Environment | **UNCHANGED** — semantic compilation distributes across existing spine, doesn't invert it |
| **I-2** One mutable state, one owner | **UNCHANGED** — SemanticObject registry is Container-owned; World Model is Agent-owned; no new mutable owners |
| **I-3** One decision, one authority | **UNCHANGED** — Agent: intent→capability. Container: object grounding. Traversal: action compilation. Environment: physical dispatch. |
| **I-4** Observation is evidence, not truth | **UNCHANGED** — SemanticElement is evidence-backed (Observation → ElementAnalysis → identity evidence), not observation-truth |
| **I-5** Plan is hypothesis, not reality | **ENHANCED** — Plan evolves from "pre-compiled action sequence" to "capability hypothesis." Still revisable when world changes. |
| **I-6** Fingerprint is evidence, not identity | **UNCHANGED** — SemanticObject identity is evidence-backed, not fingerprint-based |
| **I-7** FSM does protocol, not intelligence | **UNCHANGED** — no FSM introduction. Semantic compilation is Agent intelligence, not state-machine transitions |
| **I-8** Escalate, don't steal authority | **UNCHANGED** — Trap seam preserved. Enhanced with richer evidence context |
| **I-9** Recovery is act→observe→verify→reconcile | **UNCHANGED** — recovery mechanism unchanged. Semantic objects may enhance recovery verification |
| **I-10** Completion by Goal Evidence | **ENHANCED** — Goal Evidence becomes Capability-specific. Still the only completion trigger |
| **I-11** No legacy control structure | **UNCHANGED** — no legacy code introduced |
| **I-12** YAGNI | **RESPECTED** — Phase 2 first; Phase 5 last. Each phase purchased by executable falsifiers |
| **I-13** No God Context | **UNCHANGED** — SemanticObject registry is page-local (Container). World Model is cross-page (Agent). No single aggregate context |
| **I-14** AI is pluggable, not truth | **UNCHANGED** — VLM/LLM produces SemanticEvidence; never becomes authoritative world truth |

---

## 8. What Does NOT Change

To be explicit about architecture stability:

| Stays Exactly As-Is | Why |
|---|---|
| `IEnvironment` port | Physical boundary is correct |
| `DeviceAction` primitives | Tap, SetSwitch, ScrollForward, LaunchApp are complete |
| `SemanticEvidence` contract | `{Source, Claim, Stance, Reason?}` scales to all evidence types |
| `SemanticReconciliation.FuseBelief` | Pure fusion function unchanged |
| `Traversal.Select→Execute→Verify` flow | Correct flow; grounding signals enhanced, not replaced |
| `Trap` escalation seam | Container→Agent escalation is correct |
| `Container` as page-local state owner (I-2) | Ownership boundary is correct |
| `Agent` as run-level authority (I-2, I-3) | Authority boundary is correct |
| `ElementBounds` normalized [0,1]×[0,1] contract | Spatial evidence contract is correct |
| 14 Architecture Invariants | All unviolated |

| Evolves (semantic enhancement, not replacement) | How |
|---|---|
| `PageAnalysis` | Adds spatial evidence sources → STRUCTURAL_SCREEN level |
| `Container` | Gains SemanticObject registry + Agent reads LocalPageBeliefState |
| `Traversal` | Multi-signal grounding (Text + Bounds + PerceptionType + spatial relation) |
| `Goal` | Capability-specific evidence criteria |
| `Plan` | Capability sequence, not pre-compiled action tokens |
| `Agent` | Semantic compilation: Intent → Capability → Object → Action |

| Deprecated (kept for compatibility, removed from decision path) | Replacement |
|---|---|
| `resolveSemanticPage` as page verdict | PageAnalysis + Container belief |
| `identityRule` as sole still-mine authority | Evidence-backed page belief |
| `CategoryClassifier` caller lambda | ElementAnalysis (Runtime capability) |
| Binary `WorldBelief.Confidence` | `SemanticBeliefState` |
| `ClosedWorldConcrete` Plan representation | Capability sequence |

---

## Summary

**The target architecture evolves the Runtime from a one-way plan executor to a closed-loop semantic agent.**

### Key Corrections from Initial Review

| Correction | Detail |
|---|---|
| **SemanticObject + Capability are domain concepts** | Immutable declarative knowledge — NOT mutable state. No mutable owner. Caller/configuration provides the catalog. |
| **SemanticElement is NOT a persistent type** | ElementAnalysis is stateless (like PageAnalysis). It produces SemanticEvidence about object bindings. Container owns the mutable binding state. |
| **Semantic Compilation is a CLOSED LOOP** | NOT a one-way compiler. Agent reads belief → selects Capability → authorizes SemanticAction → Traversal lowers → Environment dispatches → Observation → Evidence → Belief update → Agent reads belief → cycle repeats. |
| **Traversal does NOT acquire business authority** | Traversal LOWERS authorized SemanticAction to ExecutionAction. Agent is the sole semantic decision authority. Traversal grounds, does not decide. |

### Architecture Diagram (Corrected)

```
CURRENT (one-way plan executor):         TARGET (closed-loop semantic agent):

Caller compiles intent                   ┌─ Agent reads Container belief ←─────────┐
        ↓                                │          ↓                             │
Plan (pre-compiled steps)                │   Belief SUPPORTED?                     │
        ↓                                │     YES → select Capability             │
Agent executes steps                     │     NO  → request evidence / adjudicate │
        ↓                                │          ↓                             │
Traversal grounds + dispatches           │   SemanticAction authorized             │
        ↓                                │          ↓                             │
Environment executes                     │   Traversal lowers → ExecutionAction    │
                                         │          ↓                             │
                                         │   Environment dispatches                │
                                         │          ↓                             │
                                         │   Fresh Observation                     │
                                         │          ↓                             │
                                         │   Evidence → Container belief update ───┘
                                         │
                                         │   (continuous closed loop)
```

### Ownership (Corrected)

| | Mutable State Owner | Domain Knowledge |
|---|---|---|
| **Agent** | RunState, Trace, BranchProgress | Capability SELECTION (not definition) |
| **Container** | Page belief, object bindings, observation | — |
| **Stateless** | — | SemanticObject definitions, Capability definitions, ElementAnalysis, PageAnalysis |
| **Perception** | — | Produces Observation (immutable) |
| **Environment** | — | Dispatches ExecutionAction |

### Evolution Phases (Corrected)

```
Phase 1: PERCEPTION EVIDENCE (DONE) ✓
Phase 2: SEMANTIC OBJECT + CAPABILITY MODEL (NEXT)
Phase 3: PERCEPTION-TO-SEMANTIC BINDING
Phase 4: SEMANTIC ACTION
Phase 5: AGENT SEMANTIC CLOSED LOOP
Phase 6: INTENT COMPILATION
```

**14/14 Architecture Invariants unviolated. No new mutable state owners. SemanticObject + Capability are declarative domain concepts — not duplicated per Container, not owned by Agent as mutable state.**

**Phase 2 (Semantic Object + Capability Model) is the next purchase.** It introduces the declarative domain vocabulary without adding any new mutable state owner. This is the minimum prerequisite for all subsequent phases.

---

*No production changes. No new classes. No implementation details. Architecture only.*

STOP.
