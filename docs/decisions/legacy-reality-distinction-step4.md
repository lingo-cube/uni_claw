# LEGACY_REALITY_DISTINCTION_RESULT — Step 4

> Generated: 2026-08-09
> Primary inputs: `docs/decisions/legacy-high-value-evidence-set-step2.md`, `docs/decisions/legacy-normalized-evidence-step3.md`
> Legacy truth source: `feature/refactor` (read-only Git objects)

---

## Atomicity Audit

**Primary Evidence Bundles:** 18
**Atomic Evidence Cases:** 48
**Bundles Split:** 8
**Bundles Already Atomic:** 10

### Split Bundles

| Bundle | Atomic Cases | Split Rationale |
|---|---|---|
| E-03 | E-03-A (full traversal), E-03-B (target search) | Different intent (exhaustive vs find-and-stop), different completion conditions, different forbidden-page rules |
| E-04 | E-04-A through E-04-G (7 FSM tests) | Different injected faults, different expected FSM transitions, different error strategies |
| E-05 | E-05-A (locate), E-05-B (enumerate), E-05-C (deep locate depth=3), E-05-D (deep enumerate depth=3) | Different scopes, different depth constraints, different completion criteria |
| E-06 | E-06-A (all screens), E-06-B (back to top), E-06-C (dedup), E-06-D (boundary), E-06-E (sparse jump), E-06-F (overlapping adaptive) | Different scroll content profiles, different disturbance types |
| E-09 | E-09-L1 through E-09-L8 | Already layered — each layer encodes a distinct historical bug class |
| E-10 | E-10-A (DFS revisit loop), E-10-B (search box input skip), E-10-C (search box misclassified) | Different failure modes reproduced from the same real run |
| E-12 | E-12-A (scroll-only dead-end), E-12-B (unconsumed FrameCompleted) | Different false-completion mechanisms |
| E-13 | E-13-A (EntryPolicy fake success), E-13-B (ADB scroll failure → end-of-list) | Different infrastructure layers (app entry vs scroll state), different failure signatures |

### Already Atomic

E-01, E-02, E-07, E-08, E-11, E-14, E-15, E-16, E-17, E-18

---

## Extraction Summary

**Accepted Reality Distinctions:** 11
**Provisional:** 3
**Simulation-Assumption-Only:** 6
**Implementation-Only:** 2
**Evidence Limitations:** 2
**Legacy Transformation Patterns:** 5

---

## Reality Distinction Catalog

---

### RD-01 — ActionExecution != ActionEffect

**Statement:** ActionExecution != ActionEffect

**Plain-language Meaning:** The fact that an action was dispatched (or claimed to be dispatched) is not equivalent to the fact that the action produced its intended effect in the external world. An action can be "executed" without changing anything observable.

**Supporting Atomic Evidence:**
- E-13-A (EntryPolicy fake success — "Cold launched..." returned without any device command; app foreground state unknown)
- E-09-L4 (StaleClick — "Stale item" clicked exactly once, page unchanged; circuit breaker needed after 3 same-page clicks)
- E-13-B (ADB scroll failure — scroll command failed or returned incomplete data, but system reported IsEnd=true)
- E-09-L8 (Subtitle double-click — element tapped but it was not a navigable target; same page observed after tap)

**Evidence Strength:** MULTI_SOURCE_CORROBORATED

Three independent sources across two infrastructure layers (app entry, action dispatch, scroll query) all demonstrate the same conflation pattern: the system treats "I did something" as equivalent to "the world changed."

**Observed Failure / Contradiction:**
- E-13-A: Engine traverses content on whatever screen is actually displayed — may be wrong app
- E-09-L4: Engine repeatedly clicks a non-functional element until circuit breaker fires
- E-13-B: Engine stops scrolling prematurely because a failed query looks identical to end-of-list
- E-09-L8: Engine double-clicks the same non-navigable element because no page change was detected

**Required Observable Consequence:** A correct system must be capable of demonstrating that it distinguishes "the action was dispatched" from "the action produced the expected world change." For entry: the app must be observably in the foreground before traversal begins. For navigation: a click must produce an observably different page. For scroll: a scroll command failure must not be reported as end-of-content.

**Counterfactual Check:** If a system intentionally treats ActionExecution == ActionEffect, then: entry never verifies app state → wrong app traversed; stale elements clicked indefinitely; ADB failures silently terminate scroll. All three failures are evidenced in the legacy corpus.

**Legacy Mechanisms Excluded:** EntryPolicyExecutor, ColdLaunch/DirectDeeplink fake returns, AdbScreenStateProvider exception swallowing, stale-click detection (must not prescribe circuit-breaker count of 3).

**Confidence:** HIGH

---

### RD-02 — WorkDispatched != WorkCompleted

**Statement:** WorkDispatched != WorkCompleted

**Plain-language Meaning:** The fact that work was dispatched on some reachable targets is not equivalent to the fact that all required work has been completed. A system can dispatch work on a subset of targets and still claim completion.

**Supporting Atomic Evidence:**
- E-07 (MultiBranchNavigation — hub with two navigation buttons; only first branch traversed, second branch 0/16 items visited; system reports AllVisited)
- E-07 non-scrollable variant (same bug with 3 static items per branch — proves the conflation is not scroll-related)

**Evidence Strength:** EXECUTABLE_REGRESSION

Deterministic failing test that reproduces every run. Three test variants (scrollable, deep nav, non-scrollable) all demonstrate the same conflation.

**Observed Failure / Contradiction:** Direct contradiction between claimed outcome (AllVisited — "everything was visited") and observed facts (listB = 0/16 items visited, second navigation button never tapped). The system dispatched work on the first branch, completed it, and treated that as equivalent to having completed all reachable work.

