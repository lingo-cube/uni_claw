## ADDED Requirements

### Requirement: Plan compiler deterministic mapping
The PlanCompiler SHALL deterministically map IntentSlots to TraversalPlan without AI dependency.

#### Scenario: Scope to completion policy mapping
- **WHEN** slots.scope = "full"
- **THEN** completion_policy.type = NONE
- **WHEN** slots.scope = "partial"
- **THEN** completion_policy.type = MAX_STEPS
- **WHEN** slots.scope = "target_only" with target
- **THEN** completion_policy.type = TARGET_FOUND
- **THEN** completion_policy.target_name = slots.target
- **WHEN** slots.scope = "target_path"
- **THEN** completion_policy.type = NONE
- **THEN** root node uses STATIC children strategy

#### Scenario: Element handling to template mapping
- **WHEN** slots.element_handling = "full_interaction"
- **THEN** dynamic_rules includes 4 templates: menu_container, switch_leaf, slider_leaf, leaf_action
- **WHEN** slots.element_handling = "menu_only"
- **THEN** dynamic_rules includes only: menu_container
- **WHEN** slots.element_handling = "safe_mode"
- **THEN** dynamic_rules includes all 4 templates
- **THEN** root_node.meta["safe_mode"] = True
- **WHEN** slots.element_handling = "read_only"
- **THEN** dynamic_rules includes only: leaf_info

#### Scenario: Navigation to fallback mapping
- **WHEN** slots.navigation = "back"
- **THEN** exit_condition.fallback = BACK
- **WHEN** slots.navigation is None or other value
- **THEN** exit_condition.fallback = AUTO_ESCAPE

#### Scenario: Completion override
- **WHEN** slots.completion = "timeout"
- **THEN** completion_policy.type = TIMEOUT
- **THEN** completion_policy.timeout_seconds = 300 (default)
- **WHEN** slots.completion = "steps"
- **THEN** completion_policy.type = MAX_STEPS
- **THEN** completion_policy.max_steps = 100 (default)
- **WHEN** both slots.scope and slots.completion are set
- **THEN** slots.completion overrides scope-derived policy
- **THEN** system logs a warning about the override

#### Scenario: Depth mapping
- **WHEN** slots.depth is specified
- **THEN** intent_slots.depth = slots.depth
- **THEN** exit_condition.max_depth = slots.depth

#### Scenario: Restore mapping
- **WHEN** slots.restore is specified
- **THEN** root_node.meta["restore"] = slots.restore

#### Scenario: Target app mapping
- **WHEN** slots.target_app is specified
- **THEN** entry_app = slots.target_app

#### Scenario: Static path generation for target_path
- **WHEN** slots.scope = "target_path"
- **WHEN** slots.target = "设置/显示/亮度"
- **THEN** system parses target by "/" separator into 3 segments
- **THEN** root_node.children_strategy.type = STATIC
- **THEN** root_node.children_strategy.static_children = [child_1_id]
- **THEN** system creates static_nodes for each segment
- **THEN** node_1.name = "设置", precondition.path = ["设置"]
- **THEN** node_2.name = "显示", precondition.path = ["设置", "显示"]
- **THEN** node_3.name = "亮度", precondition.path = ["设置", "显示", "亮度"]

### Requirement: Slot validation
The PlanCompiler SHALL validate IntentSlots before compilation.

#### Scenario: Missing target_app validation
- **WHEN** slots.target_app is None or empty
- **THEN** compiler raises CompilerError("target_app is required")

#### Scenario: Target required for target_only scope
- **WHEN** slots.scope = "target_only"
- **WHEN** slots.target is None or empty
- **THEN** compiler raises CompilerError("target is required when scope is target_only")

#### Scenario: Target required for target_path scope
- **WHEN** slots.scope = "target_path"
- **WHEN** slots.target is None or empty
- **THEN** compiler raises CompilerError("target is required when scope is target_path")

#### Scenario: Invalid depth validation
- **WHEN** slots.depth <= 0
- **THEN** compiler raises CompilerError("Invalid depth: {depth}")
- **WHEN** slots.depth > 1000
- **THEN** compiler raises CompilerError("Invalid depth: {depth}")

#### Scenario: Valid slots pass validation
- **WHEN** all required slots are provided with valid values
- **THEN** _validate_slots() returns without error
- **THEN** compilation proceeds

#### Scenario: Completion override warning
- **WHEN** both slots.scope and slots.completion are provided
- **THEN** system logs warning about completion overriding scope
- **THEN** compilation continues (not an error)
