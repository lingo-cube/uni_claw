## MODIFIED Requirements

### Requirement: TraversalEngine.RunAsync executes step loop with termination conditions
RunAsync() SHALL implement the core traversal loop: for each step up to MaxSteps, check CancellationToken, apply DelayPerStepMs if configured, call StepOrchestrator.ExecuteStep(), handle leaf-pop (pop stack when ResultVerify + depth>1 + ChildrenStrategyType.None), handle child-push→NodeSelect transition, record TraceRecord if TraceEnabled, track visited pages, and check termination conditions in priority order: (1) FrameCompleted + depth≤1 → AllVisited, (2) AntiLoopTriggered → AntiLoop, (3) CompletionPolicy checks (TargetFound/Timeout/MaxSteps per completion-policy-check spec), (4) MaxSteps → MaxSteps(engine hard limit). RunAsync() SHALL never throw exceptions to callers — all exceptions SHALL be caught and returned as TraversalResult with Reasons.Error.

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

## ADDED Requirements

### Requirement: TraversalResult.Reasons includes TargetFound and Timeout constants

TraversalResult.Reasons SHALL define `TargetFound = "target_found"` and `Timeout = "timeout"` as additional const string fields alongside existing AllVisited, AntiLoop, MaxSteps, Cancelled, and Error.

#### Scenario: Reasons.TargetFound constant exists
- **WHEN** `TraversalResult.Reasons.TargetFound` is referenced
- **THEN** its value is `"target_found"`

#### Scenario: Reasons.Timeout constant exists
- **WHEN** `TraversalResult.Reasons.Timeout` is referenced
- **THEN** its value is `"timeout"`