**Required Observable Consequence:** A correct system must be capable of demonstrating that it distinguishes "some work was dispatched and completed" from "all reachable work was dispatched and completed." The set of reachable targets must be accounted for independently from the set of dispatched targets. Completion must not be claimed until the two sets are proven equal.

**Counterfactual Check:** If a system intentionally treats WorkDispatched == WorkCompleted, then any multi-branch page will only traverse the first branch, and the system will report success. This is exactly the bug observed in E-07 — not a hypothetical.

**Legacy Mechanisms Excluded:** DFS visitation order, ChildrenStrategy, DynamicMatch, TraversalStack, Frame completion signaling. The solution must not be prescribed as "use a queue instead of a stack" or "use a graph frontier."

**Confidence:** HIGH

---

### RD-03 — ConstraintDeclared != ConstraintEnforced

**Statement:** ConstraintDeclared != ConstraintEnforced

**Plain-language Meaning:** A constraint declared at plan construction time (e.g., "max depth = 2") is not automatically enforced at every point in execution. Each execution-level expansion must independently respect the constraint.

**Supporting Atomic Evidence:**
- E-11 (SettingsEnumerateRegression — Depth:2 declared in IntentSlots, but DynamicMatch sub-frame generation did not check maxDepth; real runs reached depth=3+ pages)
- E-09-L2 (DepthConstraint_StopsAtLevel2 — fixture test verifying depth=3 pages are NOT visited when maxDepth=2)
- E-09-L3 (FsmInvariant_SubframeDepthNeverExceedsMaxDepth — invariant assertion that subframe depth ≤ maxDepth)
- E-09-L7 (DepthSemantics — at depth ≥ maxDepth+1, navigable containers degrade to non-interactive discovery-only)

**Evidence Strength:** EXECUTABLE_REGRESSION

Dedicated regression test with real API-35 Settings page structure. Two additional fixture tests (L2, L3) verify the constraint at different enforcement points. L7 provides the formula.

**Observed Failure / Contradiction:** The plan declared Depth:2 as a hard boundary. During execution, when the engine reached an Internet page at depth=2 and observed a "Wi‑Fi" menu item, it generated a navigable child at depth=3 — violating the declared constraint. The constraint existed in the plan data structure but was not propagated into the sub-frame generation logic.

**Required Observable Consequence:** A correct system must be capable of demonstrating that a constraint declared before execution is respected at every point during execution where new work is discovered. The effective constraint at any execution point must be min(declared constraint, remaining budget). Discovery must not expand beyond the declared boundary.

**Counterfactual Check:** If a system intentionally treats ConstraintDeclared == ConstraintEnforced, then depth-limited enumeration will enter arbitrarily deep pages as long as those pages are reachable through observable navigation targets. This is exactly what happened pre-fix.

**Legacy Mechanisms Excluded:** maxDepth field, DynamicMatch sub-frame generation, DepthSemantics formula (Depth ≥ MaxDepth+1 → degrade to leaf_info). The solution must not prescribe a specific formula or algorithm.

**Confidence:** HIGH

---

### RD-04 — ObservationFailed != ContentExhausted

**Statement:** ObservationFailed != ContentExhausted

**Plain-language Meaning:** The failure of an observation mechanism to return new content is not equivalent to proof that no more content exists. "I couldn't see anything new" is different from "there is nothing new to see."

**Supporting Atomic Evidence:**
- E-13-B (ADB scroll failure — any exception or missing XML attribute → IsEnd=true; ADB disconnect indistinguishable from genuine end-of-list)
- E-12-A (scroll-only dead-end — content never changes, but scroll mechanism never fails either; old behavior: infinite scroll until MaxSteps; new behavior: content stability K=3 terminates)
- E-06-E (sparse list jump recovery — gaps in content distribution must not be misread as end-of-list; half-step recovery needed)
- E-06-F (overlapping adaptive step — high overlap between scroll frames must not be misread as no-new-content)

**Evidence Strength:** MULTI_SOURCE_CORROBORATED

Three independent sources: real device failure mode (E-13-B), synthetic scroll stability (E-12-A), and content distribution edge cases (E-06-E, E-06-F). Spans real-device documentation and deterministic simulation.

**Observed Failure / Contradiction:**
- E-13-B: Engine terminates scroll early because ADB failure is silently folded into "reached end"
- E-12-A (old behavior): Engine scrolls infinitely because content never changes but scroll mechanism never reports failure either
- E-06-E: Without jump recovery, sparse content gaps would be misread as end-of-list
- E-06-F: Without adaptive step, high overlap would cause unnecessary re-scrolling of already-seen content

**Required Observable Consequence:** A correct system must be capable of demonstrating that it distinguishes three states: (1) observation returned new content, (2) observation returned no new content but the mechanism is still functional, (3) observation mechanism failed. Only state (2) repeated K times (with no intervening new content) can justify a claim of content exhaustion. State (3) must produce an explicit error, not silently become end-of-content.

**Counterfactual Check:** If a system intentionally treats ObservationFailed == ContentExhausted, then: any ADB failure silently terminates scroll; any page with identical repeated observations either loops forever or terminates arbitrarily. Both failures are evidenced.

**Legacy Mechanisms Excluded:** AdbScreenStateProvider exception swallowing, content stability K=3, seen-set differential termination, jump detection, adaptive step. The solution must not prescribe a specific stability threshold.

**Confidence:** HIGH

---

### RD-05 — ElementPresence != ElementNavigability

**Statement:** ElementPresence != ElementNavigability

**Plain-language Meaning:** The fact that an element appears in an observation is not equivalent to the fact that the element is a valid navigation target. An element can be visible on screen without being something that should be tapped to advance the task.

