# UNIFIED_SCENARIO_PRESSURE_PORTFOLIO_RESULT

> Generated: 2026-08-09
> Inputs: Steps 1–6 core pipeline + visual supplement + traversal supplement
> Role: Canonical, deduplicated Scenario Pressure Portfolio — stable pressure map

---

## Reconciliation Inputs

| Source | Formulations | After Dedup |
|---|---|---|
| Base SP (Step 5) | 13 | 13 retained as canonical |
| Visual VSP (Supplement) | 3 | 1 new CP, 2 merged as variants |
| Traversal TSP (Supplement) | 3 | 0 new CPs, 3 merged as refinements |
| **Total** | **19** | **14** |

---

## Reconciliation Summary

**Raw Formulations:** 19 (13 SP + 3 VSP + 3 TSP)
**Canonical Independent Pressures:** 14
**Merged as Variants:** 2 (VSP-02 → CP-13, VSP-03 → CP-11)
**Merged as Refinements:** 3 (TSP-01 → CP-07 + CP-14, TSP-02 → CP-04, TSP-03 → CP-11)

### Merge Rationale

| Formulation | Disposition | Reason |
|---|---|---|
| VSP-01 (Target grounding) | **NEW CP-12** | Distinct primary RD (VRD-03: Coordinate/Text Match != Semantic Target Identity). Distinct oracle: "of multiple matching candidates, was the CORRECT one selected?" Not covered by SP-07 which asks "is this element navigable at all?" |
| VSP-02 (OCR variants → stable identity) | Variant of CP-13 | Same primary RD (RD-08 / VRD-01). OCR normalization is a visual subcase of page/element identity across observations. |
| VSP-03 (Classification verification) | Variant of CP-11 | Same primary RD (RD-05 / VRD-02). Classification verification is a visual subcase of element visibility ≠ navigability. |
| TSP-01 (Type-level spec) | Refinement of CP-07 + CP-14 | TRD-01/TRD-02 strengthen depth-bound and intent-vs-method. No new oracle. |
| TSP-02 (Inventory vs scope) | Refinement of CP-04 | TRD-02/TRD-03 strengthen multi-branch completion. Same oracle. |
| TSP-03 (Category vs instance) | Variant of CP-11 | TRD-05 is a traversal-level restatement of RD-05 + VRD-02. Same oracle as SP-07. |

---

## Portfolio-Level Findings

### 1. Established Territory (FROZEN at S0)

10 pressures already frozen and proven by current Runtime (Step 6). No new semantics needed. S1 replay is evidence-maturity upgrade only.

### 2. S1 Evidence Frontier

6 pressures should consume recorded legacy reality next. SP-05 (E-13-B ADB scroll failure) is the highest-value single upgrade — P0 severity legacy gap, weakest current S0 proof.

### 3. S2 Perception Frontier

3 pressures involve visual/perception evidence requiring production-shaped pipelines (real YOLO, OCR, screenshots). CP-12 (target grounding) is the highest-value S2 target — newly identified, distinct from existing SPs, directly affects action correctness.

### 4. Semantic Frontier

**NONE.** All 14 pressures can be honestly expressed by current Runtime semantics (Step 6 confirmed 0 SEMANTIC_MODEL_GAP for the 13 SPs; VSP-01/CP-12 needs a CHALLENGE_REQUIRED assessment but the semantic model likely covers it via CandidateAuthorizationEvidence).

### 5. Architecture Frontier

**NONE.** No pressure challenges frozen ownership/authority/invariants.

### 6. Product Frontier

1 pressure (CP-14: Intent≠ExecutionMethod) is explicitly deferred to Phase 5/6 Intent→Goal/Plan synthesis.

---

## Canonical Pressure Portfolio

---

### Domain A: World Truth / Action Effect

---

### CP-01 — Entry Must Verify Foreground App Before Traversal

**Canonical Pressure ID:** CP-01
**Title:** Entry action must verify foreground app before traversal begins
**Domain:** World Truth / Action Effect
**Primary Reality Distinction:** RD-01 (ActionExecution != ActionEffect)
**Supporting Reality Distinctions:** RD-11 (PlanConstructed != ExecutionGuaranteed)
**Core Reality Requirement:** Before beginning traversal, the system must obtain fresh observational evidence that the intended application is in the foreground. "Launch command dispatched" is not equivalent to "target app is in the foreground."

**Core Scenario:**
- **Intent:** Launch the Android Settings app and enumerate its entries.
- **Given:** Device/emulator running but Settings app NOT in foreground.
- **Available Evidence:** Launch command dispatch record; fresh observation of foreground app and screen content.
- **When:** Entry mechanism reports success internally, but app did not actually come to foreground.
- **Then:** System must obtain fresh evidence confirming foreground app matches intent. Without it: report entry failure with diagnosable reason. Do NOT proceed to traversal.
- **Must Not:** Treat "launch dispatched" as "app is in foreground." Begin enumerating content on wrong screen.
- **Pass Oracle:** Fresh observation confirms foreground app matches target package AND screen content is consistent with expected entry page.
- **Fail Oracle:** Foreground app mismatch OR unrecognizable content → entry failure reported. Traversal never begins.
- **Completion Rule:** NOT_APPLICABLE (tests entry, not completion).

