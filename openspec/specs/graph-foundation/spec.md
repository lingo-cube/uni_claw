## Purpose

Graph 层规格: TraversalPlan 遍历蓝图 + PlanCompiler (IntentSlots→Plan 编译) + DynamicMatcher (5 维 conjunctive 匹配) + TemplateInstantiator (模板→TraversalNode 实例化) + 三目录分离 (Models/Abstractions/Services)。
## Requirements
### Requirement: TraversalPlan supplements 6 missing fields aligned with Python

The `TraversalPlan` record SHALL include all 12 fields aligned with the Python `TraversalPlan` class. The 6 currently missing fields MUST be added: `entry_app` (string, required), `plan_name` (string), `plan_id` (string), `entry_config` (EntryConfig?), `static_nodes` (renamed from `nodes` to `Dictionary<string, TraversalNode>`), and `template_registry` (string?). The `entry_app` field MUST be non-null and non-empty; violation SHALL throw `DomainValidationException` naming `entry_app`.

#### Scenario: TraversalPlan with all 12 fields constructs successfully
- **WHEN** a `TraversalPlan` is created with all 12 fields populated including `entry_app`, `plan_name`, `plan_id`, `entry_config`, `static_nodes`, and `template_registry`
- **THEN** the instance stores all values and does not throw

#### Scenario: TraversalPlan rejects null or empty entry_app
- **WHEN** a `TraversalPlan` is created with `entry_app` as null or empty string
- **THEN** construction throws `DomainValidationException` whose message contains the field name (`entry_app`) and the illegal value

#### Scenario: static_nodes replaces nodes field name
- **WHEN** the `TraversalPlan` type members are inspected
- **THEN** a field named `static_nodes` exists and no field named `nodes` exists

#### Scenario: entry_config is nullable and defaults to null
- **WHEN** a `TraversalPlan` is created without specifying `entry_config`
- **THEN** `entry_config` is null

#### Scenario: template_registry is nullable
- **WHEN** a `TraversalPlan` is created without specifying `template_registry`
- **THEN** `template_registry` is null

### Requirement: EntryConfig defines V6.8 entry behavior parameters

`EntryConfig` SHALL be a sealed record class with 5 fields: `wait_mode` (WaitMode enum: Fast/Polling), `wait_timeout_seconds` (double, default 10.0), `wait_interval_ms` (int, default 500), `action_delay_ms` (int, default 300), and `trace_level` (TraceLevel enum: None/Basic/Detailed/Full). `WaitMode` and `TraceLevel` SHALL use `[JsonStringEnumConverter]` and `[JsonPropertyName]` attributes matching the Python string values ("fast"/"polling" and "none"/"basic"/"detailed"/"full"). `EntryConfig` MUST validate that `wait_timeout_seconds > 0`, `wait_interval_ms > 0`, and `action_delay_ms >= 0`; violations SHALL throw `DomainValidationException`.

#### Scenario: EntryConfig with valid defaults constructs successfully
- **WHEN** an `EntryConfig` is created with `wait_mode=Fast, wait_timeout_seconds=10.0, wait_interval_ms=500, action_delay_ms=300, trace_level=None`
- **THEN** the instance stores those values and does not throw

#### Scenario: EntryConfig rejects zero wait_timeout_seconds
- **WHEN** an `EntryConfig` is created with `wait_timeout_seconds=0`
- **THEN** construction throws `DomainValidationException` naming `wait_timeout_seconds`

#### Scenario: EntryConfig rejects negative wait_timeout_seconds
- **WHEN** an `EntryConfig` is created with `wait_timeout_seconds=-1.0`
- **THEN** construction throws `DomainValidationException` naming `wait_timeout_seconds`

#### Scenario: EntryConfig rejects zero wait_interval_ms
- **WHEN** an `EntryConfig` is created with `wait_interval_ms=0`
- **THEN** construction throws `DomainValidationException` naming `wait_interval_ms`

#### Scenario: EntryConfig rejects negative action_delay_ms
- **WHEN** an `EntryConfig` is created with `action_delay_ms=-100`
- **THEN** construction throws `DomainValidationException` naming `action_delay_ms`

#### Scenario: WaitMode serializes to Python string values
- **WHEN** `WaitMode.Fast` is serialized via `DomainJsonOptions`
- **THEN** the JSON output is `"fast"` (not `"Fast"` or `"0"`)

#### Scenario: TraceLevel serializes to Python string values
- **WHEN** `TraceLevel.Detailed` is serialized via `DomainJsonOptions`
- **THEN** the JSON output is `"detailed"` (not `"Detailed"` or `"2"`)

