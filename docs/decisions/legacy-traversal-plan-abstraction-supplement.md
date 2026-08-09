# LEGACY_TRAVERSAL_PLAN_ABSTRACTION_RESULT

> Generated: 2026-08-09
> Primary inputs: Steps 1–6 + visual perception supplement
> Legacy truth source: `feature/refactor` (read-only Git objects)
> Role: Supplemental — does NOT modify existing SP/VSP portfolios

---

## Corpus Reviewed

| Source | Type | Relevance |
|---|---|---|
| `PlanCompiler.cs` | PRODUCTION | Deterministic IntentSlots → TraversalPlan with TemplateSets, DynamicMatch |
| `ScenarioPlanLoader.cs` | PRODUCTION | Hand-authored Static plan JSON → TraversalPlan |
| `TraversalPlan.cs` + `IntentSlots` | MODEL | Data structures for both plan modes |
| `IntentExtractor.cs` | PRODUCTION | AI-driven NL → ExtractedIntentSlots |
| `ITraversalAdvisor.cs` / `TraversalAdvisor.cs` | PRODUCTION | Goal-directed per-step action generation |
| `SimulationBaselineTests.cs` | TEST | 7-page Settings exhaust + target search with DynamicMatch root |
| `AIIntentSimulationTests.cs` | TEST | NL→IntentSlots→PlanCompiler→Engine end-to-end |
| `MultiBranchNavigationTests.cs` | TEST | Hub→listA/listB — type-level discovery, branch-loss bug |
| `SettingsEnumerateRegression.cs` | TEST | Depth constraint during DynamicMatch sub-frame generation |
| `FixVerificationTests.cs` L2/L3/L7 | TEST | Depth semantics during type-level traversal |
| `20260805T052309367Z_EnumerateFixtures.cs` | TEST | Real-run reconstructed type-level discovery |
| `scenarios/android-settings/*.v1.json` | FIXTURE | locate-one-item (Static plan) + enumerate-settings-safely (type-level spec) |
| `runner-through-engine-design.md` | DESIGN | "Plan mode ≠ Intent mode, But Both Use the FSM" |
| `plancompiler-default-alignment-design.md` | DESIGN | Dormant preventive fix for PlanCompiler alignment |

---

## Pass A — Plan Modes Observed

### Mode 1: Closed-World Concrete Plan

**Legacy name:** Plan mode (Static + StaticNodes), ScenarioPlanLoader

**Known Before Execution:**
- Every concrete navigation target (explicit coordinates)
- Every concrete action (Tap at (0.5, 0.7), Back, Wait)
- Expected page identity after each action
- Explicit success criteria (expectedPageIdentities)
- Explicit boundaries (maxSteps, maxScrolls, maxDuration)

**Unknown Before Execution:**
- Whether the actual screen matches the plan's coordinate assumptions
- Whether the actual app version / device / layout matches
- Whether elements have shifted position since plan authoring

**What World Assumptions Are Embedded:**
- Screen layout is stable and matches plan coordinates exactly
- Each planned tap will hit the intended element
- Each expected page identity will match the actual page after navigation
- The route is valid for the current device state

**What Happens If Reality Differs:**
- Tap at planned coordinate hits empty space or wrong element
- Expected page identity does not match observed page
- Locate fails — the plan cannot adapt because it prescribes actions, not goals
- Legacy locate scenario uses post-hoc TraceTool VerifyEngine because Host alone cannot confirm target page identity (status = "pending_verification")

**Executable Evidence:** `scenarios/android-settings/locate-one-item.v1.json` → `ScenarioPlanLoader.Load()` → `TraversalPlan` with Static + StaticNodes. Exercised by `EmulatorScenarioIntegrationTests.LocateOneItem_ThroughCoreEngine_Completes`.

**E-16 (ScenarioPlanLoader):** Materializes hand-authored JSON into executable `TraversalPlan`. Converts `JsonElement` coordinates to `Coordinate` for `OperationDispatcher`. Plan mode = "data, not code" — bypasses IntentExtractor and PlanCompiler entirely.

---

### Mode 2: Open-World Type-Level Traversal Specification

**Legacy name:** Intent mode (DynamicMatch), PlanCompiler

**Known Before Execution:**
- Task scope: full (exhaustive) or target_only (find-and-stop)
- Element handling template: full_interaction, menu_only, safe_mode, read_only
- Match conditions: which element types are navigable (menu_item → menu_container, switch → switch_leaf, button → leaf_action)
- Depth bound (optional): null = unconstrained
- Completion policy: Exhaustive (visit all reachable), TargetFound (stop at named target), Timeout, MaxSteps
- Safety constraints: which element types to interact with, which to skip
- Target name and aliases (for target_only scope)
- Entry strategy and app identity

**Unknown Before Execution:**
- Every concrete child element (only observable at runtime)
- Every concrete page (discovered through navigation)
- Every concrete element coordinate (only known from observation)
- The complete navigation route (depends on what is discovered)
- The final required-work inventory (depends on what is discoverable within constraints)

**What The Specification Constrains:**
- Which element TYPES to interact with (TemplateSets → MatchConditions)
- Which element TYPES to skip
- How deep to explore (Depth bound)
- When to stop (CompletionPolicy)
- How to enter (EntryPolicy)

**What Fresh Observation Must Provide:**
- Actual elements on each page (text, type, coordinates)
- Element types for matching against TemplateSets
- Page identity for navigation tracking and revisit detection

**When Concrete Work First Becomes Knowable:**
- When a fresh observation reveals an element matching the type-level specification
- Not before execution — the specification defines the CATEGORY of work, not the INSTANCES

**Executable Evidence:**
- E-03: 7-page Settings with DynamicMatch root — 2 DynamicRules (menu_rule for type "button" → menu_container, switch_rule for type "switch" → switch_leaf), not 18 concrete steps. Children are generated when observations match rules.
- E-05: NL intent → IntentExtractor → IntentSlots → PlanCompiler → TraversalPlan with DynamicMatch. The plan says "match menu_items" not "tap Wi‑Fi at (0.5, 0.31)."
- E-11: API-35 Settings with DynamicMatch. Depth=2 constrains discovery. Pre-fix: sub-frame generation ignored depth → entered depth=3.
- CAND-008 evidence (from Step 6): `BranchInventoryEvidence` — route discovered from fresh evidence, absent from initial Plan.