**Variants:**
- INTEGRATION variant (from E-01 locate scenario): entry strategy from scenario JSON.
- NEGATIVE_CONTROL variant (from E-13-A): explicit test that fake-success entry path is impossible.

**Current System Status:** FROZEN_CAPABILITY_COVERED (Startup.cs foreground verification, StartupForegroundVerificationFailureTests)
**Evidence Maturity:** S0 (synthetic) → Next: S1_REPLAY (recorded entry-failure evidence)

---

### CP-02 — Navigation Must Verify Observable Page Change

**Canonical Pressure ID:** CP-02
**Title:** Navigation action must detect when the observed world did not change
**Domain:** World Truth / Action Effect
**Primary Reality Distinction:** RD-01 (ActionExecution != ActionEffect)
**Supporting Reality Distinctions:** RD-05 (ElementPresence != ElementNavigability)
**Core Reality Requirement:** After tapping an element for navigation, the system must compare pre-action and post-action observations. "Tap dispatched" is not equivalent to "navigation occurred." An element tapped K times with no observable page change must not be treated as a navigation target.

**Core Scenario:**
- **Intent:** Navigate from Settings home to a sub-page by tapping a labeled entry.
- **Given:** System on known page with interactive elements. One appears to be a navigation target.
- **Available Evidence:** Pre-action observation, action dispatch record, post-action observation.
- **When:** System taps an element, but post-tap observation is materially identical to pre-tap (no page change).
- **Then:** System must detect no-change. Must NOT treat tap as successful navigation. After K consecutive no-change attempts, exclude element from navigation.
- **Must Not:** Treat "tap dispatched" as "navigation occurred." Loop indefinitely on same element.
- **Pass Oracle:** No-change element detected within K attempts. Element excluded. Other work continues.
- **Fail Oracle:** Element tapped 4+ times with no page change. Step budget exhausted or false completion. (Legacy: stale click infinite retry.)
- **Completion Rule:** NOT_APPLICABLE.

**Variants:**
- VISUAL variant (from VE-09): 20% byte-length false success — screenshot changed but page didn't.
- INTEGRATION variant (from VE-02): coordinate-only tap without post-action visual verification in ADB test.

**Current System Status:** FROZEN_CAPABILITY_COVERED (Traversal Observe→Verify, Container.IsStillMine, UncertainActionVerificationTests)
**Evidence Maturity:** S0 → Next: S1_REPLAY (recorded stale-click evidence from E-09-L4)

---

### CP-03 — Plan Validity Must Not Imply Execution Success

**Canonical Pressure ID:** CP-03
**Title:** A successfully constructed plan must not be treated as a guarantee of successful execution
**Domain:** World Truth / Action Effect
**Primary Reality Distinction:** RD-11 (PlanConstructed != ExecutionGuaranteed)
**Supporting Reality Distinctions:** RD-01 (ActionExecution != ActionEffect)
**Core Reality Requirement:** A plan that passes all internal validation (well-formed, valid coordinates, valid scope) is not a guarantee that execution will succeed. Plan-world divergence must be detectable at execution time. Plan dispatch ≠ plan success.

**Core Scenario:**
- **Intent:** Execute a locate plan with explicit coordinates.
- **Given:** Plan passes all construction-time validation (coordinates in bounds, target non-empty). But on actual device, target is at different coordinates.
- **Available Evidence:** Plan structure (internally valid), pre-action observation, action dispatch, post-action observation.
- **When:** System executes planned tap. Post-action observation shows either same page (miss) or unexpected page (wrong target).
- **Then:** System must detect divergence between plan expectation and observed outcome. Must NOT report success based on plan validity alone.
- **Must Not:** Report plan success because plan was well-formed and actions were dispatched. Ignore contradictory post-action observations.
- **Pass Oracle:** Tap misses target. System detects divergence. Does NOT report success. Either recovers or reports execution failure with reason.
- **Fail Oracle:** Tap misses. System reports success because plan was valid and action was dispatched. Target page never reached.
- **Completion Rule:** Plan completion based on observed outcomes matching expectations, not on plan dispatch. Each step verified by observation.

**Variants:**
- ARCHITECTURE variant (from I-5): Plan is hypothesis, not reality — frozen invariant.
- TRAVERSAL variant (from TRD-04): PlanConstructionValidation != WorldCorrespondenceValidation — type-level plans are always internally valid but can still be wrong about the world.

**Current System Status:** FROZEN_CAPABILITY_COVERED (I-5, I-10, GoalEvidenceCompletionTests negative)
**Evidence Maturity:** S0 → Next: NONE (architecture-level invariant, no evidence upgrade needed)

---

### Domain B: Completion / Progress

---

### CP-04 — Multi-Branch Hub Must Not Report Complete With Unvisited Branch

**Canonical Pressure ID:** CP-04
**Title:** Dispatched work on a subset of branches must not be treated as proof that all reachable work is complete
**Domain:** Completion / Progress
**Primary Reality Distinction:** RD-02 (WorkDispatched != WorkCompleted)
**Supporting Reality Distinctions:** RD-09 (PreviouslyVisited != Unexplored), TRD-02 (TaskScope != ConcreteWorkInventory)
**Core Reality Requirement:** When a hub page has multiple observable navigation targets, completion must not be claimed while any observable target remains undispatched. The set of dispatched targets must equal the set of reachable targets before completion.

