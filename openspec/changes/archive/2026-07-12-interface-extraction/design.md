## Context

Current StateMachine and Traversal components use concrete class dependencies directly. For example, `StepContext` (sealed record with 12 positional parameters) references concrete types like `TraceCoordinator`, `PageSnapshotManager`, and `DynamicChildManager`. This creates tight coupling and prevents mocking in unit tests.

The refactoring roadmap ([20-b-refactoring-roadmap-design.md](../refactor/20-b-refactoring-roadmap-design.md)) marks this as P1 priority because:
- P2 Context Decomposition (D-I) requires stable test baselines before splitting the 30-field God Object
- Current test coverage is capped by inability to mock I/O-heavy dependencies

## Goals / Non-Goals

**Goals:**
- Extract interfaces for 6+ StateMachine/Traversal components to enable test mocking
- Update StepContext to use interface types (breaking change with controlled ripple)
- Maintain backward compatibility where possible (only StepContext breaks)
- Enable unit testing of StepOrchestrator and FSM handlers without real I/O

**Non-Goals:**
- Changing behavior of existing implementations (pure extraction)
- Moving files across namespaces (interfaces stay next to implementations)
- Refactoring internal structure (only public API extraction)
- Creating new abstractions beyond surface-level interface extraction

## Decisions

### Decision 1: Interface placement — same namespace as implementation

**Choice**: Place each interface in the same namespace/file as its concrete implementation.

**Rationale**:
- Minimal disruption — consumers already importing the namespace get the interface automatically
- C# convention favors interface/implementation proximity (e.g., `IList` vs `List`)
- Avoids premature namespace reorganization (can defer to P2/P3 if needed)

**Alternatives considered**:
- Separate `Interfaces/` namespace: More "pure" but requires updating all using statements
- Abstract base class: Wrong abstraction — these are services, not inheritance hierarchies

### Decision 2: Interface granularity — public API only

**Choice**: Extract only public methods/properties used by consumers. Internal implementation details stay internal.

**Rationale**:
- Interfaces define contracts, not implementation details
- Consumers (StepOrchestrator, TraversalEngine, TraversalFSM) only use specific methods
- Smaller interface surface = easier to mock and maintain

**Example**: `TraceCoordinator` has internal recording methods, but consumers only call `BuildCorrelation()`, `RecordStepStart()`, `RecordStepEnd()`. Interface includes only these.

### Decision 3: StepContext signature change — positional parameters unchanged

**Choice**: Keep StepContext as sealed record with positional init-only parameters, only change types from concrete to interface.

**Rationale**:
- StepContext already has well-defined 12-parameter structure
- Positional records enable with-expressions (key usage pattern in StepOrchestrator)
- Only type names change, not parameter names or order

**Breaking change impact**:
```csharp
// Before
StepContext(
    trace: new TraceCoordinator(...),
    snapshotMgr: new PageSnapshotManager(...),
    ...
)

// After
StepContext(
    trace: ITraceCoordinator,  // Mock can be injected
    snapshotMgr: IPageSnapshotManager,  // Mock can be injected
    ...
)
```

All StepContext instantiation sites (currently 1: `StepOrchestrator.BuildStepContext()`) must update.

### Decision 4: Interface naming — standard C# convention

**Choice**: Prefix interface names with `I` (e.g., `IDynamicChildManager`).

**Rationale**:
- Standard C# convention (Framework Design Guidelines)
- Consistent with existing interfaces like `ITraversalStateMachine`, `ITraversalContext`
- Distinguishes interface from implementation at call sites

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| StepContext breaking change may ripple to unexpected consumers | Search all `StepContext` instantiation sites before change; compile error will catch missed sites |
| Interface extraction may expose methods that shouldn't be public | Review each interface: only extract methods actually used by consumers, not full public API of class |
| Ripple fix to StepContext may delay P2 Context Decomposition | This is explicitly a P1 prerequisite; estimation is 1-2 days, acceptable delay for P2 enablement |
| Over-extraction (creating interfaces for trivial classes) | Apply judgment: NodeStackAdapter is thin but worth mocking; single-method classes like EntryPolicyExecutor still benefit from interface for consistency |

## Migration Plan

1. **Create interfaces** (6 files):
   - Add `IDynamicChildManager.cs` to `StateMachine/`
   - Add `ITraceCoordinator.cs` to `Observability/`
   - Add `IEntryPolicyExecutor.cs` to `Traversal/`
   - Add `IPageCacheManager.cs` to `Traversal/`
   - Add `IPageSnapshotManager.cs` to `Traversal/`
   - Add `INodeStackAdapter.cs` to `StateMachine/`

2. **Update implementations** (6 classes):
   - Add `: I<Name>` to each concrete class declaration
   - Compile error check: ensures all interface members are implemented

3. **Update StepContext** (1 file):
   - Change parameter types from concrete to interface
   - Update `StepOrchestrator.BuildStepContext()` instantiation

4. **Update consumers** (2 files):
   - `TraversalEngine` constructor: accept interface types
   - All injection sites: pass concrete implementations as interface type

5. **Add tests**:
   - Create interface-based mocks for FSM handler tests
   - Verify StepOrchestrator can run with mocked dependencies

6. **Verification**:
   - `dotnet test` — all 575+ tests pass
   - New tests using interface mocks demonstrate improved coverage

**Rollback strategy**: Simple git revert (no external state changes, no migration data).

## Open Questions

None. All technical decisions are straightforward interface extraction following standard C# patterns.
