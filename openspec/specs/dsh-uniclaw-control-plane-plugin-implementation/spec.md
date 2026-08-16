# dsh-uniclaw-control-plane-plugin-implementation Specification

## Purpose
TBD - created by archiving change dsh-uniclaw-control-plane-plugin-implementation. Update Purpose after archive.
## Requirements
### Requirement: Plugin module owns the DSH dependency

The implementation MUST add one DSH plugin module that owns the DSH dependency and
SHALL leave the pinned DSH checkout (`47f943859bef60e4160492346772ded9b24f765a`)
unmodified.

#### Scenario: Plugin activates against the pinned cordis fork

Given the plugin module with its pinned `@deepseek-ai/cordis` version guard,
When the module is activated inside the harness,
Then it refuses activation unless the resolvable cordis manifest reports `4.0.1`
and the pinned DSH checkout's `git status --porcelain` stays empty.

#### Scenario: Plugin module is self-contained

Given the plugin module source tree,
When its runtime dependency set is inspected,
Then it declares only the pinned cordis peer dependency and resolves the cordis
manifest through the DSH plugin loader with an explicit environment fallback.

### Requirement: DriverHost bounded integration additions

The implementation MUST add DriverHost code only as new files under
`src/UniClaw.Runtime.DriverHost/Control/` and `src/UniClaw.Runtime.DriverHost/Transport/`
and SHALL NOT modify any existing DriverHost file.

#### Scenario: New files stay in the two allowed directories

Given the DriverHost change set for this slice,
When `git status` is inspected,
Then every added DriverHost file lives under `Control/` or `Transport/` and no
pre-existing DriverHost file shows a modification.

### Requirement: Runtime and Runtime.Agent stay untouched

The implementation MUST NOT modify any file under `src/UniClaw.Runtime/` and
SHALL NOT add DSH or Cordis references to the Runtime projects.

#### Scenario: Runtime carries no DSH reference

Given a mechanical scan of the Runtime projects,
When the scan searches for `dsh`, `deepseek`, `cordis`, or `@deepseek`,
Then it finds zero matches and zero modified files under `src/UniClaw.Runtime/`.

### Requirement: Exactly one concrete local transport

The implementation MUST use exactly one concrete local transport — loopback TCP
with newline-delimited JSON-RPC — where the DriverHost owns the listening server
and the plugin connects, and SHALL NOT introduce a generic transport abstraction
or a second transport.

#### Scenario: One loopback JSON-RPC transport only

Given the DriverHost transport and the plugin adapter,
When the transport surface is inspected,
Then it is a single concrete loopback TCP server speaking newline-delimited
JSON-RPC, with no generic transport framework and no parallel protocol.

### Requirement: Read-only control surface

The implementation MUST expose a control surface whose methods are read-only
(ping, list runs, inspect run, inspect trap, open evidence, control support,
event pages) and SHALL NOT expose any method that mutates Kernel state.

#### Scenario: Every surface method is a deterministic read

Given `IUniClawControlSurface`,
When its method set is enumerated,
Then it contains exactly the seven read-only methods and no method returns `void`
or mutates Kernel state.

### Requirement: Deterministic zero-model commands

The implemented commands (uniclaw-inspect-run, uniclaw-inspect-trap, uniclaw-evidence-open, uniclaw-runs-list) MUST execute deterministically with zero model calls and SHALL NOT detour through a model, tool, or agent loop.
#### Scenario: Command handlers never call a model

Given a static scan of the plugin command handlers,
When the scan searches for `llm`, `vlm`, or `model` references,
Then it finds zero occurrences and the handlers return formatted wire data directly.

### Requirement: Deferred control commands audited

Commands without a truthful Kernel control buyer (start, pause, resume, stop, abort) MUST be marked `DEFERRED_NO_KERNEL_CONTROL_BUYER` in a frozen audit and SHALL NOT be implemented or registered as commands in this slice.

#### Scenario: Unsupported control answers from the frozen audit

Given a request for `pause` through `control.support`,
When the frozen audit is consulted,
Then the result reports `Supported=false` with reason
`DEFERRED_NO_KERNEL_CONTROL_BUYER` and none of start/pause/resume/stop/abort is
registered as a plugin command.

### Requirement: RunSnapshot classification preserved over the wire

Every RunSnapshot field MUST cross the wire with its audited classification
(DirectPublicProjection, DerivedReadModel, NotCurrentlyAvailable) and truth source,
and unavailable fields SHALL remain visibly unavailable.

#### Scenario: Classification survives the round trip

