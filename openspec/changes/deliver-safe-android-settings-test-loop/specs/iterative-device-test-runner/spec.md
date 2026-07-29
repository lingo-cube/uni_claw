## ADDED Requirements

### Requirement: Host exposes explicit device test commands

The system SHALL provide a runnable Host with `doctor`, `analyze`, and `run --scenario` commands. `doctor` SHALL verify the selected ADB serial, boot completion, screenshot capture, UIAutomator output, and required provider configuration. `analyze` SHALL produce a page analysis without sending device actions. `run` SHALL validate the scenario and dependencies before beginning execution.

#### Scenario: Doctor reports a ready device
- **WHEN** the selected emulator is booted, screenshot/UIAutomator probes succeed, and required configuration is present
- **THEN** `doctor` exits successfully and reports the selected serial and completed probes

#### Scenario: Analyze is read-only
- **WHEN** `analyze` captures and analyzes the current Settings page
- **THEN** it persists or prints a non-empty analysis and sends no click, scroll, text, back, or launch action

#### Scenario: Invalid run input fails before action
- **WHEN** `run` receives an invalid scenario, unavailable serial, or missing provider capability
- **THEN** it exits non-zero with a classified preparation error before sending a scenario action

### Requirement: Each step follows observe-plan-gate-execute-verify order

For every scenario step, the runner SHALL capture pre-action screenshot and UI hierarchy, produce a normalized page analysis, generate a plan containing at most one candidate device action and expected outcome, obtain a deterministic safety decision, optionally execute the allowed action, capture post-decision evidence, and verify the observed outcome. The runner MUST NOT execute an action before its plan and safety decision are persisted.

#### Scenario: Allowed navigation executes in order
- **WHEN** page analysis identifies the requested safe navigation row and the safety gate allows its click
- **THEN** the step assets show observation before plan, plan before allow decision, allow decision before ADB action, and post-action verification after execution

#### Scenario: Denied action has no execution
- **WHEN** a generated candidate action is denied
- **THEN** the step records the denial and post-decision state without invoking the device action executor

### Requirement: Planning is incremental and state-aware

The runner SHALL compile the scenario into a bounded traversal intent at run start and SHALL generate each device-action plan from the most recent page analysis, traversal state, visited-entry state, and remaining budgets. A previously generated step plan MUST NOT be reused after the observed page identity changes.

#### Scenario: Page changes after navigation
- **WHEN** post-action verification observes a new page identity
- **THEN** the next candidate action is generated from the new analysis and not copied from the previous page's plan

#### Scenario: Stale plan is detected
- **WHEN** the current page fingerprint differs from the fingerprint referenced by a pending step plan
- **THEN** the runner rejects that plan, records a stale-plan issue, and re-observes before any action

### Requirement: Device and provider failures remain distinguishable

The runner SHALL preserve separate failure classifications for device unavailable, ADB timeout, screenshot failure, UI hierarchy failure, provider timeout, provider response invalid, planning failure, safety blocked, action failure, verification failure, budget exhausted, cancellation, and trace/reporting failure. A lower-layer failure MUST NOT be converted into successful completion, no-scroll, or end-of-list.

#### Scenario: ADB disconnect occurs during scroll state evaluation
- **WHEN** the selected device disconnects while determining scroll progress
- **THEN** the run records a device/ADB failure and MUST NOT mark the list complete

#### Scenario: Provider response is invalid
- **WHEN** page analysis cannot validate the provider response
- **THEN** the run records a provider-response failure with evidence and sends no candidate action from that response

### Requirement: Cancellation and cleanup are deterministic

The runner SHALL accept cancellation, stop scheduling new steps, cancel in-flight model and ADB operations, close the trace session, write a cancelled final result, and release resources it owns. It MUST NOT stop an emulator process that was already running before the run.

#### Scenario: User cancels an active run
- **WHEN** Ctrl+C is received during page analysis or action execution
- **THEN** the run terminates within the configured cancellation timeout, writes a cancelled result, closes trace output, and leaves no owned ADB child process running

### Requirement: Repeated runs are isolated and serial on one device

The Host SHALL support a positive repeat count. Repeated runs targeting one serial SHALL execute serially, assign distinct run IDs and output directories, invoke reset before every run, and generate one aggregate iteration result. Failure of one child run SHALL be recorded without overwriting another child run's assets.

#### Scenario: Ten repeated runs complete
- **WHEN** `run --repeat 10` targets one emulator
- **THEN** ten independently addressable run directories and one aggregate result are produced in serial execution order

#### Scenario: Middle iteration fails
- **WHEN** iteration 4 fails during verification and the configured policy allows remaining iterations
- **THEN** iteration 4 retains its failure assets, later iterations use new run IDs, and the aggregate reports the failed position

### Requirement: Host preserves project layer boundaries

The Host SHALL act as the composition root for Core, Device, and provider implementations. Core MUST NOT reference the Host, Device, a concrete provider, the Android SDK, or process-launch details as a result of this capability.

#### Scenario: Dependency direction is inspected
- **WHEN** project references and namespaces are checked
- **THEN** Host may reference Core/Device/providers, Device may implement Core abstractions, and Core has no reverse reference to Host/Device/concrete providers
