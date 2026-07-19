## MODIFIED Requirements

### Requirement: Graph model records SHALL fail-fast on invalid construction values

All sealed record classes in `Graph.Models` that carry numeric range constraints or non-null string requirements SHALL validate their construction-time values and throw `DomainValidationException` on violations. Specifically: `Precondition.TimeoutSeconds` (0,300], `DynamicRule.RuleId`/`ChildTemplate` (non-empty), `ChildrenStrategy.MaxChildren` [0,10000], `ErrorPolicy.MaxRetries` [0,100], `CompletionPolicy.TargetName` when `TargetFound` (non-empty), `CompletionPolicy.TimeoutSeconds` (0,86400], `CompletionPolicy.MaxSteps` [1,1000000], `EntryPolicy.TimeoutSeconds` (0,300], `TraversalNode.NodeId` (non-empty), `TraversalNode.Name` (non-empty).

#### Scenario: Invalid Precondition.TimeoutSeconds throws
- **WHEN** `new Precondition(TimeoutSeconds: 0)` is called
- **THEN** `DomainValidationException` is thrown with FieldName "TimeoutSeconds"

#### Scenario: Valid values construct normally
- **WHEN** `new Precondition(TimeoutSeconds: 30)` is called
- **THEN** the record is constructed without exception

### Requirement: PlanCompiler provides deterministic IntentSlots-to-TraversalPlan mapping

`PlanCompiler` SHALL be a sealed class that deterministically maps `IntentSlots` to `TraversalPlan` without AI dependency. It SHALL define `TEMPLATE_SETS` as a static readonly dictionary with 4 entries keyed by **ElementHandling** (not Scope): "full_interaction" → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"], "menu_only" → ["menu_container"], "safe_mode" → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"], and "read_only" → ["leaf_info"]. The `Compile()` method SHALL execute a 5-step flow: (1) `ValidateSlots` SHALL validate `TargetApp` non-empty, `Scope ∈ {full, target_only}` (rejecting legacy element_handling/target_path values), `ElementHandling` (if provided) ∈ TEMPLATE_SETS keys, `target_only ⇒ Target` non-empty, `Depth ≥ 0`, and `Completion` (if provided) ∈ `{max_steps, timeout}` (unknown values throw DomainValidationException — fail-fast, no silent None); (2) `BuildEntryPolicy` SHALL create a default `EntryPolicy` with `ColdLaunch`/`fallback=null` (not DirectDeeplink); (3) `BuildRootNode` SHALL assign `ChildrenStrategy.DYNAMIC_MATCH` with DynamicRules derived from `slots.ElementHandling ?? "full_interaction"` (NOT `Scope`), and the RootNode SHALL reflect `slots.Entry ?? slots.TargetApp`; (4) `BuildCompletionPolicy` SHALL derive `Type=Exhaustive` for `Scope=full` and `Type=TargetFound(TargetName=Target, MatchMode=Contains, ActionOnFound=MarkAndStop)` for `Scope=target_only`; a `Completion` override SHALL cover the derived Type (`max_steps → Type=MaxSteps(+MaxSteps)`, `timeout → Type=Timeout(+TimeoutSeconds)`); (5) assemble `TraversalPlan` with all required fields including `EntryApp` from `slots.TargetApp`. The legacy `target_path` scope and `BuildStaticNodes` step are REMOVED (target_path has zero scenarios; static-node construction is retired). `CompletionPolicyType.Exhaustive` (formerly `None`) has semantics clarified as "exhaustive intent".

#### Scenario: compile produces a valid TraversalPlan for target_only scope
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=target_only`, `Target="wifi"`, and a valid `TargetApp`
- **THEN** the resulting `TraversalPlan` has `root_node.children_strategy` equal to `ChildrenStrategy.DYNAMIC_MATCH`, `completion_policy.Type` equal to `TargetFound` with `TargetName="wifi"`, and `entry_app` populated

#### Scenario: compile produces a valid TraversalPlan for full scope with element_handling
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=full`, `ElementHandling="full_interaction"`, and a valid `TargetApp`
- **THEN** the resulting `TraversalPlan` has `root_node.children_strategy` equal to `ChildrenStrategy.DYNAMIC_MATCH`, its dynamic rules driven by the `full_interaction` template set, and `completion_policy.Type` equal to `Exhaustive`

