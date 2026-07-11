## ADDED Requirements

### Requirement: 5 subsystem canonical definition

TraversalRuntimeContext SHALL be formally classified into exactly 5 subsystems with canonical names, each with a clear responsibility boundary:

| # | Canonical Name | Responsibility |
|---|----------------|----------------|
| 1 | NavigationContext | DFS traversal — node selection, visited tracking, page identity, stack management |
| 2 | ErrorContext | Error tracking — error recording, retry counting, failure tracking, recovery state |
| 3 | SessionContext | Macro state — global FSM state, trace identity, device/AI configuration |
| 4 | ProgressContext | Progress control — step counting, completion policy, action audit, timing config |
| 5 | CacheContext | Cache & config — page cache, cache validity, screen snapshots (Phase 3 reserved) |

#### Scenario: Subsystem count is exactly 5
- **WHEN** the canonical subsystem definition is referenced
- **THEN** there SHALL be exactly 5 subsystems: NavigationContext, ErrorContext, SessionContext, ProgressContext, CacheContext
- **THEN** no subsystem SHALL be added or removed without a new spec-driven change

#### Scenario: Each subsystem has a unique responsibility
- **WHEN** two subsystems' responsibilities are compared
- **THEN** they SHALL have no overlapping responsibility domain
- **THEN** NavigationContext SHALL cover DFS traversal decisions only
- **THEN** ErrorContext SHALL cover error recording and recovery state only
- **THEN** SessionContext SHALL cover macro lifecycle and session configuration only
- **THEN** ProgressContext SHALL cover step counting and termination decisions only
- **THEN** CacheContext SHALL cover page data caching and validity lifecycle only

---

### Requirement: Canonical field ownership table

Every mutable field in TraversalRuntimeContext SHALL be attributed to exactly one subsystem. No field SHALL carry an ambiguity marker (`?`). The canonical attribution table SHALL be:

| Field | Subsystem |
|-------|-----------|
| `_traceId` | SessionContext |
| `_nodeStack` | NavigationContext |
| `_currentPath` | NavigationContext |
| `_currentPageAnalysis` | NavigationContext |
| `_currentFingerprint` | NavigationContext |
| `_cacheValid` | CacheContext |
| `_visitedPages` | NavigationContext |
| `_visitedLevel1Menus` | NavigationContext |
| `_visitedLevel2Menus` | NavigationContext |
| `_visitedNodes` | NavigationContext |
| `_visitedChildren` | Dictionary→NavigationContext |
| `_pageTree` | NavigationContext |
| `_actionHistory` | ProgressContext |
| `_failedNodes` | ErrorContext |
| `_consecutiveErrors` | ErrorContext |
| `_maxDepth` | ProgressContext |
| `_stepCount` | ProgressContext |
| `_retryCount` | ErrorContext |
| `_completionPolicy` | ProgressContext |
| `_deviceExperience` | SessionContext |
| `_globalState` | SessionContext |
| `_lastError` | ErrorContext |
| `_exceptionChain` | ErrorContext |
| `_aiProvider` | SessionContext |
| `_pageCache` | CacheContext |
| `_waitAfterActionMs` | ProgressContext |
| `_scrollHandler` (Phase 3) | CacheContext |
| `_currentSnapshot` (Phase 3) | CacheContext |
| `CurrentFrame` | NavigationContext |
| `_visitedChildrenReadOnly` | NavigationContext |

#### Scenario: No field has ambiguous attribution
- **WHEN** the canonical field ownership table is inspected
- **THEN** every field SHALL have exactly one subsystem attribution
- **THEN** no field SHALL carry a `?` ambiguity marker

#### Scenario: Field counts per subsystem match canonical table
- **WHEN** TraversalRuntimeContext fields are counted per subsystem
- **THEN** NavigationContext SHALL have 12 fields (10 core + CurrentFrame + _visitedChildrenReadOnly)
- **THEN** ErrorContext SHALL have 5 fields (_failedNodes, _consecutiveErrors, _retryCount, _lastError, _exceptionChain)
- **THEN** SessionContext SHALL have 5 fields (_traceId, _globalState, _deviceExperience, _aiProvider)
- **THEN** ProgressContext SHALL have 5 fields (_stepCount, _maxDepth, _completionPolicy, _actionHistory, _waitAfterActionMs)
- **THEN** CacheContext SHALL have 4 fields (_pageCache, _cacheValid, + 2 Phase 3 reserved slots)

---

### Requirement: Ambiguity resolution rationale documented

Each of the 10 originally ambiguous field attributions SHALL have a documented rationale explaining why one subsystem was chosen over alternatives. The rationale SHALL reference the field's primary consumer or purpose.

