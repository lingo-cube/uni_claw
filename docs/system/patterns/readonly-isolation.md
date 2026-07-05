# Tier 2 · Patterns — Readonly Isolation

> Update frequency: on collection exposure change (new readonly wrapper, new snapshot field, or safety annotation convention altered)

## Pattern Definition

**Readonly Isolation** is a three-level collection safety pattern that prevents external consumers from mutating engine-internal state through reference leakage. The pattern classifies every collection exposure by its cast-back resistance and assigns a safety level annotation.

The pattern guarantees that **code consuming `ITraversalContext` cannot modify engine-internal collections** -- either through the interface (which has no mutation methods), or through cast-back tricks (which are blocked at Level 3, documented at Level 2, and permitted only at Level 1 for engine-internal access).

Constitution reference: C-6 (ReadOnlySetWrapper cast-back blocking). Decision reference: D-9.

## Three-Level Collection Safety

| Safety Level | Pattern | Cast-back resistance | Mechanism | Example in this project |
|---|---|---|---|---|
| **Level 3 (strongest)** | `ReadOnlySetWrapper` private sealed class | Cast-back throws `InvalidCastException` | Wrapper does not inherit `HashSet`; runtime type is `ReadOnlySetWrapper`, not `HashSet<string>` | `VisitedChildren` nested sets |
| **Level 2 (interface-only)** | `IReadOnlySet<string>` / `IReadOnlyList<string>` / `.AsReadOnly()` | Cast-back technically possible but interface exposes no mutation methods | Interface restriction + explicit safety annotation documenting Phase 3 improvement path | `VisitedPages`, `VisitedNodes`, `CurrentPath` |
| **Level 1 (engine-internal)** | Direct `HashSet<string>` / `List<string>` / `Dictionary<,>` | No resistance -- full mutable access | Engine-internal fields, never exposed through `ITraversalContext` | `_visitedLevel1Menus`, `_visitedLevel2Menus`, `_pageCache`, `_actionHistory` |

### Level 3 -- ReadOnlySetWrapper

The strongest protection. Used for `VisitedChildren`, a nested dictionary whose values are `HashSet<string>`. Without the wrapper, a consumer could cast `(HashSet<string>)context.VisitedChildren["key"]` and call `.Add("hacked")`, silently corrupting engine state.

**How it works:**

```
TraversalRuntimeContext
  └── private sealed class ReadOnlySetWrapper : IReadOnlySet<string>
        ├── wraps HashSet<string> (no inheritance)
        ├── delegates: Count, Contains, Overlaps, SetEquals, etc.
        ├── does NOT expose: Add, Remove, Clear
        └── cast-back: (HashSet<string>)wrapper → InvalidCastException
            runtime type is ReadOnlySetWrapper, not HashSet<string>
```

The wrapper is a **private sealed class** nested inside `TraversalRuntimeContext`. It cannot be inherited, cannot be instantiated externally, and its runtime type is opaque to any consumer that only sees the `IReadOnlySet<string>` interface.

**Implementation detail (lazy rebuild):** `VisitedChildren` uses a cached `ReadOnlyDictionary<string, IReadOnlySet<string>>` that is invalidated (`_visitedChildrenReadOnly = null`) whenever `AddVisitedChild` mutates the underlying `_visitedChildren`. The next read triggers a full rebuild via `GetVisitedChildrenReadOnly()`, which wraps every inner `HashSet<string>` in a fresh `ReadOnlySetWrapper`. This ensures structural consistency: a stale wrapper never references a set that has since been modified.

**Guard test:** `VisitedChildrenIsolationTests.VisitedChildren_CastBackToHashSet_ThrowsInvalidCastException` (constitution C-6).

### Level 2 -- Interface-only protection

The middle tier. Used for flat collections (`VisitedPages`, `VisitedNodes`, `CurrentPath`) where the `IReadOnlySet<string>` or `IReadOnlyList<string>` interface itself does not expose any mutation method. A determined consumer could still cast `(HashSet<string>)context.VisitedPages` and call `.Add("hacked")`, but this is documented rather than mechanically blocked.

