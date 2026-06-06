## 1. Setup & Dependencies

- [x] 1.1 Add `ulid-py` dependency to pyproject.toml
- [x] 1.2 Create new `src/trace/` module directory structure
- [x] 1.3 Archive old trace directory to `traces/archive/`

## 2. Core Trace Models

- [x] 2.1 Implement `TraceNode` base class with ULID support
- [x] 2.2 Implement `SessionNode` with trace metadata fields
- [x] 2.3 Implement `StepNode` with traversal step fields
- [x] 2.4 Implement `SpanNode` with all span_type variants
- [x] 2.5 Implement `generate_id()` ULID utility function
- [x] 2.6 Add trace node JSON serialization/deserialization

## 3. Trace Storage

- [x] 3.1 Implement `TraceStorage` abstract interface
- [x] 3.2 Implement `FileStorage` with queue buffering
- [x] 3.3 Implement `FileStorage` background writer thread
- [x] 3.4 Implement `FileStorage` queue backpressure handling
- [x] 3.5 Implement `MemoryStorage` for simulation
- [x] 3.6 Add trace directory structure creation logic
- [x] 3.7 Add screenshot index mapping support

## 4. Trace Recorder

- [x] 4.1 Implement `StepTracker` for stack management
- [x] 4.2 Implement `TraceRecorder.init()` method
- [x] 4.3 Implement `TraceRecorder.record_step_start()` method
- [x] 4.4 Implement `TraceRecorder.record_span()` method
- [x] 4.5 Implement `TraceRecorder.record_step_end()` method
- [x] 4.6 Implement `TraceRecorder.finalize()` method
- [x] 4.7 Add "log and continue" error handling to all recorder methods

## 5. Trace Analyzer

- [x] 5.1 Implement `build_tree()` function with parent_span_id resolution
- [x] 5.2 Implement step_end Span backfill logic
- [x] 5.3 Implement session_end Span backfill logic
- [x] 5.4 Implement `TraceAnalyzer.extract_page_tree()` method
- [x] 5.5 Implement `TraceAnalyzer.extract_state_sequence()` method
- [x] 5.6 Implement `TraceAnalyzer.extract_span_chain()` method
- [x] 5.7 Implement `TraceAnalyzer.extract_ai_calls()` method
- [x] 5.8 Implement `TraceAnalyzer.extract_action_sequence()` method
- [x] 5.9 Implement `TraceAnalyzer.extract_error_statistics()` method
- [x] 5.10 Implement `TraceAnalyzer.extract_time_analysis()` method
- [x] 5.11 Implement `TraceAnalyzer.extract_coverage_analysis()` method

## 6. Context & Session Management

- [x] 6.1 Update `Session` model for trace_id and new fields
- [x] 6.2 Implement `TraversalRuntimeContext` (mutable)
- [x] 6.3 Update `TraversalContext` to frozen=True
- [x] 6.4 Implement `TraversalRuntimeContext.to_readonly()` method
- [x] 6.5 Add Session independent storage to `traces/{trace_id}/session.json`

## 7. Context Recovery

- [x] 7.1 Define `RecoveryStrategy` enum (FULL, REPLAY, MINIMAL)
- [x] 7.2 Implement `ContextRebuilder` class
- [x] 7.3 Implement FULL recovery strategy for current_path
- [x] 7.4 Implement FULL recovery strategy for node_stack
- [x] 7.5 Implement FULL recovery strategy for visited_pages
- [x] 7.6 Implement FULL recovery strategy for visited_level1_menus
- [x] 7.7 Implement FULL recovery strategy for visited_level2_menus
- [x] 7.8 Implement optional recovery for action_history, failed_nodes, consecutive_errors

## 8. Engine Integration

