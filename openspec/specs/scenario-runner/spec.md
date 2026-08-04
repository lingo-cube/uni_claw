## ADDED Requirements

### Requirement: TraversalEngine is the sole driver of scenario execution

The Host scenario runner SHALL drive device traversal through Core's `TraversalEngine`/`TraversalFSM`. The self-contained observe→plan→gate→execute→verify loop (`ScenarioRunnerBase` and its subclasses `IncrementalScenarioRunner`/`EnumerateScenarioRunner`) SHALL be deleted; no runner loop that re-implements traversal against `IPageAnalyzer` + `IActionExecutor` directly SHALL remain. This supersedes the `host-composition-root` requirement "V1 scenario runner is self-contained."

#### Scenario: No runner loop remains
- **WHEN** the Host source is inspected for a step-execution loop that drives `IPageAnalyzer`/`IActionExecutor` outside `TraversalEngine`
- **THEN** no such loop exists; `ScenarioRunnerBase`, `IncrementalScenarioRunner`, and `EnumerateScenarioRunner` are deleted

#### Scenario: Engine executes on the device path
- **WHEN** a scenario run drives a device
- **THEN** the sequence of actions and state transitions is produced by `TraversalEngine.RunAsync()` and no parallel traversal path is active

### Requirement: Plan mode and intent mode share one engine skeleton

The Host SHALL support both execution paradigms on the same engine: plan mode (scripted) via `TraversalPlan` with `ChildrenStrategy.Static` + `StaticNodes`, and intent mode (dynamic) via `TraversalPlan` with `ChildrenStrategy.DynamicMatch`. The difference between modes SHALL be limited to plan shape and Host verification semantics; no engine change SHALL distinguish the modes.

#### Scenario: Plan mode walks static nodes
- **WHEN** a plan-mode run executes
- **THEN** the engine iterates the plan's `StaticNodes` in order with the unvisited filter, and each step's expected change is verified by the Host's `VerifyHook`

#### Scenario: Intent mode walks dynamically generated children
- **WHEN** an intent-mode run executes
- **THEN** the engine generates children from page analysis via `DynamicMatch` (with navigation detection and D-90 PressBack/Pop), and the Host's `VerifyHook` is a no-op

### Requirement: Entry policy executes before the engine

The Host SHALL execute `IEntryPolicyExecutor` and verify the reset page BEFORE invoking `TraversalEngine.RunAsync()`. The engine SHALL NOT be modified to invoke entry policy internally; the engine loop continues to start at NodeSelect and use `_plan.EntryApp` as the fallback root.

#### Scenario: Reset precedes engine start
- **WHEN** a scenario run begins
- **THEN** `IEntryPolicyExecutor.ExecuteAsync` runs first and the reset page is verified before the engine's first step

#### Scenario: Engine is not modified for entry
- **WHEN** the `TraversalEngine` source is inspected
- **THEN** it contains no call to an entry-policy executor; entry is a Host composition concern

### Requirement: Immediate verification is a non-mutating hook

Plan-mode expected-change verification SHALL run in a Host `VerifyHook` on `ITraversalHook.OnAfterStep`. The hook SHALL compare the step's before/after page analysis against the expected change carried in the plan node metadata, record the pass/fail, and MUST NOT mutate engine state. A verification failure SHALL be recorded and may signal the Host to stop or pause the engine only as a Host decision.

#### Scenario: VerifyHook records plan-mode expected changes
- **WHEN** a plan-mode step completes and its expected change is met
- **THEN** the hook records the verification result; the engine continues normally

#### Scenario: VerifyHook does not alter engine state
- **WHEN** a plan-mode step's expected change is not met
- **THEN** the hook records the failure and does not rewrite engine state; stopping the run is a Host decision expressed through the engine's public control surface

### Requirement: Post-hoc analysis reads trace and journal

The Host SHALL produce a `ScenarioRunOutcome` after `TraversalEngine.RunAsync()` completes by reading `ITraceService` and the `SafetyDecisionJournal` (`VerificationAnalyzer`). Analysis SHALL be post-run only, with no real-time coupling to the engine. The outcome SHALL distinguish success, failure, and incomplete, and SHALL carry a step-level error traceback (which step failed and why — verification mismatch / safety denial / execution failure).

#### Scenario: Analyzer produces step-level traceback
- **WHEN** a run fails and `VerificationAnalyzer` consumes the trace and journal
- **THEN** it produces a `ScenarioRunOutcome` identifying the failing step and the failure cause classification

#### Scenario: Analyzer has no real-time coupling
- **WHEN** the analyzer is inspected
- **THEN** it runs strictly after the engine completes and consumes only the persisted trace and journal, never engine internals

### Requirement: Safety gate runs on the engine path via the decorated executor

The engine's `OperationDispatcher` SHALL issue device actions through the single `SafeActionExecutor`-decorated `IActionExecutor`. A Host `SafetyContextHook` SHALL push the per-step `SafetyCandidate` into `SafetyExecutionContext` before each step so `DecideAsync` sees the real candidate, never the `"unscoped"` fallback. Safety-denied actions SHALL be classified post-hoc (`blocked`/`skipped`) by the analyzer from the journal.

#### Scenario: Real candidate reaches the safety gate
- **WHEN** a step with a safety candidate executes
- **THEN** the `SafetyContextHook` has pushed the candidate into `SafetyExecutionContext` and the safety journal records real candidates with no `unscoped` fallback

#### Scenario: Denied action is classified post-hoc
- **WHEN** a safety decision denies an action
- **THEN** the denial is recorded in the journal and classified by the analyzer from the journal rather than surfacing as an engine state change

### Requirement: Run assets are written by a hook

Per-step run artifacts SHALL be written by a Host `RunAssetHook` on `OnBeforeStep`/`OnAfterStep`. Because `PageAnalysis` carries no screenshot bytes, the hook SHALL obtain step evidence itself (for example via `AdbScreenCapture`). `RunAssetStore` remains the storage.

#### Scenario: Every step produces artifacts
- **WHEN** a step executes
- **THEN** the `RunAssetHook` writes the step's before/after evidence (including a screenshot obtained by the hook) to the run asset store

### Requirement: Plans are data, provisioned by Host

The Host SHALL provision scenario execution from data: plan mode from a plan JSON (hand-authored or mock-generated) expressed as `TraversalPlan` with `ChildrenStrategy.Static` + `StaticNodes`, each node carrying its operation, target, and expected change; intent mode from the existing plan compiler's `DynamicMatch` plan. A plan derived from a previous run's trace SHALL be Host analysis output consumed as plan input. Plans SHALL NOT require new engine code.

#### Scenario: Plan JSON becomes a static traversal plan
- **WHEN** a plan-mode scenario is provisioned from plan JSON
- **THEN** the JSON is loaded into a `TraversalPlan` with `ChildrenStrategy.Static` and `StaticNodes` whose metadata carries each step's expected change

#### Scenario: Intent plan uses the existing compiler
- **WHEN** an intent-mode scenario is provisioned
- **THEN** the existing plan compiler produces the `DynamicMatch` `TraversalPlan` without modification

#### Scenario: Trace-derived plans are allowed
- **WHEN** a repeat run is provisioned from a previous run's trace
- **THEN** the plan is produced by Host analysis of the trace and consumed as plan input, with no engine involvement
