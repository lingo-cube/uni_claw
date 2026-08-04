## ADDED Requirements

### Requirement: TracePipeline is a Core common implementation with unified submission

Core SHALL provide `ITracePipeline` (Submit(AssetSubmission) / DrainAsync) with a **common implementation**: bounded Channel 256 + SingleReader writer + batched flush (50ms or 64 items) + idempotent DrainAsync. `AssetSubmission` SHALL carry category, bytes, relativePath. Producers (hook/decorator/provider) SHALL submit **relative paths only** — runId is injected at assembly, never known to producers. Host's StepAssetSink SHALL be deleted (logic moves into Core).

#### Scenario: Submit is non-blocking and counts dropped

- **WHEN** Submit is called with the channel full (256 pending)
- **THEN** it returns without blocking, the item is counted as dropped (PipelineStats.Dropped incremented), and no exception propagates to the producer

#### Scenario: Batched flush aggregates writes

- **WHEN** 100 submissions enter the pipeline and DrainAsync is not yet called
- **THEN** the number of underlying store writes is much less than 100 (aggregated by the 50ms/64-item flush)

#### Scenario: DrainAsync flushes remaining buffer and is idempotent

- **WHEN** DrainAsync is called mid-run and then called again after completion
- **THEN** all accepted submissions are on disk after the first call and the second call returns immediately

### Requirement: Asset bytes are trace information, physically separated from the event stream

Submitting bytes SHALL also write a **reference event** into the event stream (`ai.evidence`: evidence_path **relative** / evidence_type / byte_count) synchronously — trace is the index, bytes are the payload. Bytes SHALL be persisted via the pipeline to the asset space (`assets/{runId}/…`), never interleaved into the event stream.

#### Scenario: reference event carries a relative path

- **WHEN** a producer submits evidence bytes with relativePath `vision-evidence-{stepSpanId}.json`
- **THEN** the `ai.evidence` event records that relative path (no runId — producers cannot know it) and the bytes land under `assets/{runId}/`

#### Scenario: physical separation preserved

- **WHEN** a run produces screenshots and evidence
- **THEN** the event stream contains only light reference events (never image bytes), and all bytes reside in the asset space

### Requirement: Pipeline persists via the IAssetStore interface

The pipeline writer SHALL persist through `IAssetStore` (Write/Read/Exists/List, key = `{runId}/{relativePath}`) — an interface, not an implementation. Host assembly SHALL supply `FileAssetStore` (staging atomic write + writeGate). The pipeline SHALL depend on the interface only.

#### Scenario: mock backend injection

- **WHEN** a mock `IAssetStore` is injected into the pipeline
- **THEN** the pipeline writes through the mock and the store interface protocol is unchanged

#### Scenario: write failure propagates to the failure sink

- **WHEN** the store throws during a batched write
- **THEN** the pipeline emits `IPipelineFailureSink.OnWriteFailed(AssetSubmission, Exception)` and counts the failure in PipelineStats.WriteFailures

### Requirement: Write failures are observable without touching manifest

On write failure, the pipeline SHALL notify via `IPipelineFailureSink` (Core interface); Host subscription SHALL write an issue entry (`asset_write_failed`, path + exception). After DrainAsync, Host SHALL read `PipelineStats` (Accepted/Dropped/WriteFailures) and write/extend the `assets.sink_failure` summary trace event (failed/accepted/**dropped** counts). Counters SHALL live in the event/log domain — manifest SHALL NOT be written back (one-shot metadata snapshot written at run start).

#### Scenario: per-failure issue entry

- **WHEN** a write fails during a run
- **THEN** issues.jsonl contains an `asset_write_failed` entry with the asset path and exception message

#### Scenario: summary event carries all counters

- **WHEN** a run ends with 2 write failures and 1 dropped submission
- **THEN** the `assets.sink_failure` trace event metadata contains failed_count=2, accepted_count, and dropped_count=1; manifest.json is unchanged by counters

#### Scenario: dropped is not an issue

- **WHEN** submissions are dropped due to channel saturation (no exception involved)
- **THEN** no `asset_write_failed` issue is written (backpressure is designed-in behavior); the drop is visible only via PipelineStats/summary event
