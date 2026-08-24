## Purpose

Defines a Harness-owned capture lifecycle that records public run evidence without changing Runtime semantics, dispatch, recovery, or completion authority.

## ADDED Requirements

### Requirement: Capture remains outside Runtime semantic ownership
Trace capture SHALL be owned by Harness composition outside Agent, Container, and Traversal. Runtime semantic contracts and `IEnvironment` SHALL remain unchanged.

#### Scenario: Harness wrapper observes an existing run
- **WHEN** a capture-enabled run executes through the existing environment boundary
- **THEN** Harness records public inputs and outputs without adding a Runtime capture dependency

### Requirement: Capture order and correlation remain honest
Captured observations, actions, and results SHALL retain deterministic external-call order and explicit correlation where available. Missing correlation or provenance MUST remain absent rather than inferred.

#### Scenario: OFF-to-ON evidence is captured
- **WHEN** an OFF-to-ON run dispatches one action and receives a fresh post-action observation
- **THEN** the capture records the action, result, and observation in actual call order with available identifiers and sequence numbers

### Requirement: Capture failure is isolated from Runtime behavior
Capture or artifact failure SHALL NOT change Runtime outcome, GoalEvidence, dispatch count, retry, or recovery behavior. Runtime failure and capture failure SHALL be reported independently.

#### Scenario: Capture store fails while Runtime completes
- **WHEN** capture finalization or persistence fails after a Runtime run
- **THEN** the original Runtime outcome and dispatch count remain unchanged and the capture is not catalog-visible

#### Scenario: Failed Runtime run has complete capture evidence
- **WHEN** Runtime terminates with a failure but all requested capture artifacts are valid
- **THEN** Harness may persist a valid failed-run capture without converting the Runtime result
