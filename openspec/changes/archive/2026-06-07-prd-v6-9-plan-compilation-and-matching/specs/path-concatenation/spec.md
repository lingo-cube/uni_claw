## ADDED Requirements

### Requirement: Child node path concatenation
The system SHALL automatically concatenate parent path with child name during node instantiation.

#### Scenario: Template instantiation with parent path
- **WHEN** `TemplateInstantiator.instantiate()` is called with `parent_path` parameter
- **WHEN** the instantiated node has a precondition
- **THEN** system sets `node.precondition.path = parent_path + [node.name]`
- **THEN** path is a list of strings representing the navigation hierarchy

#### Scenario: Template registry forwards parent path
- **WHEN** `TemplateRegistry.instantiate()` is called with `parent_path` parameter
- **THEN** system passes `parent_path` to `TemplateInstantiator.instantiate()`
- **THEN** returned node has concatenated precondition path

#### Scenario: Dynamic child path concatenation
- **WHEN** `_generate_dynamic_children()` instantiates a matched child
- **WHEN** parent has `current_path = ["Settings", "Display"]`
- **WHEN** child name is "Brightness"
- **THEN** child's precondition.path = `["Settings", "Display", "Brightness"]`

#### Scenario: Root node path
- **WHEN** instantiating a root node with no parent
- **THEN** parent_path is `None` or empty list
- **THEN** precondition.path = `[node.name]`

#### Scenario: Node without precondition
- **WHEN** instantiating a node that has no precondition field
- **THEN** no path concatenation occurs
- **THEN** no error is raised

#### Scenario: Optional parent_path parameter
- **WHEN** `instantiate()` is called without `parent_path` parameter
- **THEN** system defaults to `None`
- **THEN** behavior is backward compatible with existing code

#### Scenario: Static node path concatenation
- **WHEN** PlanCompiler creates static nodes for a target path
- **THEN** each node's precondition.path is set to full path from root
- **THEN** path_1 = `[segment_1]`
- **THEN** path_2 = `[segment_1, segment_2]`
- **THEN** path_3 = `[segment_1, segment_2, segment_3]`
