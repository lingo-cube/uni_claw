## ADDED Requirements

### Requirement: ReadOnlySetWrapper prevents HashSet cast-back mutation

`TraversalRuntimeContext` SHALL implement a private `sealed class ReadOnlySetWrapper` that wraps `HashSet<string>` as `IReadOnlySet<string>` without exposing the underlying `HashSet<string>` reference. The wrapper SHALL delegate all `IReadOnlySet<string>` members to the internal set but SHALL NOT inherit from `HashSet<string>`. Cast-back `(HashSet<string>)wrapper` SHALL return null or throw `InvalidCastException`.

#### Scenario: ReadOnlySetWrapper delegates IReadOnlySet members correctly
- **WHEN** `ReadOnlySetWrapper.Count` and `ReadOnlySetWrapper.Contains("item")` are called on a wrapper containing a set with 3 items including "item"
- **THEN** `Count` SHALL return 3 and `Contains("item")` SHALL return true

#### Scenario: Cast-back to HashSet fails
- **WHEN** an `IReadOnlySet<string>` reference obtained from `ITraversalContext.VisitedChildren["key"]` is cast to `HashSet<string>`
- **THEN** the cast SHALL return null or throw `InvalidCastException` — the runtime type SHALL be `ReadOnlySetWrapper`, not `HashSet<string>`

#### Scenario: Modification through ITraversalContext does not affect internal data
- **WHEN** external code accesses `ITraversalContext.VisitedChildren` and attempts to cast a nested set to `HashSet<string>` and call `Add("hacked")`
- **THEN** the modification SHALL NOT affect the internal `_visitedChildren` dictionary — the internal `HashSet<string>` SHALL remain unchanged

#### Scenario: VisitedPages and VisitedNodes direct HashSet exposure is documented with safety annotation
- **WHEN** `TraversalRuntimeContext` source code is inspected for `VisitedPages` and `VisitedNodes` property implementations
- **THEN** the code SHALL include a comment annotating the safety level: "接口级安全（IReadOnlySet 不暴露修改方法），cast-back 级需 Phase 3 改进"
