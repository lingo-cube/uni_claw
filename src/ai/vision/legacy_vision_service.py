"""Legacy vision service wrapper.

This module provides a wrapper around the existing vision service
for backward compatibility with the new two-step pipeline architecture.
"""

import logging
from typing import Optional, Dict, Any

from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


class LegacyVisionService:
    """Wrapper for legacy (existing) vision service.

    This class wraps the existing one-step vision analysis service
    to maintain backward compatibility while enabling the new
    two-step pipeline architecture.
    """

    def __init__(self, existing_service=None):
        """Initialize the legacy vision service wrapper.

        Args:
            existing_service: An existing vision service instance to wrap
                           If None, will import and use ClaudeVisionService
        """
        if existing_service is None:
            # Import existing service
            try:
                from src.vision.vision_service import ClaudeVisionService
                self._service = ClaudeVisionService()
            except (ImportError, RuntimeError) as e:
                logger.warning(f"Could not import ClaudeVisionService: {e}. Legacy service will be a stub.")
                # Create a stub service for testing when anthropic is not available
                self._service = _StubLegacyService()
        else:
            self._service = existing_service

        logger.info("LegacyVisionService initialized")

    def analyze_screenshot(
        self,
        image_data: bytes,
        context: Optional[Dict[str, Any]] = None,
    ) -> PageAnalysis:
        """Analyze a screenshot using the legacy one-step approach.

        This method wraps the existing vision service's analyze method
        to maintain the same interface as the new FlattenedVisionService.

        Args:
            image_data: PNG format screenshot data
            context: Optional context (not used by legacy service)

        Returns:
            PageAnalysis from the legacy service

        Raises:
            ValueError: If image_data is invalid
            RuntimeError: If analysis fails
        """
        if not image_data:
            raise ValueError("image_data cannot be empty")

        try:
            # Call the existing service's analyze method
            # The existing service might have a different signature,
            # so we adapt as needed
            if hasattr(self._service, 'analyze'):
                result = self._service.analyze(image_data)
            elif hasattr(self._service, 'analyze_screenshot'):
                result = self._service.analyze_screenshot(image_data)
            else:
                raise RuntimeError(
                    "Wrapped service does not have analyze() or analyze_screenshot() method"
                )

            return result

        except Exception as e:
            logger.error(f"Legacy vision analysis failed: {e}")
            raise RuntimeError(f"Failed to analyze screenshot with legacy service: {e}") from e

    def __getattr__(self, name):
        """Proxy any other attributes to the wrapped service.

        This allows the wrapper to be transparent and pass through
        any other methods or properties to the underlying service.
        """
        return getattr(self._service, name)


class _StubLegacyService:
    """Stub legacy service for testing when dependencies are unavailable.

    This stub is used when the actual ClaudeVisionService cannot be imported
    (e.g., when anthropic package is not installed). It provides minimal
    functionality for testing purposes.
    """

    def __init__(self):
        """Initialize stub service."""
        logger.info("_StubLegacyService initialized for testing")

    def analyze(self, image_data: bytes):
        """Return a minimal PageAnalysis for testing.

        Args:
            image_data: PNG format screenshot data

        Returns:
            Minimal PageAnalysis object
        """
        from src.state.content_tree import PageAnalysis
        from src.models.core import MenuItem, MenuItemType, Coordinate, Direction

        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuItem(name="Stub", coordinate=Coordinate(x=0.1, y=0.1), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Stub"],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )
