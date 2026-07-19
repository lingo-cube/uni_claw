## ADDED Requirements

### Requirement: TraversalResult.Reason SHALL use four-tier classification

`TraversalResult.Reason` SHALL classify completion reasons into four tiers: **Achieved** (AllVisited, TargetFound — normal completeness proof), **Constraint-pruned** (MaxSteps, Timeout — scoped: over-cap/budget elements out-of-scope), **Anomaly** (AntiLoop, Error — hard failure, completeness not claimed), and **External** (Cancelled — user abort). The invariant SHALL be: anomaly-tier reasons MUST NEVER masquerade as AllVisited or any achieved-tier reason.

#### Scenario: AllVisited reason is classified as Achieved tier
- **WHEN** traversal completes with `CompletionReason = "all_visited"`
- **THEN** `TraversalResult.Reason` is classified in the Achieved tier and `Success = true`

#### Scenario: TargetFound reason is classified as Achieved tier
- **WHEN** traversal completes with `CompletionReason = "target_found"`
- **THEN** `TraversalResult.Reason` is classified in the Achieved tier and `Success = true`

#### Scenario: MaxSteps reason is classified as Constraint-pruned tier
- **WHEN** traversal completes with `CompletionReason = "max_steps"`
- **THEN** `TraversalResult.Reason` is classified in the Constraint-pruned tier

#### Scenario: Timeout reason is classified as Constraint-pruned tier
- **WHEN** traversal completes with `CompletionReason = "timeout"`
- **THEN** `TraversalResult.Reason` is classified in the Constraint-pruned tier

#### Scenario: AntiLoop reason is classified as Anomaly tier
- **WHEN** traversal completes with `CompletionReason = "anti_loop"`
- **THEN** `TraversalResult.Reason` is classified in the Anomaly tier

#### Scenario: Error reason is classified as Anomaly tier
- **WHEN** traversal completes with `CompletionReason = "error"`
- **THEN** `TraversalResult.Reason` is classified in the Anomaly tier

#### Scenario: Cancelled reason is classified as External tier
- **WHEN** traversal is cancelled by the user (CancellationToken)
- **THEN** `TraversalResult.Reason = "cancelled"` is classified in the External tier

#### Scenario: Anomaly never masquerades as AllVisited
- **WHEN** traversal completes with an anomaly-tier reason (AntiLoop or Error)
- **THEN** `TraversalResult.Success` MUST be `false` and `TraversalResult.Reason` MUST NOT be `"all_visited"`

## MODIFIED Requirements

### Requirement: TraversalEngine.RunAsync executes step loop with async orchestrator

RunAsync() SHALL implement the core traversal loop: for each step up to MaxSteps, check CancellationToken, apply DelayPerStepMs if configured, call `await StepOrchestrator.ExecuteStepAsync()`, handle leaf-pop, handle child-push→NodeSelect transition, record TraceRecord if TraceEnabled, track visited pages, and check termination conditions. RunAsync() SHALL await all async operations without `.GetAwaiter().GetResult()`. RunAsync() SHALL never throw exceptions to callers — all exceptions SHALL be caught and returned as TraversalResult with Reasons.Error.

The engine SHALL use `Exhaustive` (formerly `None`) as the completion policy type check for exhaustive traversal. The engine SHALL derive `effective_depth = min(config.MaxDepth, plan.IntentSlots.Depth ?? int.MaxValue)` and pass it to `CompletionContext.MaxDepth` for ContainerHandler consumption.

#### Scenario: Successful traversal completes all nodes
- **WHEN** RunAsync() runs and StepOrchestrator.ExecuteStep() returns FrameCompleted with NodeStack.Depth≤1
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="all_visited", GlobalState=Completed

#### Scenario: Anti-loop triggered
- **WHEN** RunAsync() runs and StepOrchestrator.ExecuteStep() returns AntiLoopTriggered=true
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="anti_loop", GlobalState=Completed

#### Scenario: CompletionPolicy TargetFound triggered
- **WHEN** RunAsync() runs and CompletionPolicy TargetFound check matches the current node
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="target_found", GlobalState=Completed