### Requirement: PlanCompiler provides deterministic IntentSlots-to-TraversalPlan mapping

`PlanCompiler` SHALL be a sealed class that deterministically maps `IntentSlots` to `TraversalPlan` without AI dependency. It SHALL define `TEMPLATE_SETS` as a static readonly dictionary with 4 entries: "full_interaction" → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"], "menu_only" → ["menu_container"], "safe_mode" → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"], and "read_only" → ["leaf_info"]. The `compile()` method SHALL execute a 6-step flow: (1) `_validate_slots` SHALL validate target_app non-empty, scope/target combination legality, and depth legality; (2) `_build_entry_policy` SHALL create a default `EntryPolicy`; (3) `_build_root_node` SHALL assign `ChildrenStrategy.STATIC` when scope is target_path, otherwise `ChildrenStrategy.DYNAMIC_MATCH`; (4) `_build_completion_policy` SHALL map completion override or scope to `CompletionPolicy` type; (5) assemble `TraversalPlan` with all required fields including `entry_app` from slots; (6) if scope is target_path, `_build_static_nodes` SHALL construct static path nodes.

#### Scenario: compile produces a valid TraversalPlan for target_path scope
- **WHEN** `PlanCompiler.compile(slots)` is called with `scope=target_path` and a valid `target_app`
- **THEN** the resulting `TraversalPlan` has `root_node.children_strategy` equal to `ChildrenStrategy.STATIC`, `entry_app` populated, and `static_nodes` containing path nodes

#### Scenario: compile produces a valid TraversalPlan for full_interaction scope
- **WHEN** `PlanCompiler.compile(slots)` is called with `scope=full_interaction` and a valid `target_app`
- **THEN** the resulting `TraversalPlan` has `root_node.children_strategy` equal to `ChildrenStrategy.DYNAMIC_MATCH` and `template_registry` referencing "full_interaction"

#### Scenario: compile rejects empty target_app
- **WHEN** `PlanCompiler.compile(slots)` is called with `target_app` empty or null
- **THEN** `_validate_slots` throws `DomainValidationException` naming `target_app`

#### Scenario: TEMPLATE_SETS has exactly 4 values matching Python source
- **WHEN** `PlanCompiler.TEMPLATE_SETS` keys are enumerated
- **THEN** the keys are exactly "full_interaction", "menu_only", "safe_mode", and "read_only"

#### Scenario: TEMPLATE_SETS template lists match Python source verbatim
- **WHEN** each `PlanCompiler.TEMPLATE_SETS` entry is compared to the Python `TEMPLATE_SETS` dictionary
- **THEN** the template name lists match row-for-row with zero mismatches

#### Scenario: compile assigns menu_container match condition for full_interaction
- **WHEN** the root node of a plan compiled with "full_interaction" scope is inspected
- **THEN** its dynamic match rules include a match condition `"type": "menu_item"` for the "menu_container" template

#### Scenario: Match conditions per template align with Python source
- **WHEN** the match conditions for all 5 templates are enumerated
- **THEN** they match exactly: menu_container → {"type": "menu_item"}, switch_leaf → {"type": "switch"}, slider_leaf → {"type": "slider"}, leaf_action → {"type": "button"}, leaf_info → {} (match anything)

### Requirement: DynamicMatcher matches page objects against DynamicRule conditions

`DynamicMatcher` SHALL be a sealed class implementing `IDynamicMatcher` with a `MatchCondition` record containing 6 fields: `type` (string?), `expected_action` (string?), `text_pattern` (string?), `min_index` (int?), `max_index` (int?), and `custom` (Dictionary<string, string>?). The matching logic SHALL evaluate conditions in order: (1) MenuItemType match — `type` field SHALL be resolved against `MenuItemType` values via `MenuItemTypeExtensions.FromValue`, returning false if the item's type does not match; (2) ExpectedAction match — `expected_action` SHALL be resolved against `ExpectedAction` values via `ExpectedActionExtensions.FromValue`; (3) text_pattern match — SHALL support two modes: "Exact" (string equality) and "Contains" (substring match), determined by the pattern format; (4) index range match — `min_index` and `max_index` SHALL bound the item's positional index, with null meaning unbounded; (5) custom dict match — each key-value pair in `custom` SHALL match corresponding metadata on the item. `MatchResult` SHALL be a sealed record class with 4 fields: `matched` (bool), `match_rule_id` (string), `matched_item` (object), and `action` (MatchAction enum: GenerateChild/Skip/ExecuteInline). All conditions in a `MatchCondition` MUST pass for a match to succeed (conjunctive logic).

