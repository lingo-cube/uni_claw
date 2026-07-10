## Why

Phase 2.2 has 4 constitution constraints without CI-enforced Guard tests (C-3, C-4, C-9, C-10), Container/Error handlers lack unified pipeline entry points (D-16), and Trace infrastructure lacks SpanType/PageTransition fields needed to unlock 2 TODO verification dimensions (D-E4). These gaps are accumulated from Phase 2 implementation — fixing them now prevents architecture drift and closes deferred items before Phase 3.

## What Changes

- **Guard tests**: Add 4 CI-blocking tests for C-3 (Domain sub-domain zero cross-import), C-4 (FSM independence), C-9 (sealed record class convention), C-10 (DomainValidationException unified validation)
- **Handler wrapper**: Add `ContainerHandler.HandleContainer()` and `ErrorHandler.HandleError()` as unified 3-step pipeline entry points with pipeline-level try/catch fallback. Extend `ErrorRecoveryResult` with `string? Description = null` field
- **Trace minimal**: Add `SpanType` enum (11 values), `PageTransition` sealed record class, `ExecutionRecord.SpanType?` field, and 2 new `ITraceRecorder` methods (`RecordPageTransitionAsync`, `GetPageTransitionsAsync`)
- Update constitution/constraints.md Guard fields, locked-enums.md, handler-pipeline.md, decisions/log.md (D-16 → Fixed, D-E8 new)

## Capabilities

### New Capabilities
- `phase22-guard-tests`: NamespaceIsolationGuardTests + CodingConventionGuardTests for C-3/C-4/C-9/C-10
- `span-type`: SpanType enum (11 values) for trace semantic classification
- `page-transition`: PageTransition record + ITraceRecorder extension for page navigation tracking

### Modified Capabilities
- `container-handler`: Add ContainerHandler wrapper class with HandleContainer() pipeline entry
- `error-handler`: Add ErrorHandler wrapper class with HandleError() pipeline entry + ErrorRecoveryResult.Description extension
- `trace-foundation`: Add SpanType field to ExecutionRecord + PageTransition record + 2 new ITraceRecorder methods
- `enum-value-guards`: Add SpanType_Has11Values test
