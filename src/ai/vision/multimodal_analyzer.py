"""Multimodal analyzer for screenshot analysis.

This module defines the interface and implementation for analyzing screenshots
using multimodal AI models to produce flattened screen representations.
"""

import json
import logging
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional

from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.type_hint import TypeHint
from src.models.vision.selection_state import SelectionState


logger = logging.getLogger(__name__)


@dataclass
class MultimodalAnalysisResult:
    """Result from multimodal screenshot analysis.

    Attributes:
        flattened_screen: The analyzed screen representation
        latency_ms: Analysis latency in milliseconds
        input_tokens: Input tokens consumed
        output_tokens: Output tokens consumed
        cached: Whether the result came from cache
        model: Model used for analysis
    """

    flattened_screen: FlattenedScreen
    latency_ms: float
    input_tokens: int
    output_tokens: int
    cached: bool = False
    model: str = ""


class MultimodalAnalyzer(ABC):
    """Abstract base class for multimodal screenshot analyzers.

    Analyzes screenshots using multimodal AI models to produce
    flattened screen representations with visual elements.
    """

    @abstractmethod
    def analyze(self, image_data: bytes) -> MultimodalAnalysisResult:
        """Analyze a screenshot and return flattened screen representation.

        Args:
            image_data: PNG format screenshot data

        Returns:
            MultimodalAnalysisResult containing the screen analysis and metrics

        Raises:
            ValueError: If image_data is invalid
            RuntimeError: If AI analysis fails
        """
        pass


class ClaudeMultimodalAnalyzer(MultimodalAnalyzer):
    """Claude-based multimodal analyzer implementation.

    Uses Claude Sonnet 3.5 for visual analysis of screenshots.
    """

    def __init__(
        self,
        ai_provider,
        model: str = "claude-3-5-sonnet-20241022",
        prompt: Optional[str] = None,
    ):
        """Initialize the Claude multimodal analyzer.

        Args:
            ai_provider: AI provider for making API calls
            model: Model identifier (default: claude-3-5-sonnet-20241022)
            prompt: Optional custom prompt (uses default if not provided)
        """
        self.ai_provider = ai_provider
        self.model = model
        self._prompt = prompt or self._load_default_prompt()

    def _load_default_prompt(self) -> str:
        """Load the default multimodal analysis prompt."""
        from src.ai.vision.prompts import MULTIMODAL_ANALYSIS_PROMPT
        return MULTIMODAL_ANALYSIS_PROMPT

    def analyze(self, image_data: bytes) -> MultimodalAnalysisResult:
        """Analyze a screenshot and return flattened screen representation.

        Args:
            image_data: PNG format screenshot data

        Returns:
            MultimodalAnalysisResult containing the screen analysis and metrics

        Raises:
            ValueError: If image_data is invalid
            RuntimeError: If AI analysis fails
        """
        if not image_data:
            raise ValueError("image_data cannot be empty")

        start_time = time.time()

        try:
            # Call AI provider for analysis
            response = self._call_ai_model(image_data)

            latency_ms = (time.time() - start_time) * 1000

            # Parse response into FlattenedScreen
            flattened_screen = self._parse_response(response)

            # Extract token usage if available
            input_tokens = getattr(response.usage, 'input_tokens', 0) if hasattr(response, 'usage') else 0
            output_tokens = getattr(response.usage, 'output_tokens', 0) if hasattr(response, 'usage') else 0

            return MultimodalAnalysisResult(
                flattened_screen=flattened_screen,
                latency_ms=latency_ms,
                input_tokens=input_tokens,
                output_tokens=output_tokens,
                cached=False,
                model=self.model,
            )

        except Exception as e:
            logger.error(f"Multimodal analysis failed: {e}")
            raise RuntimeError(f"Failed to analyze screenshot: {e}") from e

    def _call_ai_model(self, image_data: bytes):
        """Call the AI model with the image and prompt.

        Args:
            image_data: PNG format screenshot data

        Returns:
            AI model response
        """
        # Try to use the AI provider's complete method with image support
        if hasattr(self.ai_provider, 'complete'):
            return self.ai_provider.complete(
                prompt=self._prompt,
                image_data=image_data,
                model=self.model,
                response_format={"type": "json_object"},
            )
        else:
            raise RuntimeError(
                "AI provider does not support image analysis. "
                "Please use a provider with multimodal capabilities."
            )

    def _parse_response(self, response) -> FlattenedScreen:
        """Parse AI response into FlattenedScreen.

        Args:
            response: AI model response

        Returns:
            FlattenedScreen instance

        Raises:
            ValueError: If response cannot be parsed
        """
        try:
            # Extract content from response
            content = response.content if hasattr(response, 'content') else response
            data = json.loads(content)

        except (json.JSONDecodeError, TypeError) as e:
            raise ValueError(f"Failed to parse AI response as JSON: {e}") from e

        # Validate structure
        if not isinstance(data, dict):
            raise ValueError("Response must be a JSON object")

        if 'elements' not in data:
            raise ValueError("Response must contain 'elements' array")

        # Parse elements
        elements = []
        for elem_data in data.get('elements', []):
            try:
                element = self._parse_element(elem_data)
                elements.append(element)
            except Exception as e:
                logger.warning(f"Failed to parse element {elem_data.get('id', '?')}: {e}")
                continue

        # Create FlattenedScreen
        return FlattenedScreen(
            elements=elements,
            screen_hints=data.get('screen_hints', {}),
        )

    def _parse_element(self, elem_data: dict) -> FlattenedElement:
        """Parse a single element from AI response.

        Args:
            elem_data: Element data from AI response

        Returns:
            FlattenedElement instance

        Raises:
            ValueError: If required fields are missing or invalid
        """
        # Validate required fields
        if 'bbox' not in elem_data or not elem_data['bbox']:
            raise ValueError("Element missing required 'bbox' field")

        # Parse bounding box
        bbox_data = elem_data['bbox']
        bbox = BoundingBox(
            x=bbox_data.get('x', 0.0),
            y=bbox_data.get('y', 0.0),
            w=bbox_data.get('w', 0.001),
            h=bbox_data.get('h', 0.001),
        )

        return FlattenedElement(
            id=elem_data.get('id', 0),
            text=elem_data.get('text', ''),
            type_hint=TypeHint.from_string(elem_data.get('type_hint', 'text')),
            bbox=bbox,
            region=elem_data.get('region'),
            selection_state=SelectionState.from_string(
                elem_data.get('selection_state', 'normal')
            ),
            visual_state=elem_data.get('visual_state', {}),
            confidence=elem_data.get('confidence', 1.0),
        )