#### Scenario: CompletionPolicy Timeout triggered
- **WHEN** RunAsync() runs and elapsed time exceeds CompletionPolicy.TimeoutSeconds
- **THEN** RunAsync() returns TraversalResult with Success=false, CompletionReason="timeout", GlobalState=Terminated

#### Scenario: CompletionPolicy MaxSteps triggered
- **WHEN** RunAsync() runs and step count reaches CompletionPolicy.MaxSteps before engine hard limit
- **THEN** RunAsync() returns TraversalResult with CompletionReason="max_steps", TotalSteps <= CompletionPolicy.MaxSteps

#### Scenario: Max steps exceeded (engine hard limit)
- **WHEN** RunAsync() reaches config.MaxSteps without completion
- **THEN** RunAsync() returns TraversalResult with Success=false, CompletionReason="max_steps"

#### Scenario: Exception during step execution
- **WHEN** an exception occurs during StepOrchestrator.ExecuteStep()
- **THEN** RunAsync() catches the exception, sets ctx.GlobalState=Error, returns TraversalResult with Success=false, CompletionReason="error", Error=caught exception

#### Scenario: CancellationToken triggered
- **WHEN** CancellationToken is signaled during the loop
- **THEN** RunAsync() catches OperationCanceledException, returns TraversalResult with CompletionReason="cancelled", GlobalState=Terminated

#### Scenario: RunAsync calls ExecuteStepAsync with await
- **WHEN** `RunAsync()` executes a step iteration
- **THEN** `await _orchestrator.ExecuteStepAsync(_stepCtx)` is called
- **AND** no `.GetAwaiter().GetResult()` is present in the step loop body

#### Scenario: Trace records are recorded asynchronously
- **WHEN** `RunAsync()` records trace events (page visits, state decisions, etc.)
- **THEN** trace coordinator methods are awaited

#### Scenario: RunAsync passes ScrollSwipe to StepContext
- **WHEN** `RunAsync()` constructs `StepContext`
- **THEN** `ScrollSwipe` is set to `_config.ScrollSwipe`

#### Scenario: Exhaustive policy check uses renamed enum value
- **WHEN** RunAsync() checks completion policy type for exhaustive traversal
- **THEN** the condition SHALL be `policy.Type != CompletionPolicyType.Exhaustive` (formerly `None`)

#### Scenario: Depth flows from IntentSlots via priority min
- **WHEN** RunAsync() constructs CompletionContext for ContainerHandler
- **THEN** `CompletionContext.MaxDepth` SHALL be `min(config.MaxDepth, plan.IntentSlots.Depth ?? int.MaxValue)`
- **AND** when `IntentSlots.Depth` is null, MaxDepth is governed solely by `config.MaxDepth`

### Requirement: TraversalEngine.Done helper produces TraversalResult with correct GlobalState mapping

Done() SHALL map CompletionReason to GlobalState: AllVisited/AntiLoop/TargetFound → Completed, Cancelled/Timeout → Terminated, Error → Error. Success SHALL be true when reason is AllVisited, AntiLoop, or TargetFound. It SHALL create TraversalResult with all fields populated (Success, CompletionReason, TotalSteps, ElapsedSeconds, ActionHistory from IActionExecutor.GetHistory(), VisitedPages, Trace from TraceRecords, TraceId, FinalState from FSM, Error if present).

#### Scenario: Done with AllVisited reason
- **WHEN** Done() is called with reason "all_visited"
- **THEN** GlobalState is set to Completed, TraversalResult.Success=true

#### Scenario: Done with TargetFound reason
- **WHEN** Done() is called with reason "target_found"
- **THEN** GlobalState is set to Completed, TraversalResult.Success=true

#### Scenario: Done with Timeout reason
- **WHEN** Done() is called with reason "timeout"
- **THEN** GlobalState is set to Terminated, TraversalResult.Success=false

#### Scenario: Done with Error reason
- **WHEN** Done() is called with reason "error" and an Exception
- **THEN** GlobalState is set to Error, TraversalResult.Success=false, TraversalResult.Error=the exception

#### Scenario: Done with Cancelled reason
- **WHEN** Done() is called with reason "cancelled"
- **THEN** GlobalState is set to Terminated, TraversalResult.Success=false