**E-14 (PlanCompiler):** 5-step deterministic compiler: ValidateSlots → BuildEntryPolicy → BuildRootNode (with DynamicMatch rules from TemplateSets) → BuildCompletionPolicy → Assemble. TemplateSets keyed by ElementHandling:
- `full_interaction`: menu_container, switch_leaf, slider_leaf, leaf_action
- `menu_only`: menu_container
- `safe_mode`: menu_container, switch_leaf, slider_leaf, leaf_action
- `read_only`: leaf_info (matches anything, produces non-interactive leaf)

**E-15 (IntentExtractor):** AI-inferred fields from NL: Scope, ElementHandling, Navigation, Restore, Completion. Caller-supplied factuals: TargetApp, Target, Depth, Entry.

---

## Pass B — Atomic Traversal / Plan Evidence

### TE-01 — DynamicMatch Generates Children From Type Rules, Not Pre-Enumerated Steps

- **Source:** `SimulationBaselineTests.cs` + `PlanCompiler.cs`
- **Task Intent:** Exhaustively traverse all reachable Settings pages
- **Pre-Execution Representation:** `TraversalPlan` with `ChildrenStrategyType.DynamicMatch`, 2 DynamicRules (menu_rule: type "button" → template "menu_container"; switch_rule: type "switch" → template "switch_leaf")
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** Element types to interact with (button, switch), template for each type, completion policy (Exhaustive for full traversal, TargetFound for target search)
- **Unknown Before Execution:** Concrete page names, concrete element names, element coordinates, total page count, complete route
- **Fresh Observation Available:** Elements with types (button/switch), names ("Wi‑Fi", "Bluetooth", "Dark mode"), coordinates
- **Concrete Candidate Discovered:** When observation returns an element matching a DynamicRule type condition → GenerateChild → navigable child node created
- **Was Concrete Work Present In Initial Plan:** NO — children are dynamically generated
- **Constraints:** Depth (implicit via fixture), safety (switch toggles tracked), completion (AllVisited or TargetFound)
- **Action Taken:** Engine traverses pages by matching observed elements against DynamicRules, generating children, navigating, verifying
- **Observed Outcome:** Full traversal: 19 pages, 24 actions, 99 steps. Target search: stops at Dark mode, 14 pages, 66 steps. Forbidden pages not visited.
- **Provenance:** DETERMINISTIC_SIMULATION

### TE-02 — Static Plan With Explicit Coordinates Bypasses Discovery

- **Source:** `ScenarioPlanLoader.cs` + `locate-one-item.v1.json`
- **Task Intent:** Locate "About phone" in Settings by tapping at planned coordinates
- **Pre-Execution Representation:** Hand-authored JSON with Static nodes, each carrying explicit coordinates, expected page identities, and action sequences
- **Representation Type:** STATIC_CONCRETE
- **Known Before Execution:** Exact tap coordinates, expected page identities after each action, exact action sequence
- **Unknown Before Execution:** Whether coordinates match the current screen layout
- **Fresh Observation Available:** Page identity after each action
- **Concrete Candidate Discovered:** None — plan prescribes everything. If a step fails, the plan cannot adapt.
- **Was Concrete Work Present In Initial Plan:** YES — all work is pre-enumerated
- **Constraints:** allowedPages, maxDepth, maxSteps, safetyPolicy
- **Action Taken:** Execute plan steps in sequence: observe, verify page identity, tap at planned coordinates, verify result
- **Observed Outcome:** If coordinates match screen: target found. If coordinates don't match: tap misses, locate fails. Post-hoc TraceTool VerifyEngine required because Host alone cannot confirm page identity (status = "pending_verification").
- **Provenance:** INTEGRATION (emulator + real ADB)

### TE-03 — NL Intent → AI Extraction → Type-Level Plan Construction

