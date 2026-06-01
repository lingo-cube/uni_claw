"""ADB client interface and implementations."""

import logging
import subprocess
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Callable, Optional

logger = logging.getLogger(__name__)


class ADBError(Exception):
    """ADB operation error."""

    pass


class OperationType(Enum):
    """Type of ADB operation."""

    TAP = "tap"
    PRESS_BACK = "press_back"
    PRESS_HOME = "press_home"
    SCREENSHOT = "screenshot"
    EXECUTE = "execute"
    GET_SCREEN_SIZE = "get_screen_size"


# Error callback type: receives operation type, error message, and original exception
ErrorCallback = Callable[[OperationType, str, Optional[Exception]], None]


@dataclass
class ScreenSize:
    """Device screen dimensions."""

    width: int
    height: int

    def normalize_x(self, x_pixel: int) -> float:
        """Convert pixel x to normalized (0-1)."""
        return x_pixel / self.width if self.width > 0 else 0.0

    def normalize_y(self, y_pixel: int) -> float:
        """Convert pixel y to normalized (0-1)."""
        return y_pixel / self.height if self.height > 0 else 0.0

    def pixel_x(self, normalized_x: float) -> int:
        """Convert normalized x to pixel."""
        return int(normalized_x * self.width)

    def pixel_y(self, normalized_y: float) -> int:
        """Convert normalized y to pixel."""
        return int(normalized_y * self.height)


class ADBClient(ABC):
    """Abstract base for ADB operations."""

    def __init__(self):
        """Initialize ADB client with optional error callback."""
        self._error_callback: Optional[ErrorCallback] = None

    def set_error_callback(self, callback: ErrorCallback) -> None:
        """Set error callback for operation failures.

        Args:
            callback: Function called when an operation fails.
                     Receives (operation_type, error_message, exception)
                     If set, this overrides the default logging behavior.
        """
        self._error_callback = callback

    def _on_operation_error(
        self,
        operation: OperationType,
        message: str,
        exception: Optional[Exception] = None,
    ) -> None:
        """Default error handler - logs error only.

        This method is called when no custom callback is set.
        Subclasses can override this to provide custom default behavior.

        Args:
            operation: Type of operation that failed
            message: Error message
            exception: Original exception (if any)
        """
        logger.error(f"[{operation.value}] {message}")

    def _handle_error(
        self,
        operation: OperationType,
        message: str,
        exception: Optional[Exception] = None,
    ) -> None:
        """Handle error - uses custom callback if set, otherwise uses default logger.

        Args:
            operation: Type of operation that failed
            message: Error message
            exception: Original exception (if any)
        """
        if self._error_callback:
            self._error_callback(operation, message, exception)
        else:
            # Use default handler (log only)
            self._on_operation_error(operation, message, exception)

    @abstractmethod
    def execute(self, command: str, timeout: int = 30) -> str:
        """Execute an ADB command and return output.

        Args:
            command: ADB command (without 'adb' prefix)
            timeout: Command timeout in seconds

        Returns:
            Command stdout output

        Raises:
            ADBError: If command fails
        """
        pass

    @abstractmethod
    def tap(self, x: float, y: float) -> None:
        """Tap at normalized coordinates (0-1).

        Args:
            x: Normalized X coordinate (0-1)
            y: Normalized Y coordinate (0-1)
        """
        pass

    @abstractmethod
    def press_back(self) -> None:
        """Press back button."""
        pass

    @abstractmethod
    def press_home(self) -> None:
        """Press home button."""
        pass

    @abstractmethod
    def capture_screenshot(self, output_path: Optional[Path] = None) -> bytes:
        """Capture screenshot and return image data.

        Args:
            output_path: Optional path to save screenshot

        Returns:
            PNG image bytes
        """
        pass

    @abstractmethod
    def get_screen_size(self) -> ScreenSize:
        """Get device screen size.

        Returns:
            ScreenSize with width and height
        """
        pass

    @abstractmethod
    def is_connected(self) -> bool:
        """Check if device is connected."""
        pass

    def reconnect(self) -> bool:
        """Reconnect ADB connection.

        Returns:
            True if reconnection succeeded
        """
        try:
            # Kill and restart ADB server
            subprocess.run([self.adb_path if hasattr(self, "adb_path") else "adb", "kill-server"],
                          capture_output=True, timeout=10)
            subprocess.run([self.adb_path if hasattr(self, "adb_path") else "adb", "start-server"],
                          capture_output=True, timeout=10)
            time.sleep(1.0)
            return self.is_connected()
        except Exception as e:
            logger.error(f"ADB reconnection failed: {e}")
            return False

    def stop_app(self, app_name: str) -> bool:
        """Stop an application.

        Args:
            app_name: Package name of the app

        Returns:
            True if app stopped successfully
        """
        try:
            self.execute(f"shell am force-stop {app_name}")
            return True
        except ADBError as e:
            logger.error(f"Failed to stop app {app_name}: {e}")
            return False

    def start_app(self, app_name: str) -> bool:
        """Start an application.

        Args:
            app_name: Package name or activity of the app

        Returns:
            True if app started successfully
        """
        try:
            self.execute(f"shell monkey -p {app_name} -c android.intent.category.LAUNCHER 1")
            return True
        except ADBError as e:
            logger.error(f"Failed to start app {app_name}: {e}")
            return False


