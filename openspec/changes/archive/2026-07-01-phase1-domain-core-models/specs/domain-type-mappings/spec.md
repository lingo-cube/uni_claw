## ADDED Requirements

### Requirement: AndroidWidgetClass enum is ported from element_type_mapper.py
The `AndroidWidgetClass` enum SHALL be ported from `element_type_mapper.py` into `Domain/Mappings/`, defining every Android widget class used by the mapper table.

#### Scenario: All widget classes from the Python source are present
- **WHEN** the `AndroidWidgetClass` enum members are compared against the widget classes referenced in `element_type_mapper.py`
- **THEN** every widget class in the Python source has a corresponding enum member

#### Scenario: No duplicate widget-class members
- **WHEN** `AndroidWidgetClass` members are enumerated
- **THEN** each member name is unique (one type, one location)

### Requirement: ElementTypeMapper exposes static mapping methods
`ElementTypeMapper` SHALL expose `map_android_class`, `to_menu_item_type`, and `to_expected_action` as static methods mapping an `AndroidWidgetClass` to `TypeHint`, `MenuItemType`, and `ExpectedAction` respectively. It SHALL depend only on `Models.Content` and `Models.Vision` and SHALL NOT depend on any upper layer.

#### Scenario: map_android_class returns a TypeHint
- **WHEN** `ElementTypeMapper.map_android_class(<widgetClass>)` is called for a mapped widget class
- **THEN** it returns the canonical `TypeHint` for that class

#### Scenario: to_menu_item_type returns a MenuItemType
- **WHEN** `ElementTypeMapper.to_menu_item_type(<widgetClass>)` is called for a mapped widget class
- **THEN** it returns the corresponding `MenuItemType`

#### Scenario: to_expected_action returns an ExpectedAction
- **WHEN** `ElementTypeMapper.to_expected_action(<widgetClass>)` is called for a mapped widget class
- **THEN** it returns the corresponding `ExpectedAction`

#### Scenario: No upper-layer dependency
- **WHEN** the `Domain/Mappings/` namespace usings are inspected
- **THEN** they reference only `System.*`, `Models.Content`, and `Models.Vision` (no Graph/StateMachine/Traversal/Trace/AI)

### Requirement: Mapper table matches the Python source verbatim
The full Android widget-class → type mapping table SHALL match `element_type_mapper.py` row-for-row. A full-table scan test SHALL assert every widget class maps to the same `(TypeHint, MenuItemType, ExpectedAction)` triple as the Python source.

#### Scenario: Full-table scan matches Python source
- **WHEN** every `AndroidWidgetClass` member is run through all three mapping methods
- **THEN** the resulting triples equal the triples defined in `element_type_mapper.py`, with zero mismatches

#### Scenario: Unmapped widget class has defined behavior
- **WHEN** a widget class with no explicit mapping is passed to the mapper
- **THEN** the mapper returns a defined fallback (documented per the Python source), not an exception, unless the Python source also throws
