## ADDED Requirements

### Requirement: ai.evidence reference event contract

Submitting asset bytes SHALL write a synchronous reference event into the trace stream: record_type `ai.evidence` with fields `evidence_path` (relative — runId is injected at assembly, never known to producers), `evidence_type`, `byte_count` (TraceFields 45→48). The event SHALL be written by the submitting producer at submission time — trace is the index, bytes are the payload, physically separated into the asset space (`assets/{runId}/…`).

#### Scenario: evidence submission writes reference event
- **WHEN** a producer submits evidence bytes with relativePath `vision-evidence-{stepSpanId}.json`
- **THEN** the trace contains an `ai.evidence` event with the relative path, type, and byte count, and the bytes land under `assets/{runId}/`

#### Scenario: relative path carries no runId
- **WHEN** an `ai.evidence` event is inspected
- **THEN** its evidence_path contains no runId segment (resolution happens at assembly)

#### Scenario: bytes never interleave with the event stream
- **WHEN** a run produces screenshots and evidence
- **THEN** trace.jsonl contains only light reference events (no image bytes), and all bytes reside in the asset space
