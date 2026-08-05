## ADDED Requirements

### Requirement: Action executor settles UI after successful device operation

`PageInvalidatingActionExecutor` SHALL wait for a configurable settle duration after a successful device operation (tap, swipe, back, input_text, long_press) and cache invalidation, before returning control to the caller.

The settle duration SHALL be configurable via constructor parameter `settleDelayMs` (default 300ms). The settle SHALL NOT fire on failed operations.

#### Scenario: Successful tap triggers settle

- **WHEN** `TapAsync` is called on a `PageInvalidatingActionExecutor` with `settleDelayMs = 300` and the underlying operation succeeds
- **THEN** `_invalidate()` is called, `Task.Delay(300, ct)` is awaited, then `true` is returned

#### Scenario: Failed operation does not trigger settle

- **WHEN** a device operation returns `false`
- **THEN** no settle delay occurs and `false` is returned immediately

#### Scenario: settleDelayMs = 0 disables waiting

- **WHEN** `settleDelayMs` is set to `0`
- **THEN** no `Task.Delay` occurs on successful operations

### Requirement: Engine loop no longer delays unconditionally in production

The production `TraversalEngineConfig` SHALL set `DelayPerStepMs = 0`. The property and the engine loop's `if (_config.DelayPerStepMs > 0)` guard SHALL be preserved for test/simulation use.

#### Scenario: Production config sets zero engine delay

- **WHEN** the Host assembles `TraversalEngineConfig` for a production run
- **THEN** `DelayPerStepMs` is `0` and the engine loop proceeds without per-step waiting

#### Scenario: Tests can independently configure engine delay

- **WHEN** a unit test constructs `TraversalEngineConfig` with `DelayPerStepMs = 50`
- **THEN** the engine loop honors the configured delay

### Requirement: Settle delay configurable via environment variable

The settle delay SHALL be overridable via `UNICLAW_SETTLE_DELAY_MS` environment variable. The value SHALL be parsed as integer milliseconds. If unset or unparseable, the default of 300ms SHALL be used.

#### Scenario: Environment variable overrides default

- **WHEN** `UNICLAW_SETTLE_DELAY_MS` is set to `"150"`
- **THEN** `PageInvalidatingActionExecutor` is constructed with `settleDelayMs = 150`

#### Scenario: Invalid env var falls back to default

- **WHEN** `UNICLAW_SETTLE_DELAY_MS` is set to a non-integer value
- **THEN** `settleDelayMs` defaults to `300`