- **Source:** `AIIntentSimulationTests.cs` + `IntentExtractor.cs` + `PlanCompiler.cs`
- **Task Intent:** "Locate About phone from the Android Settings home list and verify the destination page"
- **Pre-Execution Representation:** NL description → AI IntentExtractor → `ExtractedIntentSlots` (scope: "target_only", element_handling: "menu_only", navigation: "bounded_settings", restore: true) → PlanCompiler → `TraversalPlan` with DynamicMatch
- **Representation Type:** TYPE_LEVEL (derived from NL via AI)
- **Known Before Execution:** Scope (target_only), element handling (menu_only — only menu_container rule), target name ("About phone" with aliases), depth (2), entry (Settings)
- **Unknown Before Execution:** Concrete elements, concrete pages, element coordinates, exact route to target
- **Fresh Observation Available:** Elements on each page with text, type, coordinates
- **Concrete Candidate Discovered:** Menu items matching the menu_container rule → generated as navigable children
- **Was Concrete Work Present In Initial Plan:** NO — DynamicMatch generates children from observation
- **Constraints:** Scope=target_only → CompletionPolicy.TargetFound(ActionOnFound=ExecuteThenStop), depth=2, menu_only template
- **Observed Outcome:** Target "About phone" found. Visited pages < 10 (doesn't explore unrelated pages). Battery page not visited.
- **Provenance:** DETERMINISTIC_SIMULATION (stub AI) + INTEGRATION (live Sensenova opt-in)

### TE-04 — Type-Level Specification Discovers Both Branches But Only Dispatches One

- **Source:** `MultiBranchNavigationTests.cs`
- **Task Intent:** Exhaustively enumerate all content from a hub page with two navigation branches
- **Pre-Execution Representation:** `TraversalPlan` with DynamicMatch root, 4 DynamicRules (button_rule, readonly_rule, switch_rule, back_button_rule)
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** Element types to interact with (button, readonly, switch, back_button), completion policy (Exhaustive)
- **Unknown Before Execution:** Hub has two buttons ("Go to List A", "Go to List B"), each leading to 16 scrollable items
- **Fresh Observation Available:** Both buttons observed on hub page with types and labels
- **Concrete Candidate Discovered:** Both buttons correctly matched by button_rule → both generate navigable children
- **Was Concrete Work Present In Initial Plan:** NO — both buttons were dynamically discovered
- **Constraints:** Exhaustive completion
- **Action Taken:** Engine taps "Go to List A", scrolls through all 16 items, returns to hub. Then STOPS — does not tap "Go to List B."
- **Observed Outcome:** BUG — listA 16/16, listB 0/16, CompletionReason=AllVisited. Type-level discovery correctly identified both branches as navigable, but the execution engine only dispatched one.
- **Provenance:** DETERMINISTIC_SIMULATION (unfixed failing test)

### TE-05 — Depth Constraint Declared at Plan Level, Violated During Type-Level Discovery

- **Source:** `SettingsEnumerateRegression.cs`
- **Task Intent:** Enumerate Settings entries with maxDepth=2
- **Pre-Execution Representation:** `IntentSlots("com.android.settings", "full", Depth: 2)` → PlanCompiler → DynamicMatch root with menu_container rule
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** Scope=full, depth=2, element handling (menu_container)
- **Unknown Before Execution:** API-35 Settings has 4 levels of nesting. Wi‑Fi is at depth=3.
- **Fresh Observation Available:** At depth=2 (Internet page), "Wi‑Fi" is observable as a menu_item
- **Concrete Candidate Discovered:** Wi‑Fi matches menu_container rule → pre-fix: generated as navigable child (depth=3); post-fix: blocked by depth constraint
- **Was Concrete Work Present In Initial Plan:** NO — Wi‑Fi was dynamically discovered at depth=2
- **Constraints:** Depth=2 declared in IntentSlots
- **Action Taken:** Pre-fix: engine navigated to Wi‑Fi (depth=3). Post-fix: engine stops at depth=2, never enters Wi‑Fi.
- **Observed Outcome:** Pre-fix: depth=3+ pages visited. Post-fix: depth=2 enforced. Visited pages include "network" and "internet" but not "wifi", "advanced", or "Wi-Fi".
- **Provenance:** DETERMINISTIC_SIMULATION (permanent regression)

### TE-06 — Depth Semantics: Container Degrades to Non-Interactive at Max Depth

- **Source:** `FixVerificationTests.cs` L7
- **Task Intent:** Determine element semantics at the depth boundary
- **Pre-Execution Representation:** DynamicMatch with menu_container rule, maxDepth=2
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** maxDepth=2, menu_container template
- **Unknown Before Execution:** What elements exist at each depth level
- **Fresh Observation Available:** Menu items at various depths
- **Concrete Candidate Discovered:** At stackDepth=2 (root depth 1 + 1 child): formula `Depth >= MaxDepth+1` = `2 >= 3` = false → template "menu_container" → Container type, Click operation. At stackDepth=3: `3 >= 3` = true → template degrades to "leaf_info" → LeafInfo type, NoAction operation.
- **Was Concrete Work Present In Initial Plan:** NO
- **Constraints:** maxDepth=2
- **Observed Outcome:** At depth 2, elements are navigable (Container + Click). At depth 3, elements are discover-only (LeafInfo + NoAction). The same template produces different behavior depending on current depth.
- **Provenance:** DETERMINISTIC_SIMULATION

### TE-07 — Same Type-Level Spec, Different Concrete Execution (Locate vs Deep Locate)

- **Source:** `AIIntentSimulationTests.cs` (LocateScenario vs DeepLocate)
- **Task Intent A:** "Locate About phone" (depth 2). Task Intent B: "Navigate to Internal Storage" (depth 3).
- **Pre-Execution Representation:** Same type-level shape: scope=target_only, element_handling=menu_only. Different factuals: target ("About phone" vs "Internal Storage"), depth (2 vs 3).
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** Same template (menu_only), same scope (target_only). Different depth bounds, different targets.
- **Unknown Before Execution:** Concrete elements, concrete pages, concrete routes
- **Fresh Observation Available:** Different pages, different elements, different navigation paths
- **Concrete Candidate Discovered:** Depends on which target is sought and what pages are encountered
- **Was Concrete Work Present In Initial Plan:** NO — both use DynamicMatch
- **Constraints:** Depth differs (2 vs 3), target differs ("About phone" vs "Internal Storage")
- **Observed Outcome:** Both succeed with TargetFound. Different visited page sets, different step counts. Same type-level specification shape serves two materially different concrete tasks.
- **Provenance:** DETERMINISTIC_SIMULATION

### TE-08 — Real-Run Type-Level Discovery: Elements From analysis.jsonl, Plan From plan.json

- **Source:** `20260805T052309367Z_EnumerateFixtures.cs`
- **Task Intent:** Enumerate Settings safely (real run)
- **Pre-Execution Representation:** `plan.json` from real run: safe_mode, depth=2, restore=false, 4 DynamicRules (menu_container, switch_leaf, leaf_action)
- **Representation Type:** TYPE_LEVEL (reconstructed from real run artifacts)
- **Known Before Execution:** safe_mode template, depth=2, DynamicRules
- **Unknown Before Execution:** Which concrete elements exist on each page (reconstructed post-hoc from analysis.jsonl)
- **Fresh Observation Available:** 16 elements on Settings page, 21 on Network & internet, 14 on Internet (all from real analysis.jsonl frames)
- **Concrete Candidate Discovered:** Elements matching DynamicRules discovered from recorded observation frames
- **Was Concrete Work Present In Initial Plan:** NO — plan.json has DynamicRules, not concrete steps
- **Constraints:** safe_mode, depth=2
- **Observed Outcome:** DFS revisit loop (Internet→Internet self-loop), search box misclassification. Engine exhausted max_steps (120) without restoring Settings home.
- **Provenance:** RECORDED_REALITY_DERIVED (from run `20260805T052309367Z`)

### TE-09 — ElementHandling Template Controls Which Types Are Actionable

- **Source:** `PlanCompiler.cs` TemplateSets + `AIIntentSimulationTests.cs`
- **Task Intent:** Compare menu_only vs full_interaction templates on the same fixture
- **Pre-Execution Representation:** TemplateSets keyed by ElementHandling string
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** Template controls the vocabulary of navigable element types
- **Unknown Before Execution:** Concrete elements
- **Fresh Observation Available:** Elements of various types on each page
- **Concrete Candidate Discovered:** Only elements matching the active template's MatchConditions
- **Was Concrete Work Present In Initial Plan:** NO
- **Constraints:** Template determines which types are actionable:
  - `menu_only` → only menu_item elements → safe for read-only enumeration (no state changes)
  - `full_interaction` → menu_item, switch, slider, button → can toggle, slide, click
  - `read_only` → matches anything but produces non-interactive leaf → pure observation, no interaction
- **Observed Outcome:** menu_only template: only menu_items navigated. full_interaction: switches and buttons also interacted with. Template is a TYPE-LEVEL safety constraint — it prevents entire categories of action without knowing concrete instances.
- **Provenance:** DETERMINISTIC_SIMULATION (template selection exercised in E-05)

### TE-10 — CompletionPolicy Type-Level: Exhaustive vs TargetFound

- **Source:** `PlanCompiler.cs` BuildCompletionPolicy + `SimulationBaselineTests.cs`
- **Task Intent:** Same Settings fixture, different stop conditions
- **Pre-Execution Representation:** CompletionPolicy derived from Scope or Completion override
- **Representation Type:** TYPE_LEVEL
- **Known Before Execution:** CompletionPolicy.Type (Exhaustive or TargetFound), TargetFound.TargetName, TargetFound.MatchMode, TargetFound.ActionOnFound
- **Unknown Before Execution:** Whether and when the target will be found
- **Fresh Observation Available:** Page elements, target presence
- **Concrete Candidate Discovered:** TargetFound: "Dark mode" found on Display page → stop. Exhaustive: no target → visit all reachable pages.
- **Was Concrete Work Present In Initial Plan:** NO for Exhaustive (all pages discovered dynamically). PARTIAL for TargetFound (target name known, but route to target is discovered).
- **Constraints:** Forbidden pages (Storage, Internal Storage, SD Card) must not be visited in target search
- **Observed Outcome:** Exhaustive: 19 pages, 99 steps, AllVisited. TargetFound: 14 pages, 66 steps, stops at Dark mode. Forbidden pages not visited.
- **Provenance:** DETERMINISTIC_SIMULATION

---

## Pass C — Critical Question Results

### 1. Can legitimate required work appear during execution without existing as a concrete pre-execution Plan step?

**YES.**

Evidence: TE-01 (DynamicMatch generates children from observation, not from pre-enumerated steps). TE-08 (real run used plan.json with DynamicRules, not concrete steps). E-07 (both hub buttons discovered dynamically; both are legitimate work). CAND-008 (cross-page discovery — branches discovered from fresh evidence, absent from initial Plan).

In every DynamicMatch/Intent-mode case, the plan contains RULES for what to interact with (element types), not a concrete INVENTORY of what exists. Concrete work becomes known only when observation reveals matching elements.

### 2. Can a Plan define what KIND of work is required without knowing every concrete instance?

**YES.**

Evidence: TE-09 (TemplateSets define navigable element types without knowing concrete instances). TE-01 (DynamicRules define match conditions — "match type=button" — without knowing which buttons exist). E-14 (PlanCompiler produces type-level specification, not concrete route).

The legacy TemplateSets (`full_interaction`, `menu_only`, `safe_mode`, `read_only`) are exactly this: they define categories of work (interact with menu_items, skip inputs, observe-only for read_only) without enumerating concrete instances.

### 3. Can fresh Observation change the concrete work inventory while the high-level task intent remains unchanged?

**YES.**

Evidence: TE-07 (same type-level spec, different concrete execution for different targets). TE-08 (real run's concrete work inventory differs from any pre-execution expectation — 16 Settings elements, 21 Network & internet elements, 14 Internet elements — all discovered from observation). TE-05 (at depth=2, Wi‑Fi is observable; pre-fix it changed the work inventory by adding a depth-3 child).

The high-level intent ("enumerate all safe Settings entries within depth ≤ 2") remains unchanged while observation reveals different concrete elements on different pages. The work inventory grows as new pages are visited.

### 4. Can two different concrete routes satisfy the same traversal intent?

**YES.**

Evidence: TE-07 (locate "About phone" at depth 2 vs locate "Internal Storage" at depth 3 — same intent shape, different routes). More directly: E-03-A (full traversal of 7-page Settings) — the same intent could produce different visit orders depending on which elements are observed first. The DynamicMatch rules don't prescribe order; they prescribe type-matching. Two runs on slightly different app versions could visit pages in different orders while satisfying the same intent.

Counter-example: TE-02 (Static plan with explicit coordinates) — only ONE route satisfies the plan. If the route is invalid, the task fails. The plan IS the route.

### 5. Can an initial concrete route become invalid because actual UI topology differs?

**YES.**

Evidence: TE-02 (Static plan coordinates may not match real screen → tap misses → locate fails). E-16 (ScenarioPlanLoader coordinate mismatch failure mode — plan JSON loads successfully, coordinates are valid, but they don't match real screen). E-01 locate scenario (status = "pending_verification" — Host cannot confirm target page identity without offline TraceTool verification, implying the concrete route's assumptions are not trusted).

The legacy system handles this by having two separate modes: Static plans fail when reality differs; DynamicMatch plans adapt because they don't prescribe concrete routes.

### 6. Does absence from a concrete initial Plan ever coexist with evidence that newly discovered work is still in scope?

**YES.**

Evidence: TE-04 (both hub buttons discovered dynamically, both in scope, but only one dispatched). TE-01 (all 18 Settings elements discovered dynamically, all in scope per the type-level specification). CAND-008 (cross-page discovery — work absent from initial Plan but in scope per constraints).

The type-level specification defines scope. Elements matching the specification are in scope regardless of whether they were known before execution. The E-07 bug is precisely this: both buttons are in scope (matching button_rule), but only one is dispatched.

### 7. Does completion depend on concrete work discovered during execution rather than only on exhausting prebuilt Plan steps?

**YES.**

Evidence: TE-01 (Exhaustive completion — must visit all dynamically discovered pages, not just pre-enumerated ones). TE-04 (BUG — completion reported despite undispatched dynamically-discovered branch). E-03-A (AllVisited depends on all dynamically generated children being visited). E-03-B (TargetFound depends on discovering the target during execution).

Plan-step exhaustion alone never completes a run (legacy and current architecture both agree on this: I-10, GoalEvidence required). In type-level mode, there ARE no prebuilt plan steps — completion depends on exhausted discovery.

### 8. Are scope/depth/safety constraints meaningful independently of the exact concrete route?

**YES.**

Evidence: TE-05 (Depth=2 constraint meaningful regardless of which pages exist at depth 3). TE-06 (depth semantics formula applies to any element at a given depth, independent of what that element is). TE-09 (menu_only template prevents switch interaction regardless of which switches exist). TE-10 (TargetFound stops at the named target regardless of the route taken to reach it).

These constraints operate at the TYPE level — they constrain categories of behavior, not specific instances. Depth=2 means "don't go deeper than 2" regardless of what is at depth 3. menu_only means "only interact with menu_items" regardless of which switches, buttons, or sliders exist.

---

## Pass D — Traversal Reality Distinctions

### TRD-01 — TypeLevelTraversalSpecification != ConcreteFutureRoute

**Statement:** TypeLevelTraversalSpecification != ConcreteFutureRoute

**Meaning:** A specification of what KINDS of elements to interact with, what CONSTRAINTS to respect, and what COMPLETION condition to satisfy is not equivalent to a pre-enumerated sequence of concrete actions. The specification defines the rules of engagement; the concrete route is discovered from observation within those rules.

**Supporting Atomic Evidence:** TE-01 (DynamicMatch generates children from type rules, not pre-enumerated steps), TE-03 (NL → type-level spec → DynamicMatch execution), TE-08 (real run plan.json with DynamicRules, not concrete steps), TE-09 (TemplateSets define categories, not instances).

**Observed Failure If Conflated:** TE-02 (Static plan fails when coordinates don't match — because the plan IS the route, there is no adaptation). TE-04 (type-level spec correctly identifies both branches, but execution fails to dispatch one — not a spec failure, an execution failure).

**Required Observable Consequence:** A correct system must be capable of accepting a task specification that defines interaction categories and constraints without prescribing every concrete action. Concrete work must be discoverable from observation within the specified constraints. The specification must constrain what is permissible; observation must provide what is actual.

**Implementation Independence Check:** True without Graph, Stack, FSM, Planner, DynamicPlan, BranchInventory, or Manager. The distinction is about what information exists before execution vs what can only come from observation.

**Confidence:** HIGH

---

### TRD-02 — TaskScope != ConcreteWorkInventory

**Statement:** TaskScope != ConcreteWorkInventory

**Meaning:** The declared scope of a task (which types of elements to interact with, within what boundaries) is not equivalent to the concrete inventory of work items that will be encountered. The scope defines the boundary of legitimate work; the inventory is populated by observation within that boundary.

**Supporting Atomic Evidence:** TE-05 (scope: full enumeration to depth 2. inventory: grows as pages are visited. Wi‑Fi at depth 3 is OUTSIDE the scope boundary even though it is observable), TE-04 (scope: all navigable branches. inventory: two branches discovered, only one dispatched), TE-08 (scope: safe_mode, depth=2. inventory: 16+21+14 elements across 3 pages discovered from analysis.jsonl).

**Observed Failure If Conflated:** TE-05 pre-fix (scope boundary declared but not enforced → inventory exceeded scope). TE-04 (scope correctly includes both branches, but inventory completion claimed when inventory is incomplete). E-03-B (target search scope excludes Storage subtree; conflating scope with inventory would enter forbidden pages).

**Required Observable Consequence:** A correct system must be capable of distinguishing "work discovered so far" (inventory) from "all work within scope" (boundary). Completion requires proving the inventory is complete with respect to the scope — not merely that some inventory exists.

**Implementation Independence Check:** True. Scope is a constraint on what counts as legitimate. Inventory is what observation reveals. The distinction does not require specific data structures.

**Confidence:** HIGH

---

### TRD-03 — PlanStepExhaustion != TraversalCompletion

**Statement:** PlanStepExhaustion != TraversalCompletion

**Meaning:** Having executed all pre-enumerated plan steps is not equivalent to having completed the traversal task. In type-level mode, there may be no pre-enumerated steps at all. In concrete mode, steps may complete but the task may not be satisfied. Completion is a semantic judgment about task satisfaction, not a mechanical judgment about step execution.

**Supporting Atomic Evidence:** TE-04 (DynamicMatch has no pre-enumerated steps — steps are generated from observation. Plan-step exhaustion is meaningless in this mode). TE-02 (Static plan steps execute successfully, but Host defers completion verdict to TraceTool VerifyEngine — plan-step completion ≠ task completion). E-03-B (TargetFound completion depends on recognizing the target, not on exhausting steps). Legacy invariant I-10 (completion requires Goal Evidence, not plan exhaustion).

**Observed Failure If Conflated:** TE-04 (if "all generated steps complete" were treated as completion, the bug would be correct behavior — but it's not, because the second branch was never dispatched). TE-01 (Exhaustive mode: steps are dynamically generated; "all steps complete" is only true when discovery is exhausted, which is a semantic condition, not a mechanical one).

**Required Observable Consequence:** A correct system must have a completion criterion independent of step execution. In type-level mode: all discoverable work within scope has been discovered and completed. In concrete mode: the target condition is satisfied. In neither mode is "I ran out of steps" equivalent to "I finished the task."

**Implementation Independence Check:** True. Already an architecture invariant (I-10) in the current Runtime. The legacy evidence confirms the invariant is necessary.

**Confidence:** HIGH

---

### TRD-04 — PlanConstructionValidation != WorldCorrespondenceValidation

**Statement:** PlanConstructionValidation != WorldCorrespondenceValidation

**Meaning:** Validating that a plan is internally consistent (well-formed types, non-empty targets, valid depth bounds) is not equivalent to validating that the plan corresponds to the actual external world. A plan can pass all construction-time validation and still fail at execution because the world differs from the plan's assumptions.

**Supporting Atomic Evidence:** E-14 (PlanCompiler fail-fast validation: non-empty TargetApp, valid Scope, non-empty Target for target_only, valid ElementHandling key, non-negative Depth. None of these check world correspondence.) TE-02 (Static plan JSON passes all validation — coordinates within bounds, targets non-empty, structure well-formed — but coordinates may not match real screen.) E-15 (IntentExtractor vocabulary validation: Scope ∈ {"full", "target_only"}, ElementHandling key exists. Does not validate that AI extracted the CORRECT scope for the task.)

**Observed Failure If Conflated:** TE-02 (plan valid, coordinates wrong → plan fails at execution). E-15 (AI extraction vocabulary-valid but semantically wrong: scope="full" extracted for a locate task → engine enumerates everything instead of stopping at target). E-16 (plan JSON valid, world differs → tap misses, locate fails). SP-12 (PlanConstructed != ExecutionGuaranteed — already a Step-5 scenario pressure).

**Required Observable Consequence:** A correct system must have two distinct validation stages: construction-time (is the plan internally well-formed?) and execution-time (does the plan match the observed world?). Construction-time success is necessary but not sufficient.

**Implementation Independence Check:** True. Already covered by SP-12 and architecture invariant I-5 (Plan is hypothesis, not reality). The legacy evidence provides the type-level dimension: type-level plans are always "internally valid" because they have no concrete coordinates to mismatch — but they can still be wrong about which element types exist.

**Confidence:** HIGH

---

### TRD-05 — ElementCategoryAuthorization != ConcreteCandidateExistence

**Statement:** ElementCategoryAuthorization != ConcreteCandidateExistence

**Meaning:** Authorizing a category of elements as permissible to interact with (e.g., "menu_items are safe to tap") is not equivalent to confirming that a specific observed element actually belongs to that category or that interacting with it will produce the expected outcome. Category authorization authorizes the TYPE; concrete existence and correct classification must be verified per INSTANCE.

**Supporting Atomic Evidence:** TE-09 (menu_only template authorizes menu_item interaction. But VE-06 shows a search box classified AS menu_item — the category authorization is correct for the DECLARED type, but the element's ACTUAL type is different.) VE-05 (subtitle classified as menu_item — category authorization permits tapping it, but it's not actually a menu_item.) VE-07 (type-blind matching treats text elements as navigable — category authorization doesn't filter by actual type.)

**Observed Failure If Conflated:** VE-06 (search box authorized as menu_item → tapped → search UI stuck). VE-05 (subtitle authorized as menu_item → double-click). VE-07 (text element authorized as navigation target via type-blind match → wrong element tapped).

**Required Observable Consequence:** A correct system must have per-instance verification that an observed element's actual characteristics match the authorized category before dispatch. Category-level authorization ("menu_items are OK") is necessary but not sufficient; instance-level verification ("THIS element IS a menu_item") is also required.

**Implementation Independence Check:** True. Category authorization operates at the type level (which KINDS of elements). Instance verification operates at the observation level (THIS specific element). The legacy TemplateSets provide category authorization; the legacy system lacks systematic instance verification (VE-05, VE-06, VE-07 all show failures at the instance level).

**Confidence:** MEDIUM

The distinction is evidenced but the legacy system's instance-verification gap is primarily a perception problem (YOLO/OCR errors) rather than a semantic-model gap. The current Runtime's CandidateAuthorizationEvidence (per-instance authorization) addresses this at the semantic level; S2 production-shaped perception would address it at the observation level.

---

## Pass E — Traversal Scenario Pressures

### TSP-01 — Type-Level Task Specification Must Accept Dynamically Discovered Work

**Scenario Pressure ID:** TSP-01
**Title:** A task specification defining interaction categories and constraints must permit concrete work discovered from observation without requiring pre-enumeration
**Primary TRD:** TRD-01 (TypeLevelTraversalSpecification != ConcreteFutureRoute)
**Secondary TRDs:** TRD-02 (TaskScope != ConcreteWorkInventory)
**Source Evidence:** TE-01, TE-03, TE-08, TE-09
**Evidence Strength:** MULTI_SOURCE_CORROBORATED

---

**Intent:**
Enumerate all safe Settings entries within depth ≤ 4. The task does not know in advance which specific entries exist on this device.

**Given:**
- The task specification defines: scope = full enumeration, element handling = safe_mode (menu_container, switch_leaf, slider_leaf, leaf_action), depth bound = 4, completion = Exhaustive.
- The specification does NOT enumerate concrete pages, concrete elements, element coordinates, or the navigation route.
- The device's Settings app has 14 pages across 4 levels of nesting — but this is not known before execution.

**Plan / Specification Knows:**
- Which element types to interact with (menu_item, switch, slider, button)
- How deep to go (≤ 4)
- When to stop (all reachable content within constraints exhausted)
- How to enter (cold launch Settings)

**Plan / Specification Does NOT Know:**
- Which concrete pages exist
- Which concrete elements exist on each page
- Element coordinates
- The complete navigation route
- How many total elements will be discovered

**Available Fresh Evidence:**
- Observation of each page: elements with text, types, and coordinates
- Page identity after navigation
- Element type classification from observation
- Scroll state and viewport content

**When:**
Fresh observation reveals a previously unknown page ("Network & internet") with menu items ("Internet", "SIMs", "Airplane mode", …). These elements match the type-level specification (type=menu_item → menu_container template). They were not pre-enumerated in any concrete plan because no concrete plan exists — only the type-level specification.

**Then:**
The system must recognize that these elements are legitimate work within the task's scope and constraints. It must generate navigation tasks for them, dispatch them, and incorporate their outcomes into the completion decision. The system must NOT require these elements to have been listed in a concrete pre-execution plan.

**Must Not:**
- Reject valid work solely because it was absent from a concrete pre-execution inventory
- Require pre-enumeration of all concrete future work before execution begins
- Exceed scope/depth/safety constraints in the process of discovering new work
- Claim completion based on plan-step exhaustion when the plan has no pre-enumerated steps

**Pass Oracle:**
All 14 pages across 4 levels are discovered, visited, and exhausted. Completion depends on evidence that all discoverable work within scope has been completed. The system never required a concrete pre-enumerated route.

**Fail Oracle:**
The system demands a concrete plan with explicit steps before execution. Without one, it refuses to execute. OR: the system discovers legitimate work but ignores it because it wasn't in the initial plan. (Legacy E-07: both branches discovered, one ignored.)

**Completion Rule:**
Completion when all reachable content within scope (element types, depth bound) has been discovered, dispatched, and exhausted. Fresh observation must confirm no remaining undispatched in-scope work.

**Evidence Maturity Needed:** S0 (synthetic) — already proven by E-03 Capstone. S1 (recorded replay) — attach TE-08 real-run evidence.

**What This Scenario Proves:**
A task specification can legitimately define WHAT KINDS of work to do without enumerating every concrete instance. Concrete work is discovered from observation within the specification's constraints.

---

### TSP-02 — Work Inventory Must Be Distinguished From Task Scope Boundary

**Scenario Pressure ID:** TSP-02
**Title:** Completion must be based on proving the work inventory is complete with respect to the task scope — not merely on having executed some discovered work
**Primary TRD:** TRD-02 (TaskScope != ConcreteWorkInventory)
**Secondary TRDs:** TRD-03 (PlanStepExhaustion != TraversalCompletion)
**Source Evidence:** TE-04 (E-07 bug), TE-05 (depth boundary vs inventory)
**Evidence Strength:** EXECUTABLE_REGRESSION

---

**Intent:**
Exhaustively enumerate all navigable branches from a hub page. The task scope includes all reachable branches.

**Given:**
- A hub page with two navigation buttons leading to two independent branches.
- The task specification authorizes button-type elements as navigable.
- Observation reveals both buttons ("Go to List A", "Go to List B").

**Plan / Specification Knows:**
- Buttons are navigable (type-level authorization)
- Exhaustive completion requires all reachable branches

**Plan / Specification Does NOT Know:**
- How many buttons exist on the hub
- What pages they lead to
- How many items are in each target list

**Available Fresh Evidence:**
- Hub page observation: two buttons visible
- After traversing List A: List A is exhausted (16/16 items)
- Hub page re-observed: "Go to List B" is still visible and has never been tapped

**When:**
The system has traversed List A (16/16 items) and returned to the hub. The current work inventory shows: List A = complete, List B = not dispatched. The task scope requires all reachable branches.

**Then:**
The system must compare the current work inventory against the task scope. It must recognize that List B is within scope and undispatched. The inventory is incomplete with respect to the scope. The system must dispatch List B and only report completion when the inventory matches the scope.

**Must Not:**
- Report completion when the inventory is incomplete with respect to the scope
- Treat "I finished one branch" as equivalent to "I finished all branches in scope"
- Ignore observable in-scope work that has not been dispatched

**Pass Oracle:**
Both branches are dispatched and exhausted. Inventory (both branches complete) matches scope (all reachable branches). Completion reported. Total items visited = 32.

**Fail Oracle:**
List A complete. System reports completion. List B never dispatched (0/16 items). Inventory does not match scope. (Legacy E-07 unfixed bug.)

**Completion Rule:**
Completion requires proving that the concrete work inventory is complete with respect to the task scope. This means: all observable navigation targets within scope have been dispatched, AND all dispatched branches have been exhausted, AND no page with undispatched in-scope targets remains.

**Evidence Maturity Needed:** S0 (synthetic) — E-07 is an executable failing test.

**What This Scenario Proves:**
The system does not confuse "I did some work" with "I did all work within scope."

---

### TSP-03 — Element Category Authorization Must Not Substitute for Per-Instance Verification

**Scenario Pressure ID:** TSP-03
**Title:** Authorizing a category of elements as permissible must not eliminate per-instance verification that an observed element actually belongs to that category
**Primary TRD:** TRD-05 (ElementCategoryAuthorization != ConcreteCandidateExistence)
**Secondary TRDs:** VRD-02 (Element Classification Output != Interaction Capability)
**Source Evidence:** VE-05 (subtitle phantom), VE-06 (search box misclassification), VE-07 (type-blind match)
**Evidence Strength:** RECORDED_REALITY_DERIVED

---

**Intent:**
Enumerate navigable Settings entries using a menu_only template that authorizes only menu_item elements for navigation.

**Given:**
- The task specification authorizes menu_item elements as navigable.
- The Settings home page contains:
  - Actual menu_items: "Network & internet" (correctly classified)
  - A search box misclassified as menu_item (should be input)
  - A subtitle "Bluetooth, pairing" misclassified as menu_item (should be text)

**Plan / Specification Knows:**
- menu_items are authorized for navigation
- Other element types are not authorized

**Plan / Specification Does NOT Know:**
- Which specific observed elements are correctly classified as menu_items
- That the search box and subtitle are misclassified

**Available Fresh Evidence:**
- Element observations: text, declared type, coordinates
- Post-tap observation: did the page change? To what page?

**When:**
The system observes elements declared as menu_item. Category authorization says "menu_items are OK to tap." But two of the declared menu_items are misclassified — they are not actually navigable menu items.

**Then:**
The system must not rely solely on category authorization. It must verify per-instance: after tapping an element, did the observed outcome match the expected outcome for the authorized category? If an element declared as menu_item produces no navigation, or navigates to an unexpected page, the element's actual category is not menu_item — regardless of its declaration.

**Must Not:**
- Dispatch every element matching the authorized category without per-instance outcome verification
- Treat category authorization as proof of correct classification
- Continue treating an element as navigable after it fails to produce navigation

**Pass Oracle:**
Search box (declared menu_item) is not tapped — or if tapped, the failure to navigate is detected and the element is excluded from further navigation. Subtitle is not tapped independently. Only correctly classified menu_items are navigated.

**Fail Oracle:**
Search box tapped → search UI entered → stuck. Subtitle tapped → same page as adjacent menu_item → double-click. Category authorization treated as sufficient. (Legacy VE-05, VE-06, VE-07 failures.)

**Completion Rule:**
NOT_APPLICABLE (this scenario tests per-instance verification, not completion).

**Evidence Maturity Needed:** S2 (production-shaped perception with real YOLO/OCR classification errors). S0 synthetic environments provide perfect classification.

**What This Scenario Proves:**
Category-level authorization is necessary but not sufficient. Per-instance outcome verification is required because classification can be wrong.

---

## Pass F — Relation to Existing Portfolio

| TSP | Relation | Existing SP | Rationale |
|---|---|---|---|
| TSP-01 | STRONGER_REFORMULATION_OF_EXISTING_SP | SP-04, SP-09 | TSP-01 reformulates the type-level traversal pressure more precisely than SP-04 (which focuses narrowly on depth bound enforcement) and SP-09 (which focuses on intent vs execution method at the input boundary). TSP-01 captures the core traversal abstraction: the specification defines categories and constraints; observation provides instances. |
| TSP-02 | COMPOSITION_WITH_EXISTING_SP | SP-03 | TSP-02 composes with SP-03 (multi-branch hub). SP-03 tests "don't report complete with unvisited branch." TSP-02 adds the explicit scope-vs-inventory distinction: the reason SP-03 fails is because inventory was conflated with scope. |
| TSP-03 | ATTACH_TO_EXISTING_SP | SP-07 + VSP-03 | TSP-03 is the traversal-level counterpart to SP-07 (element visibility ≠ navigability) and VSP-03 (classification must be verified). TSP-03 adds the category-vs-instance distinction specific to type-level traversal. |

**No genuinely new independent traversal pressures beyond the existing portfolio.** TSP-01, TSP-02, and TSP-03 strengthen and reframe existing pressures rather than identifying new semantic distinctions. TRD-01 through TRD-04 are already covered by SP-04, SP-09, SP-12, and SP-03 respectively. TRD-05 is covered by VSP-03 and SP-07.

---

## Closed-World vs Open-World Finding

**CLOSED_WORLD_PLAN_SUPPORTED:** YES

Evidence: TE-02 (Static plan with explicit coordinates). The legacy system supports tasks where the concrete route IS known before execution. The locate-one-item scenario uses explicit coordinates and expected page identities. This is a legitimate task class: when the world is stable and the route is known, a concrete plan is efficient and verifiable.

**OPEN_WORLD_TRAVERSAL_SUPPORTED:** YES

Evidence: TE-01 (DynamicMatch), TE-03 (NL→type-level spec), TE-08 (real-run DynamicRules). The legacy system supports tasks where only the type-level specification is known before execution. The enumerate-settings-safely scenario uses DynamicMatch rules and discovers concrete work from observation. This is also a legitimate task class: when the world is partially unknown or variable, a type-level specification is necessary.

**Relationship:**
The two modes are NOT interchangeable. Each fails in ways the other doesn't:
- Static plan fails when world differs from plan (coordinate mismatch)
- Type-level spec fails when classification is wrong (search box as menu_item) or when dispatch is incomplete (branch loss)

The legacy system implements them as two separate code paths (ScenarioPlanLoader vs PlanCompiler). They share the same execution engine (TraversalEngine/TraversalFSM) but have different plan construction paths.

The legacy documentation explicitly states: "Plan mode ≠ Intent mode, But Both Use the FSM" (`runner-through-engine-design.md`).

---

## S1 Replay-Worthy Traversal Evidence

| Priority | Evidence | What to Replay |
|---|---|---|
| **P0** | TE-08 (real run `20260805T052309367Z` — plan.json with DynamicRules, analysis.jsonl with concrete elements) | Replay the type-level plan against recorded observations. Verify: were all in-scope elements discovered? Did the engine correctly generate children for matching elements? Did it miss any? |
| **P1** | TE-04 (E-07 MultiBranchNavigation — both branches discovered, only one dispatched) | Replay as a concrete failure of scope-vs-inventory. Prove that the type-level spec correctly identifies both branches; the failure is in dispatch/completion accounting. |
| **P1** | TE-05 (E-11 SettingsEnumerateRegression — depth=2 enforced) | Replay the depth-boundary case: elements at depth=3 are observable but must not generate navigable children. Verify the constraint is enforced at the type level. |

---

## Future Intent / Planning Pressure

**What this supplement does NOT prove:**
- That the Runtime needs an Intent→Goal/Plan synthesis pipeline (this remains deferred to Phase 5/6, as confirmed in Step 6)
- That the Runtime needs a "Planner" component
- That type-level and concrete plans must be unified into one abstraction

**What this supplement DOES prove:**
- That type-level task specifications are a legitimate and necessary task class for autonomous GUI traversal
- That the distinction between "what kinds of work" and "what concrete instances of work" is fundamental, not an implementation detail
- That completion in type-level mode depends on proving the concrete work inventory is complete with respect to the type-level scope — a semantic condition, not a mechanical one

**Separation from Intent→Goal/Plan synthesis:**
The legacy system's PlanCompiler is a deterministic type-level plan constructor. It takes structured IntentSlots and produces a TraversalPlan. It does NOT take natural language and produce IntentSlots — that is IntentExtractor's role (AI-driven, probabilistic). The PlanCompiler is traversal semantics. The IntentExtractor is intent understanding. These are different concerns. The traversal semantics are evidenced and proven. The intent understanding is AI-dependent and deferred.

---

## Evidence Gaps

| Gap | Description |
|---|---|
| EVIDENCE_GAP_TYPE_LEVEL_COMPLETION_PROOF | No legacy test directly proves "inventory matches scope" as a completion condition independent of AllVisited flag. The E-07 bug shows the failure; the fix (BranchProgressEvidence in current Runtime) is not legacy-evidenced. |
| EVIDENCE_GAP_CROSS_MODE_COMPARISON | No legacy test executes the same task in both Static and DynamicMatch modes and compares outcomes. The two modes are tested independently. |
| EVIDENCE_GAP_TEMPLATE_DRIFT | Legacy TemplateSets are hardcoded (full_interaction, menu_only, safe_mode, read_only). No evidence tests what happens when the template vocabulary doesn't match the actual element taxonomy (e.g., a new Android UI element type that doesn't fit any legacy category). |

---

## Readiness

**TRAVERSAL_PLAN_PRESSURE_SUPPLEMENT_READY_FOR_CHALLENGE**

5 Traversal Reality Distinctions derived. 3 Traversal Scenario Pressures formulated and mapped to existing SP/VSP portfolio. Closed-world and open-world task classes both evidenced. No new independent pressures beyond existing portfolio — the TRDs strengthen and reframe existing SPs rather than identifying genuinely new semantic distinctions.

---

## Repository Changes

`docs/decisions/legacy-traversal-plan-abstraction-supplement.md` ONLY
