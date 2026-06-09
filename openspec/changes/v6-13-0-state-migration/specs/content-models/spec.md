## ADDED Requirements

### Requirement: Content models module structure

The system SHALL provide a centralized `src/models/content_models.py` module containing all content-related data models for simulation and testing purposes. The module SHALL contain 12 model classes organized as coordinate primitives, menu structures, page analysis models, content tree models, and simulation state container.

#### Scenario: Module imports all required classes
- **WHEN** a Python file imports from `src.models.content_models`
- **THEN** the following classes SHALL be available: `Coordinate`, `Direction`, `MenuInfo`, `MenuItem`, `MenuItemType`, `ExpectedAction`, `PageAnalysis`, `PopupInfo`, `ContentTree`, `ContentNode`, `VisitFingerprint`, `SimulationState`

#### Scenario: Module has proper documentation
- **WHEN** a developer reads the module docstring
- **THEN** it SHALL clearly state the models were moved from `src.state.content_tree` in V6.13.0
- **AND** it SHALL indicate which models are for simulation vs integration testing

### Requirement: Coordinate primitive model

The system SHALL provide a `Coordinate` BaseModel representing normalized screen coordinates (0-1 range). The model SHALL validate that x and y values are within [0.0, 1.0] range.

#### Scenario: Valid coordinate creation
- **WHEN** a Coordinate is created with x=0.5, y=0.5
- **THEN** the model SHALL be created successfully
- **AND** x SHALL equal 0.5
- **AND** y SHALL equal 0.5

#### Scenario: Invalid coordinate rejection
- **WHEN** a Coordinate is created with x=-0.1 or x=1.1
- **THEN** the model SHALL raise a validation error
- **AND** the error SHALL indicate values must be between 0 and 1

### Requirement: Direction enumeration

The system SHALL provide a `Direction` str Enum with four values: LEFT, RIGHT, TOP, BOTTOM. The Enum SHALL include helper methods: `values()` (returns list of string values), `from_value(value)` (creates enum from string), and `is_valid(value)` (checks string validity).

#### Scenario: Enum value access
- **WHEN** `Direction.LEFT` is accessed
- **THEN** it SHALL return the string "left"

#### Scenario: Helper methods work correctly
- **WHEN** `Direction.values()` is called
- **THEN** it SHALL return `["left", "right", "top", "bottom"]`
- **WHEN** `Direction.from_value("left")` is called
- **THEN** it SHALL return `Direction.LEFT`
- **WHEN** `Direction.is_valid("up")` is called
- **THEN** it SHALL return `False`

### Requirement: Menu info and item models

The system SHALL provide `MenuInfo` BaseModel with name, coordinate, and active fields. The system SHALL provide `MenuItem` BaseModel with name, type, coordinate, parent, description, expected_action, expects_page_change, and expects_state_change fields. The system SHALL provide `MenuItemType` str Enum with navigation and action types. The system SHALL provide `ExpectedAction` str Enum with NAVIGATE, TOGGLE, ACTION, NONE values.

#### Scenario: MenuInfo creation
- **WHEN** a MenuInfo is created with name="Settings", coordinate=Coordinate(x=0.5, y=0.5)
- **THEN** the model SHALL be created successfully
- **AND** active SHALL default to False

#### Scenario: MenuItem with all fields
- **WHEN** a MenuItem is created with name="Save", type=MenuItemType.BUTTON, coordinate=Coordinate(x=0.5, y=0.5)
- **THEN** the model SHALL be created successfully
- **AND** expected_action SHALL default to ACTION
- **AND** expects_page_change SHALL default to False

#### Scenario: MenuItem fingerprint generation
- **WHEN** `item.get_fingerprint("Settings", "General")` is called on a MenuItem with name="Save"
- **THEN** it SHALL return "Settings|General|Save"

### Requirement: Page analysis model

