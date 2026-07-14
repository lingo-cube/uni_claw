## MODIFIED Requirements

### Requirement: System shall provide scroll-aware traversal for DynamicMatch nodes

The system SHALL support scroll-aware traversal where a DynamicMatch node, after exhausting its currently visible unvisited children, executes scroll as an **action followed by screenshot judgment** rather than via a dedicated scroll pipeline. The engine SHALL issue a scroll via `IActionExecutor.SwipeAsync`, re-analyze the page via `IVisionProvider.AnalyzeCurrentPageAsync`, and decide continuation by diffing the newly visible element ids against a per-frame cumulative seen-element set: if previously-unseen elements appear, traversal continues (NodeSelect); otherwise the node is complete. Termination is empirical (a scroll that reveals no unseen element = end of scrollable content) and SHALL NOT depend on a pre-known scroll maximum/progress threshold.

#### Scenario: DynamicMatch triggers scroll when visible children exhausted
- **WHEN** a DynamicMatch node has visited all currently visible children
- **AND** the page is scrollable (HasScroll = true)
- **THEN** the engine SHALL issue a `SwipeAsync` and re-analyze the page to look for new content

#### Scenario: Traversal continues when a scroll reveals unseen elements
- **WHEN** a scroll has been executed and the new `PageAnalysis` contains element ids not in the per-frame seen set
- **THEN** the engine SHALL invalidate the DynamicChildManager cache and continue with NodeSelect

#### Scenario: Traversal completes when a scroll reveals no unseen elements
- **WHEN** a scroll has been executed and every element id in the new `PageAnalysis` is already in the per-frame seen set
- **THEN** the engine SHALL complete the current node (root → FrameComplete; non-root → PressBack + Pop) without further scrolls

#### Scenario: Non-scrollable pages bypass scroll
- **WHEN** a DynamicMatch node has visited all visible children
- **AND** the page has no scrollable content (HasScroll = false)
- **THEN** the system SHALL complete the node immediately without issuing a swipe

### Requirement: TraversalRuntimeContext shall maintain per-frame seen-element state for scroll termination

The system SHALL maintain, in `TraversalRuntimeContext`, a per-frame (per node-visit) cumulative set of seen element ids used by the scroll loop for termination. After each scroll-and-re-analyze, the new element ids SHALL be added to the set; the loop terminates when a scroll contributes no unseen id. This seen-element-set diff SHALL replace the previous progress-range / element-count loop-prevention mechanisms. Lifecycle (clearing on frame pop) is an implementation detail.

#### Scenario: Seen set accumulates across scrolls within a frame
- **WHEN** multiple scrolls occur while traversing one DynamicMatch node
- **THEN** each scroll's newly observed element ids are added to that frame's seen set

#### Scenario: Seen set drives termination
- **WHEN** a scroll's resulting element ids are all already present in the frame's seen set
- **THEN** the scroll loop terminates (no Continue)

### Requirement: StepOrchestrator shall execute scroll as action plus judgment in a single site

The system SHALL integrate scroll handling in `StepOrchestrator` through a single unified `TryHandleScroll` invoked from both the Step 8 (Branch) and Step 9 (NodeSelect) interception points for DynamicMatch-exhausted nodes. `TryHandleScroll` SHALL perform: (1) `IActionExecutor.SwipeAsync`, (2) `IVisionProvider.AnalyzeCurrentPageAsync`, (3) `DynamicChildManager.Invalidate`, (4) seen-set diff to decide Continue vs Stop. StepOrchestrator SHALL NOT delegate scroll decisions to `ScrollHandler` or any 7-step pipeline, and SHALL NOT downcast `IVisionProvider`/`IActionExecutor` to concrete Simulation types.

#### Scenario: Scroll executed as a swipe then re-analysis
- **WHEN** `TryHandleScroll` runs for an exhausted DynamicMatch node on a scrollable page
- **THEN** it calls `SwipeAsync` then `AnalyzeCurrentPageAsync` then `Invalidate`, and returns Continue or Stop based on the seen-set diff

#### Scenario: Single unified site shared by Step 8 and Step 9
- **WHEN** Step 8 (Branch) and Step 9 (NodeSelect) both encounter an exhausted DynamicMatch node
- **THEN** both invoke the same `TryHandleScroll` (no duplicated scroll logic)

#### Scenario: No concrete mock downcast in scroll path
- **WHEN** the `TryHandleScroll` source is scanned
- **THEN** it contains no `is ScrollableMockVisionService` / `is ScrollableMockActionExecutor` type test

## REMOVED Requirements

### Requirement: TraversalFSM shall include ScrollCheck state for scroll decision points

**Reason**: `ScrollCheck` was never added to the `TraversalState` enum — it is locked at 8 values (C-1, H-1 incident), none of which is ScrollCheck. Scroll decisions now live entirely in the engine (`StepOrchestrator.TryHandleScroll`), so no dedicated FSM scroll state is needed. `TraversalFSM.HandleBranch` SHALL return `NodeSelect` for an exhausted DynamicMatch node, leaving scroll to the orchestrator.
**Migration**: Any reference to a `ScrollCheck` state or `TryHandleScroll` on `TraversalFSM` (incl. `_visitedScrollRanges`) is removed; the loop-prevention it provided is replaced by the per-frame seen-element set in `TraversalRuntimeContext`.