#### Scenario: MatchCondition with type only matches MenuItemType
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.type="switch"` and the item's MenuItemType is `Switch`
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with type fails on mismatched MenuItemType
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.type="switch"` and the item's MenuItemType is `Button`
- **THEN** `MatchResult.matched` is false

#### Scenario: MatchCondition with expected_action matches ExpectedAction
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.expected_action="click"` and the item's ExpectedAction is `Click`
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with text_pattern Exact mode matches exact string
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.text_pattern="Settings"` in Exact mode and the item's text is "Settings"
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with text_pattern Exact mode fails on substring
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.text_pattern="Settings"` in Exact mode and the item's text is "Network Settings"
- **THEN** `MatchResult.matched` is false

#### Scenario: MatchCondition with text_pattern Contains mode matches substring
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.text_pattern="Settings"` in Contains mode and the item's text is "Network Settings"
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with index range bounds item position
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.min_index=2, max_index=5` and the item's index is 3
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with index range rejects item outside bounds
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.min_index=2, max_index=5` and the item's index is 7
- **THEN** `MatchResult.matched` is false

#### Scenario: MatchCondition with null min_index allows any lower bound
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.min_index=null, max_index=5` and the item's index is 0
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with null max_index allows any upper bound
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.min_index=2, max_index=null` and the item's index is 100
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with custom dict matches all key-value pairs
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.custom={"role": "navigation", "level": "1"}` and the item's metadata contains both matching entries
- **THEN** `MatchResult.matched` is true

#### Scenario: MatchCondition with custom dict fails on mismatched value
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.custom={"role": "navigation"}` and the item's metadata has `role="content"`
- **THEN** `MatchResult.matched` is false

