## Context

Uni-Claw V6 implements a graph-based traversal engine with state machine orchestration. Currently, the system lacks comprehensive observability:
- Debugging requires manual log analysis
- No ability to replay or audit traversal runs
- No built-in trace data for simulation validation
- No breakpoint recovery capability

The existing trace system (TraceStep) uses a simple timestamp-based directory structure and lacks industry-standard tracing features.

**Constraints**:
- Trace system must not impact traversal performance
- Must support both production (file-based) and simulation (in-memory) modes
- Should align with distributed tracing industry standards
- Integration must not break existing traversal engine behavior

## Goals / Non-Goals

**Goals:**
- Implement distributed tracing with standard terminology (Trace ID, Span ID, Parent Span ID)
- Provide pluggable storage backends (FileStorage, MemoryStorage)
- Enable comprehensive trace analysis with multiple extraction views
- Support context recovery from traces for breakpoint resume
- Maintain separation of concerns: Session (metadata), Context (runtime), Trace (history)

**Non-Goals:**
- Real-time trace visualization dashboard (deferred to future work)
- Automatic trace cleanup (external script handling)
- Legacy trace format migration (archived, not migrated)
- Performance optimization of trace writes (deferred)

## Decisions

### 1. Terminology: Industry-Standard Distributed Tracing

**Decision**: Use standard distributed tracing terminology (Trace ID, Span ID, Parent Span ID) with ULID identifiers.

**Rationale**:
- Aligns with OpenTelemetry/Jaeger standards
- ULID provides time-sortable, URL-safe identifiers without coordination
- Clear semantics: Trace ID = global task, Span ID = individual operation, Parent Span ID = call chain

**Alternatives Considered**:
- UUID v4: Not time-sortable, requires separate timestamp field
- Timestamp-based IDs: Risk of collision in parallel operations
- Custom monotonically increasing IDs: Requires coordination, not distributed

### 2. Trace Node Hierarchy

**Decision**: Three-tier node model - SessionNode (root), StepNode (traversal steps), SpanNode (component operations).

**Rationale**:
- SessionNode anchors the entire trace with metadata
- StepNode maps to graph traversal steps for intuitive navigation
- SpanNode captures fine-grained component interactions
- Clear separation: Session (task level), Step (traversal level), Span (operation level)

**Alternatives Considered**:
- Single flat node type: Lacks hierarchical context
- Two-tier (Session + Span): Hard to reason about traversal steps
- Event-based: Rebuilding tree requires complex event ordering

### 3. Storage Backend: Pluggable Abstraction

**Decision**: TraceStorage abstract interface with FileStorage (buffered async) and MemoryStorage (simple in-memory).

**Rationale**:
- FileStorage: Production use with queue-based buffering prevents blocking
- MemoryStorage: Simulation use with direct in-memory access
- Pluggable design allows future additions (S3, database)

**Alternatives Considered**:
- File-only: Can't support simulation efficiently
- Database-only: Overkill, adds dependency
- Direct file writes: Blocks traversal, impacts performance

### 4. Context Recovery Strategy

**Decision**: Rebuild TraversalRuntimeContext by replaying Span stream (FULL recovery).

**Rationale**:
- Single source of truth: Trace spans contain all state changes
- Recovery is deterministic: same operations → same context
- Extensible: Strategy enum allows future optimization (REPLAY, MINIMAL)

**Alternatives Considered**:
- Context snapshot serialization: Duplicates trace data, drift risk
- Partial recovery: Unclear what to restore, ambiguous semantics
- No recovery: Forces complete restart, loses progress

### 5. Context Duality: Mutable vs Frozen

**Decision**: Split TraversalContext into TraversalRuntimeContext (mutable) and TraversalContext (frozen).

**Rationale**:
- Engine needs mutable context for state updates
- AI advisors need immutable context for consistent decisions
- Clear ownership: Engine owns RuntimeContext, AI receives frozen copies
- Prevents accidental mutations by AI components

**Alternatives Considered**:
- Single mutable context: Risk of AI modifying engine state
- Context copy-on-write: Complex to reason about mutations
- Context proxy: Adds indirection without clear semantics

### 6. Trace Write Failure: "Log and Continue"

**Decision**: Trace write failures log warnings but don't abort traversal.