**Core Scenario:**
- **Intent:** Enumerate all reachable content from a hub page with two navigation branches.
- **Given:** Hub page with "Go to List A" (16 items) and "Go to List B" (16 items). Both observable.
- **Available Evidence:** Hub observation (both buttons visible), List A observation (16 items), post-return hub observation ("Go to List B" still visible, never tapped).
- **When:** List A fully traversed, returned to hub. "Go to List B" observable and undispatched.
- **Then:** System must recognize "Go to List B" as undispatched in-scope work. Must dispatch it. Completion only after both branches exhausted.
- **Must Not:** Report AllVisited while "Go to List B" never tapped. Treat "one branch done" as "all branches done."
- **Pass Oracle:** Both branches dispatched and exhausted. 32/32 items visited. Completion only after second branch done.
- **Fail Oracle:** List A done. System reports completion. List B 0/16. (Legacy E-07 unfixed bug.)
- **Completion Rule:** Completion when all observable navigation targets on all visited pages dispatched AND all dispatched branches exhausted AND no page with undispatched targets remains.

**Variants:**
- NEGATIVE_CONTROL variant: non-scrollable branches prove bug is independent of scroll.
- TRAVERSAL refinement (from TSP-02): explicit scope-vs-inventory distinction — completion requires proving inventory matches scope.

**Current System Status:** FROZEN_CAPABILITY_COVERED (BranchProgressEvidence.IsSubtreeComplete, CAND-004, Capstone)
**Evidence Maturity:** S0 → Next: S1_REPLAY (E-07 recorded as concrete failure evidence)

---

### CP-05 — Revisiting a Page Must Not Reset Exploration State

**Canonical Pressure ID:** CP-05
**Title:** Re-entering a previously visited page must preserve knowledge of what was already explored
**Domain:** Completion / Progress
**Primary Reality Distinction:** RD-09 (PreviouslyVisited != Unexplored)
**Supporting Reality Distinctions:** RD-02 (WorkDispatched != WorkCompleted), RD-08 (RawPageEvidence != SemanticPageIdentity)
**Core Reality Requirement:** When returning to a previously-visited page, the system must recall which navigation targets were already dispatched. It must not regenerate all targets as if the page were new, nor treat the page as done merely because it was visited before.

**Core Scenario:**
- **Intent:** Continue enumeration after returning to a previously-visited page.
- **Given:** Page with 6 navigation targets. Earlier: 2 dispatched (Internet, SIMs). 4 undispatched.
- **Available Evidence:** Page recognition (same page as before), exploration history (2 dispatched, 4 undispatched), current observation (all 6 targets visible).
- **When:** System returns to this page. Must decide what work remains.
- **Then:** System must recognize page as previously visited. Must recall 2 dispatched, 4 undispatched. Must only dispatch the 4 undispatched. After exhausting those, must recognize page as fully explored.
- **Must Not:** Regenerate all 6 targets as if page were new. Re-dispatch already-exhausted targets. Report page as "done" because it was visited before.
- **Pass Oracle:** Only 4 undispatched targets dispatched. Page recognized as previously visited. No re-dispatch.
- **Fail Oracle:** All 6 targets regenerated (revisit-as-new → loop) OR page reported done with 4 undispatched targets (premature completion). (Legacy E-10-A revisit loop + E-07 premature completion.)
- **Completion Rule:** Page exhausted when all observable navigation targets dispatched and all sub-pages exhausted.

**Variants:**
- TRAVERSAL variant (from TRD-02): scope-vs-inventory distinction applied at page level.
- REPLAY variant (from E-10-A): DFS revisit loop from real run.

**Current System Status:** FROZEN_CAPABILITY_COVERED (BranchProgressEvidence idempotence, CAND-004/005/009, Capstone assertions 6/7/8)
**Evidence Maturity:** S0 → Next: S1_REPLAY (E-10-A + E-07 recorded as revisit-preservation evidence)

---

### CP-06 — Goal Satisfaction Must Be Recognizable Without Execution

**Canonical Pressure ID:** CP-06
**Title:** When the external world already satisfies a goal, the system must recognize satisfaction without executing unnecessary actions
**Domain:** Completion / Progress
**Primary Reality Distinction:** RD-10 (GoalExpression != GoalState)
**Supporting Reality Distinctions:** RD-07 (TaskIntent != ExecutionMethod)
**Core Reality Requirement:** A goal expressed as a desired world state must be evaluable from current observation. If the world already satisfies the goal, the system must recognize satisfaction without dispatching actions merely because they are associated with achieving the goal.

**Core Scenario:**
- **Intent:** Ensure Wi‑Fi is enabled on the device.
- **Given:** Wi‑Fi is already enabled (switch = on). Task: "Make sure Wi‑Fi is turned on."
- **Available Evidence:** Current observation shows Wi‑Fi switch = on. Goal expression: "Make sure Wi‑Fi is turned on."
- **When:** System evaluates current world state against goal before dispatching any actions.
- **Then:** System must recognize goal is already satisfied. Must NOT navigate to Wi‑Fi page or toggle switch. Must report goal satisfaction from current evidence.
- **Must Not:** Execute unnecessary actions when world already satisfies goal. Require re-execution of previously-achieved goal.
- **Pass Oracle:** Wi‑Fi observed = on. Goal evaluated as satisfied. No actions dispatched. Goal satisfaction reported.
- **Fail Oracle:** System navigates to Wi‑Fi page, toggles switch (turning it OFF), reports "goal achieved" because prescribed steps were executed. World now contradicts goal.
- **Completion Rule:** Goal satisfaction determined by evaluating observed world state against goal criteria. No execution required when criteria already met.

