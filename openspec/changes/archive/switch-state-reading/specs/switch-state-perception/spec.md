# Switch State Perception

## ADDED Requirements

### Requirement: ISwitchStateReader returns qualitative three-state visual evidence
The `ISwitchStateReader` interface SHALL be in namespace `UniClaw.Runtime.Capabilities.Perception.Vision`. It SHALL declare exactly two members: `PerceptionFrame Frame { get; }` and `ValueTask<bool?> ReadAsync(ElementBounds, CancellationToken)`. The return value SHALL be `true` (visually ON), `false` (visually OFF), or `null` (UNKNOWN / insufficient evidence / invalid bounds / not a recognizable switch). No confidence, model identity, or provider metadata crosses this contract.

#### Scenario: Known ON switch
- **GIVEN** a fresh perception frame containing a visually ON toggle at normalized bounds B
- **WHEN** `ReadAsync(B)` is called on a reader bound to that frame
- **THEN** the result is `true`

#### Scenario: Known OFF switch
- **GIVEN** a fresh perception frame containing a visually OFF toggle at normalized bounds B
- **WHEN** `ReadAsync(B)` is called on a reader bound to that frame
- **THEN** the result is `false`

#### Scenario: Ambiguous or unknown switch
- **GIVEN** a fresh perception frame containing an ambiguous, animated, or unrecognizable region at bounds B
- **WHEN** `ReadAsync(B)` is called
- **THEN** the result is `null`

#### Scenario: Invalid bounds
- **GIVEN** bounds with `IsValid == false`
- **WHEN** `ReadAsync(invalidBounds)` is called
- **THEN** the result is `null`

### Requirement: ISwitchStateReader is frame-scoped
Each `ISwitchStateReader` instance SHALL be bound to exactly one immutable `PerceptionFrame`. Two readers created for different captures SHALL have non-equal `Frame` identities. `PerceptionFrame` SHALL be an opaque token with unique-per-capture identity semantics.

#### Scenario: Different captures produce different frames
- **GIVEN** two separate perception captures
- **WHEN** an `ISwitchStateReader` is created for each capture
- **THEN** `readerF1.Frame != readerF2.Frame`

#### Scenario: Same reader cannot serve two captures
- **GIVEN** a reader bound to frame F1
- **WHEN** the adapter creates a new capture frame F2
- **THEN** a new reader MUST be created for F2; the old reader's Frame remains F1

### Requirement: Stale-frame evidence must fail closed
`SwitchStateValidation.ValidateFrameMatch(reader, currentFrame, readResult)` SHALL be called before any reader result is attached to an `ObservedElement`. If `reader.Frame != currentFrame`, the result SHALL be converted to `null` (fail closed). A stale frame's trusted ON/OFF evidence SHALL NOT enter a fresh Observation.

#### Scenario: Same frame allows evidence
- **GIVEN** a reader bound to frame F and a read result of `true`
- **WHEN** `ValidateFrameMatch(reader, F, true)` is called
- **THEN** the result is `true`

#### Scenario: Stale frame true fails closed
- **GIVEN** a reader bound to frame F1 and the current observation frame F2 (F1 != F2)
- **WHEN** `ValidateFrameMatch(readerF1, F2, true)` is called
- **THEN** the result is `null` — the trusted ON from the stale frame is rejected

#### Scenario: Stale frame false fails closed
- **GIVEN** a reader bound to frame F1 and the current observation frame F2 (F1 != F2)
- **WHEN** `ValidateFrameMatch(readerF1, F2, false)` is called
- **THEN** the result is `null` — the trusted OFF from the stale frame is rejected

#### Scenario: UNKNOWN passes through
- **GIVEN** a reader and any frame
- **WHEN** `ValidateFrameMatch(reader, frame, null)` is called
- **THEN** the result is `null` — UNKNOWN is already safe

### Requirement: SwitchStateReader has no semantic authority
`ISwitchStateReader` SHALL NOT own Runtime state, semantic belief, capability selection, action authorization, or goal completion. Its namespace SHALL NOT reference `UniClaw.Runtime.Agent`. Its types SHALL NOT declare fields matching `_objectStateBeliefs`, `_localPageBeliefState`, or `_objectBindings`.

#### Scenario: ISwitchStateReader namespace is Agent-free
- **WHEN** all source files under `Capabilities/Perception/Vision/` are inspected
- **THEN** none contain `UniClaw.Runtime.Agent`

#### Scenario: ISwitchStateReader has no mutable semantic state
- **WHEN** all types under `Capabilities/Perception/Vision/` are inspected for fields
- **THEN** none declare `_objectStateBeliefs`, `_localPageBeliefState`, or `_objectBindings`

### Requirement: SwitchStateReader is replayable
The `ISwitchStateReader` contract SHALL support deterministic substitution for replay. A mock implementation SHALL produce the same result for the same frame and bounds on every invocation.

#### Scenario: Deterministic replay
- **GIVEN** a mock reader configured to return a fixed value
- **WHEN** `ReadAsync(bounds)` is called multiple times
- **THEN** the same result is returned every time

### Requirement: Runtime Core unchanged
The addition of `ISwitchStateReader` SHALL NOT modify any type in `UniClaw.Runtime.Agent`, `UniClaw.Runtime.Container`, `UniClaw.Runtime.Traversal`, or `UniClaw.Runtime.Environment`. `ObservedElement.SwitchState` already exists and requires no schema change.

#### Scenario: Agent signature unchanged
- **WHEN** `Agent.RunSemanticGoalAsync` parameters are inspected
- **THEN** none include `ISwitchStateReader`, `PerceptionFrame`, or `bool?`

#### Scenario: IEnvironment unchanged
- **WHEN** `IEnvironment` method count is inspected
- **THEN** exactly 2 methods exist: `ObserveAsync`, `ExecuteAsync`
