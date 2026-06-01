"""Vision analysis capability."""

import logging
from typing import Dict

from ..core.capability import BaseCapability
from ..core.config import AIProviderConfig
from ..core.llm_client import LLMClient
from ..core.validator import ResponseValidator
from ..core.prompts import PromptRegistry
from ..vision.service import VisionService
from ...state.content_tree import PageAnalysis

logger = logging.getLogger(__name__)


class VisionAnalysisCapability(BaseCapability[bytes, PageAnalysis]):
    """Capability to analyze screenshots using Vision Service.

    Unlike other capabilities that use LLM, this capability uses a dedicated
    Vision Service (Claude, MiMo, or Mock) to analyze screenshots.
    """

    def __init__(
        self,
        vision_service: VisionService,
        validator: ResponseValidator,
    ):
        """Initialize the capability.

        Args:
            vision_service: Vision service for screenshot analysis
            validator: Response validator for parsing
        """
        # Note: This capability doesn't use LLM client, but we pass None to satisfy base class
        # We override execute_async to use vision service instead
        super().__init__(None, validator, AIProviderConfig(api_key=""), None)
        self.vision_service = vision_service

    @property
    def system_prompt_key(self) -> str:
        return "vision_analysis.system"

    @property
    def user_prompt_key(self) -> str:
        return "vision_analysis.user"

    @property
    def response_schema(self) -> Dict:
        # Vision service handles its own schema validation
        return {
            "type": "object",
            "properties": {},
        }

    @property
    def response_type(self) -> str:
        return "PageAnalysis"

    def prepare_input(self, raw_input: bytes) -> Dict:
        """Prepare input - just pass through image data.

        Args:
            raw_input: PNG image bytes

        Returns:
            Empty dict (not used for vision)
        """
        return {}

    async def execute_async(self, input_data: bytes) -> PageAnalysis:
        """Execute screenshot analysis using Vision Service.

        Args:
            input_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements

        Raises:
            VisionError: On analysis failure
        """
        try:
            logger.info(f"Calling Vision Analysis")
            import time
            start_time = time.time()

            result = self.vision_service.analyze_screenshot(input_data)

            duration = time.time() - start_time
            logger.info(f"Vision analysis received in {duration:.2f}s")

            return result

        except Exception as e:
            logger.error(f"Vision analysis failed: {e}")
            self._archive_failure(input_data, e)
            raise


__all__ = ["VisionAnalysisCapability"]
