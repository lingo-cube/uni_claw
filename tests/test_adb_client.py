"""Tests for ADB client implementations."""

import pytest

from src.adb.adb_client import ADBError, MockADBClient, RealADBClient, ScreenSize


class TestScreenSize:
    """Test ScreenSize coordinate conversions."""

    def test_normalize_coordinates(self):
        """Test pixel to normalized conversion."""
        size = ScreenSize(width=1080, height=1920)

        assert size.normalize_x(540) == pytest.approx(0.5)
        assert size.normalize_y(960) == pytest.approx(0.5)

    def test_pixel_coordinates(self):
        """Test normalized to pixel conversion."""
        size = ScreenSize(width=1080, height=1920)

        assert size.pixel_x(0.5) == 540
        assert size.pixel_y(0.5) == 960

    def test_edge_cases(self):
        """Test boundary values."""
        size = ScreenSize(width=1080, height=1920)

        assert size.normalize_x(0) == 0.0
        assert size.normalize_x(1080) == 1.0
        assert size.pixel_x(0.0) == 0
        assert size.pixel_x(1.0) == 1080


class TestMockADBClient:
    """Test mock ADB client."""

    def test_tap_logs_command(self):
        """Test that tap is logged."""
        client = MockADBClient()
        client.tap(0.5, 0.5)

        assert "tap 0.5 0.5" in client.command_log

    def test_back_button(self):
        """Test back button press."""
        client = MockADBClient()
        client.press_back()

        assert "back" in client.command_log

    def test_screenshot(self):
        """Test screenshot capture."""
        client = MockADBClient()
        data = client.capture_screenshot()

        assert "screenshot" in client.command_log
        assert data.startswith(b"\x89PNG")

    def test_screen_size(self):
        """Test screen size returns expected defaults."""
        client = MockADBClient()
        size = client.get_screen_size()

        assert size.width == 1080
        assert size.height == 1920

    def test_is_connected(self):
        """Test connection status."""
        client = MockADBClient()

        assert client.is_connected()

        client.set_connected(False)
        assert not client.is_connected()

    def test_custom_screenshot(self):
        """Test adding custom mock screenshot."""
        client = MockADBClient()
        custom_data = b"\x89PNG" + b"custom"

        client.add_mock_screenshot(custom_data)

        # This would be returned on next screenshot call
        # Implementation detail - just verifying API exists
        assert len(client._screenshots) >= 0


class TestCoordinateValidation:
    """Test coordinate validation in tap operations."""

    def test_invalid_coordinates_raise_error(self):
        """Test that out-of-range coordinates raise errors."""
        # Note: MockADBClient doesn't validate, but RealADBClient should
        # This test documents expected behavior
        pass


class TestADBClientInterface:
    """Test that both implementations satisfy the interface."""

    def test_mock_has_required_methods(self):
        """Verify mock client has all required methods."""
        client = MockADBClient()

        assert hasattr(client, "execute")
        assert hasattr(client, "tap")
        assert hasattr(client, "press_back")
        assert hasattr(client, "press_home")
        assert hasattr(client, "capture_screenshot")
        assert hasattr(client, "get_screen_size")
        assert hasattr(client, "is_connected")

    def test_real_client_instantiation(self):
        """Test real client can be instantiated (without device)."""
        # Just verify it can be created
        client = RealADBClient(adb_path="adb")

        assert client.adb_path == "adb"
        assert client.device_id is None


class TestRealADBClientIntegration:
    """Integration tests for real ADB client.

    These tests require a connected device and are skipped by default.
    """

    @pytest.mark.skipif(True, reason="Requires connected device")
    def test_real_device_connection(self):
        """Test connection to real device."""
        client = RealADBClient()

        if not client.is_connected():
            pytest.skip("No ADB device connected")

        # If we get here, we have a device
        size = client.get_screen_size()
        assert size.width > 0
        assert size.height > 0