The system SHALL provide a `PageAnalysis` BaseModel containing level1_dir, level1_menus, level2_dir, level2_menus, current_path, items, is_popup, popup_info, close_button, back_button, has_scroll, and is_end_of_list fields. The system SHALL provide `PopupInfo` BaseModel with title, content, and close_button fields.

#### Scenario: PageAnalysis with nested models
- **WHEN** a PageAnalysis is created with level1_dir=Direction.LEFT, level1_menus=[MenuInfo(...)]
- **THEN** the model SHALL serialize and deserialize correctly
- **AND** all nested MenuInfo objects SHALL be preserved

#### Scenario: PageAnalysis with popup
- **WHEN** a PageAnalysis has is_popup=True
- **THEN** popup_info SHALL contain PopupInfo with title, content, and close_button
- **AND** the model SHALL serialize correctly

### Requirement: Content tree models for integration testing

The system SHALL provide `ContentTree` BaseModel with root_title, nodes dict, and level_counters dict. The system SHALL provide `ContentNode` BaseModel with id, title, level, parent_id, children, coordinate, node_type, description, and visited fields. The system SHALL provide `VisitFingerprint` BaseModel with level1, level2, and item_name fields.

#### Scenario: ContentTree node addition
- **WHEN** `tree.add_node("Settings", 1)` is called on a ContentTree
- **THEN** a new ContentNode SHALL be created with id="1", title="Settings", level=1
- **AND** the node SHALL be added to tree.nodes dict
- **AND** the node SHALL be returned

#### Scenario: ContentTree child node addition
- **WHEN** `tree.add_child_node("General", parent_id="1")` is called
- **THEN** a new ContentNode SHALL be created with id="1.1", title="General", level=2, parent_id="1"
- **AND** parent's children list SHALL contain "1.1"

#### Scenario: VisitFingerprint string representation
- **WHEN** a VisitFingerprint is created with level1="Settings", level2="General", item_name="Save"
- **THEN** `str(fingerprint)` SHALL return "Settings|General|Save"

#### Scenario: ContentTree to_markdown export
- **WHEN** `tree.to_markdown()` is called on a ContentTree with multiple nodes
- **THEN** it SHALL return a markdown string with hierarchical structure
- **AND** each node SHALL be indented according to its level
- **AND** visited status SHALL be indicated

### Requirement: SimulationState model (renamed from TraversalState)

The system SHALL provide a `SimulationState` BaseModel (formerly `TraversalState`) with 13 fields for simulation runtime state. The model SHALL include 8 methods: add_level1_menu, get_level2_menus, add_items, get_current_cache_key, is_visited, mark_visited, get_exception_history_summary, and get_exceptions_by_type.

#### Scenario: SimulationState creation
- **WHEN** a SimulationState is created
- **THEN** current_path SHALL default to empty list
- **AND** visited SHALL default to empty set
- **AND** content_tree SHALL default to empty ContentTree
- **AND** current_phase SHALL default to "initialized"

#### Scenario: SimulationState menu caching
- **WHEN** `state.add_level1_menu("Settings", MenuInfo(...))` is called
- **THEN** all_level1_menus dict SHALL contain "Settings" key
- **WHEN** `state.get_level2_menus("Settings")` is called
- **THEN** it SHALL return cached list of MenuInfo for "Settings"

#### Scenario: SimulationState visit tracking
- **WHEN** `state.mark_visited("Settings|General|Save")` is called
- **THEN** visited set SHALL contain "Settings|General|Save"
- **WHEN** `state.is_visited("Settings|General|Save")` is called
- **THEN** it SHALL return True

#### Scenario: SimulationState exception history
- **WHEN** exception_history_records contains exceptions
- **THEN** `get_exception_history_summary()` SHALL return total count and type breakdown
- **AND** `get_exceptions_by_type("ValidationError")` SHALL return only ValidationError records

### Requirement: Alias field serialization compatibility

