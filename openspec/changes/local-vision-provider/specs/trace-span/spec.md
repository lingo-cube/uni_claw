# trace-span delta Specification

## ADDED Requirements

### Requirement: AI vision sub-span types for local vision pipeline stages

The `SpanTypes` catalog SHALL include new span type constants for AI vision sub-spans emitted by `LocalVisionProvider` when parsing `Server-Timing` headers. These SHALL be used via the `trace-span-helpers` `RecordEventAsync` extension on `ITraceRecorder`:

- `AiYolo = "ai.yolo"` — YOLO detection stage
- `AiOcr = "ai.ocr"` — OCR stage
- `AiFusion = "ai.fusion"` — evidence fusion stage
- `AiScroll = "ai.scroll"` — scroll hints computation stage

These SHALL be point-in-time event spans (no `EndTime`, `DurationMs == 0`) recorded with `parentSpanId` set to the enclosing `ai.call` span ID. They SHALL NOT require changes to the `SpanType` enum.

#### Scenario: ai.yolo span recorded

- **WHEN** `LocalVisionProvider` parses `Server-Timing: yolo;dur=45.2` and records via `RecordEventAsync`
- **THEN** a span with `SpanType == "ai.yolo"`, `DurationMs == 0` (event marker), and `Attributes["ai.latency_ms"] == 45.2` is recorded

#### Scenario: All four timing stages recorded

- **WHEN** Python returns `Server-Timing` with all four stages (yolo, ocr, fusion, scroll)
- **THEN** exactly 4 event spans (`ai.yolo`, `ai.ocr`, `ai.fusion`, `ai.scroll`) are recorded as children of the `ai.call` span

#### Scenario: Span types in catalog

- **WHEN** `SpanTypes.AiYolo`, `SpanTypes.AiOcr`, `SpanTypes.AiFusion`, `SpanTypes.AiScroll` are referenced
- **THEN** each resolves to the correct dotted string and is present in the `SpanTypes` catalog
