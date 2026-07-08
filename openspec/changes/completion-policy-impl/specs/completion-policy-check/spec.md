## ADDED Requirements

### Requirement: RunAsync checks CompletionPolicy after AntiLoop and before engine MaxSteps

TraversalEngine.RunAsync SHALL check `_ctx.CompletionPolicy` in the step loop, positioned after AntiLoop check and before the engine hard-limit MaxSteps exhaustion. The check SHALL be skipped when `policy == null` or `policy.Type == CompletionPolicyType.None`. Priority order: AllVisited → AntiLoop → CompletionPolicy → MaxSteps(engine) → Cancelled → Error.

#### Scenario: CompletionPolicy check is positioned after AntiLoop
- **WHEN** RunAsync step loop completes a step
- **AND** AllVisited and AntiLoop checks have passed
- **THEN** CompletionPolicy check is evaluated before the loop continues to the next step or MaxSteps exhaustion

#### Scenario: CompletionPolicy null skips check
- **WHEN** `_ctx.CompletionPolicy` is null
- **THEN** no CompletionPolicy check is performed, and traversal proceeds normally via AllVisited or engine MaxSteps

#### Scenario: CompletionPolicyType.None skips check
- **WHEN** `_ctx.CompletionPolicy.Type` is `CompletionPolicyType.None`
- **THEN** no CompletionPolicy check is performed, and traversal proceeds normally

### Requirement: TargetFound checks Operation.Target.Value against policy.TargetName

TargetFound check SHALL match `_ctx.CurrentFrame.Operation?.Target?.Value` against `policy.TargetName`. When `Operation.Target.Value` is null or empty (static/root nodes with NoAction), the check SHALL fallback to `_ctx.CurrentFrame.Name`. MatchMode.Exact SHALL use `string.Equals(matchValue, policy.TargetName, StringComparison.OrdinalIgnoreCase)`. MatchMode.Contains SHALL use `matchValue.Contains(policy.TargetName, StringComparison.OrdinalIgnoreCase)`. On match, RunAsync SHALL return `Done(TraversalResult.Reasons.TargetFound, stepCount, ...)`. TargetFoundAction.ExecuteThenStop SHALL be treated equivalently to MarkAndStop in Phase A (immediate termination without executing the operation first).

#### Scenario: TargetFound exact match on dynamic node
- **WHEN** CurrentFrame is a dynamic node with `Operation.Target.Value = "Dark mode"` and `policy.TargetName = "Dark mode"` with MatchMode.Exact
- **THEN** `string.Equals("Dark mode", "Dark mode", OrdinalIgnoreCase)` returns true, and RunAsync terminates with Reasons.TargetFound

#### Scenario: TargetFound contains match
- **WHEN** CurrentFrame has `Operation.Target.Value = "Bluetooth"` and `policy.TargetName = "Blue"` with MatchMode.Contains
- **THEN** `"Bluetooth".Contains("Blue", OrdinalIgnoreCase)` returns true, and RunAsync terminates with Reasons.TargetFound

#### Scenario: TargetFound fallback to Name when Operation.Target.Value is empty
- **WHEN** CurrentFrame is a static/root node with Operation = NoAction (Target.Value is null/empty) and `policy.TargetName = "Settings App"` with MatchMode.Exact
- **THEN** fallback uses `CurrentFrame.Name = "Settings App"` and match succeeds

#### Scenario: TargetFound no match continues traversal
- **WHEN** CurrentFrame `Operation.Target.Value` does not match `policy.TargetName` in either Exact or Contains mode
- **THEN** traversal continues to the next step without TargetFound termination

#### Scenario: ExecuteThenStop treated as MarkAndStop in Phase A
- **WHEN** `policy.ActionOnFound = TargetFoundAction.ExecuteThenStop`
- **THEN** RunAsync terminates immediately upon match (same behavior as MarkAndStop), without first executing the operation on the matched node

### Requirement: Timeout terminates traversal when elapsed exceeds policy.TimeoutSeconds

Timeout check SHALL compare `stopwatch.Elapsed.TotalSeconds` against `policy.TimeoutSeconds`. When elapsed exceeds the threshold, RunAsync SHALL return `Done(TraversalResult.Reasons.Timeout, stepCount, ...)`. Timeout SHALL use `>` comparison (strictly greater than), not `>=`.

#### Scenario: Timeout triggered
- **WHEN** `stopwatch.Elapsed.TotalSeconds > policy.TimeoutSeconds` (e.g., elapsed = 0.002, TimeoutSeconds = 0.001)
- **THEN** RunAsync terminates with Reasons.Timeout

#### Scenario: Timeout not yet exceeded
- **WHEN** `stopwatch.Elapsed.TotalSeconds <= policy.TimeoutSeconds`
- **THEN** traversal continues to the next step

### Requirement: CompletionPolicy MaxSteps terminates at user-specified soft limit

CompletionPolicy MaxSteps check SHALL compare the current step count against `policy.MaxSteps`. When `i + 1 >= policy.MaxSteps`, RunAsync SHALL return `Done(TraversalResult.Reasons.MaxSteps, stepCount, ...)`. CompletionPolicy.MaxSteps (user soft limit) SHALL take precedence over engine `config.MaxSteps` (hard limit) — if user specifies 50 steps, traversal terminates at 50 even if engine hard limit is 1000.

#### Scenario: CompletionPolicy MaxSteps reached before engine hard limit
- **WHEN** `policy.MaxSteps = 5` and `config.MaxSteps = 1000`
- **AND** step count reaches 5
- **THEN** RunAsync terminates with Reasons.MaxSteps, TotalSteps <= 5

#### Scenario: CompletionPolicy MaxSteps not reached
- **WHEN** step count is less than `policy.MaxSteps`
- **THEN** traversal continues to the next step
