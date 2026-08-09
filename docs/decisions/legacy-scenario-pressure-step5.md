# LEGACY_SCENARIO_PRESSURE_RESULT — Step 5

> Generated: 2026-08-09
> Primary inputs: `docs/decisions/legacy-reality-distinction-step4.md`, `docs/decisions/legacy-normalized-evidence-step3.md`, `docs/decisions/legacy-high-value-evidence-set-step2.md`
> Legacy truth source: `feature/refactor` (read-only Git objects)

---

## Input Reality Distinctions

**Accepted RD count:** 11 (RD-01 through RD-11)

---

## Scenario Formulation Summary

**Accepted Scenario Pressures:** 13
**Provisional Scenario Pressures:** 0
**Scenario Formulation Insufficient:** 1 (RD-06 → preserved as RD, scenario formulation deferred)

### RD Wording Refinements Recommended

| RD | Issue | Recommendation |
|---|---|---|
| RD-08 | "PageIdentityObserved" overstates what is directly observable | Rename to `RawPageEvidence != SemanticPageIdentity`. Raw evidence = elements, text, coordinates, foreground app. Semantic identity = conclusion that "this is the Settings home page." Scenario SP-10 uses the refined concept. |
| RD-09 | "PreviouslyVisited" and "Unexplored" are both conclusions, not primitives; the distinction conflates visitation status with exploration status | Split into: `LocationVisited != LocationExhausted` (a location can be entered without all its reachable content being discovered). Scenario SP-03 exercises this directly. |

---

## Scenario Pressure Portfolio

---

### SP-01 — Entry Action Must Verify World Effect

**Scenario Pressure ID:** SP-01
**Title:** App entry action must verify foreground state before traversal begins
**Priority:** P0
**Primary RD:** RD-01 (ActionExecution != ActionEffect)
**Secondary RDs:** RD-11 (PlanConstructed != ExecutionGuaranteed)
**Source Evidence:** E-13-A (EntryPolicy fake success), E-01 (EmulatorScenarioIntegrationTests entry strategy)
**Evidence Strength:** MULTI_SOURCE_CORROBORATED
**Scenario Type:** NEGATIVE_CONTROL

---

**Intent:**
Launch the Android Settings app and begin enumerating its first-level entries.

**Given:**
- A device/emulator is running but the Settings app is NOT in the foreground (e.g., the home screen or a different app is visible).
- A task description requests launching `com.android.settings` and enumerating its entries.
- The entry mechanism is instructed to cold-launch the Settings app.

**Available Evidence:**
- The entry mechanism can report whether it dispatched a launch command.
- The system can observe the current foreground application.
- The system can observe visible elements on screen.
- These are distinct: "launch command sent" is not the same as "Settings app is in the foreground with recognizable content."

**When:**
The entry mechanism dispatches a launch command and reports success internally, but the app does not actually come to the foreground (launch failed silently, wrong app launched, or app crashed on startup). The system proceeds to enumerate "first-level entries" based on whatever screen is actually displayed.

**Then:**
Before beginning traversal, the system must obtain fresh observational evidence that the intended application is in the foreground and that the screen contains content consistent with the expected entry point. If this evidence cannot be obtained, the system must report entry failure with a diagnosable reason — not proceed to traversal on an unverified screen.

**Must Not:**
- Treat "launch command dispatched" as equivalent to "target app is in the foreground."
- Begin enumerating content on whatever screen happens to be displayed when the intended app failed to launch.
- Report traversal success (AllVisited, TargetFound) when the intended application was never reached.

**Pass Oracle:**
After entry, a fresh observation confirms: (a) the foreground application matches the intended package, and (b) the visible screen content is consistent with the expected entry page (e.g., recognizable Settings entries are present). Traversal then proceeds on verified ground.

**Fail Oracle:**
Entry fails observably — the foreground app does not match, or the screen content is unrecognizable. The system reports entry failure with a specific reason. It does NOT proceed to enumerate content on the wrong screen.

**Termination / Completion Rule:**
NOT_APPLICABLE (this scenario tests entry, not traversal completion).

**Legacy Mechanisms Excluded:**
EntryPolicyExecutor.ExecuteStrategy, ColdLaunch/DirectDeeplink string returns, BindCurrentScreen without verification.

**What This Scenario Proves:**
The system does not confuse "I asked the device to launch an app" with "the app is actually running in the foreground."

---

### SP-02 — Navigation Action Must Verify Page Change

**Scenario Pressure ID:** SP-02
**Title:** Navigation action must detect when the observed world did not change
**Priority:** P1
**Primary RD:** RD-01 (ActionExecution != ActionEffect)
**Secondary RDs:** RD-05 (ElementPresence != ElementNavigability)
**Source Evidence:** E-09-L4 (StaleClick — click dispatched, page unchanged), E-09-L8 (Subtitle double-click — same page after tap on non-navigable element)
**Evidence Strength:** EXECUTABLE_REGRESSION
**Scenario Type:** ATOMIC_BEHAVIOR

---

**Intent:**
Navigate from the Settings home page to a sub-page by tapping a labeled entry.

**Given:**
- The system is on a known page with multiple visible interactive elements.
- One element appears to be a navigation target (e.g., labeled "Wi‑Fi").
- The system decides to tap this element to navigate to its sub-page.

**Available Evidence:**
- Pre-action observation: elements visible before the tap, including their types, text, and coordinates.
- Action dispatch record: the tap was sent to coordinates (x, y).
- Post-action observation: elements visible after the tap.
- These are distinct: "tap sent" is not the same as "the page changed."

**When:**
The system taps an element, but the resulting page observation is materially identical to the pre-tap observation (same elements, same positions, same text — no page change occurred). This can happen because: the element is not actually a navigation target (e.g., a decorative label, a subtitle adjacent to a menu item, or a disabled button), or the tap missed its intended target.

**Then:**
The system must compare pre-action and post-action observations. If the observations are materially identical (no page change detected), the system must NOT treat the action as having successfully navigated. It must recognize that the element did not produce a navigation effect and must not repeatedly tap the same element expecting a different result. After K consecutive attempts producing no observable change, the element must be excluded from further navigation attempts.

**Must Not:**
- Treat "tap dispatched" as equivalent to "navigation occurred."
- Repeatedly tap the same element indefinitely when it produces no page change.
- Count a non-navigational tap as progress toward the task goal.

**Pass Oracle:**
When an element is tapped and produces no observable page change, the system detects the staleness within K attempts and either: (a) marks the element as non-navigable and continues to other work, or (b) reports that navigation failed. The system does NOT loop indefinitely on the same element.

**Fail Oracle:**
The system taps the same element 4+ times with no page change, treating each tap as legitimate navigation work, and either exhausts its step budget or reports false completion.

