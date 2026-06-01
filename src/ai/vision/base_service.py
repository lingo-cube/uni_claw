"""Base vision service with shared utilities."""

import base64
import json
import logging
import re
from typing import Dict, Optional

from .service import VisionService

logger = logging.getLogger(__name__)


class VisionError(Exception):
    """Exception raised by vision services."""

    pass


class BaseVisionService(VisionService):
    """Base class with shared utility functions for vision services.

    Provides:
    - Base64 image encoding
    - JSON extraction from API responses
    - Default implementations for abstract methods
    """

    @staticmethod
    def _encode_image_base64(image_data: bytes) -> str:
        """Encode image bytes to base64 string.

        Args:
            image_data: PNG image bytes

        Returns:
            Base64 encoded string with MIME type prefix
        """
        return f"data:image/png;base64,{base64.b64encode(image_data).decode('utf-8')}"

    @staticmethod
    def _extract_json(response_text: str) -> Dict:
        """Extract JSON from API response text.

        Handles both pure JSON responses and JSON wrapped in markdown code blocks.

        Args:
            response_text: Response text from the vision API

        Returns:
            Parsed JSON dict

        Raises:
            VisionError: If JSON cannot be extracted or parsed
        """
        # Try parsing as-is first
        try:
            return json.loads(response_text)
        except json.JSONDecodeError:
            pass

        # Try extracting from markdown code block
        json_match = re.search(r'```json\s*(.*?)\s*```', response_text, re.DOTALL)
        if json_match:
            try:
                return json.loads(json_match.group(1))
            except json.JSONDecodeError as e:
                raise VisionError(f"Failed to parse JSON from code block: {e}")

        # Try extracting from any code block
        code_match = re.search(r'```\s*(.*?)\s*```', response_text, re.DOTALL)
        if code_match:
            try:
                return json.loads(code_match.group(1))
            except json.JSONDecodeError as e:
                raise VisionError(f"Failed to parse JSON from code block: {e}")

        raise VisionError("Could not extract JSON from response")

    def analyze_screenshot(self, image_data: bytes) -> Dict:
        """Default implementation - raises NotImplementedError.

        Subclasses must implement this method.
        """
        raise NotImplementedError("Subclasses must implement analyze_screenshot")

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[Dict]:
        """Default implementation - raises NotImplementedError.

        Subclasses must implement this method.
        """
        raise NotImplementedError("Subclasses must implement find_app_entry")


__all__ = ["BaseVisionService", "VisionError"]
