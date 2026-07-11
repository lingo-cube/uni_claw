## Context

TraversalRuntimeContext is a God Object holding 26 core mutable fields (plus 2 reserved for Phase 3, total 28). The current grouping into 5 subsystems is **derived** from usage patterns, not canonically defined. 8 fields carry ambiguity markers (`?`) indicating multiple plausible subsystems.

The roadmap (20-b §5) defines a candidate 5-subsystem scheme, but explicitly marks it as a **design draft, not a locked decision**. D-I (P2 Context decomposition) cannot proceed without D-15's canonical output — every P2 decomposition plan must be based on D-15's final field ownership table, not on the candidate scheme.

Current code structure:
- `TraversalRuntimeContext.cs`: flat 26-field sequence, no `#region` blocks or group annotations
- `ArchitectureGuardTests.cs`: 4 guard test classes (EnumValue, DependencyDirection, NamespaceIsolation, CodingConvention) — no subsystem boundary guards yet
- `docs/system/layers/state-machine.md`: flags D-I as God Object problem, no canonical subsystem definition

## Goals / Non-Goals

**Goals:**

1. **Resolve all 8 ambiguity markers** with definitive canonical subsystem attribution and rationale
2. **Produce canonical field ownership table** — every TraversalRuntimeContext field assigned to exactly 1 subsystem, no `?` markers remaining
3. **Add subsystem boundary guard tests** — verify that TraversalRuntimeContext field annotations match the canonical table
4. **Add region annotations** in `TraversalRuntimeContext.cs` — mark each field's subsystem归属 in code comments (not `#region`, per readonly-isolation pattern: comments for attribution, regions for IDE navigation are optional)
5. **Update `docs/system/layers/state-machine.md`** §5 to reflect canonical subsystem structure and resolved ambiguities

**Non-Goals:**

- **Do NOT decompose TraversalRuntimeContext into separate classes** — that's D-I (P2), which depends on D-15's output
- **Do NOT change field signatures, access patterns, or mutation methods** — this change is documentation/annotation level only
- **Do NOT decide namespace allocation for future sub-contexts** — that's D-I's scope
- **Do NOT extract interfaces** — that's D-V (P1, parallel but independent)
- **Do NOT change ITraversalContext** — that's D-III (P2)

## Decisions

### D-15-1: 5 Subsystem Canonical Names

| # | Canonical Name | English Name | Responsibility |
|---|----------------|-------------|----------------|
| 1 | NavigationContext | DFS traversal | Node selection, visited tracking, page identity, stack management |
| 2 | ErrorContext | Error tracking | Error recording, retry counting, failure tracking, recovery state |
| 3 | SessionContext | Macro state | Global FSM state, trace identity, device/AI configuration |
| 4 | ProgressContext | Progress control | Step counting, completion policy, action audit, timing config |
| 5 | CacheContext | Cache & config | Page cache, cache validity, screen snapshots (Phase 3) |

**Alternative considered**: 4 subsystems (merge CacheContext into ProgressContext). Rejected because cache semantics (read/write/clear/invalidate lifecycle) are fundamentally different from progress semantics (increment/check/reset lifecycle). Merging would create a sub-context with two independent mutation patterns, making D-I decomposition harder.

**Alternative considered**: 6 subsystems (split NavigationContext into NavigationCore + VisitedTracker). Rejected because visited tracking and node selection are tightly coupled — DynamicChildManager needs both to decide the next child. Separating them would require cross-subsystem queries, violating the boundary principle.

### D-15-2: Canonical Field Ownership Table