**Termination / Completion Rule:**
NOT_APPLICABLE (this scenario tests action verification, not completion).

**Legacy Mechanisms Excluded:**
StaleClick circuit breaker (K=3), FSM Execute→ResultVerify transition, CallbackPageAnalyzer, FrameCompleted.

**What This Scenario Proves:**
The system does not confuse "I tapped something" with "I navigated somewhere."

---

### SP-03 — Multi-Branch Hub Must Not Report Complete With Unvisited Branch

**Scenario Pressure ID:** SP-03
**Title:** A hub with multiple navigation branches must not report AllVisited when a branch remains entirely unexplored
**Priority:** P0
**Primary RD:** RD-02 (WorkDispatched != WorkCompleted)
**Secondary RDs:** RD-09 (PreviouslyVisited != Unexplored)
**Source Evidence:** E-07 (MultiBranchNavigationTests — unfixed bug, all three variants), E-07 non-scrollable variant
**Evidence Strength:** EXECUTABLE_REGRESSION (deterministic failing test)
**Scenario Type:** NEGATIVE_CONTROL

---

**Intent:**
Enumerate all reachable content from a hub page that presents multiple navigation branches.

**Given:**
- A hub page is visible with two distinct navigation buttons: "Go to List A" and "Go to List B."
- "Go to List A" navigates to a page containing 16 scrollable items.
- "Go to List B" navigates to a different page also containing 16 scrollable items.
- The task is to exhaustively visit all reachable content.

**Available Evidence:**
- Hub page observation: both buttons are visible with distinct labels and coordinates.
- After tapping "Go to List A": the List A page is observed, containing its items.
- After scrolling through List A: all 16 items have been observed.
- After returning from List A: the hub page is observed again. "Go to List B" is still visible and has not been tapped.
- The system can track: which navigation targets on the hub have been dispatched, and which have not.

**When:**
The system traverses the first branch (List A → scroll → all 16 items visited → return to hub). At this point, the hub is visible again. "Go to List B" is observable and has never been tapped. The system has completed work on one of two known branches.

**Then:**
The system must recognize that "Go to List B" represents undispatched work. It must dispatch navigation to List B, traverse its content, and only report completion after both branches have been exhausted. The completion decision must be based on the set of known navigation targets versus the set of dispatched navigation targets — not on whether any single branch was completed.

**Must Not:**
- Report AllVisited or task completion while "Go to List B" has never been tapped.
- Treat "one branch was fully explored" as equivalent to "all reachable branches were explored."
- Ignore observable navigation targets that have not been dispatched.

**Pass Oracle:**
Both "Go to List A" and "Go to List B" appear in the action history as having been tapped. Items from both List A (16/16) and List B (16/16) have been observed. Total items visited = 32. Completion is reported only after the second branch is exhausted and no undispatched navigation targets remain on the hub.

**Fail Oracle:**
The system taps "Go to List A," exhausts it, returns to the hub, and reports completion without ever tapping "Go to List B." List B remains at 0/16 items observed. The completion claim contradicts the observable fact that a visible navigation target was never dispatched.

**Termination / Completion Rule:**
Completion is permitted when: (a) all observable navigation targets on all visited pages have been dispatched, AND (b) all dispatched branches have been exhausted (all their reachable content observed), AND (c) no page with undispatched navigation targets remains in the set of visited-but-not-exhausted locations.

**Legacy Mechanisms Excluded:**
DFS visitation order, ChildrenStrategy, DynamicMatch rule generation, Frame completion signaling, TraversalStack. The solution must not prescribe a specific data structure (graph, queue, stack, frontier).

**What This Scenario Proves:**
The system does not confuse "I finished one branch" with "I finished everything."

---

### SP-04 — Declared Depth Bound Must Be Enforced During Discovery

**Scenario Pressure ID:** SP-04
**Title:** A declared depth constraint must prevent entry into deeper discoverable pages
**Priority:** P0
**Primary RD:** RD-03 (ConstraintDeclared != ConstraintEnforced)
**Secondary RDs:** NONE
**Source Evidence:** E-11 (SettingsEnumerateRegression — depth=2 declared, depth=3 entered pre-fix), E-09-L2 (DepthConstraint_StopsAtLevel2), E-09-L3 (FsmInvariant_SubframeDepthNeverExceedsMaxDepth), E-09-L7 (DepthSemantics formula), E-08 Step2 (depth=4 reproduced from real run)
**Evidence Strength:** EXECUTABLE_REGRESSION + RECORDED_REALITY_DERIVED
**Scenario Type:** ATOMIC_BEHAVIOR

---

**Intent:**
Enumerate Android Settings entries with a hard constraint: do not go deeper than 2 levels from the home screen.

**Given:**
- The Settings app has at least 4 levels of nesting: Settings home (depth 0) → Network & internet (depth 1) → Internet (depth 2) → Wi‑Fi (depth 3).
- The task declares maxDepth = 2.
- At depth 2 (Internet page), a "Wi‑Fi" menu item is observable and tap-able.
- Tapping "Wi‑Fi" would navigate to depth 3.

**Available Evidence:**
- The declared constraint: maxDepth = 2 (available before execution begins).
- The current depth: the system can track how many navigation steps from the entry point it has taken.
- Observable navigation targets at the current depth: "Wi‑Fi" is visible.
- The effective constraint at this point: min(declared maxDepth, remaining depth budget).

**When:**
The system is on the Internet page at depth 2. It observes "Wi‑Fi" as a menu item — a valid navigation target by element type. The declared constraint says maxDepth = 2. The current depth equals the declared maximum.

**Then:**
The system must NOT generate a navigable child task for "Wi‑Fi." It may record that "Wi‑Fi" exists (as discovered content at this depth), but it must not attempt to navigate to it. The effective constraint at this execution point must be enforced: current depth (2) >= declared maxDepth (2) → no further descent. The system must continue exploring other content at depth ≤ 2.

**Must Not:**
- Navigate to Wi‑Fi (depth 3) when maxDepth = 2 is declared.
- Treat "Wi‑Fi is a valid menu_item and therefore navigable" as overriding the depth constraint.
- Exceed the declared depth bound merely because more discoverable content exists.

**Pass Oracle:**
The system visits Network & internet (depth 1) and Internet (depth 2). It observes Wi‑Fi on the Internet page but does NOT navigate to it. The visited pages do not include Wi‑Fi or any page at depth ≥ 3. The depth constraint declared before execution is respected at every execution point.

**Fail Oracle:**
The system navigates to Wi‑Fi (depth 3). Visited pages include a depth-3 page. The declared constraint existed in the plan but was not enforced during sub-page discovery. (This is the pre-fix legacy behavior — real runs hit depth=3+ pages, replay confirmed depth=4.)