#### Scenario: compile rejects legacy full_interaction as Scope
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope="full_interaction"`
- **THEN** `ValidateSlots` throws `DomainValidationException` (full_interaction is an ElementHandling value, not a Scope)

#### Scenario: compile rejects legacy target_path Scope
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope="target_path"`
- **THEN** `ValidateSlots` throws `DomainValidationException` (target_path scope removed)

#### Scenario: compile rejects target_only without Target
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=target_only` and `Target` null/empty
- **THEN** `ValidateSlots` throws `DomainValidationException` naming the scope/target combination

#### Scenario: compile rejects empty target_app
- **WHEN** `PlanCompiler.Compile(slots)` is called with `TargetApp` empty or null
- **THEN** `ValidateSlots` throws `DomainValidationException` naming `target_app`

#### Scenario: compile rejects unknown Completion override
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Completion="bogus"`
- **THEN** `ValidateSlots` throws `DomainValidationException` (fail-fast, NOT silent None)

#### Scenario: Completion max_steps override covers Type
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=full` and `Completion="max_steps"`
- **THEN** the resulting `completion_policy.Type` is `MaxSteps` (NOT Exhaustive — override covers Type so the engine bound check fires)

#### Scenario: DynamicRules derived from ElementHandling not Scope
- **WHEN** a plan is compiled with `Scope=full` and `ElementHandling="menu_only"`
- **THEN** the root node's dynamic rules contain only the `menu_container` template (from ElementHandling), ignoring Scope

#### Scenario: TEMPLATE_SETS has exactly 4 values matching Python source
- **WHEN** `PlanCompiler.TEMPLATE_SETS` keys are enumerated
- **THEN** the keys are exactly "full_interaction", "menu_only", "safe_mode", and "read_only"

#### Scenario: TEMPLATE_SETS template lists match Python source verbatim
- **WHEN** each `PlanCompiler.TEMPLATE_SETS` entry is compared to the Python `TEMPLATE_SETS` dictionary
- **THEN** the template name lists match row-for-row with zero mismatches

#### Scenario: Match conditions per template align with Python source
- **WHEN** the match conditions for all 5 templates are enumerated
- **THEN** they match exactly: menu_container → {"type": "menu_item"}, switch_leaf → {"type": "switch"}, slider_leaf → {"type": "slider"}, leaf_action → {"type": "button"}, leaf_info → {} (match anything)

## RENAMED Requirements

### Requirement: CompletionPolicyType.None → Exhaustive

**FROM:** `CompletionPolicyType.None`
**TO:** `CompletionPolicyType.Exhaustive`

`CompletionPolicyType` enum value `None` SHALL be renamed to `Exhaustive`. The semantics remain unchanged (exhaustive traversal intent). All references to `CompletionPolicyType.None` in PlanCompiler, TraversalEngine, tests, and serialization SHALL use `Exhaustive`. The JSON serialization value SHALL change from `"none"` to `"exhaustive"`.

## REMOVED Requirements

### Requirement: ExitCondition record and ExitConditionType enum

**Reason**: Once ContainerHandler is wired into the engine (see container-handler spec), `ExitCondition` has zero live consumers. Container completion judgment moves from scattered `ExitCondition` field reads to the centralized `ContainerHandler` 5-priority chain. The exit-action decision (Back/AutoEscape/Skip/Abort) is now internal to `FallbackDecider`, not stored as a field on `TraversalNode`.

**Migration**:
- Remove `ExitCondition` record (TraversalNode.cs)
- Remove `ExitConditionType` enum (4 values: AllChildrenVisited, ScrollEnd, DepthLimited, SingleLevel — all redundant)
- Remove `TraversalNode.ExitCondition` field
- Remove `ExitCondition.MaxDepth` validation clause from "Graph model records SHALL fail-fast" requirement
- Nav-subframe AutoEscape detection SHALL use node context (NodeType/Meta flag), not `ExitCondition.Fallback` field
- All 12 test files referencing `ExitCondition` in `TraversalNode` constructor SHALL be migrated