Given a registered run snapshot with one Direct field and one unavailable field,
When it is serialized and parsed back,
Then the wire carries `directPublicProjection` for the Direct field, the
unavailable field's value stays absent with `notCurrentlyAvailable`, and the
truth source is preserved.

#### Scenario: Unknown run stays unknown

Given a run id with no registered projection,
When its snapshot is requested over the wire,
Then every field remains classified `notCurrentlyAvailable` with a truthful
diagnostic and no invented value.

### Requirement: RuntimeEvent cursor semantics preserved

RuntimeEvent pages MUST preserve run-scoped cursor semantics (GetAfter,
nextCursor, hasMore) and SHALL preserve eventId, sequence, observationSequence,
correlationId, causationId, and evidenceRefs without reinterpretation.

#### Scenario: Cursor returns only newer events

Given a page whose `nextCursor.lastSequence` is 3,
When a follow-up `run.events.after` is issued with that cursor,
Then only events with sequence greater than 3 are returned and identity fields
match the original page.

### Requirement: EvidenceRef logical resolution only

Evidence inspection MUST resolve by logical locator only and SHALL never resolve
by filesystem path or embed captured content; persistent evidence resolution stays
deferred.

#### Scenario: Locator-only resolution

Given an evidence request carrying a logical locator and run id,
When it is resolved,
Then resolution matches the locator only, returns metadata without captured
content, and a catalog-less DriverHost answers `Found=false` with a diagnostic.

### Requirement: Typed deterministic errors

The transport MUST return typed deterministic error codes
(`bad_request`, `unknown_method`, `internal_error`) and the client MUST surface
connection failures as the typed `driverhost_disconnected` error.

#### Scenario: Unknown method is a typed error

Given a request for a method the transport does not implement,
When it is dispatched,
Then the server answers with error code `unknown_method` and the connection stays
usable.

#### Scenario: Client connection failure is typed

Given a request issued while no DriverHost is reachable,
When the adapter handles the failure,
Then the client rejects with the typed `driverhost_disconnected` error.

### Requirement: Reconnect without state fabrication

Reconnect MUST obtain fresh snapshot and cursor state without fabricating or
caching state across connections, and SHALL reset the drain cursor per connection.

#### Scenario: Fresh state after reconnect

Given a connection that drained events and is then disconnected,
When a new connection is established,
Then a fresh full page is returned, the drain cursor starts empty, and no state
is carried across the connection boundary.

### Requirement: DSH-native event fanout only

The plugin MUST reuse the DSH-native fanout (Cordis events, the single
`session/event` emit) and SHALL NOT introduce a UniClaw event bus, custom
WebSocket, or browser push.

#### Scenario: Fanout reuses the DSH seam

Given a session event emitted through the DSH-native fanout,
When the plugin's subscription observes it,
Then the plugin consumes the existing `session/event` subject/event pair and adds
no new event bus, WebSocket, or push channel.

### Requirement: No custom durable session events

The implementation MUST NOT declare or write custom durable session events and
SHALL mark the durable-event gates (F18–F21)
`NOT_APPLICABLE_NO_CUSTOM_DURABLE_EVENTS`.

#### Scenario: No custom durable types declared

Given the plugin's session-event surface,
When durable event declarations are inspected,
Then none exist and the F18–F21 gates are marked
`NOT_APPLICABLE_NO_CUSTOM_DURABLE_EVENTS`.

### Requirement: Architecture guards enforced

The implementation MUST add mechanical guards proving: Runtime and Runtime.Agent
carry no DSH dependency, the DriverHost carries no DSH cognition or model
dependency, DSH-specific code stays confined to the plugin and adapter boundary,
the plugin has no ADB or PhysicalEnvironment dependency, and no Container
mutation path exists from the plugin.

#### Scenario: Guards fail closed on dependency drift

Given the architecture guard test suite,
When the guards scan the Runtime, Runtime.Agent, DriverHost, and plugin sources,
Then any DSH/cognition/model dependency outside the allowed boundary fails the
guard and the whole suite passes only when the boundary holds.

### Requirement: Validation gates

The implementation MUST pass `dotnet build src/UniClaw.Runtime.sln`,
`dotnet test src/UniClaw.Runtime.sln`, the plugin `node --test` suite, the new
architecture guard tests, `scripts/check-consistency.sh`, and
`openspec validate dsh-uniclaw-control-plane-plugin-implementation --strict
--no-interactive` before claiming completion.

#### Scenario: All validation gates green

Given the completed implementation,
When every listed validation gate is executed,
Then the solution builds with zero errors, all .NET and Node tests pass, the
consistency script reports ALL PASS, and strict OpenSpec validation succeeds.