**Termination / Completion Rule:**
Completion is permitted when all reachable content at depth ≤ maxDepth has been exhausted. Content beyond maxDepth is not required to be visited.

**Legacy Mechanisms Excluded:**
DynamicMatch sub-frame generation, DepthSemantics formula (Depth ≥ MaxDepth+1 → degrade to leaf_info), NodeStack, TraversalRuntimeContext, IntentSlots.Depth field.

**What This Scenario Proves:**
The system does not confuse "I declared a boundary" with "the boundary is enforced everywhere it matters."

---

### SP-05 — Observation Failure Must Not Become Content Exhaustion

**Scenario Pressure ID:** SP-05
**Title:** A failed scroll query must not be treated as proof that the end of the list has been reached
**Priority:** P0
**Primary RD:** RD-04 (ObservationFailed != ContentExhausted)
**Secondary RDs:** RD-01 (ActionExecution != ActionEffect)
**Source Evidence:** E-13-B (ADB scroll failure → IsEnd=true), E-12-A (scroll-only dead-end — old behavior: infinite scroll), F-23 (device/ADB failures are not no-scroll or end-of-list)
**Evidence Strength:** MULTI_SOURCE_CORROBORATED
**Scenario Type:** NEGATIVE_CONTROL

---

**Intent:**
Scroll through a list to determine whether it contains more items than currently visible.

**Given:**
- A scrollable list is displayed on screen. Some items are visible. It is unknown whether more items exist below the visible region.
- The system issues a scroll command and then queries the device for the new scroll state.
- The device query mechanism can fail (ADB disconnect, command timeout, missing response data) independently of whether the list actually has more content.

**Available Evidence:**
- Pre-scroll observation: visible items before scrolling.
- Scroll command dispatch record: the swipe/scroll was sent.
- Post-scroll state query: the response from the device about current scroll position and whether the end has been reached.
- Critically: the device query response has a success/failure status that is independent of the scroll-progress data it contains. A failed query returns no reliable progress data.

**When:**
The system issues a scroll command. The post-scroll device query fails — the ADB command times out, returns an error, or returns incomplete data with missing scroll-position attributes. The query failure means the system has no reliable information about whether the scroll advanced, whether new items appeared, or whether the end of the list was reached.

**Then:**
The system must distinguish: "the query failed, I don't know the scroll state" from "I have confirmed the end of the list." It must NOT report IsEnd=true based on a failed query. It may retry the query, attempt an alternative observation method, or report that scroll state is unknown. The unknown state must be diagnosable — it must not silently become "end of list reached."

**Must Not:**
- Report "end of list reached" when the underlying device query failed.
- Treat missing or incomplete scroll-position data as equivalent to "nothing more to scroll."
- Silently fold query failure into a positive completion signal.

**Pass Oracle:**
After a failed scroll-state query, the system reports that scroll state is unresolved (not IsEnd). It either retries and obtains valid data, or reports an error with a specific reason. The system does NOT claim it has reached the end of the list.

**Fail Oracle:**
After a failed scroll-state query, the system reports IsEnd=true and terminates scrolling. Content that exists below the visible region is never visited. The system reports completion based on a false premise. (This is the documented legacy behavior — AdbScreenStateProvider exception swallowing.)

**Termination / Completion Rule:**
Content exhaustion may only be claimed when: (a) a valid device query confirms scroll position is at the end, AND (b) this confirmation has been stable across K consecutive observations with no new items appearing. A failed query resets the stability counter and does not contribute to the exhaustion decision.

**Legacy Mechanisms Excluded:**
AdbScreenStateProvider exception swallowing, uiautomator dump scrollY/scrollYMax parsing, IsEnd=true default on failure, content stability K=3.

**What This Scenario Proves:**
The system does not confuse "I couldn't see anything new because my sensors failed" with "there is nothing new to see."

---

### SP-06 — Unchanging Content Must Not Loop Forever

**Scenario Pressure ID:** SP-06
**Title:** When scrolling produces no new observable content, the system must terminate without exhausting its step budget
**Priority:** P1
**Primary RD:** RD-04 (ObservationFailed != ContentExhausted)
**Secondary RDs:** NONE
**Source Evidence:** E-12-A (scroll-only dead-end — old behavior: infinite scroll until MaxSteps; new behavior: content stability K=3 → AllVisited)
**Evidence Strength:** EXECUTABLE_REGRESSION
**Scenario Type:** ATOMIC_BEHAVIOR

---

**Intent:**
Scroll through a list to discover all its items.

**Given:**
- A scrollable list is displayed. The scroll mechanism is functional (scroll commands succeed).
- However, the list content never changes — every scroll action reveals exactly the same items. This can happen with very short lists (all items fit on one screen), or when the scroll mechanism reports "scrollable" but the content is static.
- The task is to visit all items in the list.

**Available Evidence:**
- Pre-scroll observation: a set of visible items.
- Scroll action dispatch: scroll command succeeded.
- Post-scroll observation: a set of visible items.
- The system can compare pre- and post-scroll item sets.

**When:**
The system scrolls repeatedly. Each post-scroll observation returns the same set of items (same identities, same count). The scroll mechanism reports "scrollable, not at end" but no new content ever appears. After K consecutive scrolls with identical observed content and no new items, the system has evidence of content stability.

**Then:**
The system must detect that scrolling is not revealing new content. After K consecutive observations with no new items, it must conclude that the visible content represents the complete set and terminate scrolling. It must NOT continue scrolling indefinitely merely because the scroll mechanism reports "scrollable." Content stability is the primary termination signal; scroll-mechanism signals are secondary.

**Must Not:**
- Scroll indefinitely because the page is technically "scrollable" while content never changes.
- Exhaust its step budget on a list that was already fully observed.
- Require a scroll failure to terminate — the scroll mechanism may never fail.

**Pass Oracle:**
The system scrolls K times (e.g., K=3), observes identical content each time, and terminates with AllVisited. Total steps << MaxSteps. The system did not exhaust its step budget.

**Fail Oracle:**
The system scrolls until MaxSteps is exhausted. Content was fully observed early but the system could not detect stability. (This is the pre-fix legacy behavior.)

**Termination / Completion Rule:**
Content exhaustion may be claimed when K consecutive observations produce no new items AND the scroll mechanism confirms end-of-list, OR when K consecutive observations produce no new items regardless of scroll-mechanism status. The latter case (content stability) is the safety net for when the scroll mechanism cannot be trusted.

**Legacy Mechanisms Excluded:**
Content stability counter K=3, MaxEmptyScrollRetries, gateway content-stable fingerprint comparison, seen-set differential.

**What This Scenario Proves:**
The system does not confuse "the scrollbar says there's more" with "there is actually more content to discover."

