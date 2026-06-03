"""
Unit tests for MockVisionService component - Enhanced version.

Tests the enhanced path mapping and PageAnalysis integration
with new current_path-based lookup.
"""

import pytest
from src.simulation.mock_vision import MockVisionService


class TestMockVisionServiceEnhanced:
    """Test enhanced MockVisionService with current_path support."""

    @pytest.fixture
    def sample_virtual_pages(self):
        """Sample virtual pages in new PageAnalysis format."""
        return {
            "HomeScreen": {
                "current_path": [],
                "items": [
                    {
                        "id": 1,
                        "name": "Settings",
                        "type": "button",
                        "expected_action": "navigate",
                        "coordinate": {"x": 0.5, "y": 0.5}
                    }
                ],
                "has_scroll": False,
                "is_popup": False
            },
            "SettingsPage": {
                "current_path": ["Settings"],
                "items": [
                    {
                        "id": 2,
                        "name": "Display",
                        "type": "menu_item",
                        "expected_action": "navigate",
                        "coordinate": {"x": 0.2, "y": 0.3}
                    },
                    {
                        "id": 3,
                        "name": "Sound",
                        "type": "menu_item",
                        "expected_action": "navigate",
                        "coordinate": {"x": 0.2, "y": 0.5}
                    }
                ],
                "has_scroll": False,
                "is_popup": False
            },
            "DisplaySettings": {
                "current_path": ["Settings", "Display"],
                "items": [
                    {
                        "id": 4,
                        "name": "Brightness",
                        "type": "slider",
                        "expected_action": "toggle",
                        "coordinate": {"x": 0.5, "y": 0.2}
                    }
                ],
                "has_scroll": False,
                "is_popup": False
            }
        }

    @pytest.fixture
    def vision_service(self, sample_virtual_pages):
        """Create MockVisionService instance."""
        return MockVisionService(sample_virtual_pages)

    def test_initialization(self, sample_virtual_pages):
        """Test MockVisionService initialization."""
        vision = MockVisionService(sample_virtual_pages)

        assert vision.virtual_pages == sample_virtual_pages
        assert vision._call_count == 0
        assert vision._injected_path is None

    def test_analyze_root_path(self, vision_service):
        """Test analyzing root path."""
        result = vision_service.analyze_screenshot()

        assert len(result["elements"]) == 1
        assert result["elements"][0]["text"] == "Settings"
        assert result["elements"][0]["element_type"] == "button"
        assert vision_service._call_count == 1

    def test_path_injection(self, vision_service):
        """Test path injection functionality."""
        vision_service.inject_path("Settings")
        result = vision_service.analyze_screenshot()

        assert len(result["elements"]) == 2
        assert result["elements"][0]["text"] == "Display"
        assert result["elements"][1]["text"] == "Sound"

    def test_nested_path_injection(self, vision_service):
        """Test nested path injection."""
        vision_service.inject_path("Settings/Display")
        result = vision_service.analyze_screenshot()

        assert len(result["elements"]) == 1
        assert result["elements"][0]["text"] == "Brightness"

    def test_call_counting(self, vision_service):
        """Test that analyze calls are counted."""
        assert vision_service._call_count == 0

        vision_service.analyze_screenshot()
        assert vision_service._call_count == 1

        vision_service.analyze_screenshot()
        assert vision_service._call_count == 2

    def test_get_call_count(self, vision_service):
        """Test get_call_count method."""
        assert vision_service.get_call_count() == 0

        vision_service.analyze_screenshot()
        assert vision_service.get_call_count() == 1

    def test_reset_functionality(self, vision_service):
        """Test reset functionality."""
        vision_service.analyze_screenshot()
        vision_service.inject_path("test_path")

        assert vision_service._call_count == 1
        assert vision_service._injected_path == "test_path"

        vision_service.reset()

        assert vision_service._call_count == 0
        assert vision_service._injected_path is None

    def test_page_analysis_structure(self, vision_service):
        """Test that PageAnalysis structure is correct."""
        result = vision_service.analyze_screenshot()

        assert "page_type" in result
        assert "page_path" in result
        assert "elements" in result
        assert "metadata" in result

    def test_coordinate_to_bounds_conversion(self, vision_service):
        """Test that coordinate field is converted to bounds."""
        result = vision_service.analyze_screenshot()

        element = result["elements"][0]
        assert "bounds" in element
        assert element["bounds"] == {"x": 0.5, "y": 0.5}

    def test_expected_action_to_action_hint(self, vision_service):
        """Test that expected_action becomes action_hint."""
        result = vision_service.analyze_screenshot()

        element = result["elements"][0]
        assert element["action_hint"] == "navigate"

    def test_metadata_preservation(self, vision_service):
        """Test page metadata preservation."""
        result = vision_service.analyze_screenshot()

        assert result["metadata"]["has_scroll"] is False
        assert result["metadata"]["is_popup"] is False
        assert result["metadata"]["current_path"] == []


class TestMockVisionServiceEdgeCases:
    """Test edge cases and error conditions."""

    def test_empty_virtual_pages(self):
        """Test with empty virtual pages."""
        vision = MockVisionService({})

        result = vision.analyze_screenshot()
        # Empty pages return empty page analysis with 'items' field
        assert "items" in result
        assert len(result.get("items", [])) == 0

    def test_page_with_no_items(self):
        """Test page with no items."""
        pages = {
            "EmptyPage": {
                "current_path": [],
                "items": [],
                "has_scroll": False,
                "is_popup": False
            }
        }

        vision = MockVisionService(pages)
        result = vision.analyze_screenshot()

        assert len(result["elements"]) == 0


if __name__ == "__main__":
    pytest.main([__file__, "-v"])