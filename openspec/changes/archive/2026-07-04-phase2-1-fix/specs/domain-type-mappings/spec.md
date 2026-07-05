## MODIFIED Requirements

### Requirement: ITraversalContext interface type semantics

The ITraversalContext interface SHALL expose strongly-typed readonly collections that align with Python's type semantics:

- `VisitedPages` SHALL be `IReadOnlySet<string>` (not `Dictionary<string, object>`) — matching Python `Set[str]`
- `VisitedChildren` SHALL be `IReadOnlyDictionary<string, IReadOnlySet<string>>` (not `Dictionary<string, List<string>>`) — matching Python `Dict[str, Set[str]]`
- `CurrentPath` SHALL be `IReadOnlyList<string>` (not `List<string>` directly) — readonly view of mutable internal `List<string>`
- `VisitedNodes` SHALL be `IReadOnlySet<string>` — matching Python `Set[str]`
- `NodeStack` SHALL remain `INodeStack` (mutable class, no type change)

The readonly views SHALL NOT leak mutable internal references. Specifically:
- `IReadOnlyList<string>` SHALL be implemented via `.AsReadOnly()` wrapper on `List<string>`
- `IReadOnlySet<string>` (VisitedPages/VisitedNodes) SHALL be implemented by direct expose of `HashSet<string>` (safe per .NET contract, but cast-back MUST be guarded by documentation annotation)
- `IReadOnlyDictionary<string, IReadOnlySet<string>>` SHALL ensure nested `IReadOnlySet<string>` also does not leak `HashSet<string>` references — nested sets SHALL be wrapped with `ReadOnlySetWrapper`

#### Scenario: ITraversalNode interface resides in Graph.Models namespace
- **WHEN** the `ITraversalNode` interface definition location is inspected
- **THEN** it SHALL be in `UniClaw.Core.Graph.Models` namespace (file `Graph/Models/ITraversalNode.cs`), not in `UniClaw.Core.StateMachine`

#### Scenario: IStackFrame interface resides in Graph.Models namespace
- **WHEN** the `IStackFrame` interface definition location is inspected
- **THEN** it SHALL be in `UniClaw.Core.Graph.Models` namespace, co-located with ITraversalNode

#### Scenario: TraversalNode.cs does not reference StateMachine namespace
- **WHEN** the `using` statements in `TraversalNode.cs` are inspected
- **THEN** no `using UniClaw.Core.StateMachine` SHALL exist

#### Scenario: Dependency direction is one-way (StateMachine→Graph, not Graph→StateMachine)
- **WHEN** all `using` statements in Graph layer files are inspected for StateMachine namespace references
- **THEN** no Graph layer file SHALL contain `using UniClaw.Core.StateMachine`

#### Scenario: INodeStack remains in StateMachine layer
- **WHEN** the `INodeStack` interface definition location is inspected
- **THEN** it SHALL remain in `UniClaw.Core.StateMachine` namespace (it is part of FSM context, referenced by ITraversalContext)

#### Scenario: GlobalState on ITraversalContext is evaluated for cross-FSM dependency
- **WHEN** the M-14 evaluation is completed
- **THEN** an evaluation document SHALL be produced in `docs/refactor/` describing whether GlobalState should move from ITraversalContext to engine-only property on TraversalRuntimeContext
