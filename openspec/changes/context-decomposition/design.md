# Design: Context Decomposition

## Context

**Current State**: `TraversalRuntimeContext` (in `UniClaw.Core.StateMachine`) contains 30 mutable fields representing 5 subsystems. This is a God Object anti-pattern.

**Constraints**:
- D-15 defines canonical 5-subsystem structure — must not deviate
- D-V establishes interface extraction pattern — sub-contexts should follow this pattern
- ~603 tests must remain passing — refactoring cannot change observable behavior
- Engine runtime state requires mutable fields — cannot use immutable records without performance impact

**Stakeholders**:
- FSM/Handler code — primary consumers of context state
- Traversal engine — needs access to navigation and progress state
- Test code — benefits from ability to mock individual sub-contexts

---

## Goals / Non-Goals

**Goals**:
- Create 5 sub-context classes with clear responsibility boundaries
- Enable isolated mocking of individual subsystems for testing
- Maintain backward compatibility of `ITraversalContext` (delegation pattern)
- Allow independent evolution of each subsystem
- Implement with minimal runtime overhead (mutable classes, not record copies)

**Non-Goals**:
- Changing FSM behavior or state machine semantics
- Modifying `TraversalContextSnapshot` (D-III will address this)
- Moving sub-contexts to different namespaces
- Introducing new capabilities or changing spec-level requirements

---

## Decisions

### Decision 1: Container Pattern (vs. Complete Removal)

**Choice**: `TraversalRuntimeContext` becomes a Container holding 5 sub-contexts, rather than being completely removed.

**Rationale**:
- Engine consumers only need 1 reference to the context
- `ITraversalContext` remains valid through delegation
- Backward compatible — external code sees same interface
- Alternative (complete removal) would require consumers to hold 5 separate references

**Alternatives Considered**:
- **Complete removal**: Too disruptive, consumers would need significant refactoring
- **Facade with internal delegation**: Similar to Container but hides sub-contexts; rejected because Container makes boundaries explicit

---

### Decision 2: Mutable Sealed Class (vs. Immutable Record)

**Choice**: Each sub-context is `sealed class` with mutable private fields.

**Rationale**:
- Engine runtime state changes frequently (every FSM step)
- Record `with` expressions would create many copies
- Current `TraversalRuntimeContext` is mutable — keeping consistency
- Domain layer uses `sealed record` for pure data models, but this is runtime state

**Alternatives Considered**:
- **Immutable record**: Rejected due to copy overhead on frequent updates
- **Struct**: Rejected due to semantics (this is reference-type state)

---

### Decision 3: All in StateMachine Namespace

**Choice**: All 5 sub-contexts remain in `UniClaw.Core.StateMachine`.

**Rationale**:
- Current `TraversalRuntimeContext` is in `StateMachine`
- These are FSM/Handler runtime states, not Traversal abstractions
- Minimal change — no cross-namespace moves
- Even though Navigation is consumed by Traversal layer, it belongs to FSM state machine

**Alternatives Considered**:
- **Split by consumer**: Navigation → `Traversal`, others → `StateMachine` — Rejected for inconsistency
- **All in Traversal**: Rejected because these are FSM states, not engine abstractions

---

### Decision 4: Read-Only Interfaces for Each Sub-context

**Choice**: Extract `INavigationContext`, `IErrorContext`, etc. — interfaces expose only getters.

**Rationale**:
- Enables isolated mocking for tests
- Follows D-V pattern (interface extraction)
- Isolates mutation methods (only concrete class has `IncrementStepCount()`, etc.)
- Prepares for D-III (ITraversalContext reform)

**Alternatives Considered**:
- **No interfaces**: Simpler but loses testability
- **Interfaces with setters**: Rejected to keep mutation only in concrete class

---

### Decision 5: Phase-by-Phase Implementation

**Choice**: Implement one sub-context at a time in order: Navigation → Error → Session → Progress → Cache.