**Variants:**
- SPECIFICATION variant: current model CAN express this (GoalEvidence.Satisfied from any observation) but no normative spec or test requires zero-dispatch completion.

**Current System Status:** SPECIFICATION_GAP (model capable, spec silent, test absent)
**Evidence Maturity:** S0 → Next: SPEC_RECONCILIATION (add normative SHALL + executable test)

---

### Domain C: Constraints / Boundaries

---

### CP-07 — Declared Depth Bound Must Be Enforced During Discovery

**Canonical Pressure ID:** CP-07
**Title:** A declared depth constraint must prevent entry into deeper discoverable pages
**Domain:** Constraints / Boundaries
**Primary Reality Distinction:** RD-03 (ConstraintDeclared != ConstraintEnforced)
**Supporting Reality Distinctions:** TRD-01 (TypeLevelTraversalSpecification != ConcreteFutureRoute)
**Core Reality Requirement:** A depth constraint declared at plan construction must be enforced at every execution point where new navigation targets are discovered. "Depth=2 in the plan" is not equivalent to "execution never exceeds depth 2."

**Core Scenario:**
- **Intent:** Enumerate Settings entries with hard constraint maxDepth=2.
- **Given:** Settings app with 4 levels. At depth 2 (Internet page), "Wi‑Fi" is observable as menu_item. Tapping it would reach depth 3.
- **Available Evidence:** Declared constraint (maxDepth=2). Current depth (2). Observable target ("Wi‑Fi" at depth 3).
- **When:** System is at depth 2, observes a navigable element that would lead to depth 3.
- **Then:** System must NOT generate navigable child for Wi‑Fi. May record it as discovered but must not navigate. Effective constraint: depth ≥ maxDepth → no further descent.
- **Must Not:** Navigate to depth 3 when maxDepth=2. Treat "element is valid menu_item" as overriding depth constraint.
- **Pass Oracle:** Visited pages include depth 1 (Network & internet) and depth 2 (Internet). Do NOT include Wi‑Fi or any depth 3 page.
- **Fail Oracle:** Wi‑Fi entered at depth 3. Declared constraint ignored during sub-frame generation. (Legacy E-11 pre-fix + E-08 depth=4 from real run.)
- **Completion Rule:** Completion when all reachable content at depth ≤ maxDepth exhausted.

**Variants:**
- TRAVERSAL refinement (from TSP-01): type-level specification constrains what KINDS of work; depth bound is a type-level constraint, not a concrete-route constraint.
- REPLAY variant (from E-08 Step2): depth=4 reproduced from real run.

**Current System Status:** FROZEN_CAPABILITY_COVERED (CAND-008 BranchInventoryEvidence depth bound, BoundedCrossPageDiscoveryScenarioTests)
**Evidence Maturity:** S0 → Next: S1_REPLAY (E-11 + E-08 depth-runaway evidence)

---

### CP-08 — Observation Failure Must Not Become Content Exhaustion

**Canonical Pressure ID:** CP-08
**Title:** A failed observation mechanism must not be treated as proof that no more content exists
**Domain:** Constraints / Boundaries
**Primary Reality Distinction:** RD-04 (ObservationFailed != ContentExhausted)
**Supporting Reality Distinctions:** RD-01 (ActionExecution != ActionEffect)
**Core Reality Requirement:** When a device query for content state fails, the system must distinguish "query failed, state unknown" from "end of content confirmed." A failed query must not produce a positive exhaustion signal.

**Core Scenario:**
- **Intent:** Scroll through a list to determine whether more items exist.
- **Given:** Scrollable list displayed. Unknown whether more items exist below. System issues scroll, then queries device for scroll state.
- **Available Evidence:** Pre-scroll observation. Scroll dispatch record. Post-scroll device query response (may succeed or fail).
- **When:** Post-scroll device query fails (timeout, error, incomplete data). System has no reliable scroll-state information.
- **Then:** System must distinguish "query failed, state unknown" from "end of list confirmed." Must NOT report IsEnd=true. May retry or report unresolved. Unknown state must be diagnosable.
- **Must Not:** Report "end of list" when query failed. Treat missing/incomplete data as "nothing more to scroll."
- **Pass Oracle:** After failed query, system reports unresolved (not IsEnd). Either retries with valid data or reports error with reason. Does NOT claim end-of-list.
- **Fail Oracle:** After failed query, system reports IsEnd=true. Terminates scroll. Unvisited content remains. Completion based on false premise. (Legacy E-13-B: AdbScreenStateProvider exception swallowing.)
- **Completion Rule:** Content exhaustion requires valid query confirming end AND stability across K consecutive observations. Failed query resets stability counter.

**Variants:**
- VISUAL variant (from VE-10): production ROI end-of-scroll signal ignored by verifier — correct observation, wrong consumer.

**Current System Status:** COVERED_BUT_REPLAY_EVIDENCE_NEEDED (ViewportExplorationEvidence tri-state, but S0 synthetic Environment never fails)
**Evidence Maturity:** S0 (synthetic) → Next: S1_REPLAY (E-13-B ADB scroll failure — highest-value single upgrade)

---

### CP-09 — Unchanging Content Must Not Loop Forever