#### Scenario: All conditions must pass for conjunctive match
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.type="switch"` and `condition.expected_action="click"` and the item's MenuItemType is `Switch` but its ExpectedAction is `Scroll`
- **THEN** `MatchResult.matched` is false (both conditions must pass)

#### Scenario: Empty MatchCondition matches everything (leaf_info semantics)
- **WHEN** `DynamicMatcher.match(condition, item)` is called with an empty `MatchCondition` (all fields null/empty)
- **THEN** `MatchResult.matched` is true for any item

#### Scenario: MatchResult action is GenerateChild for matching menu_container rule
- **WHEN** a menu_container rule matches an item
- **THEN** `MatchResult.action` is `MatchAction.GenerateChild`

#### Scenario: MatchResult action is Skip for skip rule
- **WHEN** a skip-type rule matches an item
- **THEN** `MatchResult.action` is `MatchAction.Skip`

#### Scenario: MatchResult action is ExecuteInline for inline rule
- **WHEN** an inline-action rule matches an item
- **THEN** `MatchResult.action` is `MatchAction.ExecuteInline`

### Requirement: TemplateInstantiator produces TraversalNode from template via 7-step flow

`TemplateInstantiator` SHALL be a sealed class that instantiates a `TraversalNode` from a template definition via a 7-step `instantiate()` flow: (1) resolve placeholders via `PlaceholderResolver` — all `{placeholder}` tokens in the template SHALL be substituted with values from the provided context; (2) construct `Operation` via `_create_operation` — using resolved values and the target/restore semantics from the template; (3) construct `Precondition` via `_create_precondition` — using resolved path values; (4) construct `ChildrenStrategy` via `_create_children_strategy` — mapping template strategy type to the corresponding `ChildrenStrategy` value; (5) construct `ErrorPolicy` via `_create_error_policy` — mapping template error handling to `ErrorPolicy`; (6) assemble the `TraversalNode` from all constructed components; (7) V6.9 path concatenation — `precondition.path` SHALL equal `parent_path + [node.name]`, where parent_path is the path of the parent node and node.name is the instantiated node's name. `TemplateInstantiator` SHALL depend on `PlaceholderResolver` (already implemented in C#) and SHALL NOT depend on AI or Traversal layers.

#### Scenario: instantiate resolves placeholders in all template fields
- **WHEN** `TemplateInstantiator.instantiate(template, context, parent_path)` is called with a template containing `{app_name}` placeholders in operation, precondition, and children_strategy fields
- **THEN** the resulting `TraversalNode` has all `{app_name}` tokens replaced with the context value for `app_name`

#### Scenario: instantiate constructs a complete TraversalNode with all components
- **WHEN** `TemplateInstantiator.instantiate(template, context, parent_path)` is called with a fully specified template
- **THEN** the resulting `TraversalNode` has non-null `operation`, `precondition`, `children_strategy`, and `error_policy` fields constructed from the template

#### Scenario: instantiate performs V6.9 path concatenation
- **WHEN** `TemplateInstantiator.instantiate(template, context, parent_path=["home", "settings"])` is called and the instantiated node's name is "wifi_switch"
- **THEN** `result.precondition.path` equals `["home", "settings", "wifi_switch"]`

#### Scenario: instantiate with empty parent_path yields path with single node name
- **WHEN** `TemplateInstantiator.instantiate(template, context, parent_path=[])` is called and the instantiated node's name is "root_menu"
- **THEN** `result.precondition.path` equals `["root_menu"]`

#### Scenario: instantiate does not depend on AI or Traversal layers
- **WHEN** the `using` statements in `TemplateInstantiator` are inspected
- **THEN** they reference only `System.*`, `Domain.*`, `Graph.*` namespace types (no AI/Traversal/Trace imports)

#### Scenario: PlaceholderResolver integration resolves nested placeholders
- **WHEN** `TemplateInstantiator.instantiate` encounters a template with nested placeholder references like `{action}_{target}`
- **THEN** `PlaceholderResolver` resolves each token independently and the final value concatenates the resolved parts

#### Scenario: instantiate creates Operation with correct target and restore
- **WHEN** a template specifies `operation_type=Click` and `target_element="switch_button"` with `restore_action=PressBack`
- **THEN** the resulting `TraversalNode.operation` has `type=Click`, `target.element="switch_button"`, and `restore=PressBack`

### Requirement: Graph layer SHALL have three-directory structure with separated concerns

The Graph layer SHALL consist of three directories: `Abstractions/` (service interfaces), `Models/` (data records, enums, and pure interfaces like `ITraversalNode`), and `Services/` (service implementations). Each directory SHALL have a distinct namespace (`UniClaw.Core.Graph.Abstractions`, `.Graph.Models`, `.Graph.Services`) and SHALL respect the dependency direction: Models → Domain only; Abstractions → Models + Domain; Services → Abstractions + Models + Domain. Models MUST NOT reference Abstractions or Services.

#### Scenario: Models directory contains only data types
- **WHEN** all files in `Graph/Models/` are inspected
- **THEN** every file SHALL be a data record, enum, or pure interface (`ITraversalNode`)
- **AND** no service implementation class (DynamicMatcher, PlanCompiler, TemplateInstantiator, PlaceholderResolver, TemplateValidator) SHALL reside in Models/

#### Scenario: Abstractions directory contains only interfaces
- **WHEN** all files in `Graph/Abstractions/` are inspected
- **THEN** every file SHALL be an interface definition
- **AND** no implementation code SHALL reside in Abstractions/

#### Scenario: Services directory contains only implementations
- **WHEN** all files in `Graph/Services/` are inspected
- **THEN** every class SHALL implement at least one interface from Abstractions/ or be a static utility (PlaceholderResolver, TemplateValidator)
- **AND** every class SHALL be in namespace `UniClaw.Core.Graph.Services`

### Requirement: Graph services SHALL expose interfaces for DI and testability

Every service class in `Graph/Services/` that performs logic SHALL have a corresponding interface in `Graph/Abstractions/`. Specifically: `DynamicMatcher` SHALL implement `IDynamicMatcher`, `PlanCompiler` SHALL implement `IPlanCompiler`, `TemplateInstantiator` SHALL implement `ITemplateInstantiator`. `ITemplateRegistry` SHALL be moved from `Models/Template.cs` to `Abstractions/ITemplateRegistry.cs` with namespace changed to `UniClaw.Core.Graph.Abstractions`.

#### Scenario: DynamicMatcher implements IDynamicMatcher
- **WHEN** `DynamicMatcher` class is inspected
- **THEN** it SHALL declare `public sealed class DynamicMatcher : IDynamicMatcher`
- **AND** all public methods (Match, MatchAll) SHALL be declared in `IDynamicMatcher`

#### Scenario: PlanCompiler implements IPlanCompiler
- **WHEN** `PlanCompiler` class is inspected
- **THEN** it SHALL declare `public sealed class PlanCompiler : IPlanCompiler`
- **AND** the Compile method SHALL be declared in `IPlanCompiler`

#### Scenario: TemplateInstantiator implements ITemplateInstantiator
- **WHEN** `TemplateInstantiator` class is inspected
- **THEN** it SHALL declare `public sealed class TemplateInstantiator : ITemplateInstantiator`
- **AND** the Instantiate method SHALL be declared in `ITemplateInstantiator`

#### Scenario: Abstractions directory locked at 4 interfaces
- **WHEN** `Graph/Abstractions/` directory is inspected
- **THEN** it SHALL contain exactly 4 interfaces: IDynamicMatcher, IPlanCompiler, ITemplateInstantiator, ITemplateRegistry
- **AND** a CI-blocking guard test (`GraphAbstractions_Has4Interfaces`) SHALL enforce this count

### Requirement: Template.cs SHALL be split by type responsibility

The `Template.cs` file currently containing 4 types SHALL be split into separate files by type: `Template` record stays in `Models/Template.cs`, `ITemplateRegistry` interface moves to `Abstractions/ITemplateRegistry.cs`, `PlaceholderResolver` static class moves to `Services/PlaceholderResolver.cs`, `TemplateValidator` static class moves to `Services/TemplateValidator.cs`.

#### Scenario: Template.cs contains only Template record
- **WHEN** `Models/Template.cs` is inspected
- **THEN** it SHALL contain only the `Template` sealed record class
- **AND** no `ITemplateRegistry`, `PlaceholderResolver`, or `TemplateValidator` type SHALL remain in the file

#### Scenario: PlaceholderResolver and TemplateValidator are in Services namespace
- **WHEN** `PlaceholderResolver` and `TemplateValidator` classes are inspected
- **THEN** their namespace SHALL be `UniClaw.Core.Graph.Services`

### Requirement: TraversalEngine SHALL depend on Graph service interfaces, not concrete types

`TraversalEngine` SHALL declare its `DynamicMatcher` and `TemplateInstantiator` dependencies as interface types (`IDynamicMatcher` and `ITemplateInstantiator`) rather than concrete types. Default implementations SHALL be instantiated as `new DynamicMatcher()` and `new TemplateInstantiator()` respectively, preserving backward compatibility.

#### Scenario: TraversalEngine fields use interface types
- **WHEN** `TraversalEngine` private fields are inspected
- **THEN** the DynamicMatcher field SHALL be typed as `IDynamicMatcher`
- **AND** the TemplateInstantiator field SHALL be typed as `ITemplateInstantiator`

#### Scenario: TraversalEngine constructs default implementations
- **WHEN** `TraversalEngine` is constructed
- **THEN** `_matcher` SHALL be initialized as `new DynamicMatcher()`
- **AND** `_instantiator` SHALL be initialized as `new TemplateInstantiator()`

### Requirement: MatchableItem and MatchResult SHALL reside in Models as interface parameter types

`MatchableItem` and `MatchResult` records, which are parameter/return types of `IDynamicMatcher` interface methods, SHALL be extracted from `Services/DynamicMatcher.cs` into separate files in `Models/` (`MatchableItem.cs` and `MatchResult.cs`). This ensures Abstractions/ can reference these types without depending on Services/.

#### Scenario: MatchableItem and MatchResult in Models namespace
- **WHEN** `MatchableItem` and `MatchResult` files are inspected
- **THEN** their namespace SHALL be `UniClaw.Core.Graph.Models`
- **AND** they SHALL remain sealed record classes with unchanged field definitions

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

### Requirement: TraversalPlan SHALL validate a provided root node

`TraversalPlan.RootNode` is nullable — when omitted, `TraversalEngine.BuildDefaultRoot` builds a default root (existing fail-safe behavior, preserved). When `RootNode` IS provided (non-null), the constructor SHALL assert it has `NodeType` `Container` or `Screen` and `Operation.Action == NoAction`; violation SHALL throw `DomainValidationException`. A null `RootNode` is permitted (engine fallback).

#### Scenario: Malformed root node type throws
- **WHEN** `TraversalPlan` is constructed with a non-null `RootNode` whose `NodeType` is `Leaf`
- **THEN** `DomainValidationException` is thrown with FieldName "RootNode.NodeType"

#### Scenario: Root node with non-NoAction operation throws
- **WHEN** `TraversalPlan` is constructed with a non-null `RootNode` whose `Operation.Action` is `Click`
- **THEN** `DomainValidationException` is thrown with FieldName "RootNode.Operation"

#### Scenario: Null root node is permitted
- **WHEN** `TraversalPlan` is constructed with `RootNode = null`
- **THEN** the record is constructed without exception (engine builds default root)

