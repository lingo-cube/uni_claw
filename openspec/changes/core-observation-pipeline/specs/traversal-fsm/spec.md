## MODIFIED Requirements

### Requirement: PreconditionCheck performs a real check
The `HandlePreconditionCheckAsync` handler SHALL optionally invoke an `IPreconditionChecker` to verify the current page is valid for the step, instead of unconditionally passing.

#### Scenario: Precondition check passes
- **WHEN** `IPreconditionChecker` is provided AND `CheckAsync` returns true
- **THEN** the FSM SHALL transition to Execute

#### Scenario: Precondition check fails
- **WHEN** `IPreconditionChecker` is provided AND `CheckAsync` returns false
- **THEN** the FSM SHALL transition to ErrorHandling

#### Scenario: No precondition checker configured
- **WHEN** `IPreconditionChecker` is null
- **THEN** `HandlePreconditionCheckAsync` SHALL behave as before (stub, always pass)

### Requirement: ErrorHandling PressBack gate uses same-page item limit
In addition to consecutive error count, `HandleErrorHandlingAsync` SHALL track the number of distinct items that have failed on the current page and trigger PressBack when a configurable limit is exceeded.

#### Scenario: Item limit exceeded
- **WHEN** `BackOnPageItemLimit` is 5 AND 5 distinct items on the current page have each caused at least one error
- **THEN** `HandleErrorHandlingAsync` SHALL call PressBack and transition to FrameComplete

#### Scenario: Navigating away resets the counter
- **WHEN** the FSM navigates to a different page (PageIdentity changes)
- **THEN** the per-page item failure counter SHALL reset

### Requirement: Back navigation after scroll exhaustion is not an error
When a scroll action is verified and the page fingerprint did not change, the `VerifyScroll` method SHALL return "success" in enumerate mode.

#### Scenario: Scroll exhausted in enumerate
- **WHEN** `EnumerateScenarioRunner.VerifyScroll` is called AND the page fingerprint is unchanged AND the current runner mode is enumerate
- **THEN** the verification SHALL return "success" with reason "scroll_exhausted_no_change"
