"""Base vision service with common functionality."""

import base64
import json
import logging
from abc import ABC, abstractmethod
from typing import Optional

from ..state.content_tree import PageAnalysis
from .vision_service import PROMPT_STRUCTURE, PROMPT_FIND_ENTRY, VisionError

logger = logging.getLogger(__name__)


class BaseVisionService(ABC):
    """Base class for vision services with common utilities."""

    def __init__(self):
        """Initialize base vision service with trace logging."""
        self._trace = None
        try:
            from ..utils.trace import TraceLogger
            self._trace = TraceLogger("vision")
        except ImportError:
            logger.debug("Trace logging not available for vision service")

    @abstractmethod
    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """Make vision API call - must be implemented by subclass.

        Args:
            prompt: Text prompt for the image
            image_data: Image bytes

        Returns:
            AI response text
        """
        pass

    def _encode_image(self, image_data: bytes, mime_type: str = "image/png") -> str:
        """Encode image bytes to base64 data URL.

        Args:
            image_data: Raw image bytes
            mime_type: MIME type (default: image/png)

        Returns:
            Base64 data URL string
        """
        base64_data = base64.b64encode(image_data).decode("utf-8")
        return f"data:{mime_type};base64,{base64_data}"

    def _encode_image_base64(self, image_data: bytes) -> str:
        """Encode image bytes to base64 string (for Anthropic format).

        Args:
            image_data: Raw image bytes

        Returns:
            Base64 string (without data URL prefix)
        """
        return base64.b64encode(image_data).decode("utf-8")

    def _extract_json(self, content: str) -> str:
        """Extract JSON from AI response, handling markdown code blocks.

        Args:
            content: Raw AI response text

        Returns:
            Extracted JSON string
        """
        # Extract JSON if embedded in markdown
        if "```json" in content:
            return content.split("```json")[1].split("```")[0].strip()
        elif "```" in content:
            return content.split("```")[1].split("```")[0].strip()
        return content

    def _normalize_page_data(self, data: dict) -> dict:
        """Normalize and fix common AI response issues.

        Args:
            data: Raw parsed JSON from AI

        Returns:
            Normalized data dict with defaults for missing/invalid values
        """
        # Handle combined direction values (e.g., 'top|bottom', 'left|right')
        # and invalid values (e.g., 'none', empty string)
        valid_directions = {'left', 'right', 'top', 'bottom'}

        def normalize_direction(dir_value: str, dir_field: str) -> str:
            """Normalize a direction field to a valid value.

            Args:
                dir_value: The raw direction value from AI
                dir_field: Field name ('level1_dir' or 'level2_dir')

            Returns:
                Valid direction string
            """
            # Default values based on field
            default = 'left' if dir_field == 'level1_dir' else 'bottom'

            # Handle None or empty values
            if not dir_value:
                logger.debug(f"Empty {dir_field}, defaulting to '{default}'")
                return default

            # Convert to string and lowercase for comparison
            dir_str = str(dir_value).lower().strip()

            # Handle 'none' or similar non-direction values
            if dir_str in ('none', 'null', 'n/a', 'undefined', '-'):
                logger.debug(f"Invalid {dir_field} '{dir_value}', defaulting to '{default}'")
                return default

            # Handle pipe-separated values (e.g., 'top|bottom', 'left|right')
            if '|' in dir_str:
                parts = [p.strip() for p in dir_str.split('|')]
                # Find first valid direction in the list
                for part in parts:
                    if part in valid_directions:
                        logger.debug(f"Normalized {dir_field} from '{dir_value}' to '{part}'")
                        return part
                # No valid direction found, use default
                logger.debug(f"No valid direction in '{dir_value}', defaulting {dir_field} to '{default}'")
                return default

            # Handle single values - check if valid
            if dir_str in valid_directions:
                return dir_str

            # Invalid single value, use default
            logger.debug(f"Invalid {dir_field} value '{dir_value}', defaulting to '{default}'")
            return default

        # Normalize both direction fields
        for dir_field in ['level1_dir', 'level2_dir']:
            original_value = data.get(dir_field, '')
            normalized_value = normalize_direction(original_value, dir_field)
            data[dir_field] = normalized_value

        # Ensure lists are present
        data.setdefault('level1_menus', [])
        data.setdefault('level2_menus', [])
        data.setdefault('items', [])
        data.setdefault('current_path', [])

        return data

    def _parse_page_analysis(self, response: str) -> PageAnalysis:
        """Parse JSON response into PageAnalysis.

        Args:
            response: JSON string from AI

        Returns:
            PageAnalysis object

        Raises:
            VisionError: If JSON parsing fails
        """
        try:
            data = json.loads(response)
            # Normalize data to handle empty strings and missing fields
            normalized = self._normalize_page_data(data)
            return PageAnalysis(**normalized)
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse AI response: {response}")
            raise VisionError(f"Invalid JSON from AI: {e}") from e

    def _parse_find_entry(self, response: str) -> dict | None:
        """Parse find_entry response.

        Args:
            response: JSON string from AI

        Returns:
            Dict with x, y, name if found, None otherwise

        Raises:
            VisionError: If JSON parsing fails
        """
        try:
            data = json.loads(response)
            logger.info(f"Find entry response: {data}")
            if data.get("found"):
                return {"x": data["x"], "y": data["y"], "name": data["name"]}
            logger.warning(f"App not found in response: found={data.get('found')}")
            return None
        except (json.JSONDecodeError, KeyError) as e:
            logger.error(f"Failed to parse find_entry response: {response}")
            raise VisionError(f"Invalid response from AI: {e}") from e

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot using vision API.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements
        """
        import time
        start_time = time.time()
        trace_context = None

        try:
            # Start trace span
            if self._trace:
                trace_context = self._trace.start_span(
                    operation="analyze_screenshot",
                    tags={
                        "image_size": len(image_data),
                        "prompt": "PROMPT_STRUCTURE"
                    }
                )
                self._trace.log_input(trace_context,
                    image_size=len(image_data),
                    image_hash=hash(image_data) % 10000
                )

            logger.info(f"[VISION] Analyzing screenshot ({len(image_data)} bytes)")

            response = self._call_vision(PROMPT_STRUCTURE, image_data)
            content = self._extract_json(response)
            logger.debug(f"AI response: {content[:200]}...")

            result = self._parse_page_analysis(content)

            duration = time.time() - start_time
            logger.info(f"[VISION] Analysis complete in {duration:.2f}s - "
                       f"{len(result.items)} items, path={result.current_path}")

            # Log trace output
            if self._trace and trace_context:
                self._trace.log_output(trace_context,
                    items_count=len(result.items),
                    current_path=result.current_path,
                    is_popup=result.is_popup,
                    has_scroll=result.has_scroll
                )
                self._trace.finish_span(trace_context, result=result)

            return result

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[VISION] Analysis failed after {duration:.2f}s: {e}")

            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)

            raise

    def find_app_entry(self, image_data: bytes, target: str) -> dict | None:
        """Find target app icon on home screen.

        Args:
            image_data: PNG image bytes
            target: App name to search for

        Returns:
            Dict with x, y, name if found, None otherwise
        """
        prompt = PROMPT_FIND_ENTRY.format(target=target)
        response = self._call_vision(prompt, image_data)
        content = self._extract_json(response)
        return self._parse_find_entry(content)
