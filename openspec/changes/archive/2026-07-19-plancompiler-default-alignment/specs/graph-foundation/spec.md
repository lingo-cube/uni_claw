## ADDED Requirements

### Requirement: IntentSlots carries orthogonal traversal-intent dimensions

`IntentSlots` SHALL be a sealed record class whose fields each express one orthogonal intent dimension: `TargetApp` (string, required — the app), `Scope` (string, required — traversal shape ∈ `{full, target_only}`), `Target` (string?, required when Scope=target_only), `Depth` (int?, nullable = no depth constraint / DescendAll), `ElementHandling` (string?, ∈ template-set keys, null defaults to full_interaction), `Navigation` (string?), `Restore` (bool?), `Completion` (string?, override ∈ `{max_steps, timeout}`), and `Entry` (string?, traversal root, null = app-root). `Scope` vocabulary SHALL be exactly `{full, target_only}` — these map 1:1 to downstream D-86 Modes (full→Exact, target_only→Subset); values outside this set (including legacy `full_interaction`/`menu_only`/`safe_mode`/`read_only`/`target_path`) SHALL be rejected. `Depth` semantics: when non-null, it is an intent depth bound resolved against `TraversalEngineConfig.MaxDepth` by priority「tighter-wins」(`min(config.MaxDepth, IntentSlots.Depth)`); null means no depth constraint (DescendAll). `Entry` expresses the traversal root (default app-root); sub-menu exhaustive traversal uses `Entry=sub-menu-root` — the boundary is inherent in Entry + Back navigation, requiring no separate SingleLevel/DepthLimited scope. `Completion` overrides the scope-derived CompletionPolicy Type (see PlanCompiler requirement); it is NOT a side-bound.

#### Scenario: IntentSlots accepts full scope
- **WHEN** an `IntentSlots` is created with `Scope="full"`
- **THEN** it constructs without error

#### Scenario: IntentSlots accepts target_only scope with Target
- **WHEN** an `IntentSlots` is created with `Scope="target_only"` and a non-empty `Target`
- **THEN** it constructs without error

#### Scenario: IntentSlots rejects legacy element_handling values as Scope
- **WHEN** an `IntentSlots` is created with `Scope="full_interaction"` (or `menu_only`/`safe_mode`/`read_only`/`target_path`)
- **THEN** PlanCompiler validation rejects it (these are ElementHandling values, not Scope)

#### Scenario: Entry field is nullable and defaults to null
- **WHEN** an `IntentSlots` is created without specifying `Entry`
- **THEN** `Entry` is null (meaning app-root)

#### Scenario: Entry accepts non-empty string for sub-menu root
- **WHEN** an `IntentSlots` is created with `Entry="network_subtree"`
- **THEN** `Entry` stores `"network_subtree"`

#### Scenario: Depth null means no depth constraint
- **WHEN** an `IntentSlots` is created with `Depth=null`
- **THEN** it signals DescendAll (no depth bound); the effective engine MaxDepth is governed solely by config until Change B wires the intent source

## MODIFIED Requirements

### Requirement: PlanCompiler provides deterministic IntentSlots-to-TraversalPlan mapping

`PlanCompiler` SHALL be a sealed class that deterministically maps `IntentSlots` to `TraversalPlan` without AI dependency. It SHALL define `TEMPLATE_SETS` as a static readonly dictionary with 4 entries keyed by **ElementHandling** (not Scope): "full_interaction" → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"], "menu_only" → ["menu_container"], "safe_mode" → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"], and "read_only" → ["leaf_info"]. The `Compile()` method SHALL execute a 5-step flow: (1) `ValidateSlots` SHALL validate `TargetApp` non-empty, `Scope ∈ {full, target_only}` (rejecting legacy element_handling/target_path values), `ElementHandling` (if provided) ∈ TEMPLATE_SETS keys, `target_only ⇒ Target` non-empty, `Depth ≥ 0`, and `Completion` (if provided) ∈ `{max_steps, timeout}` (unknown values throw DomainValidationException — fail-fast, no silent None); (2) `BuildEntryPolicy` SHALL create a default `EntryPolicy` with `ColdLaunch`/`fallback=null` (not DirectDeeplink); (3) `BuildRootNode` SHALL assign `ChildrenStrategy.DYNAMIC_MATCH` with DynamicRules derived from `slots.ElementHandling ?? "full_interaction"` (NOT `Scope`), and the RootNode SHALL reflect `slots.Entry ?? slots.TargetApp`; (4) `BuildCompletionPolicy` SHALL derive `Type=None` for `Scope=full` and `Type=TargetFound(TargetName=Target, MatchMode=Contains, ActionOnFound=MarkAndStop)` for `Scope=target_only`; a `Completion` override SHALL cover the derived Type (`max_steps → Type=MaxSteps(+MaxSteps)`, `timeout → Type=Timeout(+TimeoutSeconds)`); (5) assemble `TraversalPlan` with all required fields including `EntryApp` from `slots.TargetApp`. The legacy `target_path` scope and `BuildStaticNodes` step are REMOVED (target_path has zero scenarios; static-node construction is retired). `CompletionPolicyType.None` is retained with semantics clarified as "exhaustive intent" (rename to Exhaustive deferred to Change B due to engine L286 coupling).

#### Scenario: compile produces a valid TraversalPlan for target_only scope
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=target_only`, `Target="wifi"`, and a valid `TargetApp`
- **THEN** the resulting `TraversalPlan` has `root_node.children_strategy` equal to `ChildrenStrategy.DYNAMIC_MATCH`, `completion_policy.Type` equal to `TargetFound` with `TargetName="wifi"`, and `entry_app` populated

#### Scenario: compile produces a valid TraversalPlan for full scope with element_handling
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=full`, `ElementHandling="full_interaction"`, and a valid `TargetApp`
- **THEN** the resulting `TraversalPlan` has `root_node.children_strategy` equal to `ChildrenStrategy.DYNAMIC_MATCH`, its dynamic rules driven by the `full_interaction` template set, and `completion_policy.Type` equal to `None`

#### Scenario: compile rejects legacy full_interaction as Scope
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope="full_interaction"`
- **THEN** `ValidateSlots` throws `DomainValidationException` (full_interaction is an ElementHandling value, not a Scope)

#### Scenario: compile rejects legacy target_path Scope
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope="target_path"`
- **THEN** `ValidateSlots` throws `DomainValidationException` (target_path scope removed)

#### Scenario: compile rejects target_only without Target
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope="target_only"` and `Target` null/empty
- **THEN** `ValidateSlots` throws `DomainValidationException` naming the scope/target combination

#### Scenario: compile rejects empty target_app
- **WHEN** `PlanCompiler.Compile(slots)` is called with `TargetApp` empty or null
- **THEN** `ValidateSlots` throws `DomainValidationException` naming `target_app`

#### Scenario: compile rejects unknown Completion override
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Completion="bogus"`
- **THEN** `ValidateSlots` throws `DomainValidationException` (fail-fast, NOT silent None)

#### Scenario: Completion max_steps override covers Type
- **WHEN** `PlanCompiler.Compile(slots)` is called with `Scope=full` and `Completion="max_steps"`
- **THEN** the resulting `completion_policy.Type` is `MaxSteps` (NOT None — override covers Type so the engine bound check fires)

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