**Canonical Pressure ID:** CP-09
**Title:** When scrolling produces no new observable content, the system must terminate without exhausting its step budget
**Domain:** Constraints / Boundaries
**Primary Reality Distinction:** RD-04 (ObservationFailed != ContentExhausted)
**Supporting Reality Distinctions:** NONE
**Core Reality Requirement:** When repeated scroll actions produce identical content, the system must detect stability and terminate. It must not scroll indefinitely because the scroll mechanism reports "scrollable."

**Core Scenario:**
- **Intent:** Scroll through a list to discover all items.
- **Given:** Scrollable list where scroll mechanism reports "scrollable" but content never changes (short list, all items fit on one screen).
- **Available Evidence:** Pre-scroll items, post-scroll items (identical), scroll mechanism status ("scrollable, not at end").
- **When:** System scrolls repeatedly. Each post-scroll observation returns identical items. Scroll mechanism says "scrollable."
- **Then:** After K consecutive observations with no new items, system must conclude content is complete and terminate. Must NOT continue scrolling.
- **Must Not:** Scroll indefinitely because mechanism says "scrollable." Exhaust step budget on content already fully observed.
- **Pass Oracle:** System scrolls K times, observes identical content, terminates with completion. Total steps << MaxSteps.
- **Fail Oracle:** System scrolls until MaxSteps exhausted. Content was fully observed early but system couldn't detect stability. (Legacy E-12-A old behavior.)
- **Completion Rule:** Content exhaustion when K consecutive observations produce no new items, OR valid mechanism confirms end-of-list.

**Variants:**
- SIMULATION variant (from E-12-A): content stability K=3 → AllVisited (legacy fix).

**Current System Status:** FROZEN_CAPABILITY_COVERED (ViewportExplorationEvidence bound, bound-reached → Failed "semantic exhaustion 未获证明")
**Evidence Maturity:** S0 → Next: ATTACH_LEGACY_EVIDENCE (E-12-A for mechanism comparison)

---

### Domain D: Recovery / Error

---

### CP-10 — Recovery Attempt Must Not Imply Error Resolution

**Canonical Pressure ID:** CP-10
**Title:** Recovery actions must not reset error state; resolution must be confirmed by fresh observation
**Domain:** Recovery / Error
**Primary Reality Distinction:** RD-06 (RecoveryAction != ErrorStateReset)
**Supporting Reality Distinctions:** RD-01 (ActionExecution != ActionEffect)
**Core Reality Requirement:** Executing a recovery action is not equivalent to resolving the error condition. Recovery must include verification: act → observe → verify → reconcile. Verification failure must produce explicit failure, not silent retry.

