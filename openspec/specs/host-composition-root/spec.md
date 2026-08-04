## Requirements

### Requirement: Host is a composition root, not an implementer

The Host SHALL act solely as a composition root that configures and assembles Core components. The Host MUST NOT define or instantiate provider implementations that duplicate Core capabilities. A Host-owned provider such as `DeterministicSettingsModelProvider` is an anti-pattern and SHALL be removed; any capability gap the Host reveals SHALL be lifted into Core, not hidden behind a Host-local substitute.

#### Scenario: Host defines no provider implementation
- **WHEN** the Host source is inspected for types implementing `IModelProvider`
- **THEN** no Host-defined `IModelProvider` implementation exists and every `IModelProvider` used by the run is supplied by Core

#### Scenario: Capability gap is lifted to Core
- **WHEN** Host assembly requires a deterministic replay capability that Core does not yet expose
- **THEN** the capability is added to Core (for example as a `MockModelFixture` preset) and Host selects it via configuration, rather than defining a Host-local provider

### Requirement: IUniBrain is assembled config-driven, not hand-new-ed

The Host SHALL assemble the `IUniBrain` facade as the single AI injection point by handing `UniBrainConfig` and credentials to a Core-provided factory/builder. The Host MUST NOT directly `new` `IPageAnalyzer`, `IModelProvider`, `ITraversalAdvisor`, or `ITextUnderstanding`. Provider selection (real vs mock, vision replay) SHALL be driven by `UniBrainConfig.DefaultProvider` and capability routing inside the factory, not by Host-branching code.

#### Scenario: Host builds UniBrain from config
- **WHEN** the Host assembles the AI capability for a run
- **THEN** it invokes the Core `UniBrainFactory` with a `UniBrainConfig` and credentials and receives an `IUniBrain`, without constructing `PageAnalyzer` or any `IModelProvider` directly

#### Scenario: Replay link shape is a config selection
- **WHEN** a replay run is configured to use the mock provider with vision presets
- **THEN** the mock provider and its vision presets are selected inside UniBrain via `UniBrainConfig`, and the Host contains no branch that hand-assembles a mock analysis provider

### Requirement: Non-AI capabilities are assembled by Host outside UniBrain

Screen state observation, action execution, the safety gate, entry policy, run assets, and trace are platform/observability concerns, not AI. The Host SHALL assemble these capabilities in its composition layer and MUST NOT place them inside `IUniBrain`. `IObservableScreenStateProvider` SHALL be programmed against by the Host as the observable screen-state seam. Run assets and trace SHALL be assembled per `trace-pipeline` (Core `ITracePipeline` + `IAssetStore`; the Host supplies the backend and runId injection) — the Host assembles, it does not implement the pipeline.

#### Scenario: Screen state is not an AI capability
- **WHEN** the Host composes the per-run link
- **THEN** `IObservableScreenStateProvider`, `IActionExecutor`, `IEntryPolicyExecutor`, run assets, and `ITraceRecorder` are assembled by Host and are not reachable through `IUniBrain`

#### Scenario: Run assets assembled via Core pipeline
- **WHEN** the Host composes the per-run asset chain
- **THEN** it supplies backend + location + runId injection to the Core `ITracePipeline`/`IAssetStore` and contains no Host-local submission pipeline (StepAssetSink removed per `trace-pipeline`)

#### Scenario: Host programs the observable seam
- **WHEN** the Host needs to refresh screen state after a scroll
- **THEN** it calls `IObservableScreenStateProvider.RefreshAsync` and contains no cast of `IScreenStateProvider` to a concrete `AdbScreenStateProvider`

### Requirement: Entry policy is injected, not new-ed

The Host SHALL inject `IEntryPolicyExecutor` into consumers from the composition root. The Host MUST NOT `new EntryPolicyExecutor` inside a runner or any non-composition code. Construction of the entry policy executor SHALL live in the Host composition factory.

#### Scenario: Runner receives injected entry policy
- **WHEN** a scenario runner requires entry policy enforcement
- **THEN** it receives an `IEntryPolicyExecutor` instance via constructor injection and the runner source contains no `new EntryPolicyExecutor(` call

### Requirement: Repeat runs aggregate over isolated serial iterations

The Host SHALL support `--repeat <n>` as a composition layer over the same per-run assembly. Iterations SHALL execute serially on one device, invoke reset before each child run, assign a distinct run ID and isolated output directory per child, and feed child results to an `IterationAggregator`. The aggregator SHALL produce an aggregate report containing success rate, consecutive-success count, per-phase latency, safety totals, and new/repeated/disappeared issue fingerprints. A child run failure MUST retain its assets without overwriting another child run's assets.

#### Scenario: Ten iterations produce isolated runs and one aggregate
- **WHEN** `run --repeat 10` targets one emulator and every child run completes
- **THEN** ten independently addressable run directories are produced in serial order and one aggregate report is generated summarizing the ten child results

