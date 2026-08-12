## ADDED Requirements

### Requirement: Stable Runtime activity source
The Runtime SHALL expose one BCL `ActivitySource` emission seam with a stable source name and schema version, SHALL NOT depend on Harness types, and SHALL NOT hold per-run trace recording state.

#### Scenario: Runtime executes without an observability listener
- **WHEN** a Runtime invocation executes with no listener subscribed to the stable source
- **THEN** the Runtime SHALL produce the same semantic actions, observations, results, GoalEvidence, and final state as the uninstrumented path

#### Scenario: Harness subscribes without Runtime dependency injection
- **WHEN** the Harness subscribes to the stable Runtime activity source for one run
- **THEN** Runtime components SHALL emit activities without receiving a recorder, store, Harness model, or callback dependency

### Requirement: Required instrumentation boundary coverage
The Runtime SHALL emit bounded activities for Runtime invocation, Agent execution, Intent execution, Container refresh, Traversal execution, Environment `ObserveAsync`, Environment `ExecuteAsync`, Recovery attempt, and external capability invocation.

#### Scenario: Successful end-to-end invocation emits required boundaries
- **WHEN** an end-to-end run exercises intent execution, Agent, Container, Traversal, observation, action execution, recovery, and an external capability
- **THEN** the recorded activities SHALL contain one or more spans for every exercised approved boundary and SHALL contain no requirement to expose private method spans

#### Scenario: Unexercised optional boundary is not fabricated
- **WHEN** a run completes without recovery or external capability invocation
- **THEN** the Runtime SHALL NOT fabricate recovery or capability spans merely to satisfy a fixed shape

### Requirement: Stable layer and component attribution
Every emitted activity SHALL carry one stable layer identifier and one stable component identifier. Layers SHALL be limited to `ORCHESTRATION`, `AGENT`, `STARTUP`, `WORLD`, `CONTAINER`, `TRAVERSAL`, `RECOVERY`, `ENVIRONMENT`, `CAPABILITY`, and `HARNESS`; component identifiers SHALL be explicit contract values and SHALL NOT be derived from CLR names or diagnostic strings.

#### Scenario: Component implementation is renamed
- **WHEN** an internal CLR type or private method is renamed without changing an approved instrumentation boundary
- **THEN** the emitted layer and component identifiers SHALL remain unchanged

#### Scenario: Activity carries closed attribution
- **WHEN** an approved activity is recorded
- **THEN** its layer SHALL belong to the stable taxonomy and its component identifier SHALL be present and non-blank

### Requirement: Parent-child activity context
Runtime activities SHALL use the active BCL activity context so nested operations preserve causal parent-child relationships across asynchronous calls.

#### Scenario: Traversal invokes environment observation
- **WHEN** Traversal performs an asynchronous environment observation during one Agent execution
- **THEN** the environment observation activity SHALL be a descendant of that Traversal activity within the same Runtime invocation trace

### Requirement: Explicit non-semantic operation outcome
Each closed Runtime activity SHALL record an explicit observability outcome of `SUCCEEDED`, `FAILED`, `CANCELLED`, or `UNKNOWN`, and that outcome SHALL NOT be used as semantic action success, traversal completion, recovery success, or Goal completion evidence.

#### Scenario: Instrumented operation throws
- **WHEN** an approved boundary exits by an exception
- **THEN** its activity SHALL close with `FAILED` while the original exception and Runtime failure behavior remain unchanged

#### Scenario: Instrumented operation is cancelled
- **WHEN** an approved boundary exits due to cancellation
- **THEN** its activity SHALL close with `CANCELLED` without converting cancellation into semantic success or failure

### Requirement: Listener failure isolation
Activity creation, annotation, event emission, and closure SHALL be fail-open for Runtime behavior; a listener or recorder failure MUST NOT alter dispatch, retry, observation, verification, recovery, GoalEvidence, or the Runtime result.

#### Scenario: Listener callback fails before action dispatch
- **WHEN** a subscribed listener throws while an activity is started or annotated before an authorized action
- **THEN** the authorized Runtime operation SHALL continue according to its existing semantics and SHALL NOT be suppressed, repeated, or replaced by observability behavior

#### Scenario: Listener callback fails during activity closure
- **WHEN** a listener fails while an activity is stopping
- **THEN** the Runtime SHALL return or propagate its original result independently of the listener failure