**Core Scenario:**
- **Intent:** Continue enumeration despite encountering failures on a sub-page.
- **Given:** Multiple interaction attempts fail. System attempts recovery (backtrack, return to previous context).
- **Available Evidence:** Action failure records, recovery action records, post-recovery observation, error history.
- **When:** System experiences multiple failures, attempts recovery. Recovery action executes successfully.
- **Then:** Error resolution must be confirmed by fresh observation. Recovery attempt alone does not reset error state. If errors persist after recovery, system must escalate (not cycle).
- **Must Not:** Reset error count because recovery was attempted. Treat "I pressed back" as "problem is fixed." Retry structural failures as transient.
- **Pass Oracle:** After recovery + observation: either error resolved (page workable, next interaction succeeds) → error count reset, OR errors persist → count incremented → escalation after threshold.
- **Fail Oracle:** Every recovery resets error count. System cycles "fail → backtrack → retry → fail" indefinitely. (Legacy Bug #2: backtrack reset consecutive-error count.)
- **Completion Rule:** NOT_APPLICABLE.

**Variants:**
- DISTURBANCE variant (from E-04-A): 5-failure gate vs consecutive-error gate.
- ARCHITECTURE variant (from I-9): Recovery = act→observe→verify→reconcile, not a single PressBack.

**Current System Status:** FROZEN_CAPABILITY_COVERED (RecoveryResult.Verified|Failed, single attempt, RecoveryVerificationFailureTests)
**Evidence Maturity:** S0 → Next: NONE (verification model structurally prevents the legacy failure class)

---

### Domain E: Perception / Navigability

---

### CP-11 — Element Visibility Must Not Imply Navigability

**Canonical Pressure ID:** CP-11
**Title:** An element visible on screen must not be treated as a navigation target without evidence that it is navigable and its interaction produces the expected outcome
**Domain:** Perception / Navigability
**Primary Reality Distinction:** RD-05 (ElementPresence != ElementNavigability)
**Supporting Reality Distinctions:** VRD-02 (Element Classification Output != Interaction Capability), TRD-05 (ElementCategoryAuthorization != ConcreteCandidateExistence)
**Core Reality Requirement:** Element visibility in an observation is not equivalent to navigability. Element type, text content, classification confidence, and post-interaction outcome must all inform the navigability decision. Category-level authorization ("menu_items are OK") is necessary but not sufficient — per-instance verification is also required.

**Core Scenario:**
- **Intent:** Enumerate navigable entries on the Settings home page.
- **Given:** Settings home has mix of navigable menu items, a search input, and decorative subtitle text adjacent to menu items.
- **Available Evidence:** Element types, text, coordinates, spatial relationships, post-tap observations.
- **When:** System observes elements and must decide which are navigation targets.
- **Then:** System must use type, text, and spatial evidence — not mere presence — to decide navigability. Non-navigable elements (search inputs, subtitles, empty-text) must not generate navigation tasks. After tapping, if no page change: element is not navigable regardless of declared type.
- **Must Not:** Treat search input as navigation target. Treat decorative subtitle as independent navigation target. Generate navigation tasks from empty-text elements. Continue treating element as navigable after failed navigation.
- **Pass Oracle:** Only elements with navigable types, meaningful text, and spatial characteristics consistent with navigation generate tasks. Non-matching elements excluded. Failed-navigation elements excluded.
- **Fail Oracle:** Search input tapped → search UI → stuck. Subtitle double-clicked. Empty-text generates invalid target. (Legacy VE-05, VE-06, VE-07.)
- **Completion Rule:** NOT_APPLICABLE.

**Variants:**
- VISUAL variant (VSP-03 / VE-05): subtitle phantom menu_item — classification must be verified against interaction outcome. 91.9% of subtitle pairs affected.
- VISUAL variant (VE-06): search box misclassified as menu_item — YOLO label→type mapping error.
- VISUAL variant (VE-07): type-blind Contains matching — text elements matched as navigation targets.
- TRAVERSAL variant (TSP-03 / TRD-05): category authorization must not substitute for per-instance verification.
- GROUNDING variant (VE-03): empty/whitespace OCR — must not generate navigation tasks.

**Current System Status:** FROZEN_CAPABILITY_COVERED (CandidateAuthorizationEvidence, BoundedCandidateSafetyScenarioTests, Capstone dangerous-candidate zero-dispatch)
**Evidence Maturity:** S0 (synthetic, perfect classification) → Next: S2_PRODUCTION_PERCEPTION (real YOLO/OCR classification errors)

---

### CP-12 — Target Grounding Must Verify Semantic Identity Beyond Coordinate/Text Match

**Canonical Pressure ID:** CP-12
**Title:** Target grounding must verify that the element at matched coordinates or text is semantically the intended target — not merely that it matches a coordinate or substring
**Domain:** Perception / Grounding
**Primary Reality Distinction:** VRD-03 (Coordinate/Text Match != Semantic Target Identity)
**Supporting Reality Distinctions:** VRD-02 (Element Classification Output != Interaction Capability)
**Core Reality Requirement:** A coordinate proximity or text substring match between a target description and an observed element is not proof that the element is the semantically correct target. Element type, spatial context, and post-interaction outcome must verify the match. "Close enough" is not "correct."

**Core Scenario:**
- **Intent:** Tap the "Notifications" entry to navigate to the Notifications sub-page.
- **Given:** Settings page with menu_item "Notifications" at (0.32, 0.78) and text "Flash notifications" at (0.26, 0.73).
- **Available Evidence:** Element observations with text, type, coordinates. Target description "notifications."
- **When:** Matching algorithm finds two candidates whose text contains "notifications." Type-blind match could select text element.
- **Then:** System must verify selected element's type matches expected interaction. Text element must not be selected for navigation. If multiple candidates match, prefer candidate with matching interaction type. After tap, verify destination page matches expectation.
- **Must Not:** Select text element as navigation target via substring. Treat "Network_1" ⊆ "Network_10" as identity. Accept coordinate proximity alone as correct identification.
- **Pass Oracle:** Target resolves to menu_item "Notifications." Tap dispatched. Post-tap confirms Notifications page. Text "Flash notifications" never tapped.
- **Fail Oracle:** Target resolves to text "Flash notifications" via type-blind Contains. Tapped. Page doesn't change or wrong page appears. (Legacy run 20260806T072558649Z step 36.)
- **Completion Rule:** NOT_APPLICABLE.

**Variants:**
- GROUNDING variant (from VE-01): golden tolerance 0.08–0.1 accepts coordinate drift as "correct."
- GROUNDING variant (from VE-02): coordinate-only tap without post-action visual verification.

**Current System Status:** CHALLENGE_REQUIRED (newly identified — not yet assessed against current Runtime semantics)
**Evidence Maturity:** S0 (synthetic evidence of the failure pattern) → Next: S2_PRODUCTION_PERCEPTION + CHALLENGE_REQUIRED

---

### Domain F: Page / Container Identity

---

### CP-13 — Raw Page Evidence Must Not Be Conflated With Semantic Page Identity

**Canonical Pressure ID:** CP-13
**Title:** Two observations of the same logical page must be recognized as the same page despite minor differences in observed elements
**Domain:** Page / Container Identity
**Primary Reality Distinction:** RD-08 (RawPageEvidence != SemanticPageIdentity)
**Supporting Reality Distinctions:** VRD-01 (OCR Text Output != Semantic Element Identity), RD-09 (PreviouslyVisited != Unexplored)
**Core Reality Requirement:** Raw observation elements (text with OCR variants, coordinates with minor drift, scroll offset changes) are not equivalent to a conclusion about which logical page the system is on. Page identity is a semantic conclusion derived from observation. OCR variants for the same element must normalize to the same identity.

**Core Scenario:**
- **Intent:** Navigate Settings pages and recognize when a previously-visited page is encountered again.
- **Given:** System visited Internet page earlier. Now re-encounters it. Second observation has minor differences: scroll position changed, OCR produced slightly different text for some elements.
- **Available Evidence:** First observation element set. Second observation element set (similar but not identical). Element text normalization.
- **When:** System observes page with elements substantially similar to previously-visited page. Element sets are not identical but share structure.
- **Then:** System must recognize page as likely same logical page. Must NOT treat as brand-new. If page was exhausted: no new tasks. If page was visited but not exhausted: only remaining undispatched work.
- **Must Not:** Treat every slightly-different observation as new page. Re-generate all tasks on previously-exhausted page. Enter infinite revisit loop.
- **Pass Oracle:** Revisited page recognized as visited-and-exhausted. No new tasks generated. System navigates away or reports no new work.
- **Fail Oracle:** Revisited page not recognized. All tasks regenerated as if new. Re-enters same sub-page. Loop until step budget exhausted. (Legacy E-10-A DFS revisit loop.)
- **Completion Rule:** NOT_APPLICABLE.

**Variants:**
- VISUAL variant (VSP-02 / VE-04): OCR text variants must normalize to stable element identities — prerequisite for page recognition.
- VISUAL variant (VE-03): empty/whitespace OCR — must not generate phantom elements.
- SCROLL variant: within-page scroll changes element set → fingerprint changes → system must distinguish scroll from navigation.

**Current System Status:** FROZEN_CAPABILITY_COVERED (Container identity: SemanticPageName + IsStillMine, ViewportIdentityContinuityTests, RevisitA_IsIdempotent)
**Evidence Maturity:** S0 → Next: S1_REPLAY (E-10-A recorded revisit-loop evidence) + S2_PRODUCTION_PERCEPTION (real OCR variant evidence)

---

### Domain G: Intent / Plan

---

### CP-14 — Task Intent Must Not Be Conflated With Execution Method

**Canonical Pressure ID:** CP-14
**Title:** A task expressed as a desired outcome must not require a specific execution method when the outcome can be achieved through observation-driven discovery
**Domain:** Intent / Plan
**Primary Reality Distinction:** RD-07 (TaskIntent != ExecutionMethod)
**Supporting Reality Distinctions:** TRD-01 (TypeLevelTraversalSpecification != ConcreteFutureRoute), TRD-02 (TaskScope != ConcreteWorkInventory)
**Core Reality Requirement:** A task description of WHAT to achieve is not equivalent to a prescription of HOW to achieve it. Tasks may legitimately specify only interaction categories, constraints, and completion conditions — leaving concrete instance discovery to observation. Closed-world concrete plans and open-world type-level specifications are both legitimate task classes. They are not interchangeable.

**Core Scenario:**
- **Intent:** Enumerate all safe Settings entries within depth ≤ 4.
- **Given:** Task specifies scope (full), element categories (safe_mode: menu_container, switch_leaf, slider_leaf, leaf_action), depth bound (≤4), completion (Exhaustive). Does NOT enumerate concrete pages, coordinates, or route.
- **Available Evidence:** Type-level specification (categories, constraints, completion). Fresh observation (concrete elements, types, coordinates). Page identity after navigation.
- **When:** Observation reveals a concrete element matching the type-level specification that was not pre-enumerated in any concrete plan.
- **Then:** System must recognize element as legitimate in-scope work. Must generate navigation task. Must incorporate outcome into completion decision. Must NOT require concrete pre-enumeration.
- **Must Not:** Reject valid work because absent from concrete pre-execution inventory. Require pre-enumeration of all concrete work. Exceed scope/depth/safety constraints during discovery.
- **Pass Oracle:** All 14 pages discovered and exhausted within constraints. Completion depends on evidence-backed required work, not plan-step exhaustion.
- **Fail Oracle:** System demands concrete plan before execution. Without one, refuses to execute. OR: discovers legitimate work but ignores it. (Legacy E-07: both branches discovered, one ignored.)
- **Completion Rule:** Completion when all discoverable work within scope exhausted. Inventory complete with respect to scope.

**Variants:**
- TRAVERSAL refinement (TSP-01): type-level specification vs concrete future route.
- PLAN variant: closed-world concrete plan (Static coordinates) — separate legitimate task class. Not interchangeable with open-world type-level spec.

**Current System Status:** EXPLICITLY_DEFERRED_CAPABILITY (Intent→Goal/Plan synthesis deferred to Phase 5/6. Current system accepts pre-constructed Goal+Plan. Type-level traversal semantics exist in model but are not exposed at the task-input boundary.)
**Evidence Maturity:** S0 (traversal semantics evidenced) → Next: FUTURE_INTENT_SEMANTICS (Phase 5/6 Intent→Goal/Plan synthesis)

---

## Source Mapping

| Original | Canonical | Disposition |
|---|---|---|
| SP-01 | CP-01 | Direct |
| SP-02 | CP-02 | Direct |
| SP-03 | CP-04 | Direct |
| SP-04 | CP-07 | Direct |
| SP-05 | CP-08 | Direct |
| SP-06 | CP-09 | Direct |
| SP-07 | CP-11 | Direct (VSP-03 + TSP-03 attached as variants) |
| SP-08 | CP-10 | Direct |
| SP-09 | CP-14 | Direct (TSP-01 attached as refinement) |
| SP-10 | CP-13 | Direct (VSP-02 attached as variant) |
| SP-11 | CP-06 | Direct |
| SP-12 | CP-03 | Direct |
| SP-13 | CP-05 | Direct |
| VSP-01 | **CP-12** | **New canonical pressure** |
| VSP-02 | CP-13 variant | Merged — visual subcase of page identity |
| VSP-03 | CP-11 variant | Merged — visual subcase of element navigability |
| TSP-01 | CP-07 + CP-14 refinement | Merged — strengthens depth-bound + intent-method |
| TSP-02 | CP-04 refinement | Merged — strengthens multi-branch completion |
| TSP-03 | CP-11 variant | Merged — traversal restatement of navigability |

---

## Closed-World vs Open-World Traversal Finding

**CLOSED_WORLD_CONCRETE_PLAN_SUPPORTED:** YES
Evidence: TE-02 (Static plan with explicit coordinates). Legitimate when world is stable and route is known.

**OPEN_WORLD_TYPE_LEVEL_TRAVERSAL_SUPPORTED:** YES
Evidence: TE-01, TE-03, TE-08 (DynamicMatch, NL→type-level spec, real-run plan.json with DynamicRules). Legitimate when world is partially unknown or variable.

**Relationship:** Both are legitimate task classes. They are not interchangeable. Each fails in ways the other doesn't. Static plans fail when world differs. Type-level specs fail when classification is wrong or dispatch is incomplete.

**Intent Synthesis Status:** The legacy system's PlanCompiler is a deterministic type-level plan constructor (IntentSlots → TraversalPlan). It is traversal semantics, not intent understanding. Intent→Goal/Plan synthesis (NL → structured task specification) is explicitly deferred to Phase 5/6. The traversal representation capability exists; the intent understanding capability does not.

---

## Long-Term Target Alignment

Ranked by contribution toward: "An Agent accepts a high-level task intent and autonomously interacts with GUI reality to achieve it safely, recoverably, and with honest completion."

### FOUNDATION_ESTABLISHED (S0 frozen)

| Rank | CP | Why Foundation |
|---|---|---|
| 1 | CP-03 | Plan≠Reality + GoalEvidence is THE honest-completion invariant. All other pressures depend on this. |
| 2 | CP-04 | Multi-branch honest completion is the most direct test of "did we actually finish?" |
| 3 | CP-01 | Entry verification is the safety boundary — wrong-app traversal voids all subsequent work. |
| 4 | CP-10 | Recovery verification (act→observe→verify→reconcile) is the recoverability foundation. |

### NEXT_RECORDED_REALITY_PRESSURE (S1 replay)

| Rank | CP | Why Next |
|---|---|---|
| 5 | CP-08 | Observation failure ≠ exhaustion — P0 severity legacy gap. S1 replay of real ADB failure is the strongest single evidence upgrade. |
| 6 | CP-13 | Page recognition across observations — S1 replay of DFS revisit loop proves identity mechanism under recorded reality. |
| 7 | CP-07 | Depth-bound enforcement — S1 replay of depth=4 runaway proves constraint holds under real conditions. |

### NEXT_PRODUCTION_PERCEPTION_PRESSURE (S2)

| Rank | CP | Why S2 |
|---|---|---|
| 8 | CP-12 | Target grounding — newly identified, distinct from existing SPs. Directly affects action correctness. Requires real YOLO/OCR to exercise. |
| 9 | CP-11 | Element navigability — S2 perception will expose real classification error rates. |

### NEXT_PRODUCT_SEMANTIC_FRONTIER

| Rank | CP | Why Future |
|---|---|---|
| 10 | CP-14 | Intent≠ExecutionMethod — bridge from high-level intent to executable specification. Most important deferred capability. |

### FUTURE_LIVE_PRESSURE (S3)

Remaining pressures (CP-02, CP-05, CP-06, CP-09) gain their strongest evidence from S3 live-device scenarios. They are foundation-established at S0 with no urgent evidence-maturity gap.

---

## Recommended Next Sequence

Dependency-ordered. Does NOT authorize execution.

1. **CP-06 SPEC_RECONCILIATION** — smallest gap: add normative SHALL + one test for zero-dispatch goal satisfaction. No new concepts. Parallelizable with anything.

2. **CP-12 CHALLENGE_REQUIRED** — assess the one new canonical pressure (target grounding) against current Runtime semantics. Likely covered by CandidateAuthorizationEvidence + post-action observation but needs explicit verification.

3. **S1_REPLAY_PORTFOLIO** — attach 6 pieces of recorded legacy evidence to CP-04, CP-07, CP-08, CP-09, CP-13 (as identified in Step 6). Upgrades provenance from synthetic to recorded-reality. No new semantics.

4. **S2_PRODUCTION_PERCEPTION** — exercise CP-11 and CP-12 under real YOLO/OCR classification conditions. Requires Phase 4 perception pipeline.

5. **CP-14 FUTURE_INTENT_SEMANTICS** — when Phase 5/6 authority is granted, use the closed-world vs open-world traversal finding and the legacy PlanCompiler evidence to inform Intent→Goal/Plan synthesis design.

---

## Readiness

**UNIFIED_PRESSURE_PORTFOLIO_READY**

14 canonical pressures. 7 domains. All 19 raw formulations reconciled — 5 merged as variants/refinements. 1 new pressure (CP-12) identified and staged for challenge. Portfolio stable for S1/S2/S3 planning.

---

## Repository Changes

`docs/decisions/unified-legacy-scenario-pressure-portfolio.md` ONLY
