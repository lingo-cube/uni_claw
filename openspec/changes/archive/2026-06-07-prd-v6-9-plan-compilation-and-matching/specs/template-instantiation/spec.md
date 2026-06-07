## MODIFIED Requirements

### Requirement: Template instantiation with path concatenation
The TemplateInstantiator SHALL support optional parent_path parameter for precondition path concatenation.

#### Scenario: Instantiate with parent_path
- **WHEN** `instantiate()` is called with `parent_path = ["Settings"]`
- **WHEN** template creates node with name "Display"
- **WHEN** node has precondition
- **THEN** node.precondition.path = `["Settings", "Display"]`

#### Scenario: Instantiate without parent_path
- **WHEN** `instantiate()` is called without `parent_path` parameter
- **THEN** system defaults parent_path to None
- **THEN** node.precondition.path = `[node.name]` (if precondition exists)
- **THEN** behavior is backward compatible

#### Scenario: Instantiate node without precondition
- **WHEN** template has no precondition field
- **THEN** no path concatenation occurs
- **THEN** no error is raised

#### Scenario: TemplateRegistry forwards parent_path
- **WHEN** `TemplateRegistry.instantiate()` is called with `parent_path`
- **THEN** system passes `parent_path` to `TemplateInstantiator.instantiate()`
- **THEN** returned node has concatenated path

### Requirement: Template instantiation basic behavior unchanged
The TemplateInstantiator SHALL maintain all existing instantiation behavior.

#### Scenario: Variable substitution unchanged
- **WHEN** template contains variable placeholders
- **THEN** system substitutes with values from context dict
- **THEN** behavior is unchanged from V6.8

#### Scenario: Node creation unchanged
- **WHEN** template defines node structure
- **THEN** system creates TraversalNode with all specified fields
- **THEN** behavior is unchanged from V6.8