**Rationale**:
- Each phase can be tested independently (~603 tests must pass)
- Navigation is most complex — validates the pattern early
- Lower risk than big-bang refactoring
- Issues caught early, easier rollback

**Alternatives Considered**:
- **All at once**: Higher risk, harder to debug failures
- **Random order**: No benefit, ordered by complexity is logical

---

## Sub-Context Structure

### NavigationContext (12 fields)

**Responsibility**: DFS traversal state

**Fields**:
- `INodeStack NodeStack` — DFS traversal stack
- `IReadOnlyList<string> CurrentPath` — Current traversal path
- `PageAnalysis? CurrentPageAnalysis` — Current page interpretation
- `VisitFingerprint? CurrentFingerprint` — Page identity for revisit detection
- `IReadOnlySet<string> VisitedPages` — Visited page fingerprints
- `IReadOnlySet<string> VisitedNodes` — Visited node IDs
- `IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren` — Per-node visited children
- `IReadOnlySet<string> VisitedLevel1Menus` — DFS traversal decision
- `IReadOnlySet<string> VisitedLevel2Menus` — DFS traversal decision
- `ContentNode? PageTree` — Dynamic child enumeration data structure
- `ITraversalNode? CurrentFrame` — Current navigation position
- Private `ReadOnlyDictionary` cache for VisitedChildren

**Mutation Methods**: `AppendPath`, `PopPath`, `MarkVisited`, `MarkNodeVisited`, `AddVisitedChild`, setters for analysis/fingerprint/tree/frame

---

### ErrorContext (5 fields)

**Responsibility**: Error tracking and recovery state

**Fields**:
- `IReadOnlyDictionary<string, ErrorRecord> FailedNodes` — Failed node registry
- `int ConsecutiveErrors` — Error streak counter
- `int RetryCount` — Current node retry counter
- `Exception? LastError` — Most recent exception
- `IReadOnlyList<Exception>? ExceptionChain` — Error accumulation

**Mutation Methods**: `IncrementConsecutiveErrors`, `ResetConsecutiveErrors`, `IncrementRetryCount`, `AddFailedNode`, setters

---

### SessionContext (4 fields)

**Responsibility**: Macro session state

**Fields**:
- `string TraceId` — Traversal session identifier
- `GlobalState GlobalState` — Macro FSM state (setter on concrete class per D-7)
- `string? DeviceExperience` — Set once per session
- `string? AIProvider` — Set once per session

**Mutation Methods**: `GlobalState` setter (concrete class only), setters for device/AI

---

### ProgressContext (5 fields)

**Responsibility**: Progress control and pacing

**Fields**:
- `int StepCount` — Step counter
- `int MaxDepth` — Maximum traversal depth
- `CompletionPolicy? CompletionPolicy` — Completion decision logic
- `IReadOnlyList<ActionRecord> ActionHistory` — Recent action audit (max 5)
- `int WaitAfterActionMs` — Post-action delay

**Mutation Methods**: `IncrementStepCount`, `AddActionHistory`, setters for policy/depth/wait

---

### CacheContext (2+2 fields)

**Responsibility**: Cache and configuration

**Fields**:
- `IReadOnlyDictionary<string, object> PageCache` — Cached page data
- `bool CacheValid` — Cache validity flag
- `object? ScrollHandler` — Phase 3 reserved
- `object? CurrentSnapshot` — Phase 3 reserved

**Mutation Methods**: `SetCacheValid`, PageCache indexer access

---

## TraversalRuntimeContext Container

