# ADB Control Capability Specification

## ADDED Requirements

### Requirement: Device Interface Abstraction
The system SHALL provide abstract ADB client interface for device control.

#### Scenario: Real Device
- **WHEN** using RealADBClient
- **THEN** system executes actual ADB commands on connected device

#### Scenario: Mock Device
- **WHEN** using MockADBClient
- **THEN** system simulates ADB operations without device

### Requirement: Command Execution
The system SHALL execute ADB commands and return output.

#### Scenario: Successful Command
- **WHEN** execute() is called with valid command
- **THEN** system executes command on device
- **AND** returns command output as string

#### Scenario: Command Timeout
- **WHEN** command execution exceeds timeout
- **THEN** system raises timeout error

#### Scenario: Invalid Command
- **WHEN** execute() is called with invalid ADB syntax
- **THEN** system returns error message from ADB

### Requirement: Screen Capture
The system SHALL capture device screenshots.

#### Scenario: Standard Screenshot
- **WHEN** capture_screenshot() is called
- **THEN** system executes adb shell screencap -p
- **AND** returns screenshot as PNG bytes

#### Scenario: Screenshot Failure
- **WHEN** device is disconnected during capture
- **THEN** system raises appropriate error

### Requirement: Touch Interaction
The system SHALL simulate touch events at screen coordinates.

#### Scenario: Normalized Coordinates
- **WHEN** tap(x=0.5, y=0.5) is called
- **THEN** system converts to actual screen coordinates
- **AND** executes adb shell input tap

#### Scenario: Edge Coordinates
- **WHEN** tap(x=0.0, y=0.0) is called
- **THEN** system taps at top-left corner

#### Scenario: Full Screen Coordinates
- **WHEN** tap(x=1.0, y=1.0) is called
- **THEN** system taps at bottom-right corner

### Requirement: Navigation Button
The system SHALL simulate back button press.

#### Scenario: Standard Back
- **WHEN** press_back() is called
- **THEN** system executes adb shell input keyevent KEYCODE_BACK

### Requirement: Package Information
The system SHALL retrieve current app package information.

#### Scenario: Get Current Package
- **WHEN** get_current_package() is called
- **THEN** system executes adb shell dumpsys window
- **AND** returns current foreground package name

### Requirement: App Launch
The system SHALL launch specific apps by package name.

#### Scenario: Launch App
- **WHEN** start_app(package) is called with valid package
- **THEN** system executes adb shell am start
- **AND** app launches on device

#### Scenario: Invalid Package
- **WHEN** start_app() is called with non-existent package
- **THEN** system returns error from ADB

### Requirement: Device Connection Check
The system SHALL verify device connection status.

#### Scenario: Device Connected
- **WHEN** device is properly connected
- **THEN** commands execute normally

#### Scenario: Device Disconnected
- **WHEN** device becomes disconnected
- **THEN** commands fail with device error

### Requirement: Screen Size Detection
The system SHALL detect device screen resolution.

#### Scenario: Get Screen Size
- **WHEN** system needs screen dimensions
- **THEN** system executes adb shell wm size
- **AND** parses width and height from output

### Requirement: Mock Device Behavior
The mock ADB client SHALL simulate realistic behavior for testing.

#### Scenario: Mock Screenshot
- **WHEN** capture_screenshot() is called on MockADBClient
- **THEN** returns predefined screenshot bytes

#### Scenario: Mock Command Response
- **WHEN** execute() is called with specific command
- **THEN** returns predefined response for that command

#### Scenario: Mock Recording
- **WHEN** any method is called on MockADBClient
- **THEN** call is recorded for test verification
