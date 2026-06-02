"""Multimodal LLM client for vision + text capabilities.

This client supports the Anthropic messages API format (used by MiMo-CC)
for both multimodal vision analysis and text-based logical assembly.
"""

import base64
import logging
from typing import Dict, List, Optional, Union

import aiohttp

from .config import AIProviderConfig

logger = logging.getLogger(__name__)


class MultimodalClient:
    """Multimodal AI client supporting both vision and text.

    Uses Anthropic messages API format (compatible with MiMo-CC).
    Supports image input for vision analysis and text-only for assembly.
    """

    def __init__(self, config: AIProviderConfig):
        """Initialize the multimodal client.

        Args:
            config: AI provider configuration
        """
        self.config = config
        self._session: Optional[aiohttp.ClientSession] = None

    async def _get_session(self) -> aiohttp.ClientSession:
        """Get or create the HTTP session."""
        if self._session is None or self._session.closed:
            timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)
            headers = {
                "x-api-key": self.config.api_key,
                "anthropic-version": "2023-06-01",
            }
            self._session = aiohttp.ClientSession(
                timeout=timeout,
                headers=headers,
            )
        return self._session

    async def _close_session(self) -> None:
        """Close the HTTP session."""
        if self._session and not self._session.closed:
            await self._session.close()

    def _encode_image_base64(self, image_data: bytes) -> str:
        """Encode image bytes to base64 data URL.

        Args:
            image_data: PNG image bytes

        Returns:
            Base64 encoded data URL
        """
        return base64.b64encode(image_data).decode('utf-8')

    async def complete(
        self,
        prompt: str,
        image_data: Optional[bytes] = None,
        model: Optional[str] = None,
        response_format: Optional[Dict] = None,
        max_tokens: int = 4096,
    ) -> 'MockResponse':
        """Complete a prompt with optional image input.

        Args:
            prompt: Text prompt
            image_data: Optional PNG image bytes for multimodal
            model: Model override (uses config.model if None)
            response_format: Optional response format spec
            max_tokens: Maximum tokens in response

        Returns:
            MockResponse with content and usage info

        Raises:
            RuntimeError: On API errors
        """
        session = await self._get_session()
        model = model or self.config.model

        # Build content
        content = [{"type": "text", "text": prompt}]

        # Add image if provided
        if image_data:
            image_base64 = self._encode_image_base64(image_data)
            content.append({
                "type": "image",
                "source": {
                    "type": "base64",
                    "media_type": "image/png",
                    "data": image_base64,
                },
            })

        # Build message
        message = {"role": "user", "content": content}

        # Build payload
        payload = {
            "model": model,
            "max_tokens": max_tokens,
            "messages": [message],
        }

        # Add response format if requested (for structured output)
        if response_format:
            # Convert {"type": "json_object"} to appropriate format
            if response_format.get("type") == "json_object":
                payload["tools"] = [{
                    "type": "text_editor_20241022",
                    "name": "json_tool",
                    "description": "Output JSON",
                    "input_schema": response_format.get("json_schema", {})
                }]
                # In Anthropic API, we use tools for structured output
                # For simple JSON, we can just ask for it in the prompt

        try:
            async with session.post(
                f"{self.config.base_url}/v1/messages",
                json=payload,
            ) as response:
                if response.status >= 400:
                    error_text = await response.text()
                    logger.error(f"API error {response.status}: {error_text}")
                    raise RuntimeError(f"API error {response.status}: {error_text}")

                data = await response.json()

                # Extract response
                if "error" in data:
                    raise RuntimeError(f"API returned error: {data['error']}")

                # Get content from response
                content_block = data.get("content", [{}])[0]
                response_text = content_block.get("text", "")

                # Get usage info
                usage = data.get("usage", {})
                input_tokens = usage.get("input_tokens", 0)
                output_tokens = usage.get("output_tokens", 0)

                return MockResponse(
                    content=response_text,
                    input_tokens=input_tokens,
                    output_tokens=output_tokens,
                )

        except aiohttp.ClientError as e:
            logger.error(f"HTTP client error: {e}")
            raise RuntimeError(f"HTTP client error: {e}")
        except Exception as e:
            logger.error(f"Unexpected error: {e}")
            raise

    async def __aenter__(self):
        """Async context manager entry."""
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit."""
        await self._close_session()


class MockResponse:
    """Mock response object mimicking Anthropic API response."""

    def __init__(self, content: str, input_tokens: int = 0, output_tokens: int = 0):
        """Initialize mock response.

        Args:
            content: Response text content
            input_tokens: Input tokens consumed
            output_tokens: Output tokens generated
        """
        self.content = content
        self.usage = MockUsage(input_tokens, output_tokens)


class MockUsage:
    """Mock usage object."""

    def __init__(self, input_tokens: int, output_tokens: int):
        """Initialize mock usage.

        Args:
            input_tokens: Input tokens consumed
            output_tokens: Output tokens generated
        """
        self.input_tokens = input_tokens
        self.output_tokens = output_tokens


__all__ = ["MultimodalClient", "MockResponse", "MockUsage"]