```csharp
public sealed class TraversalRuntimeContext
{
    // 5 sub-contexts (created in constructor, never replaced)
    public NavigationContext Navigation { get; }
    public ErrorContext Error { get; }
    public SessionContext Session { get; }
    public ProgressContext Progress { get; }
    public CacheContext Cache { get; }

    // ITraversalContext implementation delegates to sub-contexts
    public INodeStack NodeStack => Navigation.NodeStack;
    public IReadOnlyList<string> CurrentPath => Navigation.CurrentPath;
    public IReadOnlySet<string> VisitedPages => Navigation.VisitedPages;
    // ... all other properties delegate

    // Constructor initializes all sub-contexts
    public TraversalRuntimeContext(string traceId, int maxDepth = 10, NodeStack? nodeStack = null)
    {
        Navigation = new NavigationContext(traceId, maxDepth, nodeStack);
        Error = new ErrorContext();
        Session = new SessionContext(traceId);
        Progress = new ProgressContext(maxDepth);
        Cache = new CacheContext();
    }

    // CreateReadOnlySnapshot unchanged (D-III will revisit)
    public TraversalContextSnapshot CreateReadOnlySnapshot() { ... }
}
```

---

## Consumer Migration

**Pattern**: All consumers change from `context.Field` to `context.Subsystem.Field`

**Examples**:
- `context.VisitedPages` → `context.Navigation.VisitedPages`
- `context.IncrementStepCount()` → `context.Progress.IncrementStepCount()`
- `context.FailedNodes.TryGetValue(nodeId, out var error)` → `context.Error.FailedNodes.TryGetValue(nodeId, out var error)`

**Major Consumers**:
- `DynamicChildManager` → `context.Navigation.VisitedLevel1Menus`, `context.Navigation.PageTree`
- `ErrorHandler`/`RecoveryExecutor` → `context.Error.FailedNodes`, `context.Error.ConsecutiveErrors`
- `GlobalFSM` → `context.Session.GlobalState`
- `CompletionDetector` → `context.Progress.StepCount`, `context.Progress.MaxDepth`
- `PageCacheManager` → `context.Cache.PageCache`, `context.Cache.CacheValid`
- `TraceCoordinator` → `context.Session.TraceId`
- `NodeStackAdapter` → `context.Navigation.NodeStack`
- `StepOrchestrator` → Multiple sub-contexts

---

## Risks / Trade-offs

### Risk 1: Large-scale refactoring breaks tests

**Mitigation**: Phase-by-phase implementation with full test run after each phase. Rollback if tests fail.

---

### Risk 2: Consumer migration misses edge cases

**Mitigation**: Comprehensive search for all `TraversalRuntimeContext` usage. Compile errors are acceptable — they catch missed migrations.

---

### Risk 3: Delegation pattern in Container adds indirection

**Mitigation**: Acceptable trade-off for clear boundaries. Delegation is cheap (property access).

---

### Risk 4: Sub-context constructors need coordination

**Mitigation**: `TraversalRuntimeContext` constructor creates all sub-contexts with shared parameters (traceId, maxDepth, nodeStack).

---

## Migration Plan

### Phase 1: NavigationContext

1. Create `INavigationContext.cs` (read-only interface)
2. Create `NavigationContext.cs` (sealed class, mutable)
3. Modify `TraversalRuntimeContext`:
   - Add `Navigation` property
   - Delegate navigation-related properties
   - Update constructor
4. Update consumers: `DynamicChildManager`, `NodeStackAdapter`, `StepOrchestrator`
5. Run tests — all 603+ must pass
6. Commit

### Phase 2-5: ErrorContext, SessionContext, ProgressContext, CacheContext

Repeat same pattern for each sub-context, updating respective consumers.

### Final State

- 5 sub-contexts implemented and integrated
- All consumers migrated
- All tests passing
- `ITraversalContext` unchanged (delegation maintains compatibility)

---

## Open Questions

None. All 6 design decisions are confirmed.

---

## Next Steps

1. Implement NavigationContext (Phase 1)
2. Implement remaining sub-contexts (Phases 2-5)
3. Archive this change → extract decisions to `docs/system/decisions/log.md`
4. Begin D-III (ITraversalContext reform)
