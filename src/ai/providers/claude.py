"""Claude AI provider implementation.

This provider implements text, vision, and multimodal completion using Anthropic's Claude API.
Claude excels at vision tasks and provides high-quality responses across all modes.
"""

import base64
import logging
import time
from typing import Dict, Optional, List
import aiohttp

from .base import AIProvider, AIResponse, AIProviderConfig

logger = logging.getLogger(__name__)


class ClaudeProvider(AIProvider):
    """Claude API provider - excellent multimodal capabilities.

    This provider supports text, vision, and multimodal modes using
    Anthropic's Claude 3.5 Sonnet model.
    """

    @property
    def provider_id(self) -> str:
        """Provider identifier."""
        return "claude"

    @property
    def supported_modes(self) -> List[str]:
        """Supported modes - all three modes."""
        return ["text", "vision", "multimodal"]

    async def _make_request(
        self,
        messages: List[Dict],
        max_tokens: int = 4096,
    ) -> Dict:
        """Make a request to Claude API.

        Args:
            messages: Message list for the API
            max_tokens: Maximum output tokens

        Returns:
            Dict: API response data

        Raises:
            RuntimeError: If API call fails
        """
        payload = {
            "model": self.config.model,
            "max_tokens": max_tokens,
            "messages": messages,
        }

        headers = {
            "x-api-key": self.config.api_key,
            "anthropic-version": "2023-06-01",
            "content-type": "application/json",
        }

        timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)

        async def make_request():
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    f"{self.config.base_url}/v1/messages",
                    headers=headers,
                    json=payload,
                ) as response:
                    if response.status >= 400:
                        error_text = await response.text()
                        raise RuntimeError(
                            f"Claude API error {response.status}: {error_text}"
                        )

                    data = await response.json()

                    if "error" in data:
                        raise RuntimeError(f"Claude API error: {data['error']}")

                    return data

        return await self._execute_with_semaphore(make_request())

    async def complete_text(
        self,
        prompt: str,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a text prompt using Claude API.

        Args:
            prompt: Text prompt to complete
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            AIResponse: The completion response
        """
        start_time = time.time()

        messages = [{"role": "user", "content": prompt}]

        try:
            data = await self._make_request(messages, max_tokens)

            content_block = data.get("content", [{}])[0]
            content = content_block.get("text", "")

            usage = data.get("usage", {})
            input_tokens = usage.get("input_tokens", 0)
            output_tokens = usage.get("output_tokens", 0)

            latency_ms = (time.time() - start_time) * 1000

            logger.info(
                f"[Claude] Text Success. Tokens: {input_tokens} in, {output_tokens} out, "
                f"latency: {latency_ms:.0f}ms"
            )

            return AIResponse(
                content=content,
                provider_id=self.provider_id,
                mode="text",
                input_tokens=input_tokens,
                output_tokens=output_tokens,
                latency_ms=latency_ms,
                model=self.config.model,
                success=True,
            )

        except Exception as e:
            logger.error(f"[Claude] Text request failed: {e}")
            raise RuntimeError(f"Claude request failed: {e}") from e

    async def complete_vision(
        self,
        prompt: str,
        image_data: bytes,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a vision prompt using Claude API.

        Args:
            prompt: Text prompt
            image_data: PNG format image data
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            AIResponse: The completion response
        """
        start_time = time.time()

        # Encode image to base64
        image_base64 = base64.b64encode(image_data).decode("utf-8")

        # Build multimodal message
        content = [
            {"type": "text", "text": prompt},
            {
                "type": "image",
                "source": {
                    "type": "base64",
                    "media_type": "image/png",
                    "data": image_base64,
                },
            },
        ]

        messages = [{"role": "user", "content": content}]

        try:
            data = await self._make_request(messages, max_tokens)

            content_block = data.get("content", [{}])[0]
            content = content_block.get("text", "")

            usage = data.get("usage", {})
            input_tokens = usage.get("input_tokens", 0)
            output_tokens = usage.get("output_tokens", 0)

            latency_ms = (time.time() - start_time) * 1000

            logger.info(
                f"[Claude] Vision Success. Tokens: {input_tokens} in, {output_tokens} out, "
                f"latency: {latency_ms:.0f}ms"
            )

            return AIResponse(
                content=content,
                provider_id=self.provider_id,
                mode="vision",
                input_tokens=input_tokens,
                output_tokens=output_tokens,
                latency_ms=latency_ms,
                model=self.config.model,
                success=True,
            )

        except Exception as e:
            logger.error(f"[Claude] Vision request failed: {e}")
            raise RuntimeError(f"Claude vision request failed: {e}") from e

    async def complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        additional_context: Optional[Dict] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a multimodal prompt using Claude API.

        This is similar to vision mode but allows additional context.

        Args:
            prompt: Text prompt
            image_data: PNG format image data
            additional_context: Additional context information
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            AIResponse: The completion response
        """
        start_time = time.time()

        # Encode image to base64
        image_base64 = base64.b64encode(image_data).decode("utf-8")

        # Build multimodal message with context
        content = [
            {"type": "text", "text": prompt},
        ]

        # Add context if provided
        if additional_context:
            import json
            content.append({
                "type": "text",
                "text": f"\nContext: {json.dumps(additional_context, indent=2)}"
            })

        content.append({
            "type": "image",
            "source": {
                "type": "base64",
                "media_type": "image/png",
                "data": image_base64,
            },
        })

        messages = [{"role": "user", "content": content}]

        try:
            data = await self._make_request(messages, max_tokens)

            content_block = data.get("content", [{}])[0]
            content = content_block.get("text", "")

            usage = data.get("usage", {})
            input_tokens = usage.get("input_tokens", 0)
            output_tokens = usage.get("output_tokens", 0)

            latency_ms = (time.time() - start_time) * 1000

            logger.info(
                f"[Claude] Multimodal Success. Tokens: {input_tokens} in, {output_tokens} out, "
                f"latency: {latency_ms:.0f}ms"
            )

            return AIResponse(
                content=content,
                provider_id=self.provider_id,
                mode="multimodal",
                input_tokens=input_tokens,
                output_tokens=output_tokens,
                latency_ms=latency_ms,
                model=self.config.model,
                success=True,
            )

        except Exception as e:
            logger.error(f"[Claude] Multimodal request failed: {e}")
            raise RuntimeError(f"Claude multimodal request failed: {e}") from e

    def get_token_estimate(self, mode: str, avg_request_tokens: int = 500) -> Dict[str, int]:
        """Estimate token usage for Claude.

        Claude's vision tasks consume more input tokens due to image encoding.

        Args:
            mode: Call mode
            avg_request_tokens: Average request tokens

        Returns:
            Dict: Token estimates
        """
        if mode in ("vision", "multimodal"):
            # Vision input requires more tokens
            return {
                "input": avg_request_tokens * 2,
                "output": avg_request_tokens,
                "total": avg_request_tokens * 3,
            }
        return {
            "input": avg_request_tokens,
            "output": avg_request_tokens // 2,
            "total": avg_request_tokens + avg_request_tokens // 2,
        }

    def get_performance_rating(self, mode: str) -> Dict[str, float]:
        """Get performance rating for Claude.

        Claude is rated as:
        - Medium latency
        - Very high quality
        - Medium token efficiency

        Args:
            mode: Call mode

        Returns:
            Dict: Performance ratings
        """
        if mode in ("text", "vision", "multimodal"):
            return {
                "latency": 0.6,  # Medium speed
                "quality": 0.95,  # Excellent quality
                "efficiency": 0.6,  # Medium efficiency
            }
        return {"latency": 0.0, "quality": 0.0, "efficiency": 0.0}
