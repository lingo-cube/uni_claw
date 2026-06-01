"""MiMo Vision Service using MCP (Model Context Protocol)."""

import logging
from typing import Optional

from ..state.content_tree import PageAnalysis
from .vision_service import VisionError, PROMPT_STRUCTURE, PROMPT_FIND_ENTRY

logger = logging.getLogger(__name__)


class MiMoMCPVisionService(VisionService):
    """Vision service using MiMo MCP server.

    This uses the Model Context Protocol integration for MiMo vision analysis.
    """

    def __init__(self, api_key: Optional[str] = None):
        """Initialize MiMo MCP vision service.

        Args:
            api_key: Optional API key (MCP may handle this internally)
        """
        self.api_key = api_key
        logger.info("Using MiMo MCP vision service")

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot using MiMo MCP.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements
        """
        import json

        # Convert bytes to base64 for MCP
        import base64
        import tempfile

        # Save to temp file for MCP
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
            f.write(image_data)
            temp_path = f.name

        try:
            # Call MCP tool - this would be invoked via the MCP integration
            # For now, return a mock response
            logger.warning("MCP vision integration pending - using fallback")

            # Fallback: try to parse as if we got a response
            return self._parse_mimo_response("")

        except Exception as e:
            logger.error(f"MiMo MCP analysis failed: {e}")
            raise VisionError(f"Vision analysis failed: {e}") from e
        finally:
            import os
            if os.path.exists(temp_path):
                os.unlink(temp_path)

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Find target app icon on home screen.

        Args:
            image_data: PNG image bytes
            target: App name to search for

        Returns:
            Dict with x, y, name if found, None otherwise
        """
        # Similar implementation using MCP
        logger.info(f"Looking for app: {target}")
        # Placeholder - would use MCP call
        return None

    def _parse_mimo_response(self, response_text: str) -> PageAnalysis:
        """Parse MiMo MCP response into PageAnalysis.

        Args:
            response_text: Raw response text

        Returns:
            PageAnalysis object
        """
        import json

        try:
            data = json.loads(response_text) if response_text else {}
            return PageAnalysis(**data)
        except Exception:
            # Return empty analysis if parsing fails
            return PageAnalysis(
                level1_dir="left",
                level1_menus=[],
                level2_dir="top",
                level2_menus=[],
                current_path=[],
                items=[],
            )
