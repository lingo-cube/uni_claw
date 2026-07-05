## ADDED Requirements

### Requirement: TraceNode hierarchy defines 4 sealed record types
The Trace model SHALL define a 4-type hierarchy: `TraceNode` (base), `SessionNode`, `StepNode`, and `SpanNode`. All SHALL be `sealed record class`. `SessionNode`, `StepNode`, and `SpanNode` SHALL extend `TraceNode`. The hierarchy SHALL NOT introduce additional intermediate types.

#### Scenario: TraceNode base record carries identity and metadata
- **WHEN** `TraceNode` is constructed with `span_id`, `parent_span_id`, `timestamp`, and `metadata`
- **THEN** the instance stores all four fields and the type is `sealed record class`

#### Scenario: SessionNode extends TraceNode with session fields
- **WHEN** `SessionNode` is constructed with TraceNode fields plus `session_id`, `device_info`, `app_info`, and `status`
- **THEN** the instance contains all 8 fields; `SessionNode` inherits from `TraceNode` and is `sealed record class`

#### Scenario: StepNode extends TraceNode with step fields
- **WHEN** `StepNode` is constructed with TraceNode fields plus `step_type`, `node_id`, `action`, and `result`
- **THEN** the instance contains all 8 fields; `StepNode` inherits from `TraceNode` and is `sealed record class`

#### Scenario: SpanNode extends TraceNode with span fields
- **WHEN** `SpanNode` is constructed with TraceNode fields plus `span_type`, `duration_ms`, and `status`
- **THEN** the instance contains all 7 fields; `SpanNode` inherits from `TraceNode` and is `sealed record class`

#### Scenario: No intermediate TraceNode subtypes exist
- **WHEN** the `UniClaw.Core.Trace` namespace (or equivalent) is inspected for TraceNode descendants
- **THEN** exactly three subtypes exist: `SessionNode`, `StepNode`, `SpanNode` — no other TraceNode-derived types

### Requirement: ITraceRecorder defines session lifecycle, span recording, and query methods
The `ITraceRecorder` interface SHALL define three method categories: session lifecycle (2 methods), span recording (4 methods), and query (5 methods). All methods SHALL be async (return `Task` or `Task<T>`). The interface SHALL NOT expose synchronous variants.

#### Scenario: Session lifecycle methods exist
- **WHEN** `ITraceRecorder` is inspected
- **THEN** it declares `StartSessionAsync` and `EndSessionAsync` as session lifecycle methods

#### Scenario: Span recording methods exist
- **WHEN** `ITraceRecorder` is inspected
- **THEN** it declares `RecordTransitionAsync`, `RecordAICallAsync`, `RecordExecutionAsync`, and `RecordErrorAsync` as span recording methods

#### Scenario: Query methods exist
- **WHEN** `ITraceRecorder` is inspected
- **THEN** it declares `GetTransitionsAsync`, `GetAICallsAsync`, `GetExecutionsAsync`, `GetErrorsAsync`, and `ExportTraceAsync` as query methods

#### Scenario: No synchronous recorder methods exist
- **WHEN** `ITraceRecorder` is inspected for non-async methods
- **THEN** all method signatures return `Task` or `Task<T>`; no synchronous variants are declared

### Requirement: TraversalRuntimeContext is a sealed class with 26 mutable fields
`TraversalRuntimeContext` SHALL be a `sealed class` (NOT a record) with exactly 26 mutable internal fields aligned to Python `src/trace/context.py`. Fields SHALL be directly assignable by the engine (no `with`-expression copy overhead). The class SHALL NOT be a `record` type.

#### Scenario: All 26 fields are present and mutable
- **WHEN** `TraversalRuntimeContext` is inspected for field declarations
- **THEN** it contains exactly 26 fields: `trace_id`, `node_stack` (List<StackFrame>), `current_path`, `current_page_analysis`, `current_fingerprint`, `cache_valid`, `visited_pages`, `visited_level1_menus`, `visited_level2_menus`, `visited_nodes`, `visited_children` (Dict<string, Set<string>>), `page_tree`, `action_history` (keep last 5), `failed_nodes`, `consecutive_errors`, `max_depth`, `step_count`, `retry_count`, `completion_policy`, `device_experience`, `global_state`, `last_error`, `exception_chain`, `ai_provider`, `page_cache`, `wait_after_action_ms`

#### Scenario: Fields are directly assignable without copy
- **WHEN** the engine assigns a new value to any field on `TraversalRuntimeContext` (e.g., `context.global_state = newState`)
- **THEN** the assignment updates the field in-place; no new object is allocated

