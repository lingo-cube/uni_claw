## Why

Uni-Claw currently lacks a comprehensive trace and observability system. Debugging traversal failures requires manual log analysis, and there is no built-in way to replay or audit traversal runs for post-mortem analysis. This limits our ability to understand engine behavior, validate simulations, and recover from interruptions.

## What Changes

**New Capabilities**:
- Distributed tracing system aligned with industry standards (Trace ID, Span ID, Parent Span ID)
- Pluggable trace storage backends (FileStorage for production, MemoryStorage for simulation)
- Trace analysis with multiple extraction views (page tree, state sequence, AI calls, actions, errors)
- Session-based metadata management with trace_id as global identifier
- Context recovery from trace spans for breakpoint resume

**BREAKING**: Old trace system (TraceStep, traces/trace_YYYYMMDD_HHMMSS/) will be archived. New traces use format `traces/{trace_id}/` with new node models.

## Capabilities

### New Capabilities

- `trace-recording`: Distributed trace recording using Span nodes with ULID identifiers, supporting state transitions, AI calls, action execution, and error tracking
- `trace-storage`: Pluggable storage abstraction with FileStorage (buffered, async file writes) and MemoryStorage (in-memory for simulation)
- `trace-analysis`: TraceAnalyzer that extracts multiple views from trace data including page trees, state sequences, span chains, AI calls, action sequences, error statistics, time analysis, and coverage analysis
- `session-management`: Session metadata management with trace_id as global identifier, stored independently at `traces/{trace_id}/session.json`
- `context-recovery`: Rebuild TraversalRuntimeContext from trace spans using FULL recovery strategy (current_path, node_stack, visited_pages, visited_nodes)

### Modified Capabilities

- `traversal-context`: **BREAKING** - Split into TraversalRuntimeContext (mutable, engine-internal) and TraversalContext (frozen, passed to AI advisors)

## Impact

**Affected Code**:
- `src/trace/` - New trace module
- `src/state/` - Session model updates
- `src/traversal/` - GraphTraversalEngine integration
- `tests/` - Comprehensive trace system tests

**New Dependencies**:
- `ulid-py` - ULID identifier generation

**Storage Changes**:
- Trace output: `traces/{trace_id}/session.json`, `traces/{trace_id}/trace.jsonl`, `traces/{trace_id}/screenshots/`
- Old traces: Archived to `traces/archive/`

**API Changes**:
- TraceRecorder API: `init()`, `record_step_start()`, `record_span()`, `record_step_end()`, `finalize()`
- TraceStorage API: `write()`, `read(trace_id)`
- TraversalRuntimeContext: New mutable context class for engine use
- TraversalContext: Frozen context for AI advisors (existing, unchanged contract)
