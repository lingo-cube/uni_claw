## ADDED Requirements

### Requirement: IDynamicChildManager interface definition

A public interface `IDynamicChildManager` SHALL be defined with exactly 3 methods mirroring DynamicChildManager's public API:

- `TraversalNode? GetNextUnvisitedChild(TraversalNode node, ITraversalContext context)`
- `void Generate(TraversalNode node, ITraversalContext context)`
- `void Invalidate(string nodeId)`

DynamicChildManager SHALL implement `IDynamicChildManager`. DynamicChildManager constructor parameter `TraceCoordinator?` SHALL change to `ITraceCoordinator?`.

#### Scenario: DynamicChildManager implements IDynamicChildManager
- **WHEN** `DynamicChildManager` class declaration is inspected
- **THEN** it SHALL declare `: IDynamicChildManager`
- **THEN** all 3 interface methods SHALL have matching implementations

#### Scenario: IDynamicChildManager methods match DynamicChildManager public API
- **WHEN** `IDynamicChildManager` method signatures are compared to `DynamicChildManager` public methods
- **THEN** `GetNextUnvisitedChild`, `Generate`, `Invalidate` SHALL match exactly (same parameters, same return types)

---

### Requirement: ITraceCoordinator interface definition

A public interface `ITraceCoordinator` SHALL be defined with exactly 18 members mirroring TraceCoordinator's public API:

- `bool Active` (property)
- `void RecordStepStart(string nodeId, string result)`
- `void RecordStepEnd(string nodeId, string result)`
- `void RecordPageAnalysis(PageAnalysis? pageAnalysis)`
- `void RecordActionExecution(string action, string target, bool success)`
- `void RecordActionExecution(OperationType action, Target? target, bool success)`
- `void RecordMetricsAsSpans(object metrics)`
- `void RecordSkipSpan(MatchResult matchResult)`
- `void RecordExecutionSpan(object ex)`
- `void RecordAICallSpan(string capability, string providerId, bool success, double latencyMs, int? tokens = null)`
- `void RecordErrorSpan(string errorType, string message, ErrorSeverity severity)`
- `void RecordDecision(string decision, ITraversalContext ctx)`
- `void RecordStateTransition(string fromState, string toState)`
- `void RecordRootNodePushed(string nodeId)`
- `void RecordPageTransition(string fromPath, string toPath, string transitionType)`
- `void RecordDynamicLifecycle(string @event, string nodeId, string parentId, string ruleId, string elementId)`
- `void RecordStateDecision(string decision, string nodeId, Dictionary<string, string>? metadata)`
- `ImmutableArray<SpanType> GetStepSnapshot()`
- `bool ShouldRecordEntryAttempt(TraceLevel level)`
- `bool ShouldRecordVisionCall(TraceLevel level)`

TraceCoordinator SHALL implement `ITraceCoordinator`.

#### Scenario: TraceCoordinator implements ITraceCoordinator
- **WHEN** `TraceCoordinator` class declaration is inspected
- **THEN** it SHALL declare `: ITraceCoordinator`
- **THEN** all 18 interface members SHALL have matching implementations

#### Scenario: ITraceCoordinator method count matches TraceCoordinator public API
- **WHEN** `ITraceCoordinator` member count is evaluated
- **THEN** it SHALL have exactly 18 members (1 property + 17 methods)

---

### Requirement: IEntryPolicyExecutor interface definition

A public interface `IEntryPolicyExecutor` SHALL be defined with exactly 2 methods:

- `EntryResult Execute(EntryPolicy policy, EntryConfig config, string targetApp)`
- `List<EntryStrategy> BuildChain(EntryPolicy policy)`

EntryPolicyExecutor SHALL implement `IEntryPolicyExecutor`.

#### Scenario: EntryPolicyExecutor implements IEntryPolicyExecutor
- **WHEN** `EntryPolicyExecutor` class declaration is inspected
- **THEN** it SHALL declare `: IEntryPolicyExecutor`
- **THEN** both interface methods SHALL have matching implementations

---

### Requirement: IPageCacheManager interface definition with ITraversalContext parameters

A public interface `IPageCacheManager` SHALL be defined with exactly 2 methods, using `ITraversalContext` instead of `TraversalRuntimeContext`:

- `void Update(string path, PageCacheInfo pageInfo, ITraversalContext context)`
- `IReadOnlyList<MenuItem>? Restore(string path, ITraversalContext context)`

PageCacheManager SHALL implement `IPageCacheManager`. The sealed class implementation SHALL cast `ITraversalContext` to `TraversalRuntimeContext` for internal access to `PageCache`.

