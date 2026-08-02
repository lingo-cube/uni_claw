# completion-monitor Specification

## Purpose
TBD - created by archiving change trace-span-observability. Update Purpose after archive.
## Requirements
### Requirement: ICompletionAnalyzer 与 CompletionVerdict

`ICompletionAnalyzer` SHALL expose `EvaluateAsync(ITraceQuery, ct)` returning `CompletionVerdict?`. `CompletionVerdict` SHALL carry `ShouldTerminate` (bool), `Reason` (string), and `Confidence` (double, clamped to 0.0–1.0). An analyzer SHALL read only `ITraceQuery` and SHALL NOT depend on engine internals or the engine instance. A null verdict SHALL mean "no signal / continue observing". `ShouldTerminate == false` SHALL NOT by itself stop the engine; only the `CompletionMonitor` SHALL act on verdicts.

#### Scenario: mock trace 无终止条件时返回 continue verdict

- **WHEN** an analyzer is evaluated against a mock span tree with pending entries remaining and no end-of-list signal
- **THEN** the returned verdict SHALL be non-null, SHALL have `ShouldTerminate == false`, and SHALL have a `Confidence` within 0.0–1.0

#### Scenario: 命中 Halt 时返回 ShouldTerminate=true confidence=1.0

- **WHEN** an analyzer is evaluated against a mock span tree whose state satisfies the Halt condition
- **THEN** the returned verdict SHALL have `ShouldTerminate == true`, `Confidence == 1.0`, and a non-empty `Reason`

#### Scenario: 分析器只读 ITraceQuery

- **WHEN** an analyzer is evaluated and its dependencies are inspected
- **THEN** the analyzer SHALL have no reference to the `TraversalEngine`, `ITraceCoordinator`, or any engine-internal type, reading only the injected `ITraceQuery`

### Requirement: EnumerateCompletionAnalyzer 判定规则

`EnumerateCompletionAnalyzer` SHALL derive from spans: `pending = observed - visited - skipped` (counts of `entry.observed`/`entry.visited`/`entry.skipped`), and SHALL apply the rules — `pending <= 0 && end_reached` → Halt (confidence 1.0); `visited >= p95 && end_reached` → Terminate (confidence 0.9); `p50 <= visited < p95 && end_reached` → Recommend (confidence 0.7); `visited >= p95 * 1.5` → Warn (confidence 0.95); otherwise → Observe. `end_reached` SHALL be derived from the presence of an `end_of_list`-style signal (e.g. a step whose child generation produced no further children). When no baseline is available, SHALL use the hardcoded defaults.

#### Scenario: pending=0 且有 end_reached step → Halt

- **WHEN** a mock span tree has `entry.observed == entry.visited + entry.skipped` (pending 0) and an end-of-list step is present
- **THEN** the analyzer SHALL return `ShouldTerminate == true` with `Reason` indicating Halt and `Confidence == 1.0`

#### Scenario: visited 超过 p95*1.5 → Warn

- **WHEN** a mock span tree has `entry.visited` exceeding `p95 * 1.5` (from baseline or hardcoded default) regardless of end_reached
- **THEN** the analyzer SHALL return a verdict indicating Warn with `Confidence == 0.95`, and `ShouldTerminate` SHALL be false

#### Scenario: 数据不足时走硬编码默认阈值

- **WHEN** no baseline file exists for the scenario and the analyzer must classify a mid-range `visited` count
- **THEN** the analyzer SHALL classify using the hardcoded default p50/p95 thresholds and SHALL NOT throw

### Requirement: ErrorLoopAnalyzer

`ErrorLoopAnalyzer` SHALL detect a stuck state from spans: 5 or more consecutive steps with all children skipped and no visited span → terminate with `Reason == "stuck_in_error_loop"` and confidence 0.9; within the same page, `skipped > visited * 4` → terminate with `Reason == "skip_rate_too_high"` and confidence 0.7. The analyzer SHALL write an `analyze.error_loop` span recording the detected reason when it terminates, and SHALL otherwise return a continue verdict.

#### Scenario: 连续 5 步全 skipped → ShouldTerminate=true

- **WHEN** a mock span tree has 5 consecutive `engine.step` spans each whose children are all `entry.skipped` with no `entry.visited` among them
- **THEN** the analyzer SHALL return `ShouldTerminate == true`, `Reason == "stuck_in_error_loop"`, and `Confidence == 0.9`, and an `analyze.error_loop` span SHALL be recorded

#### Scenario: 同页 skipped 超 visited 4 倍 → skip_rate_too_high

- **WHEN** a mock span tree for a single page has `entry.skipped` count greater than `4 * entry.visited` count
- **THEN** the analyzer SHALL return `ShouldTerminate == true` with `Reason == "skip_rate_too_high"` and `Confidence == 0.7`

