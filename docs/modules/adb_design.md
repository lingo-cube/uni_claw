# ADB Module Design Document

> **Module**: `src/adb/`
> **Version**: 1.0
> **Last Updated**: 2025-06-03

---

## Overview

The ADB (Android Debug Bridge) module provides device control abstraction for Uni-Claw. It encapsulates all interactions with Android devices through ADB, supporting both real device connections and mock implementations for testing.

---

## Module Responsibility

1. **Device Communication**: Execute ADB commands and capture output
2. **Device Control**: Implement tap, swipe, keypress, and screenshot operations
3. **Coordinate Management**: Handle normalized (0-1) and pixel coordinate conversions
4. **Error Handling**: Provide callbacks for operation failures
5. **Testing Support**: Mock implementation for unit/integration tests
6. **Trace Integration**: Optional tracing for observability

---

## Architecture

### Class Hierarchy

```
ADBClient (ABC)
    ├── RealADBClient     (Production implementation)
    └── MockADBClient     (Testing implementation)
```

### Core Components

#### 1. `ADBClient` (Abstract Base Class)

**Purpose**: Interface definition for all ADB operations

**Key Features**:
- Error callback mechanism for operation failures
- Abstract methods for device operations
- Coordinate normalization support
- Screen size caching

**Abstract Methods**:
```python
execute(command: str, timeout: int) -> str
tap(x: float, y: float) -> None
press_back() -> None
press_home() -> None
capture_screenshot(output_path: Optional[Path]) -> bytes
get_screen_size() -> ScreenSize
is_connected() -> bool
```

#### 2. `RealADBClient`

**Purpose**: Production implementation using subprocess

**Key Features**:
- Subprocess-based ADB command execution
- Multi-device support via device_id
- Binary mode screenshot capture
- Screen size caching
- Optional trace logging integration

**Implementation Details**:
- Uses `subprocess.run()` with timeouts
- Supports custom ADB path
- Converts normalized coordinates to pixels
- Captures screenshots in binary mode (PNG format)

#### 3. `MockADBClient`

**Purpose**: Testing implementation without device dependency

**Key Features**:
- Command logging for verification
- Configurable connection status
- Test error injection via `fail_next_operation()`
- Minimal PNG header generation
- Default screen size (1080x1920)

**Testing Capabilities**:
- `command_log`: Access to all executed commands
- `set_connected()`: Control connection state
- `fail_next_operation()`: Test error callbacks
- `add_mock_screenshot()`: Provide custom screenshot data

---

## Data Models

### `ScreenSize`

```python
@dataclass
class ScreenSize:
    width: int
    height: int

    def normalize_x(x_pixel: int) -> float
    def normalize_y(y_pixel: int) -> float
    def pixel_x(normalized_x: float) -> int
    def pixel_y(normalized_y: float) -> int
```

**Purpose**: Coordinate system conversion between pixel and normalized (0-1) space.

### `OperationType` (Enum)

```python
class OperationType(Enum):
    TAP = "tap"
    PRESS_BACK = "press_back"
    PRESS_HOME = "press_home"
    SCREENSHOT = "screenshot"
    EXECUTE = "execute"
    GET_SCREEN_SIZE = "get_screen_size"
```

**Purpose**: Operation categorization for error handling and tracing.

### `ErrorCallback` Type

```python
ErrorCallback = Callable[[OperationType, str, Optional[Exception]], None]
```

**Purpose**: Custom error handler signature for operation failures.

---

## Error Handling

### Error Handling Strategy

1. **Default Handler**: Log errors via Python logging
2. **Custom Callback**: Optional callback for application-specific handling
3. **Exception Types**: `ADBError` for all ADB-related failures

### Error Flow

```
Operation Failure
    ↓
_handle_error()
    ↓
Is custom callback set?
    ├─ Yes → Call callback(operation, message, exception)
    └─ No → _on_operation_error() → logger.error()
```

### Error Callback Usage

```python
def handle_error(operation: OperationType, message: str, exception: Optional[Exception]):
    # Custom error handling logic
    print(f"Failed: {operation.value} - {message}")

client.set_error_callback(handle_error)
```

---

## Coordinate System

### Normalized Coordinates

The module uses normalized coordinates (0-1) for device-independent positioning:

- `(0, 0)`: Top-left corner
- `(1, 1)`: Bottom-right corner
- `(0.5, 0.5)`: Center of screen

### Conversion Flow

```
Vision Service (normalized)
    ↓
ADBClient.tap(x, y)  # normalized input
    ↓
ScreenSize.pixel_x/y()  # convert to pixels
    ↓
ADB command: shell input tap <px> <py>
```