| Field | Type | Subsystem | Rationale |
|-------|------|-----------|-----------|
| `_traceId` | string | SessionContext | Identifies the traversal session; set once at start |
| `_nodeStack` | NodeStack | NavigationContext | DFS stack for frame push/pop — core navigation data structure |
| `_currentPath` | List<string> | NavigationContext | DFS path tracking — where we are in the tree |
| `_currentPageAnalysis` | PageAnalysis? | NavigationContext | Current page interpretation — input for DFS child selection |
| `_currentFingerprint` | VisitFingerprint? | NavigationContext | Page identity for DFS revisit detection (→ resolved from ambiguity) |
| `_cacheValid` | bool | CacheContext | Cache validity flag — controls pageCache reuse lifecycle (→ resolved) |
| `_visitedPages` | HashSet<string> | NavigationContext | DFS visited page set — prevents revisit in traversal |
| `_visitedLevel1Menus` | HashSet<string> | NavigationContext | DFS visited menu set — DynamicChildManager checks this to skip revisited L1 menus (→ resolved) |
| `_visitedLevel2Menus` | HashSet<string> | NavigationContext | DFS visited menu set — same pattern as L1, for L2 menus (→ resolved) |
| `_visitedNodes` | HashSet<string> | NavigationContext | DFS visited node set — core traversal visited tracking |
| `_visitedChildren` | Dictionary<string, HashSet<string>> | NavigationContext | Per-node child visited map — DFS traversal anti-loop mechanism |
| `_pageTree` | ContentNode? | NavigationContext | Parsed screen structure — DynamicChildManager uses it for child enumeration (→ resolved) |
| `_actionHistory` | List<ActionRecord> | ProgressContext | Recent actions audit trail — progress record of what was done (→ resolved) |
| `_failedNodes` | Dictionary<string, ErrorRecord> | ErrorContext | Failed node registry — error tracking with failure reasons |
| `_consecutiveErrors` | int | ErrorContext | Error streak counter — error recovery decision input |
| `_maxDepth` | int | ProgressContext | Maximum traversal depth — progress constraint |
| `_stepCount` | int | ProgressContext | Step counter — progress tracking |
| `_retryCount` | int | ErrorContext | Retry counter for current node — error recovery input |
| `_completionPolicy` | CompletionPolicy? | ProgressContext | Termination rules — "when should traversal end?" (→ resolved) |
| `_deviceExperience` | string? | SessionContext | Device category metadata — session-level configuration (→ resolved) |
| `_globalState` | GlobalState | SessionContext | Macro FSM lifecycle state — session state machine (→ resolved, D-7 acknowledged) |
| `_lastError` | Exception? | ErrorContext | Most recent exception — error tracking |
| `_exceptionChain` | List<Exception>? | ErrorContext | Error accumulation chain — error tracking |
| `_aiProvider` | string? | SessionContext | AI service name — session-level configuration (→ resolved) |
| `_pageCache` | Dictionary<string, object> | CacheContext | Cached page data — PageCacheManager manages this |
| `_waitAfterActionMs` | int | ProgressContext | Post-action delay — timing configuration for progress pacing |
| `_scrollHandler` | object? (Phase 3) | CacheContext | Scroll state manager — cache/interaction sub-component |
| `_currentSnapshot` | object? (Phase 3) | CacheContext | Page snapshot — PageSnapshotManager manages this |
| `CurrentFrame` | IStackFrame | NavigationContext | Current navigation position — alias for stack top |
| `_visitedChildrenReadOnly` | Lazy wrapper | NavigationContext | Read-only projection of `_visitedChildren` — same subsystem as source |

**Subsystem field counts**: NavigationContext (12), ErrorContext (5), SessionContext (5), ProgressContext (5), CacheContext (4+2 reserved)

### D-15-3: Ambiguity Resolutions

| Ambiguous Field | Candidate A | Candidate B | Decision | Rationale |
|----------------|-------------|-------------|----------|-----------|
| `_visitedLevel1Menus` | NavigationContext (DFS visited) | CacheContext (dedup) | **NavigationContext** | Primary consumer is DynamicChildManager which checks visited menus for DFS traversal decisions. Dedup is a side-effect, not the primary purpose |
| `_visitedLevel2Menus` | NavigationContext (DFS visited) | CacheContext (dedup) | **NavigationContext** | Same pattern as L1 — DFS traversal decision, not cache dedup |
| `_completionPolicy` | ProgressContext (termination) | CacheContext (strategy) | **ProgressContext** | CompletionPolicy answers "when should traversal end?" — a progress/termination question, not a cache strategy |
| `_currentFingerprint` | NavigationContext (revisit detection) | CacheContext (invalidation trigger) | **NavigationContext** | VisitFingerprint is fundamentally a page identity marker for DFS revisit detection. Cache invalidation is a downstream side-effect triggered by fingerprint mismatch |
| `_globalState` | SessionContext (macro FSM) | Remove entirely (→ D-7) | **SessionContext** | GlobalState represents the macro session lifecycle managed by GlobalFSM. D-7 (M-14) proposes removing it from ITraversalContext — but within the engine, it belongs to the session lifecycle, not removed. Acknowledged: D-7's concern is about ITraversalContext exposure, not subsystem attribution |
| `_deviceExperience` | SessionContext (metadata) | CacheContext (AI config) | **SessionContext** | Set once per session, never changes during traversal. Session-level metadata, not runtime cache |
| `_aiProvider` | SessionContext (config) | CacheContext (AI config) | **SessionContext** | Same reasoning as deviceExperience — set once, session-level configuration |
| `_pageTree` | NavigationContext (child enumeration) | CacheContext (parsed content) | **NavigationContext** | DynamicChildManager uses PageTree for child enumeration — it's the DFS traversal's primary navigation data structure. The "parsed content" aspect is how it's produced, not how it's consumed |
| `_actionHistory` | NavigationContext (action log) | ProgressContext (audit) | **ProgressContext** | Action history records what has been done recently — it's a progress/audit trail. Navigation decisions don't query it; only debugging and progress reporting consume it |
| `_cacheValid` | CacheContext (invalidation flag) | ProgressContext (checkpoint) | **CacheContext** | Literally a cache validity flag that controls whether `_pageCache` data can be reused. Set false on page change, set true after cache refresh. Cache lifecycle semantics, not progress semantics |

