"""Unit tests for exception classes."""

import pytest
from src.exception.exceptions import (
    ADBDisconnectedException,
    AIAnalysisFailedException,
    AIResponseInvalidException,
    AppCrashException,
    ClickFailedException,
    CoordinateExpiredException,
    DeviceOfflineException,
    ElementNotFoundException,
    ExceptionSeverity,
    InputFailedException,
    LoadingTimeoutException,
    OperationException,
    PageRedirectException,
    PathMismatchException,
    PopupDetectedException,
    TraversalException,
    UIException,
)


class TestTraversalException:
    """Tests for base TraversalException."""

    def test_base_exception_properties(self):
        """Test base exception has message and severity."""
        exc = TraversalException("Test error")
        assert exc.message == "Test error"
        assert exc.severity == ExceptionSeverity.ERROR  # Default

    def test_base_exception_with_severity_override(self):
        """Test severity override in constructor."""
        exc = TraversalException("Test error", severity=ExceptionSeverity.CRITICAL)
        assert exc.severity == ExceptionSeverity.CRITICAL

    def test_base_exception_with_cause(self):
        """Test exception chaining."""
        original = ValueError("Original error")
        exc = TraversalException("Wrapped error", cause=original)
        assert exc.__cause__ is original

    def test_str_representation(self):
        """Test string representation includes severity."""
        exc = TraversalException("Test error")
        assert "[ERROR]" in str(exc)
        assert "Test error" in str(exc)


class TestLocationExceptions:
    """Tests for location-related exceptions."""

    def test_element_not_found_default_severity(self):
        """Test ElementNotFoundException has ERROR severity."""
        exc = ElementNotFoundException("SubmitButton", "LoginPage")
        assert exc.severity == ExceptionSeverity.ERROR
        assert "SubmitButton" in exc.message

    def test_path_mismatch_default_severity(self):
        """Test PathMismatchException has WARNING severity."""
        exc = PathMismatchException(
            expected=["Home", "Settings"],
            actual=["Home", "Profile"]
        )
        assert exc.severity == ExceptionSeverity.WARNING

    def test_coordinate_expired_default_severity(self):
        """Test CoordinateExpiredException has ERROR severity."""
        exc = CoordinateExpiredException("(0.5, 0.5)", "UI changed")
        assert exc.severity == ExceptionSeverity.ERROR


class TestOperationExceptions:
    """Tests for operation-related exceptions."""

    def test_click_failed_default_severity(self):
        """Test ClickFailedException has ERROR severity."""
        exc = ClickFailedException("(0.5, 0.5)", 3)
        assert exc.severity == ExceptionSeverity.ERROR
        assert "3 attempts" in exc.message

    def test_input_failed_default_severity(self):
        """Test InputFailedException has ERROR severity."""
        exc = InputFailedException("PasswordField", "secret123")
        assert exc.severity == ExceptionSeverity.ERROR


class TestDeviceExceptions:
    """Tests for device-related exceptions."""

    def test_adb_disconnected_default_severity(self):
        """Test ADBDisconnectedException has CRITICAL severity."""
        exc = ADBDisconnectedException()
        assert exc.severity == ExceptionSeverity.CRITICAL

    def test_app_crash_default_severity(self):
        """Test AppCrashException has CRITICAL severity."""
        exc = AppCrashException("com.example.app", "Null pointer exception")
        assert exc.severity == ExceptionSeverity.CRITICAL

    def test_device_offline_default_severity(self):
        """Test DeviceOfflineException has FATAL severity."""
        exc = DeviceOfflineException("device123")
        assert exc.severity == ExceptionSeverity.FATAL


class TestUIExceptions:
    """Tests for UI-related exceptions."""

    def test_popup_detected_default_severity(self):
        """Test PopupDetectedException has INFO severity."""
        exc = PopupDetectedException("AdPopup")
        assert exc.severity == ExceptionSeverity.INFO

    def test_page_redirect_default_severity(self):
        """Test PageRedirectException has INFO severity."""
        exc = PageRedirectException("Home -> Login")
        assert exc.severity == ExceptionSeverity.INFO

    def test_loading_timeout_default_severity(self):
        """Test LoadingTimeoutException has WARNING severity."""
        exc = LoadingTimeoutException(30.0)
        assert exc.severity == ExceptionSeverity.WARNING


class TestAIExceptions:
    """Tests for AI-related exceptions."""

    def test_ai_analysis_failed_default_severity(self):
        """Test AIAnalysisFailedException has ERROR severity."""
        exc = AIAnalysisFailedException("Claude", "Rate limit exceeded")
        assert exc.severity == ExceptionSeverity.ERROR

    def test_ai_response_invalid_default_severity(self):
        """Test AIResponseInvalidException has WARNING severity."""
        exc = AIResponseInvalidException("Invalid JSON", "json")
        assert exc.severity == ExceptionSeverity.WARNING


class TestExceptionHierarchy:
    """Tests for exception inheritance."""

    def test_location_exception_hierarchy(self):
        """Test LocationException is TraversalException."""
        assert issubclass(ElementNotFoundException, TraversalException)

    def test_operation_exception_hierarchy(self):
        """Test OperationException is TraversalException."""
        assert issubclass(ClickFailedException, TraversalException)

    def test_device_exception_hierarchy(self):
        """Test DeviceException is TraversalException."""
        assert issubclass(ADBDisconnectedException, TraversalException)

    def test_ui_exception_hierarchy(self):
        """Test UIException is TraversalException."""
        assert issubclass(PopupDetectedException, TraversalException)

    def test_ai_exception_hierarchy(self):
        """Test AIException is TraversalException."""
        assert issubclass(AIAnalysisFailedException, TraversalException)
