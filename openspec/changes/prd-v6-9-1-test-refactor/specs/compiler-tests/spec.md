# Spec: Compiler Tests

## ADDED Requirements

### Requirement: C series tests SHALL verify PlanCompiler scope mapping
The system SHALL provide tests that verify IntentSlots.scope correctly maps to CompletionPolicy.type.

#### Scenario: Full scope maps to NONE
- **WHEN** C1 test compiles plan with `scope="full"`
- **THEN** completion_policy.type equals NONE

#### Scenario: Partial scope maps to MAX_STEPS
- **WHEN** C2 test compiles plan with `scope="partial"`
- **THEN** completion_policy.type equals MAX_STEPS

#### Scenario: Target_only scope with target maps to TARGET_FOUND
- **WHEN** C3 test compiles plan with `scope="target_only"` and target
- **THEN** completion_policy.type equals TARGET_FOUND and target_name is set

#### Scenario: Target_only without target raises error
- **WHEN** C4 test compiles plan with `scope="target_only"` and no target
- **THEN** system raises CompilerError

### Requirement: C series tests SHALL verify PlanCompiler static path compilation
The system SHALL provide tests that verify `scope="target_path"` creates STATIC node chain with proper path concatenation.

#### Scenario: Target path creates STATIC chain
- **WHEN** C5 test compiles plan with `scope="target_path"` and target="Settings/Display/Brightness"
- **THEN** children_strategy.type equals STATIC and 3 static_nodes are created

#### Scenario: Target path concatenates precondition paths
- **WHEN** C5 test compiles static path with 3 segments
- **THEN** node_1.precondition.path equals ["Settings"], node_2 equals ["Settings","Display"], node_3 equals ["Settings","Display","Brightness"]

### Requirement: C series tests SHALL verify PlanCompiler element_handling mapping
The system SHALL provide tests that verify IntentSlots.element_handling correctly maps to dynamic_rules composition.

#### Scenario: Full interaction creates 4 rules
- **WHEN** C6 test compiles plan with `element_handling="full_interaction"`
- **THEN** dynamic_rules contains 4 rules (menu_container, switch_leaf, slider_leaf, leaf_action)

#### Scenario: Menu only creates menu_container rule
- **WHEN** C7 test compiles plan with `element_handling="menu_only"`
- **THEN** dynamic_rules contains only menu_container rule

#### Scenario: Safe mode creates rules with meta flag
- **WHEN** C8 test compiles plan with `element_handling="safe_mode"`
- **THEN** dynamic_rules contains 4 rules and meta["safe_mode"] equals True

#### Scenario: Read only creates leaf_info rule
- **WHEN** C9 test compiles plan with `element_handling="read_only"`
- **THEN** dynamic_rules contains only leaf_info rule

### Requirement: C series tests SHALL verify PlanCompiler navigation mapping
The system SHALL provide tests that verify IntentSlots.navigation correctly maps to exit_condition.fallback.

#### Scenario: Navigation back maps to BACK fallback
- **WHEN** C10 test compiles plan with `navigation="back"`
- **THEN** exit_condition.fallback equals BACK

#### Scenario: No navigation maps to AUTO_ESCAPE
- **WHEN** C10 test compiles plan without navigation field
- **THEN** exit_condition.fallback equals AUTO_ESCAPE

### Requirement: C series tests SHALL verify PlanCompiler completion override
The system SHALL provide tests that verify IntentSlots.completion overrides scope-derived policy.

#### Scenario: Timeout completion overrides scope
- **WHEN** C11 test compiles plan with `scope="full"` and `completion="timeout"`
- **THEN** completion_policy.type equals TIMEOUT (not NONE)

### Requirement: C series tests SHALL verify PlanCompiler validation
The system SHALL provide tests that verify PlanCompiler validates required fields.

#### Scenario: Missing target_app raises error
- **WHEN** C12 test compiles plan without target_app
- **THEN** system raises CompilerError