### D-15-4: Implementation Approach — Annotation Only

Fields will be annotated with subsystem归属 via **structured comments** (not `#region` blocks):

```csharp
// ── NavigationContext ──────────────────────────────
private string _traceId;  // SessionContext
private NodeStack _nodeStack;  // NavigationContext
```

Rationale for comments over `#region`:
- `#region` blocks group adjacent fields, but canonical ordering may not match adjacency
- Comments on each field allow per-field attribution regardless of declaration order
- Guard tests can read comment annotations via source analysis (if needed in P2)
- `#region` is IDE navigation convenience, not architectural annotation

**Alternative considered**: `#region` blocks per subsystem. Rejected because: (1) fields may be reordered during D-I decomposition, breaking region boundaries; (2) regions don't carry attribution for cross-region fields; (3) C-9 exception already exists for TraversalRuntimeContext (not a sealed record), so regions wouldn't violate convention, but comments are more resilient to future reordering.

### D-15-5: Guard Test Design

Add a new test class `SubsystemBoundaryGuardTests` in `ArchitectureGuardTests.cs` with one test:

**`TraversalRuntimeContext_FieldAttributionMatchesCanonicalTable`**: Reflect over `TraversalRuntimeContext` private fields, verify each field has a comment annotation matching the canonical table. This is a source-code-level check, not a runtime reflection check — the guard reads the `.cs` file and parses subsystem annotations.

**Alternative considered**: Runtime reflection guard (check field names against a dictionary). Rejected because: (1) runtime reflection on private fields is fragile; (2) the annotation is a design-level artifact, not a runtime concern; (3) source-level parsing ensures annotations are visible in code review.

**Pragmatic decision**: Since source-file parsing adds significant test infrastructure complexity, and this change is annotation-only (no structural change), the initial guard will be a **field-count-per-subsystem assertion** using runtime reflection. This verifies the decomposition is numerically consistent (NavigationContext=12, ErrorContext=5, SessionContext=5, ProgressContext=5, CacheContext=4). Source-level annotation parsing can be added in P2 when D-I actually splits the class.

### D-15-6: Document Update Strategy

Update `docs/system/layers/state-machine.md` §5 (TraversalRuntimeContext) to replace the flat field listing with the canonical subsystem-attributed table. This makes the design doc the authoritative reference for D-I decomposition.

No changes to constitution docs (constitution/locked-enums.md, constraints.md) — subsystem naming is a Tier 3 layer concern, not a Tier 1 constitutional constraint.

Add entry to `docs/system/decisions/log.md` for D-15 decisions (5 subsystem names, 10 ambiguity resolutions).

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| D-15 produces a different subsystem structure than roadmap assumes (e.g., fewer/more subsystems) | Roadmap §5 explicitly marks the candidate as draft; D-15 output is canonical. If structure differs, update roadmap accordingly |
| Annotation-only approach means no runtime enforcement of subsystem boundaries | Field-count-per-subsystem guard provides numerical consistency. Full boundary enforcement happens when D-I physically splits the class (P2) |
| `_globalState` in SessionContext contradicts D-7 (remove from ITraversalContext) | These are different concerns: D-7 addresses ITraversalContext exposure, D-15 addresses internal subsystem attribution. Both are valid — GlobalState belongs to SessionContext internally, but shouldn't leak through ITraversalContext |
| `_visitedLevel1Menus`/`_visitedLevel2Menus` in NavigationContext — they also serve dedup purpose | Primary purpose is DFS traversal decision. Dedup is a beneficial side-effect. Attribution goes to primary purpose |
| Comment annotations may drift from actual field names during future refactoring | Guard test validates field counts per subsystem. D-I decomposition will make boundaries physical (separate classes), eliminating drift risk |