| Property | Interface type | Underlying type | Cast-back risk | Safety annotation |
|---|---|---|---|---|
| `VisitedPages` | `IReadOnlySet<string>` | `HashSet<string>` (direct reference) | Cast to `HashSet<string>` succeeds | `// SAFETY: Level 2 — 接口级安全（IReadOnlySet 不暴露修改方法），cast-back 级需 Phase 3 改进` |
| `VisitedNodes` | `IReadOnlySet<string>` | `HashSet<string>` (direct reference) | Cast to `HashSet<string>` succeeds | `// SAFETY: Level 2 — 接口级安全（IReadOnlySet 不暴露修改方法），cast-back 级需 Phase 3 改进` |
| `CurrentPath` | `IReadOnlyList<string>` | `List<string>.AsReadOnly()` wrapper | Cast to `List<string>` fails (returns `ReadOnlyCollection<string>`) | `// SAFETY: Level 3 — CurrentPath 通过 .AsReadOnly() 包装返回，防止 cast-back 修改` |

Note: `CurrentPath` actually achieves Level 3 safety through `.AsReadOnly()`, which returns a `ReadOnlyCollection<string>` that does not inherit `List<string>`. It is documented as Level 3 despite being in the "interface-only" tier because the mechanical cast-back block is already in place.

**Phase 3 improvement:** `VisitedPages` and `VisitedNodes` should be upgraded to Level 3 using `ReadOnlySetWrapper`, matching `VisitedChildren`. The current Level 2 is an acceptable risk because:
- The `ITraversalContext` interface has no mutation methods, so accidental modification is impossible.
- Intentional cast-back modification requires knowledge of the concrete type, which consumers should not have.
- The engine's `MarkVisited` and `MarkNodeVisited` methods are the only intended mutation paths.

### Level 1 -- Engine-internal direct access

The weakest tier, by design. These fields are **never exposed through `ITraversalContext`** -- they are only accessible on the concrete `TraversalRuntimeContext` class, which the engine uses internally.

| Field | Type | Access scope | Safety annotation |
|---|---|---|---|
| `_visitedLevel1Menus` | `HashSet<string>` | Engine-internal (property `VisitedLevel1Menus` on concrete class) | `// SAFETY: Level 1 — engine-internal, not on ITraversalContext` |
| `_visitedLevel2Menus` | `HashSet<string>` | Engine-internal (property `VisitedLevel2Menus` on concrete class) | `// SAFETY: Level 1 — engine-internal, not on ITraversalContext` |
| `_pageCache` | `Dictionary<string, object>` | Engine-internal (property `PageCache` on concrete class) | `// SAFETY: Level 1 — engine-internal, not on ITraversalContext` |
| `_actionHistory` | `List<ActionRecord>` | Engine-internal (property `ActionHistoryInternal` on concrete class) | `// SAFETY: Level 1 — engine-internal, not on ITraversalContext` |
| `_failedNodes` | `Dictionary<string, ErrorRecord>` | Not exposed at all (snapshot copies only) | `// SAFETY: Level 3 — only exposed via TraversalContextSnapshot as ImmutableDictionary` |

Level 1 fields are safe because the `TraversalEngine` holds the only reference to the concrete `TraversalRuntimeContext`. No external consumer receives the concrete class -- they receive the `ITraversalContext` interface, which does not declare these properties.

## ITraversalContext Interface Isolation

The interface is the boundary between engine internals and external consumers. It deliberately exposes only readonly collection views and a small set of controlled setters.

### Readonly properties (no mutation possible through interface)

| Property | Type | Mutation path |
|---|---|---|
| `NodeStack` | `INodeStack` | Push/Pop/Clear on concrete `NodeStack` class (not on `INodeStack` -- but `INodeStack` does expose these; see note below) |
| `CurrentPath` | `IReadOnlyList<string>` | `AppendPath` / `PopPath` on `TraversalRuntimeContext` only |
| `VisitedPages` | `IReadOnlySet<string>` | `MarkVisited` on `TraversalRuntimeContext` only |
| `VisitedChildren` | `IReadOnlyDictionary<string, IReadOnlySet<string>>` | `AddVisitedChild` on `TraversalRuntimeContext` only |
| `VisitedNodes` | `IReadOnlySet<string>` | `MarkNodeVisited` on `TraversalRuntimeContext` only |
| `StepCount` | `int` | `IncrementStepCount` on `TraversalRuntimeContext` only |

