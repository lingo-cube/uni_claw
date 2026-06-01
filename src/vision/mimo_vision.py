"""MiMo vision service implementation using OpenAI SDK (v1 endpoint)."""

import logging
import os
from typing import Optional

from openai import OpenAI

from ..state.content_tree import PageAnalysis
from .base_vision import BaseVisionService
from .vision_service import VisionError

logger = logging.getLogger(__name__)

class MiMoVisionService(BaseVisionService):
    """Vision service using XiaoMi MiMo API via OpenAI SDK (v1 endpoint).

    This is the original MiMo service using the v1 OpenAI-compatible endpoint.

    Usage:
        vision = MiMoVisionService(api_key="your_key")

        or set environment variable:
        export MIMO_API_KEY=your_key

        vision = MiMoVisionService()
    """

    def __init__(
        self,
        api_key: Optional[str] = None,
        model: str = "mimo-v2.5",
        base_url: str = "https://api.xiaomimimo.com/v1",
    ):
        """Initialize MiMo vision service.

        Args:
            api_key: MiMo API key (defaults to MIMO_API_KEY env var)
            model: Model name (default: mimo-v2.5)
            base_url: API base URL (v1 endpoint with OpenAI protocol)
        """
        self.api_key = api_key or os.environ.get("MIMO_API_KEY")
        if not self.api_key:
            raise ValueError(
                "MiMo API key required. Set MIMO_API_KEY environment variable "
                "or pass api_key parameter."
            )

        self.model = model
        self.client = OpenAI(
            api_key=self.api_key,
            base_url=base_url,
        )

    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """Make vision API call to MiMo using OpenAI protocol.

        Args:
            prompt: Text prompt for the image
            image_data: Image bytes

        Returns:
            AI response text
        """
        image_url = self._encode_image(image_data)

        try:
            response = self.client.chat.completions.create(
                model=self.model,
                messages=[
                    {
                        "role": "system",
                        "content": "You are a UI analysis assistant. Respond only with valid JSON.",
                    },
                    {
                        "role": "user",
                        "content": [
                            {
                                "type": "image_url",
                                "image_url": {"url": image_url},
                            },
                            {
                                "type": "text",
                                "text": prompt,
                            },
                        ],
                    },
                ],
                max_completion_tokens=4096,
            )

            content = response.choices[0].message.content
            logger.debug(f"MiMo response: {content[:200]}...")

            return content

        except Exception as e:
            logger.error(f"MiMo API call failed: {e}")
            raise VisionError(f"Vision API call failed: {e}") from e


class MiMoVisionServiceFactory:
    """Factory for creating MiMo vision service with configuration."""

    @staticmethod
    def from_settings(settings) -> MiMoVisionService:
        """Create MiMo service from settings object.

        Args:
            settings: Settings object with mimo_api_key, mimo_model attributes

        Returns:
            Configured MiMoVisionService
        """
        return MiMoVisionService(
            api_key=getattr(settings, "mimo_api_key", None),
            model=getattr(settings, "mimo_model", "mimo-v2.5"),
            base_url=getattr(settings, "mimo_base_url", "https://api.xiaomimimo.com/v1"),
        )