#### Scenario: TraversalRuntimeContext is sealed class, not record
- **WHEN** the type declaration of `TraversalRuntimeContext` is inspected
- **THEN** it is `sealed class` (NOT `sealed record class` or `record`)

### Requirement: ITraversalContext exposes strongly-typed readonly collections
`ITraversalContext` SHALL be a readonly interface implemented by `TraversalRuntimeContext`. It SHALL expose strongly-typed readonly collection views that prevent mutation through the interface. Mutable setters SHALL be exposed ONLY for `CurrentFrame`, `GlobalState`, and `LastError`.

#### Scenario: NodeStack is exposed as INodeStack
- **WHEN** `ITraversalContext.NodeStack` is inspected
- **THEN** its type is `INodeStack` (the readonly interface view); the internal implementation type is `NodeStack` (mutable class)

#### Scenario: CurrentPath is exposed as IReadOnlyList<string>
- **WHEN** `ITraversalContext.CurrentPath` is inspected
- **THEN** its type is `IReadOnlyList<string>`; the internal implementation type is `List<string>`

#### Scenario: VisitedPages is exposed as IReadOnlySet<string>
- **WHEN** `ITraversalContext.VisitedPages` is inspected
- **THEN** its type is `IReadOnlySet<string>`; the internal implementation type is `HashSet<string>`

#### Scenario: VisitedChildren is exposed as IReadOnlyDictionary with nested IReadOnlySet
- **WHEN** `ITraversalContext.VisitedChildren` is inspected
- **THEN** its type is `IReadOnlyDictionary<string, IReadOnlySet<string>>`; the internal implementation type is `Dictionary<string, HashSet<string>>`

#### Scenario: CurrentFrame has mutable setter on interface
- **WHEN** `ITraversalContext.CurrentFrame` is inspected
- **THEN** its signature is `ITraversalNode? { get; set; }` — the setter is exposed on the interface because the FSM step updates it

#### Scenario: VisitedNodes is exposed as IReadOnlySet<string>
- **WHEN** `ITraversalContext.VisitedNodes` is inspected
- **THEN** its type is `IReadOnlySet<string>`; the internal implementation type is `HashSet<string>`

#### Scenario: StepCount is readonly on interface
- **WHEN** `ITraversalContext.StepCount` is inspected
- **THEN** its signature is `int { get; }` — no setter; engine increments via internal method `IncrementStepCount()`

#### Scenario: GlobalState has mutable setter on interface
- **WHEN** `ITraversalContext.GlobalState` is inspected
- **THEN** its signature is `GlobalState { get; set; }` — the setter is exposed because FSM transitions update it

#### Scenario: LastError has mutable setter on interface
- **WHEN** `ITraversalContext.LastError` is inspected
- **THEN** its signature is `Exception? { get; set; }` — the setter is exposed because error handling assigns it

#### Scenario: ITraversalContext does not expose engine mutation methods
- **WHEN** `ITraversalContext` is inspected for method declarations
- **THEN** it does NOT declare `AppendPath`, `PopPath`, `MarkVisited`, `MarkNodeVisited`, `IncrementStepCount`, `IncrementRetryCount`, `IncrementConsecutiveErrors`, or `ResetConsecutiveErrors`

### Requirement: Readonly view isolation prevents mutable reference leaks
`TraversalRuntimeContext` SHALL implement readonly view isolation so that consumers of `ITraversalContext` cannot mutate internal collections through the interface. `IReadOnlyList<string>` (CurrentPath) SHALL be produced via `.AsReadOnly()` wrapping. `IReadOnlySet<string>` (VisitedPages, VisitedNodes) SHALL be directly exposed (safe per .NET contract, but cast-back MUST be guarded by documentation). `IReadOnlyDictionary<string, IReadOnlySet<string>>` (VisitedChildren) SHALL wrap nested collections to prevent `HashSet<string>` reference leaks.

#### Scenario: CurrentPath .AsReadOnly() prevents cast-back mutation
- **WHEN** a consumer receives `ITraversalContext.CurrentPath` and attempts to cast it to `List<string>`
- **THEN** the cast fails (returns null or throws); the returned wrapper is `ReadOnlyCollection<string>`, not the internal `List<string>`

#### Scenario: VisitedPages direct expose is IReadOnlySet
- **WHEN** a consumer receives `ITraversalContext.VisitedPages`
- **THEN** the type is `IReadOnlySet<string>` and calling `Add`/`Remove` on it is not possible through `IReadOnlySet<string>` members