**Note on `INodeStack`:** The `INodeStack` interface does expose `Push`, `Pop`, and `Clear`, which are mutation methods. This is an accepted deviation because `NodeStack` is the DFS traversal stack -- its mutation is tightly coupled to the frame lifecycle (push on enter, pop on exit) and the engine controls the frame lifecycle. Phase 3 may remove mutation methods from `INodeStack` to achieve full interface-level readonly isolation.

### Setter properties (controlled mutation through interface)

| Property | Type | Why setter is on interface |
|---|---|---|
| `CurrentFrame` | `ITraversalNode?` | FSM updates frame every step; setter avoids `SetCurrentFrame` method explosion |
| `GlobalState` | `GlobalState` | FSM transitions update macro state; coordination between TraversalFSM and GlobalFSM (see M-14 / D-7) |
| `LastError` | `Exception?` | Error handler assigns error; convenience setter avoids `SetLastError` method |

These three setters are on `ITraversalContext` because they are part of the FSM lifecycle contract. Every FSM step updates `CurrentFrame`; every macro-state transition updates `GlobalState`; every error handling step updates `LastError`. Putting setters on the interface avoids requiring consumers to cast to the concrete class for these routine updates.

**Known deviation (M-14 / D-7):** `GlobalState` having a setter on `ITraversalContext` means any consumer can set it, not just the GlobalFSM. This violates the FSM independence principle (C-4). The deviation is deferred to Phase 3 because the breaking change affects 6 consumers and there is no runtime defect.

### Mutation methods NOT on ITraversalContext

These methods exist only on the concrete `TraversalRuntimeContext` class. They are the engine's internal mutation API.

| Method | Collection mutated | Purpose |
|---|---|---|
| `AppendPath(string)` | `_currentPath` | Add page to current traversal path |
| `PopPath()` | `_currentPath` | Remove last path entry (backtrack) |
| `MarkVisited(string)` | `_visitedPages` | Record visited page fingerprint |
| `MarkNodeVisited(string)` | `_visitedNodes` | Record visited node ID |
| `AddVisitedChild(string, string)` | `_visitedChildren` | Record parent-child traversal; invalidates ReadOnlyDictionary cache |
| `IncrementStepCount()` | `_stepCount` | Count traversal steps |
| `IncrementRetryCount()` | `_retryCount` | Count retries for backoff calculation |
| `IncrementConsecutiveErrors()` | `_consecutiveErrors` | Track error streak |
| `ResetConsecutiveErrors()` | `_consecutiveErrors` | Reset streak after success |
| `AddActionHistory(ActionRecord)` | `_actionHistory` | Append action (keep last 5) |
| `AddFailedNode(string, ErrorRecord)` | `_failedNodes` | Record failed node with error info |

Field setters (also not on interface): `SetCurrentPageAnalysis`, `SetCurrentFingerprint`, `SetCacheValid`, `SetPageTree`, `SetCompletionPolicy`, `SetDeviceExperience`, `SetExceptionChain`, `SetAIProvider`, `SetWaitAfterActionMs`.

## TraversalContextSnapshot

The snapshot is a fully immutable, fully independent copy of the context state at a specific moment. It is designed for AI advisor consumption -- the advisor receives a snapshot, not a live context reference, so it cannot accidentally or intentionally modify engine state.

### 8 immutable fields

