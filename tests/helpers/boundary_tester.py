"""
Boundary tester for edge case testing.

Provides methods to test empty elements, excessive depth, massive elements,
unicode edge cases, and extreme coordinates.
"""

from typing import Any, Dict, List
from tests.helpers.chaos_engine import ChaosEngine


class BoundaryTester:
    """Tester for boundary conditions and edge cases.

    Used to verify system behavior at limits and with unusual inputs.
    """

    @staticmethod
    def test_empty_elements() -> Dict[str, Any]:
        """Test handling of empty element lists.

        Returns:
            Page with empty elements list
        """
        return {
            "page_name": "EmptyPage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [],
        }

    @staticmethod
    def test_excessive_depth(depth: int = 100) -> List[Dict[str, Any]]:
        """Test handling of deep nesting.

        Args:
            depth: Number of nesting levels

        Returns:
            List of pages representing deep hierarchy
        """
        pages = {}
        path = []

        for i in range(depth):
            path.append(f"level{i}")
            path_str = "/".join(path)
            pages[path_str] = {
                "page_name": f"Level{i}Page",
                "level1_dir": "RIGHT",
                "level2_dir": "BOTTOM",
                "elements": [
                    {
                        "id": f"next_{i}",
                        "type": "button",
                        "text": f"Level {i+1}",
                        "coordinate": {"x": 0.5, "y": 0.5},
                    }
                ],
            }

        return pages

    @staticmethod
    def test_massive_elements(count: int = 1000) -> Dict[str, Any]:
        """Test handling of large element counts.

        Args:
            count: Number of elements to generate

        Returns:
            Page with many elements
        """
        elements = []
        for i in range(count):
            elements.append({
                "id": f"item_{i}",
                "type": "menu_item",
                "text": f"Item {i}",
                "coordinate": {"x": 0.5, "y": (i % 10) * 0.1},
            })

        return {
            "page_name": "MassivePage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "has_scroll": True,
            "elements": elements,
        }

    @staticmethod
    def test_unicode_edge_cases() -> Dict[str, Any]:
        """Test handling of various unicode edge cases.

        Returns:
            Page with unicode edge case elements
        """
        return {
            "page_name": "UnicodePage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [
                {
                    "id": "emoji",
                    "type": "button",
                    "text": "🏠 Home 🚀",
                    "coordinate": {"x": 0.2, "y": 0.3},
                },
                {
                    "id": "zero_width",
                    "type": "button",
                    "text": "Test​Zero​Width",
                    "coordinate": {"x": 0.4, "y": 0.3},
                },
                {
                    "id": "combining",
                    "type": "button",
                    "text": "Combinińg Marks",
                    "coordinate": {"x": 0.6, "y": 0.3},
                },
                {
                    "id": "rtl",
                    "type": "button",
                    "text": "مرحبا",  # Arabic (RTL)
                    "coordinate": {"x": 0.8, "y": 0.3},
                },
                {
                    "id": "surrogate",
                    "type": "button",
                    "text": "😀",  # Surrogate pair
                    "coordinate": {"x": 0.5, "y": 0.5},
                },
            ],
        }

    @staticmethod
    def test_extreme_coordinates() -> Dict[str, Any]:
        """Test handling of boundary coordinate values.

        Returns:
            Page with extreme coordinate values
        """
        return {
            "page_name": "ExtremeCoordsPage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [
                {
                    "id": "zero_origin",
                    "type": "button",
                    "text": "Zero",
                    "coordinate": {"x": 0.0, "y": 0.0},
                },
                {
                    "id": "max_corner",
                    "type": "button",
                    "text": "Max",
                    "coordinate": {"x": 1.0, "y": 1.0},
                },
                {
                    "id": "tiny_positive",
                    "type": "button",
                    "text": "Tiny",
                    "coordinate": {"x": 0.001, "y": 0.001},
                },
                {
                    "id": "near_max",
                    "type": "button",
                    "text": "NearMax",
                    "coordinate": {"x": 0.999, "y": 0.999},
                },
                {
                    "id": "center",
                    "type": "button",
                    "text": "Center",
                    "coordinate": {"x": 0.5, "y": 0.5},
                },
            ],
        }

    @staticmethod
    def test_single_element() -> Dict[str, Any]:
        """Test handling of page with single element.

        Returns:
            Page with exactly one element
        """
        return {
            "page_name": "SingleElementPage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [
                {
                    "id": "only_element",
                    "type": "button",
                    "text": "Only One",
                    "coordinate": {"x": 0.5, "y": 0.5},
                }
            ],
        }

    @staticmethod
    def test_long_text() -> Dict[str, Any]:
        """Test handling of very long text content.

        Returns:
            Page with long text elements
        """
        long_text = "This is a very long text that exceeds typical element text length " * 10

        return {
            "page_name": "LongTextPage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [
                {
                    "id": "long_text_element",
                    "type": "menu_item",
                    "text": long_text[:500],  # Truncate for practicality
                    "coordinate": {"x": 0.5, "y": 0.3},
                }
            ],
        }

    @staticmethod
    def test_special_characters() -> Dict[str, Any]:
        """Test handling of special characters.

        Returns:
            Page with special character elements
        """
        return {
            "page_name": "SpecialCharsPage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [
                {
                    "id": "ampersand",
                    "type": "button",
                    "text": "Test & Demo",
                    "coordinate": {"x": 0.2, "y": 0.3},
                },
                {
                    "id": "quotes",
                    "type": "button",
                    "text": "Quoted and single",
                    "coordinate": {"x": 0.5, "y": 0.3},
                },
                {
                    "id": "brackets",
                    "type": "button",
                    "text": "<tag> & [bracket]",
                    "coordinate": {"x": 0.8, "y": 0.3},
                },
            ],
        }

    @staticmethod
    def generate_boundary_test_suite() -> Dict[str, Dict[str, Any]]:
        """Generate complete boundary test suite.

        Returns:
            Dictionary of all boundary test scenarios
        """
        return {
            "empty": BoundaryTester.test_empty_elements(),
            "single": BoundaryTester.test_single_element(),
            "massive": BoundaryTester.test_massive_elements(100),
            "unicode": BoundaryTester.test_unicode_edge_cases(),
            "extreme_coords": BoundaryTester.test_extreme_coordinates(),
            "long_text": BoundaryTester.test_long_text(),
            "special_chars": BoundaryTester.test_special_characters(),
        }