#### Scenario: VisitedChildren nested sets do not leak HashSet references
- **WHEN** a consumer accesses a value from `ITraversalContext.VisitedChildren` and inspects its type
- **THEN** the nested set type is `IReadOnlySet<string>` (not `HashSet<string>`); cast-back to `HashSet<string>` does not enable mutation through the interface

#### Scenario: INodeStack read-only evaluation for snapshot
- **WHEN** `TraversalContextSnapshot` is created via `CreateReadOnlySnapshot()`
- **THEN** the snapshot captures `NodeIds` (ImmutableArray<string>) from the `INodeStack` content — it does not retain a reference to the mutable `NodeStack` object; Push/Pop/Clear on the original NodeStack do not affect the snapshot

### Requirement: Engine-internal mutation methods exist on TraversalRuntimeContext only
`TraversalRuntimeContext` SHALL define engine-internal mutation methods that are NOT on the `ITraversalContext` interface. These methods SHALL be: `AppendPath(string page)`, `PopPath()`, `MarkVisited(string page)`, `MarkNodeVisited(string nodeId)`, `IncrementStepCount()`, `IncrementRetryCount()`, `IncrementConsecutiveErrors()`, and `ResetConsecutiveErrors()`. Only engine code SHALL call these methods; `ITraversalContext` consumers SHALL NOT have access.

#### Scenario: AppendPath adds to internal current_path
- **WHEN** `TraversalRuntimeContext.AppendPath("settings")` is called
- **THEN** `current_path` (internal `List<string>`) gains "settings" at the end; `ITraversalContext.CurrentPath` reflects the addition

#### Scenario: PopPath removes last from internal current_path
- **WHEN** `TraversalRuntimeContext.PopPath()` is called and `current_path` contains at least one element
- **THEN** the last element is removed from `current_path`; `ITraversalContext.CurrentPath` reflects the removal

#### Scenario: MarkVisited adds to internal visited_pages
- **WHEN** `TraversalRuntimeContext.MarkVisited("home_screen")` is called
- **THEN** `visited_pages` (internal `HashSet<string>`) gains "home_screen"; `ITraversalContext.VisitedPages` reflects the addition

#### Scenario: MarkNodeVisited adds to internal visited_nodes
- **WHEN** `TraversalRuntimeContext.MarkNodeVisited("node-42")` is called
- **THEN** `visited_nodes` (internal `HashSet<string>`) gains "node-42"; `ITraversalContext.VisitedNodes` reflects the addition

#### Scenario: IncrementStepCount increments and reflects on interface
- **WHEN** `TraversalRuntimeContext.IncrementStepCount()` is called
- **THEN** `step_count` increments by 1; `ITraversalContext.StepCount` returns the new value

#### Scenario: IncrementRetryCount increments retry_count
- **WHEN** `TraversalRuntimeContext.IncrementRetryCount()` is called
- **THEN** `retry_count` increments by 1

#### Scenario: IncrementConsecutiveErrors increments consecutive_errors
- **WHEN** `TraversalRuntimeContext.IncrementConsecutiveErrors()` is called
- **THEN** `consecutive_errors` increments by 1

#### Scenario: ResetConsecutiveErrors resets consecutive_errors to 0
- **WHEN** `TraversalRuntimeContext.ResetConsecutiveErrors()` is called
- **THEN** `consecutive_errors` becomes 0

#### Scenario: Mutation methods are not accessible through ITraversalContext
- **WHEN** a variable typed as `ITraversalContext` is inspected for available methods
- **THEN** `AppendPath`, `PopPath`, `MarkVisited`, `MarkNodeVisited`, `IncrementStepCount`, `IncrementRetryCount`, `IncrementConsecutiveErrors`, and `ResetConsecutiveErrors` are NOT callable on that variable

### Requirement: TraversalContextSnapshot is a sealed record class with 8 immutable fields
`TraversalContextSnapshot` SHALL be a `sealed record class` with exactly 8 immutable fields for the AI advisor. It SHALL be produced by `TraversalRuntimeContext.CreateReadOnlySnapshot()` and SHALL be fully independent from the source context — subsequent mutations to the source MUST NOT affect the snapshot.

#### Scenario: Snapshot contains 8 immutable fields
- **WHEN** `TraversalContextSnapshot` is inspected for field declarations
- **THEN** it contains exactly: `NodeIds` (ImmutableArray<string>), `CurrentPath` (ImmutableArray<string>), `VisitedPages` (ImmutableHashSet<string>), `VisitedNodes` (ImmutableHashSet<string>), `MaxDepth` (int), `StepCount` (int), `ActionHistory` (ImmutableArray<ActionRecord>), `FailedNodes` (ImmutableDictionary<string, ErrorRecord>)