| Field | Type | Copy mechanism | Null handling |
|---|---|---|---|
| `NodeIds` | `ImmutableArray<string>` | Builder loop over `NodeStack` depth | `IsDefault` → `ImmutableArray<string>.Empty` |
| `CurrentPath` | `ImmutableArray<string>` | `ImmutableArray.CreateRange(_currentPath)` | `IsDefault` → `ImmutableArray<string>.Empty` |
| `VisitedPages` | `ImmutableHashSet<string>` | `_visitedPages.ToImmutableHashSet()` | `null` → `ImmutableHashSet<string>.Empty` |
| `VisitedNodes` | `ImmutableHashSet<string>` | `_visitedNodes.ToImmutableHashSet()` | `null` → `ImmutableHashSet<string>.Empty` |
| `MaxDepth` | `int` | Direct value copy | No null risk (int) |
| `StepCount` | `int` | Direct value copy | No null risk (int) |
| `ActionHistory` | `ImmutableArray<ActionRecord>` | `ImmutableArray.CreateRange(_actionHistory)` | `IsDefault` → `ImmutableArray<ActionRecord>.Empty` |
| `FailedNodes` | `ImmutableDictionary<string, ErrorRecord>` | `_failedNodes.ToImmutableDictionary()` | `null` → `ImmutableDictionary<string, ErrorRecord>.Empty` |

### Isolation guarantee

After `CreateReadOnlySnapshot()` returns, any subsequent mutation on `TraversalRuntimeContext` (MarkVisited, IncrementStepCount, PopPath, etc.) has **zero effect** on the snapshot. The snapshot holds its own immutable copies, not references to the engine's mutable collections.

```
TraversalRuntimeContext (mutable, engine-owned)
  │
  ├── CreateReadOnlySnapshot()
  │     └── copies all collections to Immutable* types
  │     └── NodeIds: builder loop (not NodeStack reference)
  │     └── VisitedPages: ToImmutableHashSet() (deep copy)
  │     └── FailedNodes: ToImmutableDictionary() (deep copy)
  │
  └── TraversalContextSnapshot (immutable, AI advisor-owned)
        └── 8 fields, all ImmutableArray/ImmutableHashSet/ImmutableDictionary
        └── no reference back to TraversalRuntimeContext
        └── no mutation possible (Immutable* types have no Add/Remove)
```

The snapshot constructor also guards against `IsDefault` / `null` by replacing with empty immutable collections. This ensures the snapshot is always structurally valid, even if the source context had empty collections at creation time.

## Safety Annotation Convention

Every collection exposure in `TraversalRuntimeContext` must carry a safety annotation comment. The convention is:

```
// SAFETY: Level X — [reason]
```

| Annotation | Level | When to use |
|---|---|---|
| `// SAFETY: Level 3 — ReadOnlySetWrapper cast-back blocked` | 3 | Collection wrapped in a private sealed class that blocks cast-back to the mutable type |
| `// SAFETY: Level 3 — [collection].AsReadOnly() 包装返回，防止 cast-back 修改` | 3 | Collection wrapped in `ReadOnlyCollection<T>` which blocks cast-back to `List<T>` |
| `// SAFETY: Level 2 — 接口级安全（IReadOnlySet 不暴露修改方法），cast-back 级需 Phase 3 改进` | 2 | Interface restricts mutation but cast-back to concrete mutable type is possible |
| `// SAFETY: Level 1 — engine-internal, not on ITraversalContext` | 1 | Mutable collection not exposed through interface; engine-internal only |
| `// SAFETY: Level 3 — only exposed via TraversalContextSnapshot as Immutable*` | 3 | Mutable collection only copied into immutable snapshot; never directly exposed |

Annotations are placed on the property declaration, not on the backing field. This ensures the annotation is visible at the boundary where the exposure decision is made.

**Current annotations in `TraversalRuntimeContext`:**

| Property | Current annotation | Target level |
|---|---|---|
| `CurrentPath` | `Level 3 — .AsReadOnly() 包装返回` | 3 (achieved) |
| `VisitedPages` | `Level 2 — 接口级安全，cast-back 级需 Phase 3 改进` | 3 (Phase 3 upgrade planned) |
| `VisitedNodes` | `Level 2 — 接口级安全，cast-back 级需 Phase 3 改进` | 3 (Phase 3 upgrade planned) |
| `VisitedChildren` | `Level 3 — ReadOnlySetWrapper 确保值类型不可 cast-back 为 HashSet` | 3 (achieved) |
| `VisitedLevel1Menus` | (not yet annotated) | 1 (to annotate) |
| `VisitedLevel2Menus` | (not yet annotated) | 1 (to annotate) |
| `PageCache` | (not yet annotated) | 1 (to annotate) |
| `ActionHistoryInternal` | (not yet annotated) | 1 (to annotate) |

