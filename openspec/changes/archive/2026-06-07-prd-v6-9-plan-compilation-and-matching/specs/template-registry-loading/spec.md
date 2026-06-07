## ADDED Requirements

### Requirement: Template registry initialization
The GraphTraversalEngine SHALL initialize `TemplateRegistry` with built-in templates during engine initialization.

#### Scenario: Template registry creation
- **WHEN** `_load_template_registry()` is called during engine initialization
- **THEN** system creates a new `TemplateRegistry` instance
- **THEN** system assigns to `self.template_registry`
- **THEN** template registry always loads 3 built-in templates (menu_container, switch_leaf, slider_leaf)

#### Scenario: Custom template file loading
- **WHEN** TraversalPlan specifies `template_registry` file path
- **WHEN** the file exists at the specified path
- **THEN** system calls `template_registry.load_from_file(path)`
- **THEN** custom templates are added to registry
- **THEN** built-in templates remain available

#### Scenario: Dynamic matcher initialization
- **WHEN** template registry is initialized
- **THEN** system creates `DynamicMatcher` instance with the registry
- **THEN** system assigns to `self.dynamic_matcher`

#### Scenario: Missing template file handling
- **WHEN** TraversalPlan specifies `template_registry` file path
- **WHEN** the file does not exist at the specified path
- **THEN** system continues with built-in templates only
- **THEN** no error is raised
- **THEN** engine logs a warning about missing file

#### Scenario: No template registry specified
- **WHEN** TraversalPlan does not specify `template_registry`
- **THEN** system initializes with built-in templates only
- **THEN** dynamic matcher is still created and functional
