# Capability: Scroll-Aware Traversal

TraversalEngine 滚动感知遍历能力，支持 DynamicMatch 在子节点耗尽时触发滚动访问更多内容。

## Requirements

### Requirement: System shall provide scroll-aware traversal for DynamicMatch nodes

The system SHALL support scroll-aware traversal where DynamicMatch node generation considers scrollable content beyond the initial viewport. When all visible children have been visited and additional content exists via scrolling, the system SHALL trigger scroll operations to access subsequent content segments.

#### Scenario: DynamicMatch triggers scroll when visible children exhausted
- **WHEN** a DynamicMatch node has visited all visible children (from initial viewport)
- **AND** the current page has scrollable content (HasScroll = true)
- **AND** the end of list has not been reached (IsEndOfList = false)
- **THEN** the system SHALL trigger a scroll decision to access more content

#### Scenario: Traversal completes when all scroll segments exhausted
- **WHEN** a DynamicMatch node has visited all visible children
- **AND** the current page has scrollable content (HasScroll = true)
- **AND** the end of list has been reached (IsEndOfList = true)
- **THEN** the system SHALL complete the current node without triggering additional scrolls

#### Scenario: Non-scrollable pages bypass scroll check
- **WHEN** a DynamicMatch node has visited all visible children
- **AND** the current page has no scrollable content (HasScroll = false)
- **THEN** the system SHALL complete the current node immediately (no scroll check)

### Requirement: IVisionProvider shall expose scroll state query methods

The system SHALL extend IVisionProvider with three scroll-aware query methods: `HasScroll()` to check if current page has scrollable content, `GetScrollProgress()` to retrieve current scroll progress (0.0-1.0), and `IsEndOfList()` to check if traversal has reached the end of scrollable content.

#### Scenario: Scrollable page reports HasScroll = true
- **WHEN** IVisionProvider.HasScroll() is called on a page with scrollable content
- **THEN** the method SHALL return true

#### Scenario: Scrollable page reports current progress
- **WHEN** IVisionProvider.GetScrollProgress() is called on a scrollable page
- **THEN** the method SHALL return a value between 0.0 and 1.0 representing current scroll position

#### Scenario: End of scrollable content detection
- **WHEN** IVisionProvider.IsEndOfList() is called on a scrollable page
- **AND** scroll progress has reached the maximum threshold
- **THEN** the method SHALL return true

#### Scenario: Non-scrollable page defaults
- **WHEN** IVisionProvider scroll methods are called on a non-scrollable page
- **THEN** HasScroll() returns false, GetScrollProgress() returns 0.0, IsEndOfList() returns true

### Requirement: TraversalFSM shall include ScrollCheck state for scroll decision points

The system SHALL include a `ScrollCheck` state in TraversalFSM that serves as a decision point for whether to trigger scrolling, continue to next node, or complete current node traversal.

#### Scenario: FSM transitions to ScrollCheck when scroll conditions met
- **WHEN** all visible children of a DynamicMatch node have been visited
- **AND** scrollable content exists
- **THEN** TraversalFSM transitions to ScrollCheck state

#### Scenario: ScrollCheck triggers scroll action when more content exists
- **WHEN** TraversalFSM is in ScrollCheck state
- **AND** IsEndOfList = false
- **THEN** FSM triggers scroll action and transitions to ActionExecute state

#### Scenario: ScrollCheck completes node when at end of list
- **WHEN** TraversalFSM is in ScrollCheck state
- **AND** IsEndOfList = true
- **THEN** FSM completes current node and transitions to Container state

### Requirement: TraversalRuntimeContext shall maintain scroll progress state

The system SHALL maintain current scroll progress in TraversalRuntimeContext, updated after each scroll action, and accessible via `CurrentScrollProgress` property and `HasScrollableContent` indicator.

#### Scenario: Scroll progress updates after scroll action
- **WHEN** a scroll action is executed successfully
- **THEN** TraversalRuntimeContext.CurrentScrollProgress is updated to the new progress value

#### Scenario: Scrollable content indicator reflects page state
- **WHEN** TraversalRuntimeContext.HasScrollableContent is queried
- **THEN** it returns true if current page has scroll data, false otherwise

#### Scenario: Scroll progress initializes to zero
- **WHEN** TraversalRuntimeContext is initialized for a new traversal
- **THEN** CurrentScrollProgress defaults to 0.0

### Requirement: StepOrchestrator shall integrate ScrollHandler for scroll decisions

The system SHALL integrate ScrollHandler into StepOrchestrator's execution flow, delegating scroll decisions to ScrollHandler when scroll conditions are met, and executing scroll actions through IActionExecutor.

#### Scenario: ScrollHandler decision triggered on exhausted visible children
- **WHEN** StepOrchestrator detects all visible children visited AND HasScroll=true AND IsEndOfList=false
- **THEN** StepOrchestrator delegates to ScrollHandler.DecideScroll() to determine scroll action

#### Scenario: Scroll action executed through IActionExecutor
- **WHEN** ScrollHandler returns a scroll decision (ScrollDown/ScrollUp)
- **THEN** StepOrchestrator executes the scroll action and updates TraversalRuntimeContext scroll progress

#### Scenario: Non-scrollable pages skip scroll decision
- **WHEN** StepOrchestrator detects HasScroll=false
- **THEN** scroll decision is skipped and normal execution continues

