## ADDED Requirements

### Requirement: Device factory shall provide device specification management
The test framework SHALL provide a `DeviceFactory` class at `tests/factories/device_factory.py` that manages device specifications as test data (not constants).

#### Scenario: DeviceFactory module exists
- **WHEN** test code imports from `tests.factories.device_factory`
- **THEN** the module SHALL be importable without errors
- **AND** the module SHALL provide `DeviceFactory` and `DeviceSpec` classes

### Requirement: Device specifications shall be predefined
The `DeviceFactory` class SHALL provide predefined device specifications for common test scenarios.

#### Scenario: Default phone device is accessible
- **WHEN** test code accesses `DeviceFactory.DEFAULT_PHONE`
- **THEN** the device SHALL have width=1440 and height=3168
- **AND** the device name SHALL be `"default_phone"`

#### Scenario: Small phone device is accessible
- **WHEN** test code accesses `DeviceFactory.SMALL_PHONE`
- **THEN** the device SHALL have width=1080 and height=2340

#### Scenario: Tablet device is accessible
- **WHEN** test code accesses `DeviceFactory.TABLET`
- **THEN** the device SHALL have width=2048 and height=2732

### Requirement: Device specifications are test data, not constants
Device specifications SHALL be managed as test data in factory methods, NOT as global constants.

#### Scenario: Device spec values can be modified
- **WHEN** a test requires a custom device size
- **THEN** the test SHALL be able to create a custom `DeviceSpec`
- **AND** the test SHALL NOT be constrained to predefined constant values

### Requirement: Coordinate factory shall provide coordinate data management
The `CoordinateFactory` class SHALL provide methods for creating coordinate test data.

#### Scenario: CoordinateFactory creates arbitrary coordinates
- **WHEN** test code calls `CoordinateFactory.create(0.3, 0.7)`
- **THEN** the result SHALL be a `Coordinate` object with x=0.3 and y=0.7
- **AND** the factory SHALL support any valid coordinate values

#### Scenario: CoordinateFactory provides common positions
- **WHEN** test code calls `CoordinateFactory.center()`
- **THEN** the result SHALL be a `Coordinate` object with x=0.5 and y=0.5

#### Scenario: CoordinateFactory provides named positions
- **WHEN** test code calls `CoordinateFactory.top_left()`, `top_menu()`, or other named methods
- **THEN** each method SHALL return the appropriate coordinate
- **AND** these SHALL be convenience methods, NOT constants

### Requirement: Coordinate values are business-generated, not constant
Coordinate values SHALL be treated as business-generated test data, NOT as configuration constants.

#### Scenario: Coordinates support arbitrary values
- **WHEN** a test needs a specific coordinate based on UI layout
- **THEN** the test SHALL be able to use `CoordinateFactory.create()` with any values
- **AND** the test SHALL NOT be constrained to predefined constant positions

### Requirement: Coordinate factory provides both object and dict formats
The factory SHALL support both `Coordinate` dataclass objects and dictionary formats for compatibility.

#### Scenario: Dict format for coordinate
- **WHEN** test code calls `DeviceFactory.create_coordinate(0.5, 0.5)`
- **THEN** the result SHALL be a dictionary `{'x': 0.5, 'y': 0.5}`

#### Scenario: Object format for coordinate
- **WHEN** test code calls `CoordinateFactory.create(0.5, 0.5)`
- **THEN** the result SHALL be a `Coordinate` dataclass with x and y attributes
