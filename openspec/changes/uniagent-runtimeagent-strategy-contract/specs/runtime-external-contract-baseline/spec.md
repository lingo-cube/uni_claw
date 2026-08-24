## MODIFIED Requirements

### Requirement: Implemented-plane freezing

The Goal and Data planes' implemented surfaces MUST be frozen as contract clauses with their exact current semantics. The Goal plane MAY grow only through additive operations that preserve the frozen `run.start` operation; this change adds `run.strategy.start` as a distinct start-time operation and does not realize the deferred mid-Run Guidance plane.

#### Scenario: goal plane maps to run.start

Given the Goal plane clause,
When it is inspected,
Then it references exactly `run.start` with `RunStartRequest { goal, objects,
capabilities, device }` → `RunAccepted { accepted, runId, runState }`,
DriverHost-owned runId, asynchronous, deterministic `request_rejected`, with no
field or semantic changes.

#### Scenario: goal plane adds bounded strategy start

Given the Goal plane clause,
When the Strategy Contract is implemented,
Then it also references `run.strategy.start` with a typed, UniAgent-authored
`StrategyDirective`, deterministic pre-Run admission, and an accepted result that
creates at most one Agent-owned Run without mid-Run strategy replacement.

#### Scenario: data plane maps to the frozen read-only surface

Given the Data plane clause,
When it is inspected,
Then it references exactly the 8 read-only methods (`ping`, `run.list`,
`run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`,
`evidence.get`, `control.support`), the 13-field classified `RunSnapshot`, the
18-family `RuntimeEvent` vocabulary, and logical `EvidenceRef`.
