## ADDED Requirements

### Requirement: Engine records destination fingerprint on verified navigation
The traversal engine SHALL maintain a per-parent set of visited destination page fingerprints. On `verification_passed`, the engine SHALL record `(parentNodeId, currentPageFingerprint)` in this set.

#### Scenario: First navigation from parent records destination
- **WHEN** a child node's click results in `verification_passed` and `fromState` is `ResultVerify`
- **THEN** the current page fingerprint is added to the parent's visited destination set

#### Scenario: Empty page is not recorded
- **WHEN** `verification_passed` occurs but `currentPageFingerprint` is 0 (empty/null page)
- **THEN** the fingerprint is NOT recorded and duplicate check is skipped

#### Scenario: Popup-handling path is excluded
- **WHEN** `verification_passed` occurs but `fromState` is NOT `ResultVerify` (e.g., PopupHandling)
- **THEN** the fingerprint is NOT recorded

### Requirement: Engine skips child whose destination was already visited by a sibling
When a child node's navigation results in a page fingerprint already present in the parent's visited destination set, the engine SHALL mark the child node as visited. The engine SHALL NOT pop the stack, SHALL NOT execute PressBack, and SHALL NOT skip step-end cleanup (EndEngineStepSpan, TraceRecord, fromState update).

#### Scenario: Duplicate destination detected
- **WHEN** `verification_passed` occurs, `fromState` is `ResultVerify`, and `currentPageFingerprint` matches an entry in `_childDestinations[parentNodeId]`
- **THEN** the engine records decision `child_destination_duplicate`, marks `stepFrame.NodeId` as visited, and continues the normal step loop

#### Scenario: Different destination is not flagged
- **WHEN** `verification_passed` occurs and `currentPageFingerprint` does NOT exist in `_childDestinations[parentNodeId]`
- **THEN** the fingerprint is added and no duplicate action is taken

### Requirement: Parent node is correctly identified for both container and leaf children
The engine SHALL determine the parent node ID using the pre-step `stepFrame` snapshot: if `stepFrame` still equals the current stack top, the parent is `Peek(1)` (container child, not popped). If `stepFrame` differs from the current stack top, the parent is `Peek()` (leaf child, already popped by RunAsync line 342-357).

#### Scenario: Container child parent detection
- **WHEN** a DynamicMatch container child navigates successfully (ChildrenStrategy is DynamicMatch, not popped by line 351)
- **THEN** `parentNodeId = NodeStack.Peek(1).NodeId`

#### Scenario: Leaf child parent detection
- **WHEN** a leaf child navigates successfully (ChildrenStrategy is None, popped by line 342-357)
- **THEN** `parentNodeId = NodeStack.Peek().NodeId`

### Requirement: Destination set is scoped to a single RunAsync invocation
The destination set SHALL be initialized as an empty dictionary at the start of `RunAsync`. It SHALL NOT persist across runs.

#### Scenario: Cross-run isolation
- **WHEN** `TraversalEngine.RunAsync` is called a second time on the same engine instance
- **THEN** the destination set is re-initialized and contains no entries from the previous run