#### Scenario: Middle iteration failure is preserved
- **WHEN** iteration 4 of a `--repeat 10` run fails during verification and the configured policy allows remaining iterations
- **THEN** iteration 4's failure assets remain in their isolated directory, iterations 5 through 10 receive new run IDs, and the aggregate report records the failed position and its failure classification

#### Scenario: Pending verification is never a false failure
- **WHEN** the `IterationAggregator` consumes child results whose `result.json` status is `pending_verification` (per `trace-based-validation` — judgment is an external `trace verify` command)
- **THEN** pending child runs are counted as "not yet judged" (never failure), the aggregate report carries a pending count, and verdicts are consumed from `result.json` only after verification wrote them

#### Scenario: Aggregate report contains required metrics
- **WHEN** the `IterationAggregator` consumes the child run results
- **THEN** the aggregate report includes success rate, consecutive-success count, per-phase latency, safety decision totals, a fingerprint diff of new, repeated, and disappeared issues, and a pending-verification count

### Requirement: Probes route diagnostics through the trace recorder

The `doctor` and `analyze` probes are Host conveniences. They SHALL record their diagnostics through `ITraceRecorder` and submit via the Core `ITracePipeline` (per `trace-pipeline` — sync `ai.evidence` reference event + bytes via the pipeline; no Host-local submission logic). The Host MUST NOT introduce a parallel diagnostic output format for probes. New probes SHALL be added using the same trace-routed path.

#### Scenario: Doctor output is trace-correlated
- **WHEN** `doctor` runs its verification probes against a booted emulator
- **THEN** each probe result is recorded via `ITraceRecorder`, submitted through the Core `ITracePipeline`, and no separate diagnostic output stream is produced alongside the trace

#### Scenario: Analyze records a single observation on trace
- **WHEN** `analyze` captures and analyzes the current page without sending actions
- **THEN** the observation is recorded through `ITraceRecorder` and submitted via `ITracePipeline`, with no parallel analysis output format

### Requirement: V1 scenario runner is self-contained

For V1, the scenario runner SHALL own the observe→plan→gate→execute→verify loop and MUST NOT depend on `TraversalEngine` or `TraversalFSM`. `HostRunServices.CreateTraversalEngine` SHALL be retained but marked unused with a note referencing this design. If a future change routes the runner through `TraversalEngine`, it SHALL update both this spec and the `traversal-engine` canonical spec.

#### Scenario: Runner loop does not touch the traversal engine
- **WHEN** the V1 scenario runner executes a scenario step
- **THEN** the observe→plan→gate→execute→verify sequence is performed by the runner and no `TraversalEngine` or `TraversalFSM` type is referenced by the runner's step loop

#### Scenario: Unused factory method is annotated
- **WHEN** the Host source is inspected for `HostRunServices.CreateTraversalEngine`
- **THEN** the method is retained and annotated with a note stating it is unused in V1 and referencing the host-target-architecture design

### Requirement: Structural guard prevents AI-provider bypass

An architecture guard test SHALL enforce that the Host assembles `IUniBrain` rather than directly constructing `IPageAnalyzer` or `IModelProvider`. The Host source MUST contain no `new PageAnalyzer(` or `new <ModelProvider>(` construction of an AI provider. The guard SHALL fail the build on regression to the bypass pattern.

#### Scenario: Guard detects direct AI provider construction
- **WHEN** the Host source is modified to introduce a `new PageAnalyzer(` or direct `IModelProvider` construction
- **THEN** the architecture guard test fails and blocks the change

#### Scenario: Guard passes for config-driven assembly
- **WHEN** the Host assembles `IUniBrain` only through the Core factory
- **THEN** the architecture guard test passes and no direct AI-provider construction is present in Host

### Requirement: Host holds exactly one decorated action executor

The Host SHALL hold exactly one `IActionExecutor` instance in `HostRunServices`, which is the `SafeActionExecutor`-decorated instance. All action consumers, including recovery and popup paths, SHALL receive that same decorated instance. The Host MUST NOT construct or expose a second un-decorated `IActionExecutor` (for example a bare `AdbActionExecutor`) reachable by any action path. An architecture guard SHALL assert that no second un-decorated `IActionExecutor` exists, so recovery and popup paths cannot bypass the safety gate.

#### Scenario: Recovery path uses the decorated executor
- **WHEN** a recovery or popup action path issues a device action
- **THEN** it does so through the single `SafeActionExecutor`-decorated `IActionExecutor` held by `HostRunServices`, and no bare `AdbActionExecutor` is reachable to that path

#### Scenario: Guard rejects a second un-decorated executor
- **WHEN** the Host is modified to expose a second `IActionExecutor` that is not `SafeActionExecutor`-decorated
- **THEN** the architecture guard test fails and blocks the change