#### Scenario: 正常 run 返回 continue

- **WHEN** a mock span tree has healthy visited/skipped ratios and no consecutive all-skipped steps
- **THEN** the analyzer SHALL return `ShouldTerminate == false` and SHALL NOT write an `analyze.error_loop` span

### Requirement: CompletionMonitor 调度

`CompletionMonitor` SHALL poll each registered `ICompletionAnalyzer` at a configurable interval (default 500 ms) against `ITraceQuery`, and SHALL write an `analyze.completion` span for every poll regardless of whether termination occurs. Confidence-to-action SHALL be: `confidence >= 0.9` → cancel the linked CTS to terminate the engine; `0.7 <= confidence < 0.9` → invoke a Recommend callback (`true` → cancel, `false` → continue, `null` → downgrade to Observe); `confidence < 0.7` → continue observing. The monitor SHALL be wired around `engine.RunAsync(cts.Token)` at the Host composition root, and a monitor crash SHALL NOT crash the engine (the engine simply runs to completion without cancellation).

#### Scenario: analyzer 返回 confidence 0.95 → 引擎被 cancel

- **WHEN** an analyzer returns `ShouldTerminate == true` with `Confidence == 0.95` during a poll
- **THEN** the monitor SHALL call `Cancel()` on the linked CTS, the engine run SHALL observe `OperationCanceledException` and exit, and an `analyze.completion` span SHALL have been written for that poll

#### Scenario: 返回 confidence 0.5 → 引擎继续

- **WHEN** an analyzer returns a verdict with `Confidence == 0.5` during a poll
- **THEN** the monitor SHALL NOT cancel the CTS, the engine run SHALL continue, and an `analyze.completion` span SHALL have been written for that poll

#### Scenario: Recommend 回调决定是否继续

- **WHEN** an analyzer returns `0.7 <= Confidence < 0.9` and the Recommend callback returns `false`
- **THEN** the monitor SHALL NOT cancel, SHALL continue polling, and SHALL record the callback outcome in the poll's `analyze.completion` span

### Requirement: 冷启动

When fewer than 10 baseline records exist for the scenario, `CompletionMonitor`/analyzers SHALL honor only the Halt and Warn verdicts; `Terminate` and `Recommend` SHALL NOT trigger cancellation or callbacks. Every determination SHALL still write the `analyze.completion` (or `analyze.error_loop`) span so data accumulates toward the first usable baseline.

#### Scenario: 基线 5 条 + visited 达 p50 → 不触发 Recommend

- **WHEN** the scenario baseline file has 5 records and an analyzer returns a Recommend-class verdict (confidence in 0.7–0.9)
- **THEN** the monitor SHALL ignore the Recommend, SHALL NOT invoke the Recommend callback, SHALL NOT cancel, and SHALL still write the `analyze.completion` span

#### Scenario: 冷启动时 Halt 仍生效

- **WHEN** the scenario baseline file has fewer than 10 records and an analyzer returns Halt (confidence 1.0)
- **THEN** the monitor SHALL cancel the linked CTS and terminate the engine despite the cold-start state

### Requirement: 边界条件

The following edge cases SHALL hold: a missing or corrupt baseline file SHALL degrade to pure Observe with only Halt effective, plus a logged warning; a second `Recommend` for the same run SHALL escalate to `Terminate` (anti-nuisance); an observed-count spike greater than `p95 * 2` SHALL be flagged as abnormal and reported without terminating; and each `scenarioId` SHALL have its own independent baseline such that one scenario's data never affects another's thresholds.

#### Scenario: 基线损坏时仅 Halt 可终止

- **WHEN** the baseline file for a scenario exists but is corrupt (unparseable lines) and an analyzer returns a Recommend-class verdict
- **THEN** the monitor SHALL log a warning, treat the baseline as unavailable (cold-start rules), NOT cancel on Recommend, and SHALL still cancel on a genuine Halt verdict

#### Scenario: 同 run 二次 Recommend 升级为 Terminate

- **WHEN** a Recommend verdict is delivered for a run and then a second Recommend verdict is delivered for the same run in a later poll
- **THEN** the second occurrence SHALL be treated as `Terminate`, the linked CTS SHALL be cancelled, and the escalation SHALL be recorded in the `analyze.completion` span

#### Scenario: observed 突增只上报不终止

- **WHEN** an analyzer observes an `entry.observed` count exceeding `p95 * 2` in a single poll
- **THEN** the monitor SHALL record an abnormal-spike flag in the `analyze.completion` span and SHALL NOT cancel the engine on that basis alone

#### Scenario: 不同 scenarioId 各用独立基线

- **WHEN** scenario A and scenario B each have baseline files with different record counts and thresholds
- **THEN** analyzing scenario A SHALL read only A's baseline, and analyzing scenario B SHALL read only B's baseline, with no cross-scenario influence