#### Scenario: PageCacheManager implements IPageCacheManager
- **WHEN** `PageCacheManager` class declaration is inspected
- **THEN** it SHALL declare `: IPageCacheManager`
- **THEN** both interface methods SHALL have matching implementations with ITraversalContext parameters

#### Scenario: IPageCacheManager uses ITraversalContext not TraversalRuntimeContext
- **WHEN** `IPageCacheManager` method signatures are inspected
- **THEN** both methods SHALL use `ITraversalContext` as parameter type
- **THEN** `TraversalRuntimeContext` SHALL NOT appear in any IPageCacheManager method signature

---

### Requirement: IPageSnapshotManager interface definition with instance methods

A public interface `IPageSnapshotManager` SHALL be defined with exactly 2 instance methods:

- `int Fingerprint(PageAnalysis? pageAnalysis)`
- `bool HasChanged(PageAnalysis? before, PageAnalysis? after)`

PageSnapshotManager SHALL implement `IPageSnapshotManager`. The 2 static methods in PageSnapshotManager SHALL be converted to instance methods (remove `static` modifier, logic unchanged).

#### Scenario: PageSnapshotManager implements IPageSnapshotManager
- **WHEN** `PageSnapshotManager` class declaration is inspected
- **THEN** it SHALL declare `: IPageSnapshotManager`
- **THEN** both `Fingerprint` and `HasChanged` SHALL be instance methods (not static)

#### Scenario: IPageSnapshotManager methods are instance methods
- **WHEN** `IPageSnapshotManager` method declarations are inspected
- **THEN** `Fingerprint` and `HasChanged` SHALL be instance method signatures (no static keyword)

#### Scenario: PageSnapshotManager instance methods preserve static logic
- **WHEN** `PageSnapshotManager.Fingerprint(pageAnalysis)` is called as an instance method
- **THEN** the result SHALL match the previous static implementation exactly (deterministic character-based hash)
- **WHEN** `PageSnapshotManager.HasChanged(before, after)` is called as an instance method
- **THEN** the result SHALL match the previous static implementation exactly (fingerprint comparison)

---

### Requirement: INodeStackAdapter interface definition with ITraversalContext parameters

A public interface `INodeStackAdapter` SHALL be defined with exactly 3 methods:

- `void Push(TraversalNode child)`
- `TraversalNode? Pop()`
- `TraversalNode? Peek()`

NodeStackAdapter SHALL implement `INodeStackAdapter`. NodeStackAdapter constructor parameter `TraversalRuntimeContext context` SHALL change to `ITraversalContext context`. The sealed class implementation SHALL cast `ITraversalContext` to `TraversalRuntimeContext` for internal access to `NodeStack`.

#### Scenario: NodeStackAdapter implements INodeStackAdapter
- **WHEN** `NodeStackAdapter` class declaration is inspected
- **THEN** it SHALL declare `: INodeStackAdapter`
- **THEN** all 3 interface methods SHALL have matching implementations

#### Scenario: NodeStackAdapter constructor uses ITraversalContext
- **WHEN** `NodeStackAdapter` constructor parameters are inspected
- **THEN** the first parameter SHALL be `ITraversalContext context` (not `TraversalRuntimeContext`)
- **THEN** the second parameter SHALL be `INodeRegistry registry`

---

### Requirement: Interface compliance guard test

ArchitectureGuardTests SHALL include a new test class `InterfaceComplianceGuardTests` that verifies each sealed class correctly implements its corresponding interface.

#### Scenario: Each sealed class implements its corresponding interface
- **WHEN** `InterfaceComplianceGuardTests` runs
- **THEN** it SHALL verify `DynamicChildManager : IDynamicChildManager`
- **THEN** it SHALL verify `TraceCoordinator : ITraceCoordinator`
- **THEN** it SHALL verify `EntryPolicyExecutor : IEntryPolicyExecutor`
- **THEN** it SHALL verify `PageCacheManager : IPageCacheManager`
- **THEN** it SHALL verify `PageSnapshotManager : IPageSnapshotManager`
- **THEN** it SHALL verify `NodeStackAdapter : INodeStackAdapter`

#### Scenario: Interface method count matches sealed class public method count
- **WHEN** each interface is compared to its corresponding sealed class
- **THEN** `IDynamicChildManager` SHALL have exactly 3 methods
- **THEN** `ITraceCoordinator` SHALL have exactly 18 members
- **THEN** `IEntryPolicyExecutor` SHALL have exactly 2 methods
- **THEN** `IPageCacheManager` SHALL have exactly 2 methods
- **THEN** `IPageSnapshotManager` SHALL have exactly 2 methods
- **THEN** `INodeStackAdapter` SHALL have exactly 3 methods
