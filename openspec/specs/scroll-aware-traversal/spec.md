# Capability: Scroll-Aware Traversal

TraversalEngine 滚动感知遍历能力，支持 DynamicMatch 在子节点耗尽时触发滚动访问更多内容。

## ADDED Requirements

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
