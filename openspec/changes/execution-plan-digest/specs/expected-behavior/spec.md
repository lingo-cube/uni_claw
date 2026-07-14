## ADDED Requirements

### Requirement: ExpectedBehavior SHALL support operation_rules verification dimension

ExpectedBehavior SHALL include an `OperationRules` field of type `OperationRulesExpectation` (sealed record class with `DepthFirstOrder: bool = false` and `NoDuplicateActionsMax: int = 0`). When both fields are at their default values (false/0), the `VerifyOperationRules` method SHALL produce no `RuleResult`. This dimension validates that the traversal engine's action sequence is operationally sound: DFS stack discipline is maintained and no element is repeatedly clicked in a dead loop.

#### Scenario: depth_first_order passes when DFS stack discipline is correct
- **WHEN** `OperationRules.DepthFirstOrder` is true
- **AND** traversal of a hub→listA→(back)→listB→(back) action sequence exhibits correct stack discipline (tap pushes +1, back pops -1, depth never negative, at least one back action exists)
- **THEN** rule `operation_rules:depth_first_order` SHALL pass

#### Scenario: depth_first_order fails when engine never backs (single-branch traversal)
- **WHEN** `OperationRules.DepthFirstOrder` is true
- **AND** the action history contains forward (tap) actions but zero back actions (engine terminates without returning from any branch)
- **THEN** rule `operation_rules:depth_first_order` SHALL fail

#### Scenario: depth_first_order fails on stack underflow (back before forward)
- **WHEN** `OperationRules.DepthFirstOrder` is true
- **AND** the action history contains a back action that occurs before any forward action (stack depth would go negative)
- **THEN** rule `operation_rules:depth_first_order` SHALL fail

#### Scenario: no_duplicate_actions passes when no element exceeds consecutive repeat limit
- **WHEN** `OperationRules.NoDuplicateActionsMax` is 3
- **AND** no element_id appears more than 3 times consecutively in the action history
- **THEN** rule `operation_rules:no_duplicate_actions` SHALL pass

#### Scenario: no_duplicate_actions fails when an element is clicked in a dead loop
- **WHEN** `OperationRules.NoDuplicateActionsMax` is 3
- **AND** element_id "button_x" appears 5 times consecutively in the action history
- **THEN** rule `operation_rules:no_duplicate_actions` SHALL fail with a message identifying the element and its consecutive count

#### Scenario: OperationRules with default values produces no RuleResult
- **WHEN** `OperationRules` is `OperationRulesExpectation()` (DepthFirstOrder=false, NoDuplicateActionsMax=0)
- **THEN** `VerifyOperationRules` SHALL return an empty list (no RuleResult produced)

### Requirement: ExpectedBehavior SHALL support trace_integrity verification dimension

ExpectedBehavior SHALL include a `TraceIntegrity` field of type `TraceIntegrityExpectation` (sealed record class with `RequiredSpanTypes: ImmutableArray<SpanType> = default` and `MinPageTransitions: int = 0`). When both fields are at their default values (empty/0), the `VerifyTraceIntegrity` method SHALL produce no `RuleResult`. This dimension validates that the trace data captured during traversal is complete: expected span types are recorded and page transitions are properly tracked.

#### Scenario: span_types_present passes when required span types exist in trace
- **WHEN** `TraceIntegrity.RequiredSpanTypes` contains `[StateDecision, PageAnalysis, DfsForward, AICall, ErrorHandling]`
- **AND** every specified SpanType appears in at least one `TraceRecord.SpanTypes` array
- **THEN** each SpanType SHALL produce a passing `RuleResult` with rule ID `trace_integrity:span_type:<SpanTypeName>`

#### Scenario: span_types_present fails when a required span type is missing
- **WHEN** `TraceIntegrity.RequiredSpanTypes` contains `[ErrorHandling]`
- **AND** no `TraceRecord` in the trace emits `SpanType.ErrorHandling` (error-free traversal)
- **THEN** rule `trace_integrity:span_type:ErrorHandling` SHALL fail

#### Scenario: page_transitions passes when sufficient transitions are recorded
- **WHEN** `TraceIntegrity.MinPageTransitions` is 10
- **AND** the trace contains at least 10 records where `PageTransitionType` is not null
- **THEN** rule `trace_integrity:page_transitions` SHALL pass

#### Scenario: page_transitions fails when too few transitions are recorded
- **WHEN** `TraceIntegrity.MinPageTransitions` is 10
- **AND** the trace contains only 3 records with non-null `PageTransitionType`
- **THEN** rule `trace_integrity:page_transitions` SHALL fail with actual count in message

#### Scenario: TraceIntegrity with default values produces no RuleResult
- **WHEN** `TraceIntegrity` is `TraceIntegrityExpectation()` (RequiredSpanTypes=empty, MinPageTransitions=0)
- **THEN** `VerifyTraceIntegrity` SHALL return an empty list (no RuleResult produced)

### Requirement: ExpectedBehavior JSON schema SHALL be backward-compatible with new optional keys

The `expected-behavior` JSON schema SHALL accept optional `operationRules` and `traceIntegrity` objects at the top level alongside existing keys (`scenario`, `description`, `completion`, `pageCoverage`, `elementCoverage`, `collisionProof`, `dfsProperties`, `numericAnchor`). When either key is absent from a JSON file, the `FromJson` method SHALL construct the corresponding Expectation with default values (all false/0/empty), ensuring existing baseline JSON files produce identical `VerificationReport` results without modification.

#### Scenario: Existing JSON without new keys deserializes correctly
- **WHEN** a JSON file contains only the original keys (scenario through numericAnchor) without `operationRules` or `traceIntegrity`
- **THEN** `ExpectedBehavior.FromJson` SHALL deserialize it with `OperationRules = OperationRulesExpectation()` and `TraceIntegrity = TraceIntegrityExpectation()`
- **AND** `Verify()` produces the same `AllPassed` result as before the schema extension

#### Scenario: JSON with new keys enables the new verification rules
- **WHEN** a JSON file contains `"operationRules": { "depthFirstOrder": true, "noDuplicateActionsMax": 3 }`
- **THEN** `VerifyOperationRules` SHALL produce RuleResult entries for both rules