- [x] 8.1 Update `GraphTraversalEngine` to create Session on start
- [x] 8.2 Update `GraphTraversalEngine` to initialize TraceRecorder
- [x] 8.3 Update `GraphTraversalEngine` to use TraversalRuntimeContext
- [x] 8.4 Add state transition span generation at state changes
- [x] 8.5 Add AI call span generation for AI client interactions
- [x] 8.6 Add execution span generation for action execution
- [x] 8.7 Add error span generation for error handling
- [x] 8.8 Add step boundary tracking (NODE_SELECT, FRAME_COMPLETE)
- [x] 8.9 Update AI advisor integration to use frozen TraversalContext

## 9. Component Integration

- [x] 9.1 Update `StateMachine` to report state transitions
- [x] 9.2 Update `AIClient` to report AI call metrics
- [x] 9.3 Update `ActionExecutor` to report execution metrics
- [x] 9.4 Update `ExceptionChain` to report error details
- [x] 9.5 Ensure component data collection doesn't block

## 10. Testing - Unit Tests

- [x] 10.1 Test trace model construction and serialization
- [x] 10.2 Test ULID generation and uniqueness
- [x] 10.3 Test TraceNode parent_span_id relationships
- [x] 10.4 Test SessionNode creation and fields
- [x] 10.5 Test StepNode creation and fields
- [x] 10.6 Test SpanNode all span_type variants
- [x] 10.7 Test FileStorage write and read operations
- [x] 10.8 Test FileStorage queue buffering
- [x] 10.9 Test MemoryStorage write and read operations
- [x] 10.10 Test TraceRecorder init, step, span, finalize methods
- [x] 10.11 Test StepTracker stack operations (enter, exit, parent)
- [x] 10.12 Test build_tree with parent_span_id resolution
- [x] 10.13 Test step_end backfill logic
- [x] 10.14 Test session_end backfill logic
- [x] 10.15 Test all TraceAnalyzer extraction methods
- [x] 10.16 Test TraversalRuntimeContext creation and mutation
- [x] 10.17 Test TraversalContext frozen behavior
- [x] 10.18 Test to_readonly() conversion
- [x] 10.19 Test ContextRebuilder FULL recovery strategy
- [x] 10.20 Test Span field validation (internal vs external)

## 11. Testing - Integration Tests

- [x] 11.1 Create MockEngine for trace testing
- [x] 11.2 Test end-to-end trace generation with MockEngine
- [x] 11.3 Test trace output format and structure
- [x] 11.4 Test trace JSONL parsing and validation
- [x] 11.5 Test session.json creation and content
- [x] 11.6 Test screenshot index mapping
- [x] 11.7 Test context recovery from generated traces
- [x] 11.8 Test context recovery correctness validation
- [x] 11.9 Test trace directory structure and file organization

## 12. Testing - Simulation Tests

- [x] 12.1 Integrate MemoryStorage with simulation runner
- [x] 12.2 Test trace generation during simulation
- [x] 12.3 Test TraceAnalyzer with simulation traces
- [x] 12.4 Test simulation trace-based verification
- [x] 12.5 Test visualization report generation from traces

## 13. Validation & Regression Testing

- [x] 13.1 Run existing test suite to verify no regressions
- [x] 13.2 Run V6 simulation tests with new trace system
- [x] 13.3 Verify trace write performance is non-blocking
- [x] 13.4 Verify FileStorage queue doesn't block traversal
- [x] 13.5 Verify "log and continue" on storage failures
- [x] 13.6 Verify old trace archive structure is preserved

## 14. Documentation

- [x] 14.1 Update CLAUDE.md with trace system references
- [x] 14.2 Add trace module documentation to src/trace/README.md
- [x] 14.3 Document trace directory structure and file formats
- [x] 14.4 Document TraceStorage interface and implementations
- [x] 14.5 Document TraceRecorder usage and integration points
- [x] 14.6 Document TraceAnalyzer extraction methods
- [x] 14.7 Document ContextRebuilder recovery strategies
- [x] 14.8 Document TraversalRuntimeContext vs TraversalContext
- [x] 14.9 Add trace examples to documentation
- [x] 14.10 Update test documentation with trace testing guidance