**Supporting Atomic Evidence:**
- E-09-L8 (SubtitleDegraded — text element "Bluetooth, pairing" at dy_full=0.0336 adjacent to "Connected devices" menu item; subtitle was tapped in historical run, causing same-page double-click)
- E-10-C (SearchBoxMenuItem — search input "Q Search settings" misclassified by YOLO as type=menu_item instead of type=input; engine treated it as navigable, entered search UI, could not escape)
- E-10-B (SearchBoxInput — same search box correctly typed as input; DynamicRule skipped it; engine succeeded)
- E-09-L5 (EmptyTextItem — OCR returns empty "" or whitespace "   " as element text; generating navigable children from empty text causes invalid navigation targets)

**Evidence Strength:** RECORDED_REALITY_DERIVED + EXECUTABLE_REGRESSION

E-10-B and E-10-C derive from real run `20260805T052309367Z` where YOLO misclassification caused a stuck state. E-09-L8 derives from real run `20260806T072558649Z` where a subtitle was double-clicked. E-09-L5 is a synthetic fixture encoding a real OCR edge case. All three are deterministic regression tests.

**Observed Failure / Contradiction:**
- E-09-L8: Subtitle text element adjacent to menu item → double-clicked → same page observed → stuck
- E-10-C: Search input misclassified as menu_item → tapped → entered search UI → self-loop → stuck
- E-09-L5: Empty-text element → child task generated with empty name → invalid navigation

**Required Observable Consequence:** A correct system must be capable of demonstrating that it distinguishes "an element is present in the observation" from "an element is a valid navigation target for the current task." The distinction must account for element type, element text content, and spatial relationship to known navigation targets. Presence alone is insufficient to justify action.

**Counterfactual Check:** If a system intentionally treats ElementPresence == ElementNavigability, then: every text element adjacent to a menu item becomes a clickable target; every misclassified element type leads to navigation into dead ends; every empty OCR result generates an invalid task. All three failures are evidenced.

**Legacy Mechanisms Excluded:** DynamicMatch MatchCondition type filtering, NormalizeItemText, subtitle degradation detection, YOLO label classification. The solution must not prescribe specific element type taxonomies.

**Confidence:** HIGH

---

### RD-06 — RecoveryAction != ErrorStateReset

**Statement:** RecoveryAction != ErrorStateReset

**Plain-language Meaning:** Executing a recovery action (backtrack, retry, dismiss popup) is not equivalent to resetting the error state. Recovery can be attempted while error history continues to accumulate.

**Supporting Atomic Evidence:**
- E-04-B (ConsecutiveErrors across backtracks — after 2 backtracks, ConsecutiveErrors = 2; backtrack did NOT reset the counter; the bug was that it previously DID reset, hiding error accumulation)
- E-04-A (5-failure gate — after 5 distinct failed items on a sub-page, PressBack fires; consecutive-error gate at ≥3 must NOT fire before item-gate at ≥5; verified-success resets ConsecutiveErrors but unsuccessful backtrack does not)
- E-04-G (AI empty response — classified as non-transient, not retried; structural failure ≠ transient error)

**Evidence Strength:** EXECUTABLE_REGRESSION

Dedicated FSM regression suite. Bug #2 (E-04-B) specifically encodes the historical bug where backtrack incorrectly reset the consecutive-error count.

**Observed Failure / Contradiction:**
- Pre-fix E-04-B: Each backtrack reset the error count → error accumulation hidden → system never escalated to stronger recovery
- Post-fix: Consecutive errors accumulate across backtracks; gate fires at ≥3; gate produces Pop-only when no physical navigation has occurred (because the frame never dispatched an operation → physical page is still the parent)
- E-04-A: Interleaved deny/success — verified success resets ConsecutiveErrors, but item-gate (≥5) fires before consecutive-gate (≥3) in this pattern

**Required Observable Consequence:** A correct system must be capable of demonstrating that it distinguishes "a recovery action was attempted" from "the error condition was resolved." Error history must persist across recovery attempts. Recovery escalation must be based on accumulated evidence, not reset on each attempt. Different recovery actions (backtrack, pop, press-back) must be selected based on whether physical navigation has occurred.

**Counterfactual Check:** If a system intentionally treats RecoveryAction == ErrorStateReset, then every backtrack hides error accumulation, error escalation never triggers, and the system retries the same failing pattern indefinitely. This is what happened pre-fix.

**Legacy Mechanisms Excluded:** ConsecutiveErrors counter, ErrorStrategy.Backtrack, ErrorStrategy.PressBack, Pop-only vs PressBack decision, item-gate vs consecutive-gate. The solution must not prescribe specific counter thresholds or strategy enums.

**Confidence:** HIGH

---

### RD-07 — TaskIntent != ExecutionMethod

**Statement:** TaskIntent != ExecutionMethod

**Plain-language Meaning:** A description of what the user wants to achieve is not equivalent to a description of how to achieve it. Intent can be expressed without prescribing execution steps. Execution can be specified without understanding intent.

**Supporting Atomic Evidence:**
- E-15 + E-14 + E-05-A (Intent mode: NL description → AI IntentExtractor → IntentSlots → PlanCompiler → TraversalPlan with DynamicMatch rules; the plan describes WHAT to look for, not WHERE to tap)
- E-16 + E-01 locate (Plan mode: hand-authored plan JSON with explicit Static nodes and coordinates → ScenarioPlanLoader → TraversalPlan; the plan prescribes exact actions without understanding intent)
- E-05-A vs E-05-C (same locate intent "find About phone" vs "find Internal Storage" — different targets, same intent shape, different execution paths)
- E-05-B vs E-05-D (same enumerate intent, different depth constraints — intent shape preserved, execution boundary differs)

**Evidence Strength:** MULTI_SOURCE_CORROBORATED