---

### SP-07 — Element Visibility Must Not Imply Navigability

**Scenario Pressure ID:** SP-07
**Title:** An element visible on screen must not be treated as a navigation target without evidence that tapping it produces navigation
**Priority:** P1
**Primary RD:** RD-05 (ElementPresence != ElementNavigability)
**Secondary RDs:** RD-01 (ActionExecution != ActionEffect)
**Source Evidence:** E-09-L8 (SubtitleDegraded — subtitle text double-clicked), E-10-C (SearchBoxMenuItem — search input misclassified as menu_item → stuck), E-10-B (SearchBoxInput — correctly typed as input → skipped), E-09-L5 (EmptyTextItem — empty OCR text → invalid child task)
**Evidence Strength:** RECORDED_REALITY_DERIVED + EXECUTABLE_REGRESSION
**Scenario Type:** NEGATIVE_CONTROL

---

**Intent:**
Enumerate navigable entries on the Settings home page.

**Given:**
- The Settings home page contains a mix of element types: navigable menu items ("Wi‑Fi", "Bluetooth", "Display"), a search input box, and decorative text elements (subtitles like "Bluetooth, pairing" adjacent to menu items).
- The system observes all elements with their types, text, and coordinates.
- Some elements look like navigation targets but are not: a search input opens a search UI (not a sub-page), a subtitle is informational text (tapping it does nothing), an element with empty text has no meaningful target.

**Available Evidence:**
- Element observation: type classification, text content, coordinates, spatial relationship to other elements.
- Historical evidence about element types: inputs are not navigation targets; decorative text adjacent to menu items is not independently navigable; elements with empty/whitespace-only text have no navigation semantics.
- Post-tap observation: whether tapping the element produced a page change.

**When:**
The system observes an element that may or may not be a navigation target. It must decide whether to generate a navigation task for this element.

Case A: A search input ("Q Search settings") is classified as type=input. Tapping it would open a search UI, not navigate to a sub-page.

Case B: A subtitle text ("Bluetooth, pairing") appears adjacent to the "Connected devices" menu item. It has a small spatial footprint (dy_full=0.0336). Tapping it produces no page change.

Case C: An element has empty text ("") or whitespace-only text ("   "). There is no meaningful navigation target.

**Then:**
The system must use element type, text content, and spatial evidence — not mere presence — to decide navigability. An element whose type indicates non-navigable semantics (input, text, decorative) must not generate a navigation task. An element whose text is empty or whitespace-only must not generate a navigation task. After tapping any element, if no page change is observed, the element must not be treated as navigable regardless of its declared type.

**Must Not:**
- Treat a search input as a navigation target.
- Treat decorative subtitle text as an independent navigation target.
- Generate navigation tasks from elements with empty or whitespace-only text.
- Continue treating an element as navigable after it has been tapped and produced no observable page change.

**Pass Oracle:**
Navigation tasks are generated only for elements with navigable types, non-empty meaningful text, and spatial characteristics consistent with navigation targets. Search inputs, subtitles, and empty-text elements are excluded. After a tap on any element that produces no page change, the element is excluded from further navigation attempts.

**Fail Oracle:**
A search input is tapped → system enters search UI → cannot escape → stuck. A subtitle is double-clicked → same page observed → system re-taps indefinitely. An empty-text element generates a navigation task → invalid target. (All three failures are evidenced in legacy runs.)

**Termination / Completion Rule:**
NOT_APPLICABLE.

**Legacy Mechanisms Excluded:**
DynamicMatch MatchCondition type filtering, NormalizeItemText, subtitle degradation detection, YOLO label classification taxonomy, dy_full spatial threshold.

**What This Scenario Proves:**
The system does not confuse "something is on the screen" with "something should be tapped."

---

### SP-08 — Recovery Attempt Must Not Imply Error Resolution

**Scenario Pressure ID:** SP-08
**Title:** Recovery actions must not reset error history; error resolution must be confirmed by fresh observation
**Priority:** P1
**Primary RD:** RD-06 (RecoveryAction != ErrorStateReset)
**Secondary RDs:** RD-01 (ActionExecution != ActionEffect)
**Source Evidence:** E-04-B (consecutive errors across backtracks), E-04-A (5-failure gate vs consecutive gate), E-04-G (AI empty response non-transient)
**Evidence Strength:** EXECUTABLE_REGRESSION
**Scenario Type:** DISTURBANCE

---

**Intent:**
Continue enumerating Settings entries despite encountering failures on a sub-page.

**Given:**
- The system is on a sub-page attempting to interact with its elements.
- Multiple interaction attempts fail (action denied, unexpected page state, AI returns empty response).
- The system attempts recovery actions: backtrack (return to previous context), retry (attempt same action again).

**Available Evidence:**
- Action failure records: which actions failed and why.
- Recovery action records: which recovery actions were attempted.
- Post-recovery observation: the state of the page after recovery.
- Error history: how many failures have occurred, of what types, across how many distinct interaction targets.
- These must be tracked independently: "I attempted recovery" is not the same as "the error condition is resolved."

**When:**
The system has experienced multiple failures. It attempts a recovery action (backtrack). The recovery action executes successfully — the system returns to the previous context. However, the underlying condition that caused the failures may still exist. A fresh observation is needed to determine whether the error condition has actually been resolved.

**Then:**
The system must track error history across recovery attempts. Executing a recovery action must not automatically reset the error count or mark the error as resolved. Error resolution must be confirmed by fresh observation showing that: (a) the page is in a workable state, and (b) further interactions succeed. If errors continue to accumulate across recovery attempts, the system must escalate its recovery strategy (e.g., from retry to backtrack to abandoning the current context). Structural failures (empty AI responses, precondition violations) must be classified as non-transient — retrying them is not appropriate.

**Must Not:**
- Reset the error count to zero merely because a recovery action was attempted.
- Treat "I pressed back" as equivalent to "the problem is fixed."
- Retry structural failures (empty AI responses) as if they were transient.
- Continue the same failing pattern indefinitely without escalating recovery.

**Pass Oracle:**
After recovery + fresh observation, the system either: (a) confirms the error is resolved (page is workable, next interaction succeeds) and resets the error count, or (b) detects that errors persist, increments the error count, and escalates recovery (e.g., after N consecutive errors without resolution, abandons the current context). The error history correctly reflects accumulated failures and recovery attempts.

