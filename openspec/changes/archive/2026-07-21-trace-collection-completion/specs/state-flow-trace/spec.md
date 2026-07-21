## ADDED Requirements

### Requirement: GlobalFSM Context non-null
GlobalFSM transition callbacks SHALL populate TraceContext with NodeId (from current frame), StepNumber, and TraceId (from engine context), with StepSpanId=null.

#### Scenario: GlobalFSM transition trace context
- **WHEN** a GlobalFSM state transition fires (e.g., Traversing→Paused)
- **THEN** the resulting StateTransition.Context has NodeId matching _ctx.CurrentFrame?.NodeId, StepNumber matching _ctx.StepCount,TraceId matching _ctx.TraceId, and StepSpanId=null

#### Scenario: ForceState does NOT trigger trace
- **WHEN** GlobalFSM.ForceState is called for state recovery
- **THEN** no StateTransition record is produced (ForceState semantics: no callback)

### Requirement: GlobalFSM 8-state coverage
GlobalFSM SHALL register state callbacks for all 8 states (excluding terminal states Completed and Terminated which have no outgoing transitions).

#### Scenario: All non-terminal states registered
- **WHEN** GlobalFSM is initialized
- **THEN** callbacks are registered for Idle, Traversing, Pausing, Paused, Resuming, Error (6 of 8 states), and Completed/Terminated are excluded

### Requirement: PageTransition — RunAsync navigation detection
The system SHALL record a PageTransition when RunAsync detects the current page fingerprint differs from the previous step's.

#### Scenario: Page transition detected in run loop
- **WHEN** GetCurrentPageId() returns a different value than lastPageId during the RunAsync loop
- **THEN** RecordPageTransitionAsync is called with fromPage=lastPageId, toPage=currentPageId, transitionType="navigation"

### Requirement: PageTransition — PressBack+Pop
The system SHALL record a PageTransition when InterceptionHandler executes a press_back+pop strategy.

#### Scenario: Page transition on press back
- **WHEN** OnDynamicMatchNodeSelect chooses press_back+pop (fingerprint mismatch)
- **THEN** RecordPageTransitionAsync is called with fromPage=currentFrame.NodeId, toPage=parentNodeId (from NodeStack.Peek()), transitionType="press_back"

### Requirement: PageId on ExecutionRecord
The system SHALL populate ExecutionRecord.PageId from the current step context when recording action executions.

#### Scenario: PageId populated on action execution
- **WHEN** RecordActionExecutionAsync is called during a step with a valid CurrentFrame
- **THEN** the resulting ExecutionRecord.PageId equals _ctx.CurrentFrame.NodeId

#### Scenario: PageId NOT on non-ExecutionRecord types
- **WHEN** StateTransition, ErrorRecord, PageTransition, or AICallRecord are recorded
- **THEN** those records SHALL NOT have PageId set (PageId is ExecutionRecord-only per TraceContext 4-field rule)
