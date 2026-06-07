"""
Chaos engine for randomization and fault injection.

Provides methods to randomize page order, inject delays, corrupt page data,
and duplicate elements for chaos testing scenarios.
"""

import random
import time
from typing import Any, Dict, List, Optional


class ChaosEngine:
    """Engine for introducing controlled chaos into test scenarios.

    Used for testing system resilience under various failure conditions
    and unexpected input variations.
    """

    @staticmethod
    def randomize_page_order(elements: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Randomize the order of page elements.

        Args:
            elements: List of element dictionaries

        Returns:
            Elements in randomized order
        """
        shuffled = elements.copy()
        random.shuffle(shuffled)
        return shuffled

    @staticmethod
    def inject_delay(delay_ms: int, variance_ms: int = 0) -> float:
        """Inject a delay with optional variance.

        Args:
            delay_ms: Base delay in milliseconds
            variance_ms: Optional variance range (+/-)

        Returns:
            Actual delay time in seconds
        """
        if variance_ms > 0:
            actual_delay = delay_ms + random.randint(-variance_ms, variance_ms)
        else:
            actual_delay = delay_ms

        delay_seconds = max(0, actual_delay / 1000.0)
        time.sleep(delay_seconds)

        return delay_seconds

    @staticmethod
    def corrupt_page_data(corruption_type: str) -> Dict[str, Any]:
        """Return page data with specific corruption type.

        Args:
            corruption_type: Type of corruption ('missing_field', 'null_value',
                             'wrong_type', 'empty_string')

        Returns:
            Corrupted page data dictionary
        """
        base_page = {
            "page_name": "CorruptedPage",
            "level1_dir": "RIGHT",
            "level2_dir": "BOTTOM",
            "elements": [
                {
                    "id": "element1",
                    "type": "button",
                    "text": "Button",
                    "coordinate": {"x": 0.5, "y": 0.5},
                }
            ],
        }

        if corruption_type == "missing_field":
            # Remove essential field
            corrupted = base_page.copy()
            if "elements" in corrupted:
                corrupted["elements"][0].pop("text", None)
            return corrupted

        elif corruption_type == "null_value":
            # Set field to null
            corrupted = base_page.copy()
            if "elements" in corrupted and corrupted["elements"]:
                corrupted["elements"][0]["text"] = None
            return corrupted

        elif corruption_type == "wrong_type":
            # Set field to wrong type
            corrupted = base_page.copy()
            if "elements" in corrupted and corrupted["elements"]:
                corrupted["elements"][0]["coordinate"] = "not_a_dict"
            return corrupted

        elif corruption_type == "empty_string":
            # Set field to empty string
            corrupted = base_page.copy()
            if "elements" in corrupted and corrupted["elements"]:
                corrupted["elements"][0]["text"] = ""
            return corrupted

        return base_page

    @staticmethod
    def duplicate_elements(page: Dict[str, Any], count: int = 2) -> Dict[str, Any]:
        """Duplicate some elements in the page.

        Args:
            page: Page data dictionary
            count: Number of duplicates to create per element

        Returns:
            Page with duplicated elements
        """
        result = page.copy()
        elements = result.get("elements", []).copy()

        duplicated = []
        for element in elements:
            duplicated.append(element)
            # Create duplicates
            for i in range(count - 1):
                dup = element.copy()
                dup["id"] = f"{element.get('id', 'elem')}_dup{i}"
                duplicated.append(dup)

        result["elements"] = duplicated
        return result

    @staticmethod
    def scramble_coordinates(elements: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Randomize coordinate values while keeping valid range.

        Args:
            elements: List of element dictionaries

        Returns:
            Elements with scrambled coordinates
        """
        result = []
        for element in elements:
            elem = element.copy()
            if "coordinate" in elem and isinstance(elem["coordinate"], dict):
                elem["coordinate"] = {
                    "x": round(random.random(), 2),
                    "y": round(random.random(), 2),
                }
            result.append(elem)
        return result

    @staticmethod
    def shuffle_text_cases(elements: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Randomize text case for testing case-insensitive matching.

        Args:
            elements: List of element dictionaries

        Returns:
            Elements with randomized text case
        """
        result = []
        for element in elements:
            elem = element.copy()
            if "text" in elem and isinstance(elem["text"], str):
                text = elem["text"]
                # Randomly capitalize
                elem["text"] = "".join(
                    c.upper() if random.random() > 0.5 else c.lower() for c in text
                )
            result.append(elem)
        return result