## When to Use Each Level

### Decision criteria

| Question | Answer → Level |
|---|---|
| Is the collection exposed through an interface to external consumers? | No → **Level 1** (engine-internal, annotate only) |
| Is cast-back modification a real risk (nested collections, AI advisor access, cross-assembly consumers)? | Yes → **Level 3** (ReadOnlySetWrapper / AsReadOnly / Immutable copy) |
| Is the collection flat (single-level), consumed only by trusted internal code, and mutation through interface is impossible? | Yes → **Level 2** (interface-only, annotate with Phase 3 upgrade path) |
| Does the consumer need a time-frozen view that must remain valid even after the source is mutated? | Yes → **TraversalContextSnapshot** (Immutable copy, zero reference leakage) |

### Pattern selection flowchart

```
Collection exposure decision:

1. Is the collection exposed through ITraversalContext (or any public interface)?
   └─ No  → Level 1: engine-internal direct access + SAFETY annotation
   └─ Yes → 2.

2. Will external consumers (AI advisor, cross-assembly code) receive this reference?
   └─ Yes, and they need a time-frozen view → TraversalContextSnapshot
   └─ Yes, but they only need live readonly access → 3.

3. Is the collection nested (dictionary of sets, etc.)?
   └─ Yes → Level 3: ReadOnlySetWrapper (cast-back must be blocked)
   └─ No  → 4.

4. Is cast-back to the mutable type possible?
   └─ Yes → Level 2: interface-only (annotate + plan Phase 3 upgrade)
            OR Level 3: .AsReadOnly() (if List→ReadOnlyCollection is sufficient)
   └─ No (wrapper already blocks it) → Level 3: achieved
```

### Common pitfalls

| Pitfall | How this pattern prevents it |
|---|---|
| Exposing `HashSet<string>` as `IReadOnlySet<string>` directly (reference identity preserved) | Level 3 requires `ReadOnlySetWrapper` for nested collections; Level 2 requires explicit annotation documenting the cast-back risk |
| Returning `List<T>` and trusting consumers not to modify it | Level 2 uses `IReadOnlyList<string>` interface; Level 3 uses `.AsReadOnly()` wrapper |
| Giving AI advisor a live context reference | TraversalContextSnapshot creates Immutable copies; no reference to mutable source |
| Adding mutation methods to `ITraversalContext` | Prohibited pattern (constitution prohibited-patterns.md); all mutation is on concrete `TraversalRuntimeContext` only |
| Forgetting to invalidate cached readonly wrapper when source is mutated | `AddVisitedChild` sets `_visitedChildrenReadOnly = null`; next read rebuilds |

## Relationship to Other Patterns

| Pattern | Relationship |
|---|---|
| **Dispatch Table** (patterns/dispatch-table.md) | Handlers receive `ITraversalContext` (readonly interface) as part of their context parameter; they cannot mutate collections through the interface |
| **Handler Pipeline** (patterns/handler-pipeline.md) | Pipeline steps that need to record state (visited pages, errors) use engine-internal mutation methods, not the interface |
| **FSM Design** (patterns/fsm-design.md) | FSM transitions update `CurrentFrame`, `GlobalState`, `LastError` through the three controlled setters on `ITraversalContext`; all other state mutation goes through engine-internal methods |

## Source Files

| File | What it contains |
|---|---|
| `src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs` | `TraversalRuntimeContext` (26 mutable fields), `ReadOnlySetWrapper` (private sealed class), `TraversalContextSnapshot` (8 immutable fields), all mutation methods and field setters |
| `src/UniClaw.Core/StateMachine/TraversalState.cs` | `ITraversalContext` interface (readonly collections + 3 setters), `INodeStack` interface, `ITraversalStateMachine` interface |
| `openspec/specs/readonly-set-wrapper/spec.md` | Spec for C-6: ReadOnlySetWrapper cast-back blocking requirement and test scenarios |