Two completely different plan construction paths (Intent mode and Plan mode) both produce valid execution but through entirely different mechanisms. Intent mode = what-to-do (DynamicMatch rules). Plan mode = how-to-do-it (Static coordinates).

**Observed Failure / Contradiction:**
- Plan mode failure mode: JSON coordinates don't match real screen → tap misses → locate fails. Plan mode cannot adapt to observation because it prescribes actions, not goals.
- Intent mode failure mode: AI misclassifies element handling → wrong template set → wrong elements treated as navigable. Intent mode cannot follow a precise script because it prescribes goals, not actions.
- The two modes are not interchangeable — each fails in ways the other doesn't.

**Required Observable Consequence:** A correct system must be capable of accepting task descriptions at different levels of specificity without conflating them. A task expressed as "find X" must not be treated as "tap at coordinate (0.5, 0.3)." A task expressed as "follow these exact steps" must not be reinterpreted by AI. The boundary between WHAT and HOW must be explicit and honored at execution time.

**Counterfactual Check:** If a system intentionally treats TaskIntent == ExecutionMethod, then either: hand-authored precise plans are reinterpreted by AI (losing precision), or NL intents are executed as literal scripts (losing adaptability). The legacy system avoids this by having two separate code paths, but the separation is in the Host orchestration, not in the Runtime engine.

**Legacy Mechanisms Excluded:** IntentExtractor, PlanCompiler, ScenarioPlanLoader, DynamicMatch vs Static, ExtractedIntentSlots. The solution must not prescribe two separate code paths.

**Confidence:** MEDIUM

The distinction is strongly evidenced but the required system behavior is harder to specify without implying two modes. The distinction may refine into a narrower claim with further analysis.

---

### RD-08 — PageIdentityObserved != PageIdentityConcluded

**Statement:** PageIdentityObserved != PageIdentityConcluded

**Plain-language Meaning:** Raw elements observed on screen (text, coordinates, types) are not equivalent to a conclusion about which logical page the system is on. Page identity is a semantic conclusion derived from observation, not a direct observable.

**Supporting Atomic Evidence:**
- E-01 locate scenario (post-hoc TraceTool VerifyEngine required to confirm target page identity; Host alone cannot confirm it — status = "pending_verification")
- E-10-A (DFS revisit loop — engine re-enters the Internet page but treats it as new work because the observation is not recognized as "same page as before")
- E-03 simulation assumption (page identity is supplied directly by fixture — no page-recognition step exists in simulation)
- E-06 simulation assumption (scroll progress 0.0–1.0 is supplied directly — no inference from observation)

**Evidence Strength:** MULTI_SOURCE_CORROBORATED

E-01 shows the real system defers page-identity verification to an offline tool. E-10-A shows the engine fails to recognize it's on a previously-visited page. E-03 and E-06 show the simulation bypasses page-recognition entirely.

**Observed Failure / Contradiction:**
- E-01: Host reports "pending_verification" — it cannot confirm the target page was reached without offline analysis
- E-10-A: Engine re-enters same page because observation is not recognized as matching a previously-visited page → revisit loop
- E-03, E-06: Simulation never tests page-recognition because identity is pre-supplied

**Required Observable Consequence:** A correct system must be capable of demonstrating that it derives page identity from observation rather than assuming it. Two observations that represent the same logical page (despite minor differences in element positions, OCR text, or scroll offset) must be recognized as the same page. Two observations that represent different logical pages must be distinguished even if they share elements.