class RealADBClient(ADBClient):
    """Real ADB client using subprocess."""

    def __init__(
        self,
        adb_path: str = "adb",
        device_id: Optional[str] = None,
    ):
        """Initialize ADB client.

        Args:
            adb_path: Path to adb executable
            device_id: Optional device ID for multi-device setups
        """
        super().__init__()
        self.adb_path = adb_path
        self.device_id = device_id
        self._screen_size: Optional[ScreenSize] = None

        # Trace logging
        self._trace = None
        try:
            from ..utils.trace import TraceLogger
            self._trace = TraceLogger("adb")
        except ImportError:
            pass

    def _build_command(self, adb_cmd: str) -> list[str]:
        """Build full command list."""
        cmd = [self.adb_path]
        if self.device_id:
            cmd.extend(["-s", self.device_id])
        cmd.extend(adb_cmd.split())
        return cmd

    def execute(self, command: str, timeout: int = 30) -> str:
        """Execute ADB command."""
        cmd = self._build_command(command)
        logger.debug(f"ADB: {' '.join(cmd)}")

        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=timeout,
                check=True,
            )
            return result.stdout.strip()
        except subprocess.CalledProcessError as e:
            error_msg = f"Command failed: {e.stderr}"
            self._handle_error(OperationType.EXECUTE, error_msg, e)
            raise ADBError(error_msg) from e
        except subprocess.TimeoutExpired as e:
            error_msg = f"Command timed out after {timeout}s"
            self._handle_error(OperationType.EXECUTE, error_msg, e)
            raise ADBError(error_msg) from e

    def tap(self, x: float, y: float) -> None:
        """Tap at normalized coordinates."""
        trace_context = None
        if self._trace:
            trace_context = self._trace.start_span(
                operation="tap",
                tags={"x": x, "y": y}
            )

        if not (0.0 <= x <= 1.0 and 0.0 <= y <= 1.0):
            error_msg = f"Invalid coordinates: ({x}, {y})"
            if self._trace and trace_context:
                self._trace.finish_span(trace_context,
                    error=Exception(error_msg))
            self._handle_error(OperationType.TAP, error_msg)
            raise ADBError(error_msg)

        try:
            screen = self.get_screen_size()
            px = screen.pixel_x(x)
            py = screen.pixel_y(y)

            logger.info(f"[ADB] Tapping at normalized ({x:.3f}, {y:.3f}) -> pixel ({px}, {py})")

            self.execute(f"shell input tap {px} {py}")

            if self._trace and trace_context:
                self._trace.log_output(trace_context,
                    normalized_x=x, normalized_y=y,
                    pixel_x=px, pixel_y=py)
                self._trace.finish_span(trace_context)

        except ADBError as e:
            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)
            raise

    def press_back(self) -> None:
        """Press back button."""
        trace_context = None
        if self._trace:
            trace_context = self._trace.start_span(operation="press_back")

        try:
            logger.info("[ADB] Pressing BACK button")
            self.execute("shell input keyevent KEYCODE_BACK")

            if self._trace and trace_context:
                self._trace.finish_span(trace_context)

        except ADBError as e:
            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)
            self._handle_error(OperationType.PRESS_BACK, str(e), e)
            raise

    def press_home(self) -> None:
        """Press home button."""
        trace_context = None
        if self._trace:
            trace_context = self._trace.start_span(operation="press_home")

        try:
            logger.info("[ADB] Pressing HOME button")
            self.execute("shell input keyevent KEYCODE_HOME")

            if self._trace and trace_context:
                self._trace.finish_span(trace_context)

        except ADBError as e:
            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)
            self._handle_error(OperationType.PRESS_HOME, str(e), e)
            raise

    def capture_screenshot(self, output_path: Optional[Path] = None) -> bytes:
        """Capture screenshot and return image data.

        Args:
            output_path: Optional path to save screenshot

        Returns:
            PNG image bytes
        """
        trace_context = None
        if self._trace:
            trace_context = self._trace.start_span(
                operation="screenshot",
                tags={"output_path": str(output_path) if output_path else None}
            )

        try:
            logger.debug(f"[ADB] Capturing screenshot")

            if output_path:
                output_path.parent.mkdir(parents=True, exist_ok=True)
                # Use raw binary mode for screenshot
                cmd = self._build_command("shell screencap -p")
                result = subprocess.run(
                    cmd,
                    capture_output=True,
                    check=True,
                )
                with open(output_path, "wb") as f:
                    f.write(result.stdout)

                logger.info(f"[ADB] Screenshot saved to {output_path} ({len(result.stdout)} bytes)")

                if self._trace and trace_context:
                    self._trace.log_output(trace_context,
                        saved_to=str(output_path),
                        size=len(result.stdout))
                    self._trace.finish_span(trace_context)

                return result.stdout

            # Capture to memory using binary mode
            cmd = self._build_command("shell screencap -p")
            result = subprocess.run(
                cmd,
                capture_output=True,
                check=True,
            )

            logger.info(f"[ADB] Screenshot captured ({len(result.stdout)} bytes)")

            if self._trace and trace_context:
                self._trace.log_output(trace_context, size=len(result.stdout))
                self._trace.finish_span(trace_context)

            return result.stdout

        except (subprocess.CalledProcessError, OSError) as e:
            error_msg = f"Screenshot failed: {e}"
            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)
            self._handle_error(OperationType.SCREENSHOT, error_msg, e)
            raise ADBError(error_msg) from e

    def get_screen_size(self) -> ScreenSize:
        """Get screen size, caching result."""
        if self._screen_size is None:
            try:
                output = self.execute("shell wm size")
                # Parse "Physical size: 1080x1920"
                size_str = output.split(": ")[1]
                width, height = map(int, size_str.split("x"))
                self._screen_size = ScreenSize(width=width, height=height)
            except (IndexError, ValueError) as e:
                error_msg = f"Failed to parse screen size: {output}"
                self._handle_error(OperationType.GET_SCREEN_SIZE, error_msg, e)
                logger.warning(error_msg)
                # Default to common resolution
                self._screen_size = ScreenSize(width=1080, height=1920)
            except ADBError:
                # Already handled by execute()
                raise

        return self._screen_size

    def is_connected(self) -> bool:
        """Check if device is connected."""
        try:
            devices = self.execute("devices")
            # Check for device in output
            lines = devices.split("\n")[1:]  # Skip header
            return any("device" in line for line in lines)
        except ADBError:
            return False