### Screen Size Detection

```python
# Command: adb shell wm size
# Output: Physical size: 1080x1920
```

Fallback to 1080x1920 if detection fails.

---

## Dependencies

### External Dependencies

```python
import subprocess    # Command execution
import logging       # Error logging
from pathlib import Path  # File paths
from dataclasses import dataclass  # Data models
from enum import Enum        # Enums
from abc import ABC, abstractmethod  # Interface definition
```

### Internal Dependencies

```python
from ..utils.trace import TraceLogger  # Optional tracing
```

### Module Dependencies (Incoming)

```python
# Used by TraversalEngine
from ..adb.adb_client import ADBClient, RealADBClient, MockADBClient

# Used by test modules
from src.adb.adb_client import MockADBClient, ScreenSize
```

---

## Design Decisions

### 1. Abstract Base Class Pattern

**Decision**: Use ABC for `ADBClient`

**Rationale**:
- Ensures interface consistency across implementations
- Supports dependency injection
- Enables easy testing with mock implementations

### 2. Normalized Coordinates

**Decision**: Use 0-1 normalized coordinates as primary interface

**Rationale**:
- Device independence
- Simplifies coordinate calculation from vision analysis
- Consistent with UI automation best practices

### 3. Binary Screenshot Mode

**Decision**: Use binary mode for screenshot capture

**Rationale**:
- More efficient than base64 encoding
- Direct PNG output
- No text encoding/decoding overhead

### 4. Screen Size Caching

**Decision**: Cache screen size in `RealADBClient`

**Rationale**:
- Avoids repeated ADB calls
- Screen size rarely changes during traversal
- Improves performance

### 5. Error Callback Pattern

**Decision**: Optional callback instead of exceptions-only

**Rationale**:
- Application can handle errors without try/catch
- Supports logging-only mode
- Flexible for different error handling strategies

### 6. Mock Client Design

**Decision**: Provide feature-rich mock client

**Rationale**:
- Enables unit testing without devices
- Command log verification
- Error injection for testing error handling
- Configurable behavior

---

## Observability

### Trace Integration

The module optionally integrates with the trace system:

```python
try:
    from ..utils.trace import TraceLogger
    self._trace = TraceLogger("adb")
except ImportError:
    pass
```

### Traced Operations

- `tap`: Coordinates, pixel conversion
- `press_back`: Button press
- `press_home`: Button press
- `screenshot`: Output path, size
- Errors and exceptions

---

## Testing

### Test Coverage

Located in `src/adb/test_client.py`:

- `TestScreenSize`: Coordinate conversion tests
- `TestMockADBClient`: Mock client functionality
- `TestCoordinateValidation`: Input validation
- `TestADBClientInterface`: Interface compliance
- `TestRealADBClientIntegration`: Device integration (skipped by default)

### Test Patterns

```python
def test_tap_logs_command():
    client = MockADBClient()
    client.tap(0.5, 0.5)
    assert "tap 0.5 0.5" in client.command_log
```

---

## Future Considerations

### Potential Enhancements

1. **Swipe Support**: Add swipe gesture support
2. **File Transfer**: Support push/pull operations
3. **App Lifecycle**: Enhanced app start/stop with activity selection
4. **Connection Pooling**: Multi-device parallel operations
5. **Async Operations**: AsyncIO-based command execution

### V6 Integration

The module remains compatible with V6 architecture:
- Used by `TraversalEngine`
- Mock client supports simulation testing
- Trace integration supports visualization

---

## Usage Example

```python
from src.adb import RealADBClient, MockADBClient

# Production
client = RealADBClient(device_id="emulator-5554")
client.tap(0.5, 0.5)
screenshot = client.capture_screenshot()

# Testing
mock = MockADBClient()
mock.tap(0.5, 0.5)
assert "tap 0.5 0.5" in mock.command_log
```

---

## API Reference

### `ADBClient`

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `execute()` | command, timeout | str | Execute ADB command |
| `tap()` | x, y | None | Tap at normalized coordinates |
| `press_back()` | - | None | Press back button |
| `press_home()` | - | None | Press home button |
| `capture_screenshot()` | output_path | bytes | Capture screenshot |
| `get_screen_size()` | - | ScreenSize | Get screen dimensions |
| `is_connected()` | - | bool | Check device connection |
| `set_error_callback()` | callback | None | Set error handler |
| `reconnect()` | - | bool | Reconnect ADB |

---

**Document Version**: 1.0
**Author**: Uni-Claw Development Team