**Rationale**:
- Trace is auxiliary functionality, not critical path
- Engine should continue even if trace is incomplete
- Warnings allow operators to identify storage issues
- Aligns with principle: observability shouldn't break functionality

**Alternatives Considered**:
- Fail fast on trace errors: Breaks traversals for storage issues
- Silent ignore: Hides problems, makes debugging harder
- Retry queue: Adds complexity, still may fail

### 7. Integration Layering: Component Collection, Engine Assembly

**Decision**: Low-level components collect raw metrics; Engine assembles complete Span nodes.

**Rationale**:
- Separation of concerns: Components focus on domain logic
- Engine owns trace format and assembly
- Components remain decoupled from trace system
- Single point of trace format control

**Alternatives Considered**:
- Components write directly to TraceRecorder: Tight coupling
- TraceRecorder polls component state: Complex state synchronization
- Post-hoc log parsing: Fragile, loses structured data

## Risks / Trade-offs

### Risk: Trace Storage Performance Impact

**Risk**: File I/O blocking traversal execution

**Mitigation**:
- FileStorage uses queue + background writer thread
- Queue size limit provides backpressure
- "Log and continue" prevents trace failures from blocking

### Risk: ULID Collision

**Risk**: Parallel operations could generate colliding ULIDs

**Mitigation**:
- ULID has 128-bit randomness, collision probability negligible
- Time component provides ordering within same millisecond
- If collision occurs, write fails but traversal continues

### Risk: Context Recovery Completeness

**Risk**: Recovered context missing critical state

**Mitigation**:
- FULL strategy restores all essential fields (current_path, node_stack, visited_pages)
- Optional fields (page_tree, page_analysis) rebuilt on-demand
- Validation tests verify recovery correctness

### Trade-off: Trace Data Volume vs Completeness

**Trade-off**: Detailed traces increase storage but improve analysis

**Balance**:
- Span data includes all metrics (tokens, latency, results)
- Screenshots use ID references to avoid duplicate storage
- External scripts handle cleanup and archiving

### Trade-off: Breaking Change vs Legacy Compatibility

**Trade-off**: New format incompatible with existing traces

**Balance**:
- Old traces archived (not deleted, accessible via old tools)
- New format provides significant capabilities
- Migration complexity deferred (manual if needed)

## Migration Plan

### Phase 1: Implementation
1. Add `ulid-py` dependency
2. Implement trace models (TraceNode, SessionNode, StepNode, SpanNode)
3. Implement TraceStorage (FileStorage, MemoryStorage)
4. Implement TraceRecorder with StepTracker
5. Implement TraceAnalyzer with extraction methods
6. Implement ContextRebuilder with FULL strategy
7. Update Session model for trace_id

### Phase 2: Engine Integration
1. Add TraversalRuntimeContext (mutable)
2. Keep TraversalContext (frozen) for AI
3. Integrate TraceRecorder into GraphTraversalEngine
4. Add span generation at state transitions
5. Add span generation for AI calls
6. Add span generation for action execution
7. Add span generation for errors

### Phase 3: Testing
1. Unit tests for all trace models
2. Unit tests for TraceStorage implementations
3. Unit tests for TraceRecorder
4. Unit tests for TraceAnalyzer
5. Unit tests for ContextRebuilder
6. Integration tests with MockEngine
7. Simulation tests with MemoryStorage

### Phase 4: Validation
1. Run existing test suite to ensure no regressions
2. Run simulation tests with trace collection
3. Verify trace output format and structure
4. Verify context recovery correctness
5. Performance testing for FileStorage overhead

### Rollback Strategy
- Revert to pre-integration commit
- Old traces remain in archive/
- New trace directory structure ignored
- No data loss (old traces preserved)

## Open Questions

1. **Screenshot cleanup policy**: Should we implement automatic cleanup, or rely on external scripts?
   - Current decision: External scripts (out of scope for V6.3)

2. **Trace retention period**: How long should traces be kept before archival?
   - Current decision: Not specified in V6.3, deferred to operations policy

3. **Performance thresholds**: What trace write latency is acceptable?
   - Current decision: Queue + background thread should make this negligible, but no specific threshold defined

4. **Legacy trace migration**: Should we provide a migration tool for old traces?
   - Current decision: No, archive old traces and move forward
