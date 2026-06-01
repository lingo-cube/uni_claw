"""Exception injection test framework.

This module provides utilities for injecting exceptions during traversal
to test exception handling behavior.
"""

import pytest
from typing import Callable, Optional
from unittest.mock import Mock, patch
from src.exception import TraversalException, ExceptionSeverity
from src.adb.adb_client import MockADBClient


class ExceptionInjector:
    """Helper class for injecting exceptions during traversal."""

    def __init__(self):
        """Initialize exception injector."""
        self.injected_exceptions = {}
        self.injection_count = 0

    def inject_on_operation(
        self,
        operation: str,
        exception: TraversalException,
        call_count: int = 1,
    ):
        """Configure exception to be raised on specific operation.

        Args:
            operation: Operation name (e.g., "tap_and_wait", "analyze_screenshot")
            exception: Exception to raise
            call_count: Raise exception on this call number (1 = first call)
        """
        key = f"{operation}_{call_count}"
        self.injected_exceptions[key] = exception

    def should_inject(self, operation: str, call_count: int) -> Optional[TraversalException]:
        """Check if exception should be injected for this operation/call.

        Args:
            operation: Operation name
            call_count: Current call number

        Returns:
            Exception to inject, or None
        """
        key = f"{operation}_{call_count}"
        return self.injected_exceptions.get(key)

    def clear(self):
        """Clear all configured injections."""
        self.injected_exceptions.clear()
        self.injection_count = 0


class MockADBWithExceptionInjection(MockADBClient):
    """Mock ADB client that can inject exceptions."""

    def __init__(self, injector: ExceptionInjector):
        """Initialize with exception injector.

        Args:
            injector: ExceptionInjector instance
        """
        super().__init__()
        self.injector = injector
        self.operation_counts = {}

    def tap(self, x: float, y: float) -> None:
        """Tap with potential exception injection."""
        op_name = "tap"
        self.operation_counts[op_name] = self.operation_counts.get(op_name, 0) + 1
        call_count = self.operation_counts[op_name]

        exc = self.injector.should_inject(op_name, call_count)
        if exc:
            raise exc

        super().tap(x, y)

    def press_back(self) -> None:
        """Press back with potential exception injection."""
        op_name = "press_back"
        self.operation_counts[op_name] = self.operation_counts.get(op_name, 0) + 1
        call_count = self.operation_counts[op_name]

        exc = self.injector.should_inject(op_name, call_count)
        if exc:
            raise exc

        super().press_back()

    def capture_screenshot(self, output_path=None):
        """Capture screenshot with potential exception injection."""
        op_name = "capture_screenshot"
        self.operation_counts[op_name] = self.operation_counts.get(op_name, 0) + 1
        call_count = self.operation_counts[op_name]

        exc = self.injector.should_inject(op_name, call_count)
        if exc:
            raise exc

        return super().capture_screenshot(output_path)


class ExceptionInjectionTest:
    """Base test class with exception injection utilities."""

    @pytest.fixture
    def exception_injector(self):
        """Provide ExceptionInjector instance."""
        injector = ExceptionInjector()
        yield injector
        injector.clear()

    @pytest.fixture
    def mock_adb_with_injection(self, exception_injector):
        """Provide MockADBClient with exception injection."""
        return MockADBWithExceptionInjection(exception_injector)

    def assert_operation_count(self, adb: MockADBWithExceptionInjection, operation: str, expected: int):
        """Assert operation was called expected number of times.

        Args:
            adb: Mock ADB client with injection
            operation: Operation name to check
            expected: Expected call count
        """
        actual = adb.operation_counts.get(operation, 0)
        assert actual == expected, f"{operation} called {actual} times, expected {expected}"


class TestExceptionInjection(ExceptionInjectionTest):
    """Tests for exception injection framework."""

    def test_inject_on_first_call(self, exception_injector, mock_adb_with_injection):
        """Test injecting exception on first operation call."""
        from src.exception.exceptions import ADBDisconnectedException

        exception_injector.inject_on_operation(
            "tap",
            ADBDisconnectedException(),
            call_count=1
        )

        with pytest.raises(ADBDisconnectedException):
            mock_adb_with_injection.tap(0.5, 0.5)

    def test_inject_on_third_call(self, exception_injector, mock_adb_with_injection):
        """Test injecting exception on third operation call."""
        from src.exception.exceptions import ElementNotFoundException

        exception_injector.inject_on_operation(
            "tap",
            ElementNotFoundException("Button"),
            call_count=3
        )

        # First two calls succeed
        mock_adb_with_injection.tap(0.5, 0.5)
        mock_adb_with_injection.tap(0.5, 0.5)

        # Third call raises exception
        with pytest.raises(ElementNotFoundException):
            mock_adb_with_injection.tap(0.5, 0.5)

    def test_no_injection_configured(self, exception_injector, mock_adb_with_injection):
        """Test operations succeed when no injection configured."""
        mock_adb_with_injection.tap(0.5, 0.5)
        mock_adb_with_injection.press_back()

        self.assert_operation_count(mock_adb_with_injection, "tap", 1)
        self.assert_operation_count(mock_adb_with_injection, "press_back", 1)

    def test_multiple_injections(self, exception_injector, mock_adb_with_injection):
        """Test multiple different exception injections."""
        from src.exception.exceptions import (
            ADBDisconnectedException,
            AppCrashException,
        )

        exception_injector.inject_on_operation("tap", ADBDisconnectedException(), call_count=2)
        exception_injector.inject_on_operation("press_back", AppCrashException("app"), call_count=1)

        # First tap succeeds
        mock_adb_with_injection.tap(0.5, 0.5)

        # First press_back raises
        with pytest.raises(AppCrashException):
            mock_adb_with_injection.press_back()

        # Second tap raises
        with pytest.raises(ADBDisconnectedException):
            mock_adb_with_injection.tap(0.5, 0.5)


# Common exception scenarios for testing


class CommonExceptionScenarios:
    """Predefined common exception scenarios for testing."""

    @staticmethod
    def popup_on_second_action() -> tuple:
        """Scenario: Popup appears on second tap action.

        Returns:
            Tuple of (injector_config, expected_behavior)
        """
        from src.exception.exceptions import PopupDetectedException

        def setup(injector):
            injector.inject_on_operation("tap", PopupDetectedException("Ad"), call_count=2)

        return setup

    @staticmethod
    def device_disconnects_on_screenshot() -> tuple:
        """Scenario: Device disconnects during screenshot.

        Returns:
            Tuple of (injector_config, expected_behavior)
        """
        from src.exception.exceptions import ADBDisconnectedException

        def setup(injector):
            injector.inject_on_operation("capture_screenshot", ADBDisconnectedException(), call_count=1)

        return setup

    @staticmethod
    def element_not_found_retry_scenario() -> tuple:
        """Scenario: Element not found, should retry.

        Returns:
            Tuple of (injector_config, expected_behavior)
        """
        from src.exception.exceptions import ElementNotFoundException

        def setup(injector):
            # Fail on first attempt, succeed on second
            injector.inject_on_operation("tap", ElementNotFoundException("Button"), call_count=1)

        return setup
