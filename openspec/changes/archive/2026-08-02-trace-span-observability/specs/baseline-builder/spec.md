## ADDED Requirements

### Requirement: BaselineBuilder 提取每 run 聚合

At the end of each run, `BaselineBuilder` SHALL extract from that run's spans a per-run aggregate and append one line to `artifacts/baselines/<scenarioId>.jsonl`. The aggregate SHALL include: `itemsObserved` (count of `entry.observed`), `itemsVisited`, `itemsSkipped`, `stepsUsed` (count of `engine.step`), `scrollCount` (count of `action.scroll`), `endOfListDetected`, `success`, `aiLatencyP50`, and `aiLatencyP95`. An unparseable run SHALL be skipped with a logged warning and SHALL NOT corrupt the file.

#### Scenario: 一次 run 后文件多一行且字段齐全

- **WHEN** a run completes and `BaselineBuilder` processes its spans
- **THEN** the file `artifacts/baselines/<scenarioId>.jsonl` SHALL gain exactly one new line, and that line SHALL contain all nine aggregate fields with values derived from the run's spans

#### Scenario: 空 run 不写损坏行

- **WHEN** a run completes but its span tree cannot be aggregated (e.g. no `engine.step` spans present)
- **THEN** `BaselineBuilder` SHALL log a warning and SHALL NOT append a line for that run

### Requirement: 每场景独立文件

`BaselineBuilder` SHALL write exactly one file per `scenarioId` at `artifacts/baselines/<scenarioId>.jsonl`. Aggregates from different scenarios SHALL be written to their own files and SHALL never be interleaved in a shared file.

#### Scenario: 两个不同 scenarioId 的 run 写各自文件

- **WHEN** a run for scenario A and a run for scenario B each complete
- **THEN** `artifacts/baselines/A.jsonl` SHALL contain only A's aggregate lines and `artifacts/baselines/B.jsonl` SHALL contain only B's aggregate lines

#### Scenario: 同名 scenarioId 追加到同一文件

- **WHEN** two runs of the same `scenarioId` complete in sequence
- **THEN** both aggregates SHALL appear in the same `artifacts/baselines/<scenarioId>.jsonl` file as two distinct lines in append order

### Requirement: 阈值计算

Once a scenario's baseline has 10 or more records, `BaselineBuilder` SHALL compute p50 and p95 percentiles of `itemsVisited`, `stepsUsed`, and `aiLatency` over those records. With fewer than 10 records the thresholds SHALL be marked unavailable, and no threshold SHALL be derived from an insufficient sample.

#### Scenario: 11 条记录后能产出 p50/p95

- **WHEN** a scenario's baseline file contains 11 records and thresholds are requested
- **THEN** p50/p95 SHALL be computable and returned for `itemsVisited`, `stepsUsed`, and `aiLatency`

#### Scenario: 9 条时不启用

- **WHEN** a scenario's baseline file contains 9 records and thresholds are requested
- **THEN** thresholds SHALL be marked unavailable and callers SHALL NOT receive p50/p95 values for that scenario

#### Scenario: 阈值随新记录更新

- **WHEN** a scenario's baseline grows from 10 to 20 records and thresholds are recomputed
- **THEN** the returned p50/p95 SHALL reflect the full 20-record sample, and recomputation SHALL NOT mutate the append-only file

### Requirement: 数据驱动阈值闭环

`EnumerateCompletionAnalyzer` SHALL load `artifacts/baselines/<scenarioId>.jsonl` for its scenario. With 10 or more records it SHALL use the computed p50/p95 as dynamic thresholds in place of the hardcoded defaults. With fewer than 10 records it SHALL operate in cold-start mode (only Halt and Warn fire). The loaded thresholds SHALL drive the `visited >= p95` → Terminate and `p50 <= visited < p95` → Recommend rules.

#### Scenario: 基线 ≥10 条时 visited 达 p95 → Terminate

- **WHEN** a scenario has 11 baseline records, the analyzer's scenario `visited` count reaches the baseline-derived `p95`, and `end_reached` is present
- **THEN** the analyzer SHALL return `ShouldTerminate == true` with a Terminate reason and `Confidence == 0.9`

#### Scenario: 无基线时仅 Halt

- **WHEN** a scenario has no baseline file and an analyzer's classification would normally produce a Recommend verdict
- **THEN** the analyzer SHALL suppress the Recommend, classify the state as Observe (cold-start), and only a genuine Halt condition SHALL yield `ShouldTerminate == true`

#### Scenario: 硬编码默认被替代

- **WHEN** a scenario has 10 or more baseline records whose `itemsVisited` p95 differs from the hardcoded default
- **THEN** the analyzer SHALL classify using the baseline-derived p95, not the hardcoded default

### Requirement: 追加只写 + 可检视

`artifacts/baselines/<scenarioId>.jsonl` SHALL be an append-only JSONL file in which each line is a standalone JSON object (one per historical run), readable by standard text tooling and diffable across runs. Existing lines SHALL NOT be modified or deleted by subsequent runs; a new run only appends.

#### Scenario: 文件每行独立可解析

- **WHEN** a baseline file contains multiple aggregate lines and each line is parsed independently as JSON
- **THEN** every line SHALL parse as a valid standalone JSON object with the nine aggregate fields, and the count of lines SHALL equal the number of completed runs for that scenario

#### Scenario: 追加不改写历史行

- **WHEN** a run completes and `BaselineBuilder` appends its aggregate to an existing file
- **THEN** the byte content of the pre-existing lines SHALL be unchanged, and the new line SHALL appear at the end of the file
