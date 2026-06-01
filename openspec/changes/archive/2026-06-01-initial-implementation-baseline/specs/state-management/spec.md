# State Management Capability Specification

## ADDED Requirements

### Requirement: Traversal State Model
The system SHALL maintain complete traversal state in a single model.

#### Scenario: State Initialization
- **WHEN** TraversalState is created
- **THEN** all fields are initialized to default values
- **AND** current_path is empty list
- **AND** visited is empty set

#### Scenario: Current Path Tracking
- **WHEN** system navigates through menus
- **THEN** current_path reflects current location [level1, level2]

#### Scenario: Visited Tracking
- **WHEN** element is successfully visited
- **THEN** element fingerprint is added to visited set

### Requirement: Menu Caching
The system SHALL cache menu structures to avoid repeated AI analysis.

#### Scenario: Level 1 Menu Cache
- **WHEN** system first analyzes page
- **THEN** all level1_menus are cached in all_level1_menus dict
- **AND** cache uses menu name as key

#### Scenario: Level 2 Menu Cache
- **WHEN** system analyzes a level1 menu
- **THEN** level2_menus for that menu are cached
- **AND** cache uses level1 name as key

#### Scenario: Cache Retrieval
- **WHEN** cached menus are needed
- **THEN** system retrieves from cache without AI call

### Requirement: Items Caching
The system SHALL cache content items by location.

#### Scenario: Items Cache Key
- **WHEN** caching items for location
- **THEN** cache key is "level1|level2" format

#### Scenario: Items Cache Retrieval
- **WHEN** system needs items for current location
- **THEN** get_items(cache_key) returns cached list

#### Scenario: Cache Miss
- **WHEN** cache_key doesn't exist
- **THEN** get_items() returns empty list

### Requirement: Content Tree Structure
The system SHALL maintain hierarchical tree of discovered content.

#### Scenario: Tree Initialization
- **WHEN** ContentTree is created
- **THEN** root_title is "Root"
- **AND** nodes dict is empty

#### Scenario: Add Level 1 Node
- **WHEN** add_node() is called with level=1
- **THEN** node is added with hierarchical ID
- **AND** parent_id is "0"

#### Scenario: Add Level 2 Node
- **WHEN** add_node() is called with level=2
- **THEN** node ID is "1.1" format (parent.child)
- **AND** parent linkage is established

#### Scenario: Add Child Node
- **WHEN** add_child_node() is called
- **THEN** node ID continues parent sequence
- **AND** is appended to parent's children list

#### Scenario: Tree Export
- **WHEN** to_markdown() is called
- **THEN** returns hierarchical markdown with indentation

### Requirement: Visit Fingerprint
The system SHALL generate unique fingerprints for visited elements.

#### Scenario: Fingerprint Format
- **WHEN** fingerprint is created for element
- **THEN** format is "level1|level2|item_name"

#### Scenario: Fingerprint Uniqueness
- **WHEN** same element name exists in different locations
- **THEN** fingerprints are different due to path prefix

#### Scenario: Fingerprint String Representation
- **WHEN** VisitFingerprint is converted to string
- **THEN** returns "level1|level2|item_name" format

### Requirement: State Persistence
The system SHALL save and load traversal state.

#### Scenario: State Save
- **WHEN** save() is called on StateManager
- **THEN** state is serialized to JSON file
- **AND** visited set is converted to list

#### Scenario: State Load
- **WHEN** load() is called on StateManager
- **THEN** JSON file is deserialized to TraversalState
- **AND** list is converted back to visited set

#### Scenario: No Existing State
- **WHEN** load() is called but state file doesn't exist
- **THEN** new TraversalState is created with defaults

### Requirement: Cache Key Generation
The system SHALL generate consistent cache keys from current path.

#### Scenario: Full Path Key
- **WHEN** current_path has 2+ elements
- **THEN** get_current_cache_key() returns "l1|l2"

#### Scenario: Partial Path Key
- **WHEN** current_path has fewer than 2 elements
- **THEN** get_current_cache_key() returns "root"

### Requirement: Coordinate Data Model
The system SHALL use normalized coordinates (0-1 range).

#### Scenario: Valid Coordinates
- **WHEN** Coordinate is created
- **THEN** x and y must be between 0.0 and 1.0

#### Scenario: Coordinate Validation
- **WHEN** coordinate value is outside 0-1 range
- **THEN** Pydantic validation raises error

### Requirement: Menu Info Model
The system SHALL store menu information with state.

#### Scenario: Menu Info Structure
- **WHEN** MenuInfo is created
- **THEN** it contains: name, coordinate, active state

#### Scenario: Active State Tracking
- **WHEN** menu is currently highlighted
- **THEN** active field is true

### Requirement: Progress Tracking
The system SHALL track traversal progress.

#### Scenario: Step Counter
- **WHEN** traversal step is executed
- **THEN** step_count is incremented

#### Scenario: Phase Tracking
- **WHEN** traversal phase changes
- **THEN** current_phase reflects current phase

#### Scenario: Error Tracking
- **WHEN** error occurs during traversal
- **THEN** consecutive_errors is incremented
- **AND** last_error is updated

### Requirement: Content Tree Node Types
The system SHALL support different node types for special elements.

#### Scenario: Standard Item Node
- **WHEN** regular content item is added
- **THEN** node_type is "item"

#### Scenario: Popup Node
- **WHEN** popup is recorded
- **THEN** node_type is "popup"

#### Scenario: Jump Node
- **WHEN** page jump is recorded
- **THEN** node_type is "jump"

#### Scenario: No Feedback Node
- **WHEN** item with no feedback is recorded
- **THEN** node_type is "no_feedback"
