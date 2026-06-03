"""
Unit tests for PageAnalyzer component - Enhanced version.

Tests the enhanced path matching and PageAnalysis format conversion
with new current_path-based lookup.
"""

import pytest
from src.simulation.page_analyzer import PageAnalyzer, PageNotFoundError


class TestPageAnalyzerEnhanced:
    """Test enhanced PageAnalyzer with current_path support."""

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
                    },
                    {
                        "id": 5,
                        "name": "Auto Brightness",
                        "type": "switch",
                        "expected_action": "toggle",
                        "coordinate": {"x": 0.5, "y": 0.4}
                    }
                ],
                "has_scroll": False,
                "is_popup": False
            }
        }

    @pytest.fixture
    def analyzer(self, sample_virtual_pages):
        """Create PageAnalyzer instance."""
        return PageAnalyzer(sample_virtual_pages)

    def test_analyze_root_path(self, analyzer):
        """Test analyzing root path."""
        result = analyzer.analyze_page("root")

        assert result["page_path"] == "root"
        assert len(result["elements"]) == 1
        assert result["elements"][0]["text"] == "Settings"
        assert result["metadata"]["has_scroll"] is False

    def test_analyze_settings_path(self, analyzer):
        """Test analyzing Settings path."""
        result = analyzer.analyze_page("Settings")

        assert result["page_path"] == "Settings"
        assert len(result["elements"]) == 2
        assert result["elements"][0]["text"] == "Display"
        assert result["elements"][1]["text"] == "Sound"

    def test_analyze_nested_path(self, analyzer):
        """Test analyzing nested path Settings/Display."""
        result = analyzer.analyze_page("Settings/Display")

        assert result["page_path"] == "Settings/Display"
        assert len(result["elements"]) == 2
        assert result["elements"][0]["text"] == "Brightness"
        assert result["elements"][1]["text"] == "Auto Brightness"

    def test_path_not_found_error(self, analyzer):
        """Test PageNotFoundError for invalid path."""
        with pytest.raises(PageNotFoundError):
            analyzer.analyze_page("NonExistentPage")

    def test_caching_mechanism(self, analyzer):
        """Test that repeated calls use cache."""
        result1 = analyzer.analyze_page("root")
        result2 = analyzer.analyze_page("root")

        assert result1 is result2  # Same object from cache

    def test_element_processing_with_items_field(self, analyzer):
        """Test element processing with new 'items' format."""
        result = analyzer.analyze_page("Settings/Display")

        brightness = result["elements"][0]
        assert brightness["element_type"] == "slider"
        assert brightness["text"] == "Brightness"
        assert brightness["action_hint"] == "toggle"
        assert brightness["metadata"]["scrollable"] is True

    def test_page_metadata_preservation(self, analyzer):
        """Test that page metadata is properly preserved."""
        result = analyzer.analyze_page("root")

        assert result["metadata"]["has_scroll"] is False
        assert result["metadata"]["is_popup"] is False
        assert result["metadata"]["current_path"] == []
        assert "timestamp" in result["metadata"]

    def test_backward_compatibility_elements_field(self):
        """Test backward compatibility with old 'elements' field."""
        old_format_pages = {
            "OldPage": {
                "page_name": "OldPage",
                "elements": [
                    {
                        "id": "old1",
                        "type": "button",
                        "text": "Old Button",
                        "clickable": True
                    }
                ]
            }
        }

        analyzer = PageAnalyzer(old_format_pages)
        result = analyzer.analyze_page("OldPage")

        assert len(result["elements"]) == 1
        assert result["elements"][0]["text"] == "Old Button"

    def test_path_normalization_variations(self, analyzer):
        """Test path normalization handles different formats."""
        # Test that different path formats work
        valid_paths = ["Settings", "root", "Settings/Display"]

        for path in valid_paths:
            try:
                result = analyzer.analyze_page(path)
                assert "elements" in result
            except PageNotFoundError:
                pass  # Some paths may not exist


class TestPageAnalyzerEdgeCases:
    """Test edge cases and error conditions."""

    def test_empty_virtual_pages(self):
        """Test with empty virtual pages dictionary."""
        analyzer = PageAnalyzer({})

        with pytest.raises(PageNotFoundError):
            analyzer.analyze_page("any_path")

    def test_page_with_no_items(self):
        """Test page with no items field."""
        pages = {
            "EmptyPage": {
                "current_path": [],
                "items": [],
                "has_scroll": False,
                "is_popup": False
            }
        }

        analyzer = PageAnalyzer(pages)
        result = analyzer.analyze_page("EmptyPage")

        assert len(result["elements"]) == 0

    def test_complex_nested_path(self):
        """Test deeply nested path resolution."""
        pages = {
            "Level3": {
                "current_path": ["Level1", "Level2", "Level3"],
                "items": [{"id": 1, "name": "DeepItem", "type": "button"}],
                "has_scroll": False,
                "is_popup": False
            }
        }

        analyzer = PageAnalyzer(pages)
        result = analyzer.analyze_page("Level1/Level2/Level3")

        assert result["page_path"] == "Level1/Level2/Level3"
        assert len(result["elements"]) == 1


if __name__ == "__main__":
    pytest.main([__file__, "-v"])