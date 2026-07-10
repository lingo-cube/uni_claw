## ADDED Requirements

### Requirement: SpanType enum defines 11 trace semantic classification values

UniClaw.Core.Observability namespace SHALL define a `SpanType` enum with exactly 11 values: DfsForward, DfsBacktrack, RestoreOp, SkipDangerous, PopupHandling, ContainerHandling, ErrorHandling, PageAnalysis, CacheOp, AICall, StateDecision. Each value SHALL have a `<summary>` XML doc comment describing its semantic meaning. SpanType value count SHALL be locked at 11 via EnumValueGuardTests.SpanType_Has11Values. Adding or removing values SHALL require constitution change flow (C-11 style).

#### Scenario: SpanType has exactly 11 values
- **WHEN** `Enum.GetValues<SpanType>().Length` is queried
- **THEN** it MUST return 11

#### Scenario: SpanType values cover operation_rules classification
- **WHEN** SpanType.RestoreOp and SpanType.SkipDangerous are referenced
- **THEN** they MUST exist for operation_rules verification (restore_ops and skip_dangerous)

#### Scenario: SpanType values cover trace_integrity classification
- **WHEN** SpanType.DfsForward, SpanType.DfsBacktrack, SpanType.PageAnalysis, SpanType.StateDecision are referenced
- **THEN** they MUST exist for trace_integrity verification (span_types and page_transitions)