**Fail Oracle:**
Every recovery attempt resets the error count to zero. The system cycles between "fail → backtrack → retry → fail" indefinitely, never escalating because the error counter is always reset. (This is the pre-fix legacy Bug #2 behavior.)

**Termination / Completion Rule:**
NOT_APPLICABLE (this scenario tests error handling, not completion).

**Legacy Mechanisms Excluded:**
ConsecutiveErrors counter, ErrorStrategy.Backtrack/PressBack/Pop-only, item-gate vs consecutive-gate, FSM ErrorHandling state, StrategyForcingHandler.

**What This Scenario Proves:**
The system does not confuse "I tried to fix it" with "it is fixed."

---

### SP-09 — Same Intent, Different Execution Methods

**Scenario Pressure ID:** SP-09
**Title:** A task expressed as a desired outcome must not require a specific execution method when the outcome can be achieved differently
**Priority:** P2
**Primary RD:** RD-07 (TaskIntent != ExecutionMethod)
**Secondary RDs:** RD-11 (PlanConstructed != ExecutionGuaranteed)
**Source Evidence:** E-15 + E-14 + E-05 (Intent mode — DynamicMatch), E-16 + E-01 (Plan mode — Static coordinates), E-05-A vs E-05-C (same intent shape, different targets)
**Evidence Strength:** MULTI_SOURCE_CORROBORATED
**Scenario Type:** TRANSFORMATION

---

**Intent:**
Find the "Dark mode" setting in the Android Settings app and verify it exists.

**Given:**
- The Settings app is in the foreground.
- The task is described as: "Find the Dark mode setting and confirm it exists. Do not change its value."
- The system has two ways this could be achieved:
  - Method A: Follow a pre-scripted path with explicit coordinates (tap Display at (0.5, 0.4), then observe the Display page for a "Dark mode" element).
  - Method B: Explore dynamically — observe the home screen, identify Display as a navigation target, navigate to it, observe the Display page, identify Dark mode, confirm it exists, stop.

**Available Evidence:**
- Task description: the desired outcome (find Dark mode, confirm existence, don't change it).
- Method A provides: exact coordinates and expected page identities. Does NOT provide: adaptability if coordinates don't match.
- Method B provides: element discovery rules and target matching criteria. Does NOT provide: exact coordinates or guaranteed stop condition.
- The desired outcome is the same regardless of method.

**When:**
The task is given as a desired outcome ("find and confirm Dark mode exists"). The system must execute it. If Method A is used and the coordinates match, the system navigates correctly and finds Dark mode. If Method A is used but coordinates don't match the current screen (different device, different layout), the system taps empty space and fails. If Method B is used, the system discovers the path dynamically and succeeds regardless of coordinate differences.

**Then:**
The system must accept the task as a desired outcome description. If the task provides explicit execution steps, the system must execute them as specified — not reinterpret them. If the task provides only the desired outcome, the system must discover the execution method from observation. The two input forms must not be conflated: a desired-outcome task must not be executed as literal coordinates; a literal-coordinate task must not be reinterpreted by AI.

**Must Not:**
- Reinterpret explicit execution steps as if they were a desired-outcome description.
- Execute a desired-outcome description as if it contained literal coordinates.
- Fail to achieve the desired outcome when a valid execution path exists, merely because the system only supports one input form.

**Pass Oracle:**
Given an explicit-coordinate plan that matches the current screen: the system executes it faithfully and finds Dark mode. Given a desired-outcome description on a different device where coordinates differ: the system discovers the path dynamically and finds Dark mode. Both input forms succeed for the scenarios they are designed for.

**Fail Oracle:**
Given an explicit-coordinate plan on a device where coordinates differ: the system taps empty space and reports failure, even though Dark mode is reachable. Given a desired-outcome description: the system demands explicit coordinates and refuses to execute. (The legacy system avoids this by having two separate code paths, but the conflation risk exists at the interface boundary.)

**Termination / Completion Rule:**
Completion is permitted when the target ("Dark mode") has been observed on a page consistent with the expected location (Display page, not Storage page). The method of reaching the target is not part of the completion criteria.

**Legacy Mechanisms Excluded:**
IntentExtractor, PlanCompiler, ScenarioPlanLoader, DynamicMatch vs Static, ExtractedIntentSlots, two separate Host code paths.

**What This Scenario Proves:**
The system does not confuse "what should be achieved" with "exactly how to achieve it."

---

### SP-10 — Same Logical Page Must Be Recognized Across Observations

**Scenario Pressure ID:** SP-10
**Title:** Two observations of the same logical page must be recognized as the same page despite minor differences in observed elements
**Priority:** P1
**Primary RD:** RD-08 (RawPageEvidence != SemanticPageIdentity)
**Secondary RDs:** RD-09 (PreviouslyVisited != Unexplored)
**Source Evidence:** E-10-A (DFS revisit loop — engine re-enters Internet page, treats it as new work), E-01 (locate scenario — TraceTool VerifyEngine required for page identity confirmation), E-03 (simulation supplies page identity from fixture)
**Evidence Strength:** RECORDED_REALITY_DERIVED
**Scenario Type:** ATOMIC_BEHAVIOR

---

**Intent:**
Navigate through Settings pages and recognize when a previously-visited page is encountered again.

**Given:**
- The system has previously visited a page (e.g., the Internet page under Network & internet).
- It navigated away from that page (pressed back).
- Later, through a different navigation path or a self-loop transition, the same logical page is encountered again.
- The second observation may differ slightly from the first: scroll position may be different, some elements may have shifted slightly, or OCR may return slightly different text for the same elements.

**Available Evidence:**
- First observation of the page: a set of elements with types, text, and approximate coordinates.
- Second observation of a page: another set of elements.
- The system can compare element sets across observations: similar elements at similar positions, similar page structure, same foreground application.
- Raw element text and coordinates may vary between observations of the same logical page.

**When:**
The system observes a page whose elements are substantially similar to a page it has already visited. The element sets are not identical (text variations, coordinate drift, scroll offset), but they share enough structure to suggest this is the same logical page, not a new unexplored page.

**Then:**
The system must recognize that this page is likely the same logical page as one previously visited. It must NOT treat it as a brand-new unexplored page with fresh work to do. If the page was previously exhausted (all its reachable content was visited), the system must not re-generate navigation tasks for it. If the page was visited but not exhausted (e.g., it has navigation targets that were not yet dispatched), the system must recognize that only the remaining undispatched work needs attention.

**Must Not:**
- Treat every observation with slightly different elements as a new, never-before-seen page.
- Re-generate all navigation tasks from scratch when revisiting a previously-exhausted page.
- Enter an infinite loop by revisiting the same page and treating it as new each time.

**Pass Oracle:**
The system revisits a previously-exhausted page. It recognizes the page as visited-and-exhausted. It does NOT generate new navigation tasks. It navigates away or reports that no new work exists on this page.

**Fail Oracle:**
The system revisits a page it has already exhausted. It fails to recognize the page. It generates all navigation tasks again as if the page were new. It navigates into the same sub-page again. This cycle repeats until the step budget is exhausted. (This is the DFS revisit loop evidenced in E-10-A.)

**Termination / Completion Rule:**
NOT_APPLICABLE (this scenario tests page recognition, not completion).

**Legacy Mechanisms Excluded:**
Page fingerprinting, StateFixture page identity, TraceTool VerifyEngine, VisitedPages set, analysis.jsonl frame comparison.

**What This Scenario Proves:**
The system does not confuse "these elements look slightly different from last time" with "this is a completely new page."

---

### SP-11 — Goal Satisfaction Without Execution

**Scenario Pressure ID:** SP-11
**Title:** When the external world already satisfies a stated goal, the system must recognize satisfaction without executing unnecessary actions
**Priority:** P2
**Primary RD:** RD-10 (GoalExpression != GoalState)
**Secondary RDs:** RD-07 (TaskIntent != ExecutionMethod)
**Source Evidence:** E-17 (ITraversalAdvisor — stateless goal-as-string), E-03-B (SimulationBaselineTests target search — TargetFound via structured field evaluation, not re-asking AI)
**Evidence Strength:** MULTI_SOURCE_CORROBORATED
**Scenario Type:** NEGATIVE_CONTROL

---

**Intent:**
Ensure that Wi‑Fi is enabled on the device.

**Given:**
- The device's Wi‑Fi is already enabled (the switch is in the "on" position).
- The task is expressed as a natural-language goal: "Make sure Wi‑Fi is turned on."
- The system can observe the current Wi‑Fi state from the Settings home screen or the Wi‑Fi sub-page.

**Available Evidence:**
- Previous observation: the Wi‑Fi switch state was observed as "on" during earlier traversal.
- Current observation: the system can re-observe the Wi‑Fi state.
- The goal expression: "Make sure Wi‑Fi is turned on" — a string describing desired world state.
- The goal state: Wi‑Fi is currently on ≡ the goal is already satisfied.
- These are distinct: having a goal string is not the same as knowing whether the goal is satisfied.

**When:**
The system is given the goal "Make sure Wi‑Fi is turned on." Before taking any action, it observes (or recalls from recent observation) that Wi‑Fi is already on. The external world already matches the desired state.

**Then:**
The system must evaluate the current world state against the goal and recognize that the goal is already satisfied. It must NOT execute unnecessary actions (tapping the Wi‑Fi entry, toggling the switch, navigating to the Wi‑Fi page) merely because those actions are associated with achieving the goal. The goal's satisfaction must be evaluable from observed state, not only from having executed prescribed steps.

**Must Not:**
- Navigate to the Wi‑Fi page and toggle the switch (turning Wi‑Fi OFF) because the system's "goal satisfaction" is defined only as "I executed the steps."
- Require re-execution of a previously-achieved goal when world state already satisfies it.
- Be unable to answer "is the goal satisfied?" without re-prompting an AI model.

**Pass Oracle:**
The system observes Wi‑Fi = on. It evaluates the goal "Make sure Wi‑Fi is turned on" against current world state. It determines the goal is already satisfied. It reports goal satisfaction without dispatching any new actions. No unnecessary navigation or toggle occurs.

**Fail Oracle:**
The system navigates to Wi‑Fi page. It toggles the switch (turning Wi‑Fi OFF). It reports "goal achieved" because it executed the steps associated with the goal, despite the world state now contradicting the goal. Or: the system re-prompts an AI model with "Is Wi‑Fi enabled?" and the model incorrectly answers "no" because it has no access to observed state.

**Termination / Completion Rule:**
Goal satisfaction is determined by evaluating observed world state against the goal criteria. No execution steps are required when the criteria are already met.

**Legacy Mechanisms Excluded:**
ITraversalAdvisor.DecideNextActionAsync, stateless per-call goal string, TargetFound CompletionPolicy with TargetName, PromptTemplateRegistry.

**What This Scenario Proves:**
The system does not confuse "someone asked me to achieve X" with "I must perform actions, even if X is already true."

---

### SP-12 — Plan Validity Must Not Imply Execution Success

**Scenario Pressure ID:** SP-12
**Title:** A successfully constructed plan must not be treated as a guarantee of successful execution; plan-world divergence must be detectable
**Priority:** P2
**Primary RD:** RD-11 (PlanConstructed != ExecutionGuaranteed)
**Secondary RDs:** RD-01 (ActionExecution != ActionEffect)
**Source Evidence:** E-14 (PlanCompiler — internal validation only), E-16 (ScenarioPlanLoader — coordinate mismatch failure mode), E-15 (IntentExtractor — vocabulary validation only, not correctness)
**Evidence Strength:** MULTI_SOURCE_CORROBORATED
**Scenario Type:** NEGATIVE_CONTROL

---

**Intent:**
Execute a plan to locate "About phone" in Settings using explicit coordinates.

**Given:**
- A plan has been constructed specifying: "Tap at coordinates (0.5, 0.7) to select About phone."
- The plan passes all internal validation: coordinates are within screen bounds, target is non-empty, the plan structure is well-formed.
- However, on the actual device, "About phone" is at coordinates (0.5, 0.85) — the planned coordinate will miss the target.
- The system does not know about the mismatch before execution.

**Available Evidence:**
- Plan structure: valid, internally consistent.
- Pre-action observation: elements visible before the tap.
- Action dispatch: tap sent to (0.5, 0.7).
- Post-action observation: elements visible after the tap — the page did not change, or changed to an unexpected page.
- Plan expectation: the tap should have navigated to the "About phone" page.

**When:**
The system executes the planned tap at (0.5, 0.7). The post-action observation shows either: (a) the same page (tap hit empty space or wrong element), or (b) an unexpected page (tap hit a different navigation target). The plan's assumption about the world was wrong, but the plan itself was structurally valid.

**Then:**
The system must detect the divergence between plan expectation and observed outcome. It must NOT report success based on the plan's internal validity. It must either: (a) attempt recovery (re-observe, re-identify the target), or (b) report that the plan could not be executed as specified. Plan success must be conditional on observed outcome matching plan expectation, not on the plan having been dispatched.

**Must Not:**
- Report plan success because the plan was well-formed and the actions were dispatched.
- Ignore post-action observations that contradict plan expectations.
- Continue executing subsequent plan steps when an earlier step's outcome contradicts the plan's assumptions.

**Pass Oracle:**
The plan's tap misses "About phone." The system observes that the page did not change to the expected target page. It detects the divergence. It does NOT report plan success. It either recovers (re-locates the target and taps correctly) or reports execution failure with a specific reason.

**Fail Oracle:**
The plan's tap misses. The system reports plan success because the plan was valid and the action was dispatched. The "About phone" page was never reached, but the system claims the target was found. (This is the implied failure mode of E-16 coordinate mismatch combined with E-13-A action-without-verification.)

**Termination / Completion Rule:**
Plan completion must be based on observed outcomes matching plan expectations, not on plan dispatch. Each plan step's expected outcome must be verified by observation before the next step proceeds.

**Legacy Mechanisms Excluded:**
PlanCompiler.Compile() validation, ScenarioPlanLoader.Load(), JsonElement → Coordinate conversion, fail-fast validation rules.

**What This Scenario Proves:**
The system does not confuse "the plan looks correct on paper" with "the plan worked in reality."

---

### SP-13 — Revisiting a Page Must Not Reset Exploration State

**Scenario Pressure ID:** SP-13
**Title:** Re-entering a previously visited page must preserve knowledge of what was already explored on that page
**Priority:** P1
**Primary RD:** RD-09 (PreviouslyVisited != Unexplored)
**Secondary RDs:** RD-02 (WorkDispatched != WorkCompleted), RD-08 (RawPageEvidence != SemanticPageIdentity)
**Source Evidence:** E-10-A (DFS revisit loop — Internet page visited, then re-entered via self-loop, treated as new), E-12-B (unconsumed FrameCompleted — child frame stuck, parent page visited but child work unresolved), E-07 (hub page visited, first branch explored, second branch never dispatched)
**Evidence Strength:** EXECUTABLE_REGRESSION + RECORDED_REALITY_DERIVED
**Scenario Type:** ATOMIC_BEHAVIOR

---

**Intent:**
Continue enumerating Settings pages after returning to a previously-visited page.

**Given:**
- The system visited the Network & internet page earlier and navigated into its sub-pages (Internet, SIMs, etc.).
- It has now returned to the Network & internet page via back navigation.
- Some sub-pages were explored; others may not have been.
- The page has multiple navigation targets — some dispatched, some not.

**Available Evidence:**
- Page recognition: this is the same Network & internet page visited before.
- Exploration history for this page: which navigation targets were previously dispatched, which were exhausted, which remain undispatched.
- Current observation: the navigation targets currently visible.
- The system can compare current visible targets against historical dispatch records.

**When:**
The system returns to a page it has visited before. The page has 6 navigation targets. Earlier, the system dispatched 2 of them (Internet and SIMs). 4 remain undispatched. The system must decide what work remains on this page.

**Then:**
The system must recognize that this is a previously-visited page. It must recall which navigation targets were already dispatched and which were not. It must NOT regenerate all 6 navigation tasks as if the page were new. It must only dispatch the 4 undispatched targets. After dispatching and exhausting those, it must recognize that all targets on this page have been exhausted.

**Must Not:**
- Treat the page as brand-new and regenerate all navigation tasks.
- Lose track of which sub-pages were already explored.
- Re-dispatch navigation targets that were already exhausted.

**Pass Oracle:**
The system returns to Network & internet. It recognizes the page. It dispatches only the 4 previously-undispatched navigation targets. It does NOT re-enter Internet or SIMs unless there is evidence of unexplored content within them. After exhausting the remaining 4 targets, it reports the page as fully explored.

**Fail Oracle:**
The system treats the page as new. It regenerates all 6 navigation tasks. It re-enters Internet, re-traverses it, returns, re-enters SIMs, etc. — looping through previously-exhausted content. OR: the system reports the page as "done" because it was visited before, ignoring the 4 undispatched targets. (Both failures are evidenced: E-10-A for regeneration, E-07 for premature completion.)

**Termination / Completion Rule:**
A page is exhausted when all its observable navigation targets have been dispatched and all dispatched sub-pages have been exhausted. Visitation without exhaustion is an intermediate state.

**Legacy Mechanisms Excluded:**
VisitedPages set, AllVisited flag, FrameCompleted signaling, ChildrenStrategy, DFS order, TraversalStack, NodeStack.

**What This Scenario Proves:**
The system does not confuse "I've been here before" with either "everything here is new" or "everything here is done."

---

## Negative Controls

The following scenarios are explicitly designed as negative controls — they test that the system does NOT claim success under false premises:

| SP | What Is Removed | Expected System Behavior |
|---|---|---|
| SP-01 | Entry verification (no foreground check) | Must NOT proceed to traversal; must report entry failure |
| SP-03 | Second branch dispatch (only first branch traversed) | Must NOT report AllVisited; must recognize undispatched work |
| SP-05 | Valid scroll state query (query fails) | Must NOT report IsEnd; must report unresolved or error |
| SP-11 | World already satisfies goal (no action needed) | Must NOT execute unnecessary actions; must recognize satisfaction |
| SP-12 | Plan-world correspondence (plan doesn't match reality) | Must NOT report plan success; must detect divergence |

---

## Intent / Goal / Plan Scenario Pressures

Three scenarios exercise the medium-confidence Intent/Goal/Plan distinctions:

| SP | RD | Focus |
|---|---|---|
| SP-09 | RD-07 | Same intent achievable through different execution methods; system must accept both desired-outcome descriptions and explicit plans without conflating them |
| SP-11 | RD-10 | Goal satisfaction evaluable from observed world state, not only from having executed prescribed steps |
| SP-12 | RD-11 | Plan validity (internal consistency) is not a guarantee of execution success; plan-world divergence must be detectable |

RD-10 and RD-11 have MEDIUM confidence. Their scenarios are formulated conservatively — they test concrete behavioral distinctions without presuming architectural conclusions (e.g., "therefore we need a Planner").

---

## Navigation / Completion Scenario Pressures

| SP | Focus |
|---|---|
| SP-02 | Navigation action verification: did the page actually change? |
| SP-03 | Multi-branch completion: all branches must be dispatched before completion |
| SP-04 | Depth-bounded navigation: declared constraints enforced during discovery |
| SP-10 | Page recognition across observations: same page, different elements |
| SP-13 | Exploration state across revisits: what was already explored here? |

---

## Observation / Action Scenario Pressures

| SP | Focus |
|---|---|
| SP-01 | Entry verification: is the right app in the foreground? |
| SP-05 | Observation failure vs content exhaustion: query failed ≠ nothing left |
| SP-06 | Content stability vs infinite scroll: unchanging content must terminate |
| SP-07 | Element navigability vs visibility: not everything visible should be tapped |

---

## Recovery / Error Scenario Pressures

| SP | Focus |
|---|---|
| SP-08 | Recovery action vs error resolution: attempting recovery ≠ problem solved |

RD-06 (RecoveryAction != ErrorStateReset) has only one scenario (SP-08). The legacy evidence is heavily FSM-internal. The scenario was formulated to be architecture-neutral by focusing on observable behavior: error history persistence and recovery escalation. Further scenarios may become possible with additional evidence.

---

## Provisional Pressures

None. All 13 scenarios have sufficient evidence support from the Step 4 Reality Distinction catalog.

### Scenario Formulation Insufficient

**RD-06 (RecoveryAction != ErrorStateReset):** Only one scenario (SP-08) was formulated. The legacy evidence (E-04-A through E-04-G) is heavily tied to FSM internal state transitions, error strategy enums, and counter thresholds. Additional scenarios (e.g., distinguishing retry-appropriate errors from abort-appropriate errors, or testing popup-dismissal-vs-navigation-back as distinct recovery actions) would require stronger architecture-neutral behavioral evidence than the legacy FSM harness currently provides. The RD is preserved as valid; SP-08 exercises its core claim.

---

## RD Coverage Matrix

| RD | Coverage | Exercised By |
|---|---|---|
| RD-01 (ActionExecution != ActionEffect) | DIRECTLY_EXERCISED | SP-01 (primary), SP-02 (primary), SP-05 (secondary), SP-07 (secondary), SP-08 (secondary), SP-12 (secondary) |
| RD-02 (WorkDispatched != WorkCompleted) | DIRECTLY_EXERCISED | SP-03 (primary), SP-13 (secondary) |
| RD-03 (ConstraintDeclared != ConstraintEnforced) | DIRECTLY_EXERCISED | SP-04 (primary) |
| RD-04 (ObservationFailed != ContentExhausted) | DIRECTLY_EXERCISED | SP-05 (primary), SP-06 (primary) |
| RD-05 (ElementPresence != ElementNavigability) | DIRECTLY_EXERCISED | SP-07 (primary), SP-02 (secondary) |
| RD-06 (RecoveryAction != ErrorStateReset) | DIRECTLY_EXERCISED | SP-08 (primary) |
| RD-07 (TaskIntent != ExecutionMethod) | DIRECTLY_EXERCISED | SP-09 (primary), SP-11 (secondary) |
| RD-08 (RawPageEvidence != SemanticPageIdentity) | DIRECTLY_EXERCISED | SP-10 (primary), SP-13 (secondary) |
| RD-09 (PreviouslyVisited != Unexplored) | DIRECTLY_EXERCISED | SP-13 (primary), SP-03 (secondary), SP-10 (secondary) |
| RD-10 (GoalExpression != GoalState) | DIRECTLY_EXERCISED | SP-11 (primary) |
| RD-11 (PlanConstructed != ExecutionGuaranteed) | DIRECTLY_EXERCISED | SP-12 (primary), SP-01 (secondary), SP-09 (secondary) |

All 11 RDs have at least one scenario where they are the primary distinction. No RD is left without DIRECTLY_EXERCISED coverage.

---

## Scenario Deduplication

Two candidate scenarios were considered and merged:

| Candidate | Merged Into | Reason |
|---|---|---|
| "Search box misclassification → stuck" (from E-10-C) | SP-07 (ElementPresence != ElementNavigability) | Same primary RD (RD-05), same required behavior (element type must inform navigability). The search-box case is one of three variants in SP-07. |
| "Hub page revisited after first branch" (from E-07 revisit angle) | SP-13 (Revisiting a Page Must Not Reset Exploration State) | SP-03 already covers the multi-branch completion pressure. SP-13 covers the revisit-without-reset pressure. The hub-revisit case is an instance of SP-13. |

No other candidates were merged — the remaining scenarios exercise materially different world conditions, required behaviors, or pass/fail oracles.

---

## Portfolio Priority

### P0 (Strongest evidence, safety-critical false-success prevention)

| SP | Title | Why P0 |
|---|---|---|
| SP-01 | Entry Action Must Verify World Effect | Wrong-app traversal is a safety boundary; legacy fake-success gap is P0 severity |
| SP-03 | Multi-Branch Hub Must Not Report Complete With Unvisited Branch | Unfixed production bug; strongest executable regression evidence in corpus |
| SP-04 | Declared Depth Bound Must Be Enforced During Discovery | Permanent regression from real bug; real runs confirmed depth=4 violation |
| SP-05 | Observation Failure Must Not Become Content Exhaustion | Safety-critical: false end-of-list → premature termination → unvisited content; legacy gap is P0 severity |

### P1 (Strong evidence, behavioral correctness)

| SP | Title | Why P1 |
|---|---|---|
| SP-02 | Navigation Action Must Verify Page Change | Strong regression evidence; prevents stale-click loops |
| SP-06 | Unchanging Content Must Not Loop Forever | Strong regression evidence; prevents step-budget exhaustion |
| SP-07 | Element Visibility Must Not Imply Navigability | Real-run replay evidence; prevents stuck states from misclassification |
| SP-08 | Recovery Attempt Must Not Imply Error Resolution | Strong FSM regression evidence; prevents infinite error-recovery loops |
| SP-10 | Same Logical Page Must Be Recognized Across Observations | Real-run replay evidence; prevents revisit loops |
| SP-13 | Revisiting a Page Must Not Reset Exploration State | Executable regression + recorded reality; prevents both regeneration loops and premature completion |

### P2 (Medium confidence, structural/design pressure)

| SP | Title | Why P2 |
|---|---|---|
| SP-09 | Same Intent, Different Execution Methods | MEDIUM confidence RD; structural evidence from two-mode plan construction |
| SP-11 | Goal Satisfaction Without Execution | MEDIUM confidence RD; structural evidence from stateless goal design |
| SP-12 | Plan Validity Must Not Imply Execution Success | MEDIUM confidence RD; structural evidence from plan validation vs execution gap |

---

## Scenario Quality Gate

All 13 scenarios verified against the 10-point quality gate:

| Gate | SP-01 | SP-02 | SP-03 | SP-04 | SP-05 | SP-06 | SP-07 | SP-08 | SP-09 | SP-10 | SP-11 | SP-12 | SP-13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1. Concrete world/task | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 2. Primary RD exercised | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 3. No architecture in G/W/T | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 4. Externally observable | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 5. Pass oracle not status-flag-only | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 6. Fail oracle evidence-backed | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 7. No legacy FSM/DFS/Stack | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 8. Radically different impl OK | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 9. Evidence-supported semantics | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 10. Clear "what this proves" | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

All 13 scenarios pass all 10 quality gates.

---

## Readiness

**SCENARIO_PRESSURE_PORTFOLIO_READY_FOR_SPEC_ARCHITECTURE_CHALLENGE**

13 Scenario Pressures formulated from 11 Reality Distinctions. All RDs directly exercised. 5 negative controls included. 10-point quality gate passed by all scenarios. Portfolio prioritized P0–P2. No current-Runtime mapping performed.

---

## Repository Changes

`docs/decisions/legacy-scenario-pressure-step5.md` ONLY
