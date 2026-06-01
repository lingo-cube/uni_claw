"""MiMo vision service implementation using Anthropic SDK (Claude protocol).

This is the MiMo CC version that uses the /anthropic endpoint with Claude protocol,
mimicking a real Claude client for compatibility with MIMO's Anthropic-compatible API.
"""

import logging
import os
from typing import Optional

from anthropic import Anthropic

from ..state.content_tree import PageAnalysis
from .base_vision import BaseVisionService
from .vision_service import VisionError

logger = logging.getLogger(__name__)


class MiMoCCVisionService(BaseVisionService):
    """Vision service using XiaoMi MiMo API via Anthropic SDK (Claude protocol).

    This version uses MIMO's /anthropic endpoint with Anthropic SDK,
    making it appear as a Claude client request.

    Usage:
        vision = MiMoCCVisionService(api_key="your_key")

        or set environment variable:
        export MIMO_API_KEY=your_key

        vision = MiMoCCVisionService()
    """

    def __init__(
        self,
        api_key: Optional[str] = None,
        model: str = "mimo-v2.5",
        base_url: str = "https://token-plan-cn.xiaomimimo.com/anthropic",
    ):
        """Initialize MiMo CC vision service.

        Args:
            api_key: MiMo API key (defaults to MIMO_API_KEY env var)
            model: Model name (default: mimo-v2.5)
            base_url: API base URL (Anthropic protocol endpoint)
        """
        # Initialize parent class for trace logging
        super().__init__()

        self.api_key = api_key or os.environ.get("MIMO_API_KEY")
        if not self.api_key:
            raise ValueError(
                "MiMo API key required. Set MIMO_API_KEY environment variable "
                "or pass api_key parameter."
            )

        self.model = model
        self.client = Anthropic(
            api_key=self.api_key,
            base_url=base_url,
            default_headers={
                "anthropic-version": "2023-06-01",
                "accept": "application/json",
            },
        )

    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """Make vision API call to MiMo using Anthropic (Claude) protocol.

        Args:
            prompt: Text prompt for the image
            image_data: Image bytes

        Returns:
            AI response text
        """
        base64_image = self._encode_image_base64(image_data)

        # Retry logic for handling empty responses
        max_retries = 3
        last_error = None

        for attempt in range(max_retries):
            try:
                response = self.client.messages.create(
                    model=self.model,
                    max_tokens=8192,
                    system="You are a UI analysis assistant. Respond only with valid JSON.",
                    messages=[
                        {
                            "role": "user",
                            "content": [
                                {
                                    "type": "image",
                                    "source": {
                                        "type": "base64",
                                        "media_type": "image/png",
                                        "data": base64_image,
                                    },
                                },
                                {
                                    "type": "text",
                                    "text": prompt,
                                },
                            ],
                        },
                    ],
                )

                # Extract text from response content
                # Handle different content block types (TextBlock, ThinkingBlock, etc.)
                logger.debug(f"Response content blocks: {len(response.content)} blocks")

                # Look for TextBlock in the content (skip thinking blocks)
                for block in response.content:
                    if hasattr(block, 'text'):
                        text = block.text
                        # Skip empty text from thinking blocks
                        if text and text.strip():
                            logger.debug(f"MiMo CC response: {text[:200]}...")
                            return text

                # If no TextBlock found, log details and retry
                logger.warning(f"Attempt {attempt + 1}/{max_retries}: MiMo CC returned no text content")
                logger.warning(f"Stop reason: {response.stop_reason}")

                if attempt < max_retries - 1:
                    import time
                    wait_time = (attempt + 1) * 2  # Exponential backoff: 2s, 4s, 6s
                    logger.info(f"Retrying in {wait_time}s...")
                    time.sleep(wait_time)
                    continue
                else:
                    # Final attempt failed, raise error
                    raise VisionError("No text content in MiMo CC response")

            except Exception as e:
                last_error = e
                if "No text content" not in str(e):  # Don't retry on VisionError from our code
                    raise
                if attempt < max_retries - 1:
                    import time
                    wait_time = (attempt + 1) * 2
                    logger.info(f"API error, retrying in {wait_time}s...: {e}")
                    time.sleep(wait_time)
                    continue
                else:
                    raise VisionError(f"Vision API call failed after {max_retries} attempts: {e}") from e


class MiMoCCVisionServiceFactory:
    """Factory for creating MiMo CC vision service with configuration."""

    @staticmethod
    def from_settings(settings) -> MiMoCCVisionService:
        """Create MiMo CC service from settings object.

        Args:
            settings: Settings object with mimo_api_key, mimo_model attributes

        Returns:
            Configured MiMoCCVisionService
        """
        return MiMoCCVisionService(
            api_key=getattr(settings, "mimo_api_key", None),
            model=getattr(settings, "mimo_cc_model", "mimo-v2.5"),
            base_url=getattr(settings, "mimo_cc_base_url", "https://token-plan-cn.xiaomimimo.com/anthropic"),
        )
