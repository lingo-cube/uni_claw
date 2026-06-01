"""Vision service abstract interface."""

from abc import ABC, abstractmethod
from typing import Dict, Optional

from ...state.content_tree import PageAnalysis


class VisionService(ABC):
    """Abstract base class for vision analysis services.

    This class defines the interface for services that analyze
    screenshots to extract page structure and element information.
    """

    @abstractmethod
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze a screenshot and extract page structure.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements and structure
        """
        pass

    @abstractmethod
    def find_app_entry(self, image_data: bytes, target: str) -> Optional[Dict]:
        """Find an app icon on the home screen.

        Args:
            image_data: PNG image bytes
            target: App name to search for

        Returns:
            Dict with found=true, name, x, y, confidence if found,
            or found=false, coordinates=null if not found
        """
        pass


__all__ = ["VisionService"]
