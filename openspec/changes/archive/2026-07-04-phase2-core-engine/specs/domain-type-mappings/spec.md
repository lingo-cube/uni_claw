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
- `IReadOnlySet<string>` SHALL be implemented by direct expose of `HashSet<string>` (safe: no mutation methods exposed), but callers SHALL NOT cast back to `HashSet<string>`
- `IReadOnlyDictionary<string, IReadOnlySet<string>>` SHALL ensure nested `IReadOnlySet<string>` also does not leak `HashSet<string>` references

#### Scenario: VisitedPages as readonly set prevents mutation

- **WHEN** external code accesses `ITraversalContext.VisitedPages`
- **THEN** the returned `IReadOnlySet<string>` SHALL NOT expose Add/Remove/Clear methods
- **AND** the underlying `HashSet<string>` SHALL NOT be castable-back-modifiable from the interface reference

#### Scenario: VisitedChildren nested readonly prevents mutation

- **WHEN** external code accesses `ITraversalContext.VisitedChildren["container_id"]`
- **THEN** the returned `IReadOnlySet<string>` SHALL NOT expose Add/Remove methods
- **AND** modifications to the internal `HashSet<string>` SHALL only occur via TraversalRuntimeContext engine-internal methods (MarkVisited, MarkNodeVisited)

#### Scenario: CurrentPath readonly prevents external mutation

- **WHEN** external code accesses `ITraversalContext.CurrentPath`
- **THEN** the returned `IReadOnlyList<string>` SHALL NOT expose Add/Remove/Insert methods
- **AND** path modifications SHALL only occur via TraversalRuntimeContext engine-internal methods (AppendPath, PopPath)

#### Scenario: VisitedNodes readonly set prevents mutation

- **WHEN** external code accesses `ITraversalContext.VisitedNodes`
- **THEN** the returned `IReadOnlySet<string>` SHALL NOT expose Add/Remove methods
- **AND** node visitation SHALL only occur via TraversalRuntimeContext engine-internal method (MarkNodeVisited)
