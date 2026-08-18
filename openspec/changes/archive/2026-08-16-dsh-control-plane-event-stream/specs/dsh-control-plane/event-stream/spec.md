# Spec: dsh-control-plane/event-stream

> 控制平面实时事件流能力：`uniclaw-events-after` 命令 + 控制平面真实事件时间线。
> 契约锚点：DriverHost wire 方法 `run.events.after`（`dsh-uniclaw-control-plane-protocol-baseline`，
> 已冻结）；`RuntimeEventProjector` / `RuntimeEventStore`（`dsh-kernel-read-only-observability`，已毕业）。

## ADDED Requirements

### Requirement: uniclaw-events-after command exposes frozen event read

The DSH plugin SHALL register a zero-model read-only command `uniclaw-events-after`
that retrieves RuntimeEvent pages through the frozen `run.events.after` wire method,
preserving the classification of every event.

#### Scenario: Command returns a classified event page

Given a DriverHost serving events for run `run-x` through `run.events.after`,
When `/uniclaw-events-after run-x` executes,
Then it returns a page of classified events ordered by sequence,
each carrying `eventId`, `kind`, `sequence`, and its payload,
and the command makes ZERO model calls (no LLM/VLM invocation).

#### Scenario: Cursor continuation

Given the command supports an optional `--cursor <sequence>` argument,
When `/uniclaw-events-after run-x --cursor 5` executes,
Then it returns only events with `sequence > 5`,
enabling incremental reads without re-fetching the full history.

#### Scenario: Unknown run is a truthful error

Given a runId with no events on the DriverHost,
When `/uniclaw-events-after <unknown-run>` executes,
Then the command reports a non-success result describing the missing run,
and never fabricates events.

### Requirement: Control plane renders the real event timeline

The control plane (client bundle) SHALL render the task workbench event stream
from `uniclaw-events-after` output (the real RuntimeEvent channel), not from a
shadow-digest derivation, so the timeline reflects actual Kernel events.

#### Scenario: Workbench shows live events for the selected task

Given a selected task with a DriverHost-served event history,
When the task workbench loads,
Then it renders the events as a time-ordered timeline
（each row: kind indicator + event text），
and the stream is refreshable to pick up new events.

#### Scenario: No events is an explicit empty state

Given a task with no events on the DriverHost,
When the workbench requests the event stream,
Then it shows an explicit empty state instead of a stale or fabricated timeline.

### Requirement: Event read stays within the frozen read-only surface

Every event read MUST go through the frozen `run.events.after` wire method;
the change SHALL NOT introduce new wire methods, mutate Kernel state, or
weaken the read-only consumption boundary.

#### Scenario: Wire surface unchanged

Given the adapter wire table after this change,
When the set of requested wire methods is inspected,
Then it is a subset of the frozen read-only methods
and includes `run.events.after`.

#### Scenario: Zero Kernel mutation

Given a control-plane event-stream fetch,
When the DriverHost request log is inspected,
Then no mutating method (run.pause/stop/abort/start, action.*, adb.*, control.*)
was requested.