**Counterfactual Check:** If a system intentionally treats PageIdentityObserved == PageIdentityConcluded, then: every observation is treated as a unique page (→ revisit loops), or page identity is assumed from a plan (→ can't detect navigation errors). Both failures are evidenced.

**Legacy Mechanisms Excluded:** TraceTool VerifyEngine, fixture-supplied page identity, page fingerprinting. The solution must not prescribe a specific page-recognition algorithm.

**Confidence:** MEDIUM

The evidence for this distinction is primarily from what the legacy system CANNOT do (verify page identity online, detect revisit) and what the simulation ASSUMES (page identity from fixture). Direct failure evidence is limited to E-10-A. Further confirmation may come from additional real-run traces.

---

### RD-09 — PreviouslyVisited != Unexplored

**Statement:** PreviouslyVisited != Unexplored

**Plain-language Meaning:** Having visited a page before is not equivalent to that page being fully explored. A page can be visited (entered, observed) without all of its reachable content having been discovered.

**Supporting Atomic Evidence:**
- E-07 (MultiBranchNavigation — hub page is visited, both buttons are observed, but only one branch is explored; the hub is "visited" but not "exhausted")
- E-10-A (DFS revisit loop — Internet page is visited, then revisited via self-loop transition; each revisit treats the page as new work)
- E-12-B (unconsumed FrameCompleted — child frame has no work to do, but its completion signal is unconsumed; the parent page is "visited" but the child context is "stuck")

**Evidence Strength:** EXECUTABLE_REGRESSION + RECORDED_REALITY_DERIVED

E-07 is a deterministic failing test. E-10-A derives from a real run. E-12-B is a deterministic regression.

**Observed Failure / Contradiction:**
- E-07: Hub page visited, first branch explored. System treats "hub was visited" as "hub is done" — second branch never dispatched. But visiting a page is not the same as exhausting all its reachable branches.
- E-10-A: Internet page visited, then self-loop → visited again. System treats each visit as a new exploration opportunity rather than recognizing it as previously-visited-and-exhausted.

**Required Observable Consequence:** A correct system must be capable of demonstrating that it distinguishes three states for each reachable location: (1) not yet visited, (2) visited but not yet exhausted (more content may be reachable), (3) visited and exhausted (no more reachable content). Completion requires all reachable locations to be in state (3).

**Counterfactual Check:** If a system intentionally treats PreviouslyVisited == Unexplored, then every revisit becomes an infinite loop (E-10-A). If a system treats PreviouslyVisited == FullyExplored, then multi-branch pages lose branches (E-07). Both failures are evidenced.

**Legacy Mechanisms Excluded:** VisitedPages set, AllVisited flag, FrameCompleted, ChildrenStrategy, DFS order. The solution must not prescribe a specific data structure for tracking visit/exploration state.

**Confidence:** HIGH

---

### RD-10 — GoalExpression != GoalState

**Statement:** GoalExpression != GoalState

**Plain-language Meaning:** A natural-language string expressing a goal is not equivalent to a tracked, evaluable state representing progress toward that goal. "Find the dark mode setting" is a string; knowing whether dark mode has been found is a state.

**Supporting Atomic Evidence:**
- E-17 (ITraversalAdvisor — `DecideNextActionAsync(string goal, ...)` takes a goal string per call; no goal persistence; each call is independent; the model must re-derive context from the goal string and current observation)
- E-03-B (target search — CompletionPolicy TargetFound with TargetName "Dark mode"; the target is a structured field, not a free-text goal; the engine can evaluate "is this page the target?" without AI)
- E-17 (GAP-P1-01 — DecideNextActionAsync has only test callers, no production engine caller; the goal-directed path exists as code but is not wired into the main execution loop)

**Evidence Strength:** MULTI_SOURCE_CORROBORATED

E-17 shows the goal is a per-call string with no persistence mechanism. E-03-B shows an alternative where the target is a structured field evaluable without AI. The gap documentation (GAP-P1-01) confirms the goal-directed path is not wired into production.

**Observed Failure / Contradiction:**
- E-17: Each call to DecideNextActionAsync is independent. If the goal is "enumerate all Settings entries," the model must determine whether enumeration is complete from the goal string + current observation alone — there is no persistent "how many entries remain" counter. The model may repeat work, skip entries, or stop early because it has no state tracking progress.
- E-03-B: TargetFound completion works because the target is a structured field ("Dark mode") that can be evaluated deterministically — no AI needed to answer "have we found it yet?"

**Required Observable Consequence:** A correct system must be capable of demonstrating that it tracks progress toward a goal independently of the goal's expression. The question "is the goal satisfied?" must be answerable from tracked state, not by re-asking an AI model with the goal string and current observation.

**Counterfactual Check:** If a system intentionally treats GoalExpression == GoalState, then progress tracking is lost between steps, the AI model must re-derive context each time, and completion cannot be verified without re-prompting. This is the state of the legacy E-17 path — it exists but is not trusted for production use.

**Legacy Mechanisms Excluded:** ITraversalAdvisor, DecideNextActionAsync, goal string parameter, PromptTemplateRegistry, stateless per-call design. The solution must not prescribe goal-as-persistent-object; it only requires that progress be tracked.

**Confidence:** MEDIUM

Supported by the structural design of E-17 and the contrast with E-03-B. However, no direct failure evidence exists because the goal-directed path was never wired into production — it cannot fail if it was never used. The distinction is structural rather than failure-derived.

---

### RD-11 — PlanConstructed != ExecutionGuaranteed

**Statement:** PlanConstructed != ExecutionGuaranteed

**Plain-language Meaning:** The successful construction of an execution plan is not equivalent to a guarantee that the plan can be executed correctly. A plan can be internally valid but fail at execution time because the world does not match the plan's assumptions.

**Supporting Atomic Evidence:**
- E-14 (PlanCompiler — fail-fast validation ensures the plan is internally consistent: valid scope, non-empty target for target_only, valid element handling key, non-negative depth; but no validation that the plan matches the actual device state)
- E-16 + E-01 locate (Plan mode — hand-authored JSON with explicit coordinates; plan loads successfully but coordinates may not match real screen → tap misses → locate fails)
- E-15 (IntentExtractor — AI extracts valid IntentSlots, but AI can return wrong scope, wrong element handling; post-extraction validation only checks vocabulary, not correctness)

**Evidence Strength:** MULTI_SOURCE_CORROBORATED

E-14 validates internal consistency but not external correspondence. E-16 demonstrates the failure mode: plan JSON is valid, coordinates are valid, but they don't match the real screen. E-15 shows AI extraction is vocabulary-validated but not correctness-validated.

**Observed Failure / Contradiction:**
- E-16: Plan JSON loads successfully → PlanCompiler validates successfully → execution reaches coordinate → tap hits empty space → page doesn't change → locate fails. The plan was "correct" internally but wrong externally.
- E-15: AI extracts scope="full" for a locate task → PlanCompiler produces Exhaustive plan → engine enumerates everything instead of stopping at target. Vocabulary is correct, semantics are wrong.

**Required Observable Consequence:** A correct system must be capable of detecting when execution diverges from the plan's assumptions and must not treat "plan validated" as "execution will succeed." The plan is a hypothesis about the world; execution must verify the hypothesis against observation.

**Counterfactual Check:** If a system intentionally treats PlanConstructed == ExecutionGuaranteed, then: stale coordinates cause silent failures, wrong AI inferences cause wrong execution modes, and the system reports success based on plan assumptions rather than observed outcomes.

**Legacy Mechanisms Excluded:** PlanCompiler.Compile(), ScenarioPlanLoader.Load(), IntentExtractor vocabulary validation, fail-fast validation rules. The solution must not prescribe a specific plan/execution separation.

**Confidence:** MEDIUM

The distinction is supported by structural evidence and documented failure modes (E-16 coordinate mismatch). However, there is no dedicated test that exercises plan-vs-reality divergence — the failure modes are implied by the design rather than demonstrated by a specific regression test.

---

## Contradiction-Derived Distinctions

### From E-07 (MultiBranchNavigation — unfixed false-completion bug)

**Primary distinction:** RD-02 (WorkDispatched != WorkCompleted)

E-07 is the canonical evidence for RD-02. The system dispatched work on the first branch, completed it, and treated that as equivalent to having completed all reachable work. The hub page has two observable navigation targets; only one was dispatched; the system claimed AllVisited.

**Secondary distinctions contributed:**
- RD-09 (PreviouslyVisited != Unexplored): visiting the hub page is not equivalent to exhausting all its branches
- RD-05 (ElementPresence != ElementNavigability): both buttons were present in the observation but only one was treated as a navigation target to dispatch

### From E-13 (GAP-P0-02 — documented behavioral gaps)

**E-13-A (EntryPolicy fake success) → RD-01 (ActionExecution != ActionEffect)**

The entry operation reports "Cold launched..." without executing any device command. The system treats "the entry function returned a success string" as equivalent to "the app is in the foreground and ready."

**E-13-B (ADB scroll failure → end-of-list) → RD-04 (ObservationFailed != ContentExhausted)**

The ADB query failure or missing XML attributes are silently folded into IsEnd=true. The system treats "the query returned no useful data" as equivalent to "there is no more content to scroll."

---

## Intent / Goal / Plan Distinctions

### Distinctions Derived

- **RD-07 (TaskIntent != ExecutionMethod):** Derived from the two-mode plan construction (Intent mode vs Plan mode). Both modes produce valid execution but through fundamentally different mechanisms. They are not interchangeable.

- **RD-10 (GoalExpression != GoalState):** Derived from the stateless goal-as-string design of ITraversalAdvisor contrasted with the structured TargetFound completion policy. A goal string is not a tracked state.

- **RD-11 (PlanConstructed != ExecutionGuaranteed):** Derived from the gap between plan validation (internal consistency) and plan execution (external correspondence). A valid plan is not a guarantee of successful execution.

### Factual Transformation Boundaries That Did NOT Qualify as Semantic Distinctions

These are the 5 transformation boundaries cataloged in Step 3. They are factual observations about how the legacy system worked, but they do not meet the derivation test for a Reality Distinction:

| Boundary | Why NOT a Reality Distinction |
|---|---|
| NL → ExtractedIntentSlots (E-15) | This is a LEGACY_TRANSFORMATION_PATTERN. The existence of an AI-driven NL→IntentSlots step does not prove that NL and structured intent must be separated in all implementations. A different system might accept structured intent directly without NL. |
| IntentSlots → TraversalPlan (E-14) | LEGACY_TRANSFORMATION_PATTERN. The 5-step deterministic compiler is one way to produce a plan from intent. The distinction between "intent slots" and "traversal plan" is a legacy design choice, not a universal semantic boundary. |
| Plan JSON → TraversalPlan (E-16) | LEGACY_TRANSFORMATION_PATTERN. The existence of a hand-authored plan mode is a legacy design choice, not a semantic necessity. That the system had two modes is an architectural fact, not a Reality Distinction. |
| Goal string + context → next action (E-17) | LEGACY_TRANSFORMATION_PATTERN. This is a specific AI-driven mechanism. The fact that it's stateless and per-call is a design observation that feeds RD-10, but the transformation itself is not a distinction. |
| Python NL task → IntentSlots (E-18) | PROVENANCE_GAP. The Python code exists but has no C# equivalent. This is evidence of an unimplemented path, not evidence of a semantic distinction. It may become relevant if the gap is closed, but it does not currently prove X != Y. |

---

## Replay-Derived Distinctions

### Provenance Strength Preserved

| Evidence | Provenance | What It Proves |
|---|---|---|
| E-08 (TraceReplayFromRunTests) | REPLAY of RECORDED_RUN | Real run failures can be deterministically reproduced from recorded artifacts without emulator. Does NOT prove the replay matches live-world behavior beyond what was recorded. |
| E-09-L1 (ReplayRegression) | REPLAY of RECORDED_RUN | Post-fix engine replay of pre-fix trace shows divergence — the fix changes behavior. Recording provenance is essential: replay is of historical state, not current state. |
| E-10 (20260805T052309367Z) | RECORDED_SOURCE_DERIVED_RECONSTRUCTION | Fixtures were hand-reconstructed from recorded analysis.jsonl and run.log. Reconstruction fidelity depends on the accuracy of the reconstruction. Replay proves the failure pattern is reproducible, not that every detail matches the original run. |

### Distinctions Supported by Replay Evidence

- RD-02 (WorkDispatched != WorkCompleted): E-08 depth runaway replay shows dispatched work exceeded declared constraints
- RD-03 (ConstraintDeclared != ConstraintEnforced): E-08 Step2 diagnoses depth=4 violation; E-09-L2/L3 verify fix
- RD-05 (ElementPresence != ElementNavigability): E-10-B and E-10-C replay search-box misclassification from real run
- RD-08 (PageIdentityObserved != PageIdentityConcluded): E-10-A DFS revisit loop from real run

Replay evidence does NOT independently prove any distinction not also supported by simulation or integration evidence. Replay's unique contribution is provenance strength — it shows these distinctions manifest in real runs, not just synthetic fixtures.

---

## Simulation-Only Distinctions

### RD-SIM-01: DeclaredElementType != ObservedElementClassification

**Status:** SIMULATION_ASSUMPTION_ONLY (transitioning to PROVISIONAL)

The simulation supplies element types (button, switch, menu_item, readonly, input) directly from the fixture. In a real system, element type must be inferred from observation (YOLO classification, OCR, spatial analysis). The simulation assumption is that type classification is perfect.

**Why this is NOT (yet) a Reality Distinction:** While E-10-C (search box misclassified by YOLO as menu_item) proves that observation-level misclassification causes failures, this specific failure is already covered by RD-05 (ElementPresence != ElementNavigability). The narrower distinction "DeclaredElementType != ObservedElementClassification" restates the simulation assumption rather than identifying a new semantic boundary.

**Required for promotion:** Evidence of a failure caused specifically by the conflation of declared type with observed type (e.g., an element whose type differs between plan and observation, causing wrong navigation decision). E-10-C is close but the failure is more precisely described by RD-05.

### RD-SIM-02: FixturePageIdentity != ObservedPageIdentity

**Status:** SIMULATION_ASSUMPTION_ONLY

Covered by RD-08 (PageIdentityObserved != PageIdentityConcluded).

### RD-SIM-03 through RD-SIM-06

| Assumption | Status | Covered By |
|---|---|---|
| Transition graph is declared in fixture (click→navigate is pre-programmed) | SIMULATION_ASSUMPTION_ONLY | RD-01 (ActionExecution != ActionEffect) — the click effect is assumed, not observed |
| Scroll progress (0.0–1.0) and IsEndOfList are known exactly | SIMULATION_ASSUMPTION_ONLY | RD-04 (ObservationFailed != ContentExhausted) |
| Items have known unique names — no OCR dedup ambiguity | SIMULATION_ASSUMPTION_ONLY | RD-08 (PageIdentityObserved != PageIdentityConcluded) — item identity is a conclusion from observation |
| AI intent extraction returns perfect canned JSON | SIMULATION_ASSUMPTION_ONLY | RD-11 (PlanConstructed != ExecutionGuaranteed) — plan correctness is assumed, not verified |

---

## Non-Distinction Findings

### Simulation Assumptions (preserved for S1/S2 pressure)

These are the 10 simulation assumptions from Step 3. The 6 that are NOT covered by an accepted RD are preserved here:

| ID | Assumption | Why Preserved |
|---|---|---|
| SA-01 | Element type classification provided by fixture | Future evidence may show type-classification failures cause distinct problems beyond RD-05 |
| SA-02 | Page identity known from fixture | May refine RD-08 with additional evidence |
| SA-03 | Transition graph declared in fixture | Future multi-path evidence may show transition-graph conflation distinct from RD-01 |
| SA-04 | Scroll progress known exactly | Already partially covered by RD-04 |
| SA-05 | Element visibility mathematically modeled | Future real-scroll evidence may reveal visibility-model conflation |
| SA-06 | Error strategies, page analysis, precondition results injected by harness | May reveal distinct error-classification issues beyond RD-06 |

### Implementation-Only Evidence

| ID | Finding | Why Not a Distinction |
|---|---|---|
| IMP-01 | FSM harness fault injection mechanism (E-04 harness) | The harness is a test tool, not a semantic claim. The behaviors it tests (RD-06) are real; the harness itself is implementation. |
| IMP-02 | TraceReplayHarness record-then-replay pattern (E-08 harness) | The replay mechanism is a diagnostic tool. The distinctions it reveals (RD-02, RD-03, RD-05) are real; the mechanism is not. |

### Evidence Limitations

| ID | Limitation | Impact |
|---|---|---|
| LIM-01 | E-13 (GAP-P0-02) is DOCUMENT_ONLY — no executable test isolates the fake-success or scroll-failure-conflation behaviors | RD-01 and RD-04 are strongly supported by other evidence; E-13 provides real-world context but not executable proof |
| LIM-02 | E-17 (ITraversalAdvisor) has only test callers — the goal-directed path was never exercised in production | RD-10 is supported by structural analysis but has no production failure evidence |

### Legacy Transformation Patterns (preserved for reference)

| Pattern | Description |
|---|---|
| LTP-01 | NL → ExtractedIntentSlots (AI-driven, E-15) |
| LTP-02 | IntentSlots → TraversalPlan (deterministic compiler, E-14) |
| LTP-03 | Plan JSON → TraversalPlan (hand-authored bypass, E-16) |
| LTP-04 | Goal string + context → next action (stateless AI, E-17) |
| LTP-05 | Python NL task → IntentSlots (unimplemented in C#, E-18) |

These are factual observations about the legacy system's architecture. They may inform design but do not qualify as Reality Distinctions because they do not identify a conflation that causes failure.

---

## Cross-Evidence Clusters

Each accepted RD is independently supported by multiple atomic evidence cases:

### RD-01 (ActionExecution != ActionEffect)

| Evidence | Strength |
|---|---|
| E-13-A | DOCUMENT_ONLY (EntryPolicy fake success) |
| E-09-L4 | EXECUTABLE_REGRESSION (stale click circuit breaker) |
| E-13-B | DOCUMENT_ONLY (ADB scroll failure → end-of-list) |
| E-09-L8 | EXECUTABLE_REGRESSION (subtitle double-click, same page) |
| E-12-A | EXECUTABLE_REGRESSION (scroll without content change) |

### RD-02 (WorkDispatched != WorkCompleted)

| Evidence | Strength |
|---|---|
| E-07 (scrollable) | EXECUTABLE_REGRESSION (failing test) |
| E-07 (non-scrollable) | EXECUTABLE_REGRESSION (failing test, proves bug independence from scroll) |
| E-07 (deep nav) | EXECUTABLE_REGRESSION (failing test) |

### RD-03 (ConstraintDeclared != ConstraintEnforced)

| Evidence | Strength |
|---|---|
| E-11 | EXECUTABLE_REGRESSION (permanent depth regression) |
| E-09-L2 | EXECUTABLE_REGRESSION (depth constraint fixture test) |
| E-09-L3 | EXECUTABLE_REGRESSION (FSM invariant assertion) |
| E-09-L7 | EXECUTABLE_REGRESSION (depth semantics formula verification) |
| E-08 Step2 | REPLAY of RECORDED_RUN (depth=4 reproduced) |

### RD-04 (ObservationFailed != ContentExhausted)

| Evidence | Strength |
|---|---|
| E-13-B | DOCUMENT_ONLY (ADB failure → IsEnd) |
| E-12-A | EXECUTABLE_REGRESSION (scroll stability K=3) |
| E-06-E | EXECUTABLE_REGRESSION (sparse jump recovery) |
| E-06-F | EXECUTABLE_REGRESSION (overlapping adaptive step) |

### RD-05 (ElementPresence != ElementNavigability)

| Evidence | Strength |
|---|---|
| E-09-L8 | EXECUTABLE_REGRESSION (subtitle degraded) |
| E-10-C | RECORDED_REALITY_DERIVED (search box misclassified) |
| E-10-B | RECORDED_REALITY_DERIVED (search box correctly typed — contrast) |
| E-09-L5 | EXECUTABLE_REGRESSION (empty text skip) |

### RD-06 (RecoveryAction != ErrorStateReset)

| Evidence | Strength |
|---|---|
| E-04-B | EXECUTABLE_REGRESSION (consecutive errors across backtracks) |
| E-04-A | EXECUTABLE_REGRESSION (5-failure gate vs consecutive gate) |
| E-04-G | EXECUTABLE_REGRESSION (AI empty response non-transient) |

### RD-07 (TaskIntent != ExecutionMethod)

| Evidence | Strength |
|---|---|
| E-15 + E-14 + E-05-A | DETERMINISTIC_SIMULATION (intent mode chain) |
| E-16 + E-01 | INTEGRATION (plan mode chain) |
| E-05-A vs E-05-C | DETERMINISTIC_SIMULATION (same intent shape, different targets) |

### RD-08 (PageIdentityObserved != PageIdentityConcluded)

| Evidence | Strength |
|---|---|
| E-01 | INTEGRATION (deferred page verification) |
| E-10-A | RECORDED_REALITY_DERIVED (DFS revisit loop) |
| E-03 | DETERMINISTIC_SIMULATION (fixture-supplied identity) |

### RD-09 (PreviouslyVisited != Unexplored)

| Evidence | Strength |
|---|---|
| E-07 | EXECUTABLE_REGRESSION (visited but branch unexplored) |
| E-10-A | RECORDED_REALITY_DERIVED (visited but re-entered via self-loop) |
| E-12-B | EXECUTABLE_REGRESSION (visited but child frame stuck) |

### RD-10 (GoalExpression != GoalState)

| Evidence | Strength |
|---|---|
| E-17 | PRODUCTION_IMPLEMENTATION (stateless goal-as-string) |
| E-03-B | DETERMINISTIC_SIMULATION (structured TargetFound vs free-text goal) |

### RD-11 (PlanConstructed != ExecutionGuaranteed)

| Evidence | Strength |
|---|---|
| E-14 | PRODUCTION_IMPLEMENTATION (internal validation only) |
| E-16 | PRODUCTION_IMPLEMENTATION (coordinate mismatch failure mode) |
| E-15 | PRODUCTION_IMPLEMENTATION (AI vocabulary validation only) |

---

## Quality Gate Verification

Each accepted RD was verified against the 7-point quality gate:

| RD | X!=Y | Neutral X/Y | Source Evidence | Conflate→Fail | No Implementation | Architecture-Independent | No Legacy Terms |
|---|---|---|---|---|---|---|---|
| RD-01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-04 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-06 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-07 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-08 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-09 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-10 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RD-11 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

All 11 accepted RDs pass all 7 quality gates. Three RDs (07, 08, 10) have MEDIUM confidence due to weaker failure evidence or structural rather than failure-derived support.

---

## Unresolved Questions

These questions are genuinely unsupported by current legacy evidence. They are recorded for potential future investigation, not answered speculatively:

1. **Does the distinction between "page visited" and "page exhausted" (RD-09) require tracking per-element exploration state, or is per-page tracking sufficient?** E-07 shows branch-level loss; E-10-A shows page-level revisit. The evidence does not resolve whether element-level tracking is required.

2. **Is the Plan mode (Static coordinates) a necessary capability or a migration artifact?** E-16 exists and is used by E-01 locate scenario. The evidence shows it works but does not prove it is the only way to achieve locate behavior. RD-07 establishes that intent and execution method are distinct; it does not establish that both modes must exist.

3. **Does RD-10 (GoalExpression != GoalState) require persistent goal objects, or is stateless goal evaluation sufficient with better progress tracking?** E-17 is stateless; E-03-B tracks target as a structured field. The evidence shows two approaches but does not resolve which aspect (expression vs state) is the critical boundary.

4. **Are the 6 simulation assumptions (SA-01 through SA-06) masking additional Reality Distinctions, or are they implementation conveniences without semantic implications?** The simulation bypasses observation→conclusion inference. Some of these may be RD-08 refinements; others may be purely implementation conveniences. Current evidence is insufficient to distinguish.

5. **Does the Python task_parser.py gap (E-18) represent a missing semantic capability (deterministic NL parsing) or a different design philosophy?** The Python code exists but its behavioral contract is not documented in the C# corpus. Without inspecting the Python source, we cannot determine whether it proves a distinction not already covered by RD-07 or RD-10.

---

## Readiness

**REALITY_DISTINCTION_CATALOG_READY_FOR_SCENARIO_PRESSURE_FORMULATION**

11 accepted Reality Distinctions with supporting evidence across 48 atomic evidence cases. 3 provisional, 6 simulation-assumption-only, 2 implementation-only, and 2 evidence limitations classified. 5 legacy transformation patterns preserved for reference. All accepted RDs pass the 7-point quality gate. Cross-evidence clusters confirm multi-source corroboration for the strongest distinctions.

---

## Repository Changes

`docs/decisions/legacy-reality-distinction-step4.md` ONLY
