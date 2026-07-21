## MODIFIED Requirements

### Requirement: TraceContext SHALL be a sealed record class with 6 optional fields
TraceContext SHALL be a `sealed record class` with exactly 6 optional fields: `string? NodeId = null`, `string? StepSpanId = null`, `int? StepNumber = null`, `string? TraceId = null`, `string? VisitSpanId = null`, `string? ParentSpanId = null`. The first 4 fields are the original correlation envelope (what/where/when of each event). VisitSpanId captures the current node visit span. ParentSpanId captures the parent span in the span tree for automatic span nesting. All fields are optional — Context itself is null when no trace context is available (e.g., RecordRootNodePushed before step loop).

#### Scenario: 6-field TraceContext construction
- **WHEN** constructing TraceContext with all 6 fields
- **THEN** NodeId, StepSpanId, StepNumber, TraceId, VisitSpanId, ParentSpanId are all accessible

#### Scenario: VisitSpanId captures node visit span
- **WHEN** a DfsForward event enters a node with SpanId="abc-000041"
- **THEN** Context.VisitSpanId = "abc-000041"; all child records within this visit carry the same VisitSpanId

#### Scenario: ParentSpanId captures span tree nesting
- **WHEN** a ContainerHandling span (id="abc-000010") contains an AICall
- **THEN** AICallRecord.Context.ParentSpanId = "abc-000010"

#### Scenario: Null VisitSpanId when not entering a node
- **WHEN** a step_end event occurs (not a node entry)
- **THEN** Context.VisitSpanId is null

#### Scenario: Null ParentSpanId when no parent span
- **WHEN** a span occurs at the top level (no enclosing span)
- **THEN** Context.ParentSpanId is null

#### Scenario: Backward compatible — 4-field JSONL deserializes correctly
- **WHEN** deserializing old JSONL with only 4 context fields (NodeId, StepSpanId, StepNumber, TraceId)
- **THEN** VisitSpanId and ParentSpanId default to null; no exception thrown