class MockADBClient(ADBClient):
    """Mock ADB client for testing."""

    def __init__(self):
        """Initialize mock with default screen size."""
        super().__init__()
        self._screen_size = ScreenSize(width=1080, height=1920)
        self._command_log: list[str] = []
        self._screenshots: list[bytes] = []
        self._connected = True
        self._fail_next_operation: bool = False  # For testing error callbacks

    def execute(self, command: str, timeout: int = 30) -> str:
        """Mock execute - logs command."""
        self._command_log.append(command)
        logger.debug(f"[MOCK ADB] {command}")

        # Mock responses
        if "wm size" in command:
            return f"Physical size: {self._screen_size.width}x{self._screen_size.height}"
        if "devices" in command:
            return "List of devices attached\ndevice\tdevice"

        return ""

    def tap(self, x: float, y: float) -> None:
        """Mock tap."""
        if self._fail_next_operation:
            error_msg = f"Mock tap failed at ({x}, {y})"
            self._handle_error(OperationType.TAP, error_msg)
            self._fail_next_operation = False
            return
        self._command_log.append(f"tap {x} {y}")

    def press_back(self) -> None:
        """Mock back."""
        if self._fail_next_operation:
            self._handle_error(OperationType.PRESS_BACK, "Mock back failed")
            self._fail_next_operation = False
            return
        self._command_log.append("back")

    def press_home(self) -> None:
        """Mock home."""
        if self._fail_next_operation:
            self._handle_error(OperationType.PRESS_HOME, "Mock home failed")
            self._fail_next_operation = False
            return
        self._command_log.append("home")

    def capture_screenshot(self, output_path: Optional[Path] = None) -> bytes:
        """Mock screenshot - returns dummy PNG."""
        if self._fail_next_operation:
            self._handle_error(OperationType.SCREENSHOT, "Mock screenshot failed")
            self._fail_next_operation = False
            return b"\x89PNG\r\n\x1a\n" + b"\x00" * 100
        self._command_log.append("screenshot")
        # Return minimal PNG header
        return b"\x89PNG\r\n\x1a\n" + b"\x00" * 100

    def get_screen_size(self) -> ScreenSize:
        """Return mock screen size."""
        return self._screen_size

    def is_connected(self) -> bool:
        """Return mock connection status."""
        return self._connected

    def reconnect(self) -> bool:
        """Mock reconnect - sets connected to True."""
        self._connected = True
        self._command_log.append("reconnect")
        return True

    def stop_app(self, app_name: str) -> bool:
        """Mock stop app."""
        self._command_log.append(f"stop_app {app_name}")
        return True

    def start_app(self, app_name: str) -> bool:
        """Mock start app."""
        self._command_log.append(f"start_app {app_name}")
        return True

    @property
    def command_log(self) -> list[str]:
        """Get list of executed commands."""
        return self._command_log

    def set_connected(self, connected: bool) -> None:
        """Set connection status."""
        self._connected = connected

    def add_mock_screenshot(self, data: bytes) -> None:
        """Add a mock screenshot to return."""
        self._screenshots.append(data)

    def fail_next_operation(self) -> None:
        """Make the next operation fail (for testing error callbacks)."""
        self._fail_next_operation = True
