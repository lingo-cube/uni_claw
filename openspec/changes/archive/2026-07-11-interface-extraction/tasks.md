## 1. Interface Definitions

- [x] 1.1 Define `IDynamicChildManager` interface in TraversalEngine.cs (3 methods: GetNextUnvisitedChild, Generate, Invalidate). Change DynamicChildManager constructor `TraceCoordinator?` → `ITraceCoordinator?`. Add `: IDynamicChildManager` to class declaration
- [x] 1.2 Define `ITraceCoordinator` interface in TraversalEngine.cs (18 members: Active + 16 Record methods + ShouldRecordEntryAttempt + ShouldRecordVisionCall + GetStepSnapshot). Add `: ITraceCoordinator` to TraceCoordinator class declaration
- [x] 1.3 Define `IEntryPolicyExecutor` interface in TraversalEngine.cs (2 methods: Execute, BuildChain). Add `: IEntryPolicyExecutor` to EntryPolicyExecutor class declaration
- [x] 1.4 Define `IPageCacheManager` interface in TraversalEngine.cs (2 methods with ITraversalContext parameters). Change PageCacheManager method signatures: `TraversalRuntimeContext` → `ITraversalContext`. Add `: IPageCacheManager` to class declaration. Implement cast pattern for PageCache internal access
- [x] 1.5 Define `IPageSnapshotManager` interface in TraversalEngine.cs (2 instance methods: Fingerprint, HasChanged). Convert PageSnapshotManager static methods → instance methods (remove `static` modifier). Add `: IPageSnapshotManager` to class declaration
- [x] 1.6 Define `INodeStackAdapter` interface in TraversalEngine.cs (3 methods: Push, Pop, Peek). Change NodeStackAdapter constructor `TraversalRuntimeContext` → `ITraversalContext`. Add `: INodeStackAdapter` to class declaration. Implement cast pattern for NodeStack internal access

## 2. StepContext Parameter Type Sync

- [x] 2.1 Change StepContext positional parameters: `DynamicChildManager ChildMgr` → `IDynamicChildManager ChildMgr`, `TraceCoordinator Trace` → `ITraceCoordinator Trace`, `PageSnapshotManager SnapshotMgr` → `IPageSnapshotManager SnapshotMgr`, `NodeStackAdapter Stack` → `INodeStackAdapter Stack`
- [x] 2.2 Update TraversalEngine.Initialize() StepContext assembly to use interface-typed local variables: `IDynamicChildManager childMgr = new DynamicChildManager(...)` etc.

## 3. Guard Tests

- [x] 3.1 Add `InterfaceComplianceGuardTests` class to ArchitectureGuardTests.cs with 6 tests verifying each sealed class implements its corresponding interface (DynamicChildManager→IDynamicChildManager, TraceCoordinator→ITraceCoordinator, EntryPolicyExecutor→IEntryPolicyExecutor, PageCacheManager→IPageCacheManager, PageSnapshotManager→IPageSnapshotManager, NodeStackAdapter→INodeStackAdapter)
- [x] 3.2 Add interface method-count assertions: IDynamicChildManager=3, ITraceCoordinator=18, IEntryPolicyExecutor=2, IPageCacheManager=2, IPageSnapshotManager=2, INodeStackAdapter=3
- [x] 3.3 Run `dotnet test` — all existing tests pass + new InterfaceComplianceGuardTests pass

## 4. Documentation Updates

- [x] 4.1 Update `docs/system/layers/traversal.md` §1 Interfaces table — add 6 new interfaces with method counts and descriptions
- [x] 4.2 Update `docs/system/layers/traversal.md` §10 Design Issues — mark D-V as resolved (interface extraction completed)
- [x] 4.3 Add D-V decision entries to `docs/system/decisions/log.md` — 7 key decisions (interface location, static→instance, ITraversalContext params, StepContext sync, backward compat, DynamicChildManager ctor, interface method mirror)

## 5. Verification

- [x] 5.1 Run `dotnet test` — all tests pass (existing + new guard tests)
- [x] 5.2 Verify no static method calls to PageSnapshotManager remain (all calls use instance method via IPageSnapshotManager)