#### Scenario: _visitedLevel1Menus attributed to NavigationContext
- **WHEN** _visitedLevel1Menus attribution is inspected
- **THEN** it SHALL belong to NavigationContext
- **THEN** rationale SHALL state: "Primary consumer is DynamicChildManager for DFS traversal decisions. Dedup is a side-effect, not primary purpose"

#### Scenario: _visitedLevel2Menus attributed to NavigationContext
- **WHEN** _visitedLevel2Menus attribution is inspected
- **THEN** it SHALL belong to NavigationContext
- **THEN** rationale SHALL state: "Same pattern as L1 — DFS traversal decision, not cache dedup"

#### Scenario: _completionPolicy attributed to ProgressContext
- **WHEN** _completionPolicy attribution is inspected
- **THEN** it SHALL belong to ProgressContext
- **THEN** rationale SHALL state: "CompletionPolicy answers 'when should traversal end?' — a progress/termination question"

#### Scenario: _currentFingerprint attributed to NavigationContext
- **WHEN** _currentFingerprint attribution is inspected
- **THEN** it SHALL belong to NavigationContext
- **THEN** rationale SHALL state: "VisitFingerprint is a page identity marker for DFS revisit detection. Cache invalidation is a downstream side-effect"

#### Scenario: _globalState attributed to SessionContext
- **WHEN** _globalState attribution is inspected
- **THEN** it SHALL belong to SessionContext
- **THEN** rationale SHALL state: "GlobalState represents macro session lifecycle managed by GlobalFSM. D-7 addresses ITraversalContext exposure, not internal attribution"

#### Scenario: _deviceExperience attributed to SessionContext
- **WHEN** _deviceExperience attribution is inspected
- **THEN** it SHALL belong to SessionContext
- **THEN** rationale SHALL state: "Set once per session, never changes during traversal — session-level metadata"

#### Scenario: _aiProvider attributed to SessionContext
- **WHEN** _aiProvider attribution is inspected
- **THEN** it SHALL belong to SessionContext
- **THEN** rationale SHALL state: "Set once, session-level configuration — same reasoning as deviceExperience"

#### Scenario: _pageTree attributed to NavigationContext
- **WHEN** _pageTree attribution is inspected
- **THEN** it SHALL belong to NavigationContext
- **THEN** rationale SHALL state: "DynamicChildManager uses PageTree for child enumeration — DFS traversal's primary navigation data structure"

#### Scenario: _actionHistory attributed to ProgressContext
- **WHEN** _actionHistory attribution is inspected
- **THEN** it SHALL belong to ProgressContext
- **THEN** rationale SHALL state: "Action history records recent actions — progress/audit trail. Navigation decisions don't query it"

#### Scenario: _cacheValid attributed to CacheContext
- **WHEN** _cacheValid attribution is inspected
- **THEN** it SHALL belong to CacheContext
- **THEN** rationale SHALL state: "Cache validity flag controlling _pageCache reuse lifecycle — cache semantics, not progress semantics"

---

### Requirement: Subsystem boundary guard tests

ArchitectureGuardTests SHALL include a new test class `SubsystemBoundaryGuardTests` that validates the canonical subsystem structure is numerically consistent with TraversalRuntimeContext's actual field count.

#### Scenario: Field-count-per-subsystem assertion passes
- **WHEN** `SubsystemBoundaryGuardTests` runs
- **THEN** it SHALL assert NavigationContext has exactly 12 attributed fields in TraversalRuntimeContext
- **THEN** it SHALL assert ErrorContext has exactly 5 attributed fields
- **THEN** it SHALL assert SessionContext has exactly 5 attributed fields
- **THEN** it SHALL assert ProgressContext has exactly 5 attributed fields
- **THEN** it SHALL assert CacheContext has exactly 4 attributed fields (excluding Phase 3 reserved slots)
- **THEN** total core field count SHALL equal 26 (26 core fields + 2 Phase 3 reserved + 2 derived fields = 30 total, but guard counts 26 core attributable fields)

#### Scenario: Subsystem boundary guard is CI-blocking
- **WHEN** any field is moved to a different subsystem without updating the guard
- **THEN** the guard test SHALL fail
- **THEN** the failure SHALL block CI just like existing enum value guards

---

### Requirement: Source annotations in TraversalRuntimeContext

Each mutable field declaration in TraversalRuntimeContext SHALL have a structured comment annotation indicating its canonical subsystem归属.

#### Scenario: Every field has a subsystem annotation comment
- **WHEN** TraversalRuntimeContext.cs source is inspected
- **THEN** every private field declaration SHALL include a comment of the form `// <SubsystemName>` indicating canonical attribution
- **THEN** the annotation SHALL match the canonical field ownership table exactly

#### Scenario: Reserved fields annotated with subsystem
- **WHEN** Phase 3 reserved fields (`_scrollHandler`, `_currentSnapshot`) are inspected
- **THEN** they SHALL include `// CacheContext (Phase 3)` annotation
