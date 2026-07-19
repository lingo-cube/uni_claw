## ADDED Requirements

### Requirement: ContainerHandler is wired into the production traversal path

ContainerHandler SHALL be invoked during live frame completion in the traversal engine, not only in unit tests. The engine SHALL construct a `CompletionContext` from runtime state and call `ContainerHandler.HandleContainer()` at frame-completion decision points. ContainerHandler SHALL be the sole authority for container completion — no other component SHALL directly set `FrameCompleted`.

#### Scenario: ContainerHandler is called during frame completion
- **WHEN** the traversal engine processes a frame completion event (OnFrameComplete hook)
- **THEN** `ContainerHandler.HandleContainer()` is invoked with a `CompletionContext` constructed from runtime state
- **AND** the returned `ContainerActionResult` determines `FrameCompleted`

#### Scenario: ContainerHandler has non-test call sites
- **WHEN** the codebase is searched for `ContainerHandler` references outside test files
- **THEN** at least one production call site exists (in InterceptionHandler or StepOrchestrator)

### Requirement: ContainerActionResult is translated to FrameCompleted

The caller SHALL translate `ContainerActionResult` to frame lifecycle actions: `Back`, `AutoEscape`, `Skip` SHALL set `FrameCompleted = true` (frame will be popped); `Abort` SHALL NOT set `FrameCompleted` (engine enters error/abort path, producing Error reason).

#### Scenario: Back action sets FrameCompleted
- **WHEN** `HandleContainer` returns `ContainerActionResult` with `Action = Back`
- **THEN** the caller sets `FrameCompleted = true`

#### Scenario: Abort action does not set FrameCompleted
- **WHEN** `HandleContainer` returns `ContainerActionResult` with `Action = Abort`
- **THEN** the caller does NOT set `FrameCompleted`; the engine enters error/abort path

## MODIFIED Requirements

### Requirement: CompletionDetector.detect_completion()

ContainerHandler SHALL provide a `CompletionDetector` that determines container traversal completion status through a pure-computation priority chain with no caching.

#### Scenario: TIMEOUT priority — returns BACK with should_backtrack

WHEN `CompletionDetector.detect_completion()` is called on a container context whose elapsed time exceeds the configured timeout threshold
THEN the detector SHALL return a completion result with status `BACK` and flag `should_backtrack = true`
AND this outcome SHALL have the highest priority in the chain (priority 1)

#### Scenario: MAX_DEPTH priority — returns BACK with should_backtrack

WHEN the container context does not exceed the timeout threshold
AND the current traversal depth exceeds the configured maximum depth limit
THEN the detector SHALL return a completion result with status `BACK` and flag `should_backtrack = true`
AND this outcome SHALL have priority 2 in the chain

#### Scenario: No children — returns ALL_VISITED with BACK

WHEN the container context does not exceed timeout or max depth
AND the container has no child nodes to traverse
THEN the detector SHALL return a completion result with status `ALL_VISITED` and flag `BACK`
AND this outcome SHALL have priority 3 in the chain

#### Scenario: All children visited — returns ALL_VISITED; FallbackDecider determines exit action

WHEN the container context does not exceed timeout or max depth
AND the container has children but all are already visited
THEN the detector SHALL return a completion result with status `ALL_VISITED`
AND the exit action (Back/AutoEscape/Skip/Abort) SHALL be determined by `FallbackDecider` based on completion context and `canContinue` flag
AND this outcome SHALL have priority 4 in the chain
AND the detector SHALL NOT read `ExitConditionFallback` — exit-action is internal to FallbackDecider

#### Scenario: Still processing — returns INCOMPLETE

WHEN the container context does not exceed timeout or max depth
AND at least one child node remains unvisited
THEN the detector SHALL return a completion result with status `INCOMPLETE`
AND this outcome SHALL have priority 5 (lowest) in the chain

#### Scenario: Pure computation with no cache

WHEN any call to `CompletionDetector.detect_completion()` is made
THEN the detector SHALL perform a fresh computation from the input context every time
AND SHALL NOT cache or memoize previous results
AND SHALL NOT rely on any mutable internal state between invocations

### Requirement: FallbackDecider.decide_fallback()

ContainerHandler SHALL provide a `FallbackDecider` that selects a fallback action based on container completion status through a pure-computation priority chain with no caching. For AllVisited completion, the default exit action SHALL be `Back`; for nav-subframe containers, the exit action SHALL be `AutoEscape` (detected via context: NodeType or Meta flag, not an ExitCondition field).

#### Scenario: Timeout or max depth — always returns BACK

WHEN `FallbackDecider.decide_fallback()` is called on a container context that triggered a TIMEOUT or MAX_DEPTH completion condition
THEN the decider SHALL always return `FallbackAction.BACK`
AND SHALL NOT consider any other fallback option regardless of suggested action or can_continue flag

#### Scenario: Complete with AllVisited — returns Back by default

WHEN the container context has a completion status of `ALL_VISITED` and the node is not a nav-subframe
THEN the decider SHALL return `FallbackAction.BACK`

#### Scenario: Nav-subframe AllVisited — returns AutoEscape

WHEN the container context has a completion status of `ALL_VISITED` and the node is detected as a nav-subframe (via NodeType or Meta flag)
THEN the decider SHALL return `FallbackAction.AUTO_ESCAPE`

#### Scenario: Cannot continue — returns BACK

WHEN the container context has a completion status other than TIMEOUT/MAX_DEPTH/ALL_VISITED
AND the `can_continue` flag is false
THEN the decider SHALL return `FallbackAction.BACK`
AND SHALL NOT attempt any other fallback strategy

#### Scenario: Incomplete and can continue — returns SKIP

WHEN the container context has a completion status of `INCOMPLETE`
AND the `can_continue` flag is true
THEN the decider SHALL return `FallbackAction.SKIP`
AND SHALL NOT default to BACK when traversal can still proceed

#### Scenario: Pure computation with no cache

WHEN any call to `FallbackDecider.decide_fallback()` is made
THEN the decider SHALL perform a fresh computation from the input context every time
AND SHALL NOT cache or memoize previous results
AND SHALL NOT rely on any mutable internal state between invocations

## REMOVED Requirements

### Requirement: CompletionContext.ExitConditionFallback field

**Reason**: The `ExitConditionFallback` field on `CompletionContext` was used to pass a plan-influenced exit action through to the completion detector. With ContainerHandler canonicalization, exit-action is now entirely internal to `FallbackDecider` — it decides based on completion status + node context (nav-subframe detection via NodeType/Meta), not a stored field. AllVisited defaults to `Back`; nav-subframe → `AutoEscape`.

**Migration**:
- Remove `ExitConditionFallback` field from `CompletionContext`
- `CompletionDetector` Priority 4 no longer reads `ExitConditionFallback`
- `FallbackDecider` determines exit action from: AllVisited → Back (default), nav-subframe → AutoEscape (context detection)
