## ADDED Requirements

### Requirement: Graph model records SHALL fail-fast on invalid construction values

All sealed record classes in `Graph.Models` that carry numeric range constraints or non-null string requirements SHALL validate their construction-time values and throw `DomainValidationException` on violations. Specifically: `Precondition.TimeoutSeconds` (0,300], `DynamicRule.RuleId`/`ChildTemplate` (non-empty), `ChildrenStrategy.MaxChildren` [0,10000], `ErrorPolicy.MaxRetries` [0,100], `ExitCondition.MaxDepth` when `DepthLimited` (0,1000], `CompletionPolicy.TargetName` when `TargetFound` (non-empty), `CompletionPolicy.TimeoutSeconds` (0,86400], `CompletionPolicy.MaxSteps` [1,1000000], `EntryPolicy.TimeoutSeconds` (0,300], `TraversalNode.NodeId` (non-empty), `TraversalNode.Name` (non-empty).

#### Scenario: Invalid Precondition.TimeoutSeconds throws
- **WHEN** `new Precondition(TimeoutSeconds: 0)` is called
- **THEN** `DomainValidationException` is thrown with FieldName "TimeoutSeconds"

#### Scenario: Valid values construct normally
- **WHEN** `new Precondition(TimeoutSeconds: 30)` is called
- **THEN** the record is constructed without exception

### Requirement: PlaceholderResolver SHALL throw on unresolved placeholders

`PlaceholderResolver.Resolve()` SHALL call `HasUnresolvedPlaceholders()` after substitution and throw `DomainValidationException` listing the unresolved placeholder names, instead of silently returning the input with placeholders intact.

#### Scenario: Unresolved placeholder throws
- **WHEN** `Resolve("click {{unknown}}", context)` is called and "unknown" is not in context
- **THEN** `DomainValidationException` is thrown with FieldName "placeholder"

### Requirement: TemplateInstantiator SHALL preserve all fields from template

`TemplateInstantiator.CreateOperation()` SHALL pass the `meta` dictionary to `Target.Meta`. It SHALL parse `restore.target` and `restore.params` from the operation dictionary and pass them to `RestoreAction`. `CreatePrecondition()` SHALL read `ui_condition` from the template dictionary.

#### Scenario: Target.Meta is populated from template
- **WHEN** `CreateOperation()` processes a template with `"meta": {"key": "value"}`
- **THEN** the resulting `Target.Meta` SHALL contain `{"key": "value"}`

### Requirement: TraversalPlan SHALL validate root node

`TraversalPlan` constructor SHALL assert that `RootNode` is non-null, has type `Container` or `Screen`, and has `Operation.Type == NoAction`. Violation SHALL throw `DomainValidationException`.

#### Scenario: Null root node throws
- **WHEN** `TraversalPlan` is constructed with `RootNode = null`
- **THEN** `DomainValidationException` is thrown
