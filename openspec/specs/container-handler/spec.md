# ContainerHandler Spec

> D-3: Container handler — pure computation, no caching

## ADDED Requirements

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

#### Scenario: All children visited — returns ALL_VISITED; exit_condition decides flag

WHEN the container context does not exceed timeout or max depth
AND the container has children but all are already visited
THEN the detector SHALL return a completion result with status `ALL_VISITED`
AND the exit_condition of the container SHALL determine the accompanying flag (exit_condition governs whether traversal continues or terminates)
AND this outcome SHALL have priority 4 in the chain

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

---

### Requirement: FallbackDecider.decide_fallback()

ContainerHandler SHALL provide a `FallbackDecider` that selects a fallback action based on container completion status through a pure-computation priority chain with no caching.

#### Scenario: Timeout or max depth — always returns BACK

WHEN `FallbackDecider.decide_fallback()` is called on a container context that triggered a TIMEOUT or MAX_DEPTH completion condition
THEN the decider SHALL always return `FallbackAction.BACK`
AND SHALL NOT consider any other fallback option regardless of suggested action or can_continue flag

#### Scenario: Complete with suggested action — uses suggested action

WHEN the container context has a completion status of `ALL_VISITED`
AND the completion result includes a suggested fallback action
THEN the decider SHALL return the suggested fallback action
AND SHALL NOT override the suggested action

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

---

### Requirement: ContainerActionExecutor hook dispatch

ContainerHandler SHALL provide a `ContainerActionExecutor` that dispatches container fallback actions through a hook-based dispatch table.

#### Scenario: Hook dispatch table structure

WHEN `ContainerActionExecutor` is initialized
THEN it SHALL contain a dispatch table of type `Dictionary<FallbackAction, Func<ContainerContext, ContainerActionResult>>`
AND the dispatch table SHALL contain exactly 4 hooks for the 4 `FallbackAction` values: BACK, AUTO_ESCAPE, SKIP, ABORT
AND each hook SHALL be a `Func<ContainerContext, ContainerActionResult>` delegate

#### Scenario: BACK hook execution

WHEN `ContainerActionExecutor.execute()` is called with `FallbackAction.BACK`
THEN the executor SHALL invoke the BACK hook delegate from the dispatch table
AND SHALL pass the `ContainerContext` as the sole argument
AND SHALL return the `ContainerActionResult` produced by the hook

#### Scenario: AUTO_ESCAPE hook execution

WHEN `ContainerActionExecutor.execute()` is called with `FallbackAction.AUTO_ESCAPE`
THEN the executor SHALL invoke the AUTO_ESCAPE hook delegate from the dispatch table
AND SHALL pass the `ContainerContext` as the sole argument
AND SHALL return the `ContainerActionResult` produced by the hook

#### Scenario: SKIP hook execution

WHEN `ContainerActionExecutor.execute()` is called with `FallbackAction.SKIP`
THEN the executor SHALL invoke the SKIP hook delegate from the dispatch table
AND SHALL pass the `ContainerContext` as the sole argument
AND SHALL return the `ContainerActionResult` produced by the hook

#### Scenario: ABORT hook execution

WHEN `ContainerActionExecutor.execute()` is called with `FallbackAction.ABORT`
THEN the executor SHALL invoke the ABORT hook delegate from the dispatch table
AND SHALL pass the `ContainerContext` as the sole argument
AND SHALL return the `ContainerActionResult` produced by the hook

#### Scenario: Exception fallback to BACK

WHEN any hook delegate throws an exception during execution
THEN the executor SHALL NOT propagate the exception
AND SHALL fall back to executing the BACK action instead
AND SHALL return the `ContainerActionResult` from the BACK fallback execution

---

### Requirement: ContainerHandler statistics

ContainerHandler SHALL track and report traversal statistics across all handled containers.

#### Scenario: Statistics fields

WHEN `ContainerHandler.statistics` is accessed
THEN it SHALL expose the following fields:
- `processed_count`: total number of containers that entered the handler pipeline
- `completed_count`: number of containers that reached ALL_VISITED completion status
- `action_statistics`: `Dictionary<FallbackAction, int>` counting how many times each fallback action was executed
- `avg_depth`: average traversal depth across all processed containers (computed as processed_count > 0 ? sum_depth / processed_count : 0)
- `completion_rate`: ratio of completed_count to processed_count (computed as processed_count > 0 ? completed_count / processed_count : 0.0)

#### Scenario: Statistics are immutable snapshots

WHEN `ContainerHandler.statistics` is read
THEN the returned statistics object SHALL be an immutable snapshot at the point of query
AND subsequent handler activity SHALL NOT mutate the previously returned snapshot
AND each read SHALL produce a new snapshot reflecting the current state
