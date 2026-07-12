# Navigation Context Specification

## ADDED Requirements

### Requirement: Navigation context tracks DFS traversal state
The system SHALL provide a `NavigationContext` class that encapsulates all DFS traversal state including node stack, current path, page identity, and visited tracking.

#### Scenario: Navigation context initialization
- **WHEN** a `NavigationContext` is created with traceId, maxDepth, and optional nodeStack
- **THEN** the context initializes with empty collections for path, visited sets, and a valid node stack

#### Scenario: Node stack access
- **WHEN** consumer accesses `Navigation.NodeStack`
- **THEN** system returns the `INodeStack` interface for DFS stack operations

### Requirement: Current path tracking
The system SHALL track the current DFS traversal path as a read-only list of page identifiers.

#### Scenario: Path append on forward movement
- **WHEN** DFS moves to a child page via `Navigation.AppendPath(pageId)`
- **THEN** the pageId is added to the end of CurrentPath

#### Scenario: Path pop on backtrack
- **WHEN** DFS backtracks via `Navigation.PopPath()`
- **THEN** the last entry is removed from CurrentPath

#### Scenario: Read-only path access
- **WHEN** consumer accesses `Navigation.CurrentPath`
- **THEN** system returns `IReadOnlyList<string>` that cannot be modified through the interface

### Requirement: Visited tracking for anti-loop
The system SHALL maintain separate visited sets for pages, nodes, and per-node children to prevent DFS loops.

#### Scenario: Mark page as visited
- **WHEN** a page is visited via `Navigation.MarkVisited(pageFingerprint)`
- **THEN** the fingerprint is added to VisitedPages set

#### Scenario: Mark node as visited
- **WHEN** a node is visited via `Navigation.MarkNodeVisited(nodeId)`
- **THEN** the nodeId is added to VisitedNodes set

#### Scenario: Track visited children per node
- **WHEN** a child is visited via `Navigation.AddVisitedChild(parentId, childId)`
- **THEN** the childId is added to the parentId's visited children set

#### Scenario: Read-only visited access
- **WHEN** consumer accesses `Navigation.VisitedPages`, `Navigation.VisitedNodes`, or `Navigation.VisitedChildren`
- **THEN** system returns read-only interfaces that cannot be modified through the interface

### Requirement: Page identity for revisit detection
The system SHALL maintain current page analysis and fingerprint for detecting page revisits.

#### Scenario: Set current page analysis
- **WHEN** vision analysis completes via `Navigation.SetCurrentPageAnalysis(analysis)`
- **THEN** CurrentPageAnalysis is updated with the new analysis

#### Scenario: Set current fingerprint
- **WHEN** fingerprint is computed via `Navigation.SetCurrentFingerprint(fingerprint)`
- **THEN** CurrentFingerprint is updated with the new fingerprint

#### Scenario: Read-only page identity access
- **WHEN** consumer accesses `Navigation.CurrentPageAnalysis` or `Navigation.CurrentFingerprint`
- **THEN** system returns the current value (nullable)

### Requirement: Menu visited tracking for DFS decisions
The system SHALL maintain separate visited sets for level 1 and level 2 menus to support DynamicMatch DFS decisions.

#### Scenario: Access menu visited sets
- **WHEN** consumer accesses `Navigation.VisitedLevel1Menus` or `Navigation.VisitedLevel2Menus`
- **THEN** system returns `HashSet<string>` with engine-internal mutability

### Requirement: Page tree for dynamic child enumeration
The system SHALL maintain a ContentNode tree structure for dynamic child enumeration.

#### Scenario: Set page tree
- **WHEN** page tree is built via `Navigation.SetPageTree(tree)`
- **THEN** PageTree is updated with the new ContentNode

#### Scenario: Access page tree
- **WHEN** consumer accesses `Navigation.PageTree`
- **THEN** system returns the current ContentNode (nullable)

### Requirement: Current frame tracking
The system SHALL track the current navigation position (stack top) as CurrentFrame.

#### Scenario: Current frame reflects stack top
- **WHEN** node stack changes (push/pop)
- **THEN** CurrentFrame property reflects the new stack top or null if empty

#### Scenario: Set current frame explicitly
- **WHEN** FSM sets `Navigation.CurrentFrame` property
- **THEN** the property updates to the new ITraversalNode value

### Requirement: Read-only interface isolation
The system SHALL provide `INavigationContext` interface with only read-only property getters.

#### Scenario: Interface exposes no mutation methods
- **WHEN** consumer holds `INavigationContext` reference
- **THEN** only read-only properties are accessible (NodeStack, CurrentPath, VisitedPages, etc.)

#### Scenario: Mutation methods only on concrete class
- **WHEN** consumer needs to mutate state
- **THEN** they must cast to or hold `NavigationContext` concrete class reference