The system SHALL maintain serialization compatibility for fields with aliases. The `exception_history_records` field SHALL use alias `_exception_history_records`. The `node_stack` field SHALL use alias `_node_stack`.

#### Scenario: JSON serialization uses alias
- **WHEN** a SimulationState is serialized with `state.json()`
- **THEN** the JSON SHALL contain `_exception_history_records` key (not `exception_history_records`)
- **AND** the JSON SHALL contain `_node_stack` key (not `node_stack`)

#### Scenario: JSON deserialization accepts alias
- **WHEN** JSON with `_exception_history_records` is deserialized
- **THEN** SimulationState SHALL parse correctly
- **AND** the field SHALL be accessible as `exception_history_records`

### Requirement: Backward compatibility alias

The system SHALL provide a backward compatibility alias in `src/models/__init__.py` where `TraversalState` points to `SimulationState`. This allows existing code to continue using `TraversalState` name during migration period.

#### Scenario: TraversalState alias works
- **WHEN** `from src.models import TraversalState` is imported
- **THEN** it SHALL return the SimulationState class
- **AND** `TraversalState is SimulationState` SHALL be True

#### Scenario: Alias can instantiate
- **WHEN** `state = TraversalState()` is called
- **THEN** it SHALL create a SimulationState instance
- **AND** all SimulationState methods SHALL be available

### Requirement: Deprecation warnings for legacy imports

The system SHALL provide deprecation warnings when importing from `src.state` module during V6.13.0. The warnings SHALL indicate the new import location and that the module will be removed in V6.14.0.

#### Scenario: Importing from src.state shows warning
- **WHEN** a Python file imports `from src.state import Coordinate`
- **THEN** a DeprecationWarning SHALL be raised
- **AND** the warning SHALL mention "Use src.models.content_models instead"
- **AND** the warning SHALL mention "This module will be removed in V6.14.0"

#### Scenario: Pydantic serialization skips warning
- **WHEN** Pydantic serializes a model (accessing __module__)
- **THEN** NO deprecation warning SHALL be raised
- **AND** serialization SHALL complete successfully

### Requirement: Fixture compatibility

The system SHALL maintain backward compatibility with existing fixture files. JSON fixtures SHALL deserialize correctly with new models. Pickle fixtures with old `__module__` references SHALL be detected and either migrated or regenerated.

#### Scenario: JSON fixture deserialization
- **WHEN** a JSON fixture containing PageAnalysis structure is loaded
- **THEN** it SHALL deserialize correctly using PageAnalysis from src.models.content_models
- **AND** all nested models SHALL be preserved

#### Scenario: Pickle fixture detection
- **WHEN** a pickle file is checked for compatibility
- **THEN** files with `__module__ = "src.state.content_tree"` SHALL be flagged
- **AND** a migration script SHALL be provided to update or regenerate

### Requirement: TYPE_CHECKING import handling

The system SHALL update `src/exception/context.py` to use type alias for TraversalState in TYPE_CHECKING block. The alias SHALL point to `TraversalRuntimeContext` from src.trace.context.

#### Scenario: TYPE_CHECKING import works
- **WHEN** mypy runs on src/exception/context.py
- **THEN** type checking SHALL pass
- **AND** TraversalState type SHALL resolve correctly
- **AND** runtime behavior SHALL be unchanged

### Requirement: No remaining src.state imports after V6.14.0

The system SHALL have zero imports from `src.state` module across all Python files after V6.14.0 cleanup. All 33 files SHALL be updated to import from `src.models.content_models`.

#### Scenario: Verification finds no imports
- **WHEN** `grep -r "from src.state" src/ tests/` is run
- **THEN** no results SHALL be returned
- **AND** all tests SHALL pass

#### Scenario: All tests pass after migration
- **WHEN** `pytest tests/ -v` is run
- **THEN** all tests SHALL pass
- **AND** no import errors SHALL occur