### Requirement: ExitCondition shall support AllChildrenVisitedOrScrollEnd type

The system SHALL support an `AllChildrenVisitedOrScrollEnd` ExitConditionType that completes node traversal when either all children have been visited OR end of scrollable content has been reached.

#### Scenario: ExitCondition satisfied when children exhausted
- **WHEN** ExitCondition type is AllChildrenVisitedOrScrollEnd
- **AND** all children (visible and scrolled) have been visited
- **THEN** the condition is satisfied and node completes

#### Scenario: ExitCondition satisfied at scroll end
- **WHEN** ExitCondition type is AllChildrenVisitedOrScrollEnd
- **AND** IsEndOfList = true (even if not all items visited)
- **THEN** the condition is satisfied and node completes

#### Scenario: Backward compatibility with AllChildrenVisited
- **WHEN** ExitCondition type is AllChildrenVisited (existing type)
- **THEN** behavior is unchanged (only checks child visitation, ignores scroll state)

### Requirement: Scroll state shall be consistent across page transitions

The system SHALL maintain independent scroll state for each page, preserving scroll progress during page navigation away from and back to scrollable pages.

#### Scenario: Scroll progress preserved during page navigation
- **WHEN** user scrolls network_list to 50% progress, navigates to app_list, then returns to network_list
- **THEN** network_list retains 50% scroll progress

#### Scenario: Each page maintains independent scroll state
- **WHEN** multiple pages have scrollable content (network_list, app_list, perm_list)
- **THEN** each page maintains its own independent scroll progress and end-of-list state

### Requirement: DynamicMatch shall traverse all sibling navigation children

A DynamicMatch parent node whose current page exposes multiple navigation children (matched elements whose `ExpectedAction` is `Navigate` / `ExpectsPageChange` is true) SHALL have every sibling navigation child entered — not only the first. For each navigation child, the engine SHALL navigate into its destination sub-page, traverse that sub-page fully (including its own scroll-to-end via the action+judgment loop), then return to the parent's original page and continue selecting remaining unvisited sibling children. This SHALL hold for arbitrarily deep navigation trees (a navigation child's sub-page may itself contain further navigation children, restored layer-by-layer via PressBack).

#### Scenario: Two sibling navigation branches both traversed
- **WHEN** a DynamicMatch parent page has two navigation buttons `to_A` (→ page listA) and `to_B` (→ page listB), each backed by scrollable content
- **THEN** the engine SHALL visit every item of listA AND every item of listB
- **AND** the traversal SHALL execute a PressBack returning from listA to the parent page before entering listB

#### Scenario: Deep navigation tree is fully covered
- **WHEN** navigation chains root → page1 → page2, where each page has scrollable content
- **THEN** the engine SHALL traverse page1's items AND page2's items
- **AND** SHALL PressBack from page2 to page1, then from page1 to root, restoring each parent page before regenerating its remaining children

### Requirement: Navigation children shall push a sub-page frame attributed to the navigation child

When a DynamicMatch child whose matched element has `ExpectedAction == Navigate` is executed and the page changes, the engine SHALL push a DynamicMatch sub-page frame whose children are generated from the navigation destination page and attributed to that navigation child's NodeId (not to the root). The engine SHALL distinguish navigation-caused page changes (which require a sub-page frame and page restore on return) from scroll-caused page-item changes (which require child regeneration) using the matched element's navigation metadata, NOT by inferring from page-fingerprint changes.

#### Scenario: Navigated sub-page items are attributed to the navigation child frame
- **WHEN** a navigation child `to_A` is executed and navigates from hub to listA
- **THEN** listA's generated children SHALL belong to `to_A`'s sub-page frame (parent NodeId = the navigation child), not to the root frame
- **AND** exhausting listA SHALL pop `to_A`'s frame (at depth ≥ 2) and trigger PressBack to hub before the root regenerates hub's children

#### Scenario: Scroll page-item change still regenerates (not framed)
- **WHEN** a scroll reveals new items on the same page (no navigation)
- **THEN** the engine SHALL regenerate children from the new page content via the action+judgment loop (existing behavior), NOT push a sub-page frame or PressBack

#### Scenario: False navigation falls back to leaf
- **WHEN** a child is marked navigation (`ExpectedAction.Navigate`) but its execution does NOT change the page fingerprint
- **THEN** the engine SHALL treat it as an ordinary leaf child (no sub-page frame is pushed)

### Requirement: all_visited completion shall require all navigation branches

A DynamicMatch node SHALL report completion (`all_visited`) only after every sibling navigation child has been entered and its destination sub-page fully traversed. `VisitedNodes` SHALL deduplicate navigation children across frames so each navigation child is counted as visited exactly once. This corrects the prior behavior where un-regenerated sibling navigation children made `all_visited` trivially true despite incomplete coverage.

#### Scenario: all_visited false until sibling branch entered
- **WHEN** a DynamicMatch parent has navigation children `to_A` and `to_B`, and only `to_A`'s sub-page has been traversed
- **THEN** the parent SHALL NOT be considered `all_visited`
- **AND** after `to_B`'s sub-page is also traversed, the parent SHALL be `all_visited`

#### Scenario: Navigation child counted once across frames
- **WHEN** a navigation child is visited, then its sub-page frame is popped and the parent page restored
- **THEN** the navigation child SHALL appear in `VisitedNodes` exactly once
- **AND** re-generation of the parent's children SHALL mark it visited (not re-entered)