#### Scenario: Snapshot is sealed record class
- **WHEN** the type declaration of `TraversalContextSnapshot` is inspected
- **THEN** it is `sealed record class`

#### Scenario: Snapshot is independent from source context
- **WHEN** `TraversalRuntimeContext.CreateReadOnlySnapshot()` produces a snapshot, then the engine mutates the source context (e.g., calls `MarkVisited("new_page")`, `IncrementStepCount()`)
- **THEN** the snapshot's `VisitedPages` does NOT contain "new_page" and `StepCount` reflects the value at snapshot creation time, not the incremented value

#### Scenario: Snapshot NodeIds capture stack state at creation time
- **WHEN** `CreateReadOnlySnapshot()` is called while the NodeStack contains frames for "root", "settings", "volume"
- **THEN** `snapshot.NodeIds` contains ["root", "settings", "volume"] (or equivalent ID sequence); after the engine calls `PopPath()` on the context, `snapshot.NodeIds` remains unchanged

### Requirement: Reserved interface positions exist as TODO placeholders
`TraversalRuntimeContext` SHALL include TODO-comment placeholder positions for `IScrollHandler` and `IPageSnapshot`. These SHALL NOT have implementation in Phase 2; they SHALL be reserved for Phase 3.

#### Scenario: IScrollHandler position is reserved
- **WHEN** `TraversalRuntimeContext` is inspected for scroll-related fields or properties
- **THEN** a `IScrollHandler? ScrollHandler` property exists with a TODO comment indicating Phase 3 implementation; the property is null by default

#### Scenario: IPageSnapshot position is reserved
- **WHEN** `TraversalRuntimeContext` is inspected for snapshot-related fields or properties
- **THEN** an `IPageSnapshot? CurrentSnapshot` property exists with a TODO comment indicating Phase 3 implementation; the property is null by default

#### Scenario: Reserved positions are not on ITraversalContext
- **WHEN** `ITraversalContext` is inspected for `ScrollHandler` or `CurrentSnapshot`
- **THEN** these properties are NOT declared on the interface (they are internal-only reservations on `TraversalRuntimeContext`)

### Requirement: ULID generation produces 26-char Crockford Base32 identifiers
ULID generation SHALL produce 26-character strings using Crockford Base32 encoding. The first 10 characters SHALL encode a 48-bit millisecond timestamp; the last 16 characters SHALL encode an 80-bit random component. ULIDs SHALL be monotonically sortable within the same millisecond.

#### Scenario: ULID is exactly 26 characters
- **WHEN** a ULID is generated
- **THEN** the resulting string is exactly 26 characters long

#### Scenario: ULID uses only Crockford Base32 characters
- **WHEN** a ULID is generated and its characters are inspected
- **THEN** every character is in the set {0-9, A-Z excluding I, L, O, U} (Crockford Base32 alphabet)

#### Scenario: ULID timestamp portion is first 10 characters
- **WHEN** a ULID generated at a known millisecond timestamp is decoded
- **THEN** the first 10 characters encode the 48-bit millisecond timestamp matching the input time

#### Scenario: ULIDs within same millisecond are sortable
- **WHEN** two ULIDs are generated within the same millisecond in sequence
- **THEN** the later ULID sorts after the earlier ULID in lexicographic order

### Requirement: Trace writes use Log-and-Continue pattern
All trace write operations SHALL use a try-catch wrapper that catches exceptions and logs them without interrupting the traversal. Trace write failures SHALL NOT propagate to the engine loop. When `ITraceRecorder` is null or `active=False`, all trace methods SHALL be no-ops.

#### Scenario: Trace write failure does not interrupt traversal
- **WHEN** a trace write method (e.g., `RecordTransitionAsync`) throws an exception
- **THEN** the exception is caught, a warning is logged, and the traversal step continues without interruption

#### Scenario: Null recorder is a no-op
- **WHEN** the `ITraceRecorder` reference is null
- **THEN** all trace write and query methods return immediately without executing any logic

#### Scenario: Inactive recorder is a no-op
- **WHEN** the `ITraceRecorder` is set but `active=False` (no `trace_id`)
- **THEN** all trace write and query methods return immediately without executing any logic

#### Scenario: Trace write failure is logged
- **WHEN** a trace write method throws an exception
- **THEN** a warning-level log entry is produced containing the method name and exception summary; no error-level log or re-throw occurs
