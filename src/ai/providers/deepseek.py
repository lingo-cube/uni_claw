"""DeepSeek AI provider implementation.

This provider implements text completion using DeepSeek's API.
DeepSeek specializes in text processing and does not support vision/multimodal modes.
"""

import logging
import time
from typing import Dict, Optional, List
import aiohttp

from .base import AIProvider, AIResponse, AIProviderConfig

logger = logging.getLogger(__name__)


class DeepSeekProvider(AIProvider):
    """DeepSeek API provider - focused on text processing.

    This provider supports only text mode and uses DeepSeek's V4 Flash model
    for fast, efficient text completion.
    """

    @property
    def provider_id(self) -> str:
        """Provider identifier."""
        return "deepseek"

    @property
    def supported_modes(self) -> List[str]:
        """Supported modes - text only."""
        return ["text"]

    async def complete_text(
        self,
        prompt: str,
        schema: Optional[Dict] = None,
        max_tokens: int = 2048,
        **kwargs
    ) -> AIResponse:
        """Complete a text prompt using DeepSeek API.

        Args:
            prompt: Text prompt to complete
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters (temperature, top_p, etc.)

        Returns:
            AIResponse: The completion response

        Raises:
            RuntimeError: If API call fails
            ValueError: If parameters are invalid
        """
        start_time = time.time()

        # Build request payload
        payload = {
            "model": self.config.model,
            "messages": [{"role": "user", "content": prompt}],
            "max_tokens": max_tokens,
        }

        # Add optional parameters
        if "temperature" in kwargs:
            payload["temperature"] = kwargs["temperature"]
        if "top_p" in kwargs:
            payload["top_p"] = kwargs["top_p"]
        if "reasoning_level" in kwargs:
            # DeepSeek-specific parameter
            payload["reasoning_level"] = kwargs["reasoning_level"]

        # Add JSON mode if schema provided
        if schema:
            payload["response_format"] = {"type": "json_object"}

        headers = {
            "Authorization": f"Bearer {self.config.api_key}",
            "Content-Type": "application/json",
        }

        try:
            timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)
            async with self._execute_with_semaphore(
                aiohttp.ClientSession(timeout=timeout).__aenter__()
            ) as session:
                async with session.post(
                    f"{self.config.base_url}/chat/completions",
                    headers=headers,
                    json=payload,
                ) as response:
                    if response.status >= 400:
                        error_text = await response.text()
                        raise RuntimeError(
                            f"DeepSeek API error {response.status}: {error_text}"
                        )

                    data = await response.json()

                    if "error" in data:
                        raise RuntimeError(f"DeepSeek API error: {data['error']}")

                    message = data["choices"][0]["message"]
                    content = message["content"]

                    usage = data.get("usage", {})
                    input_tokens = usage.get("prompt_tokens", 0)
                    output_tokens = usage.get("completion_tokens", 0)

                    latency_ms = (time.time() - start_time) * 1000

                    logger.info(
                        f"[DeepSeek] Success. Tokens: {input_tokens} in, {output_tokens} out, "
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

        except aiohttp.ClientError as e:
            logger.error(f"[DeepSeek] Network error: {e}")
            return AIResponse(
                content="",
                provider_id=self.provider_id,
                mode="text",
                input_tokens=0,
                output_tokens=0,
                latency_ms=(time.time() - start_time) * 1000,
                model=self.config.model,
                success=False,
                error_message=str(e),
            )
        except Exception as e:
            logger.error(f"[DeepSeek] Request failed: {e}")
            raise RuntimeError(f"DeepSeek request failed: {e}") from e

    async def complete_vision(
        self,
        prompt: str,
        image_data: bytes,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """DeepSeek does not support vision mode.

        Args:
            prompt: Text prompt
            image_data: Image data
            schema: Optional schema
            max_tokens: Maximum tokens
            **kwargs: Additional parameters

        Raises:
            NotImplementedError: Always raised
        """
        self._check_mode_supported("vision")
        # This line should never be reached due to the check above
        raise NotImplementedError(f"{self.provider_id} does not support vision mode")

    async def complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        additional_context: Optional[Dict] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """DeepSeek does not support multimodal mode.

        Args:
            prompt: Text prompt
            image_data: Image data
            additional_context: Additional context
            schema: Optional schema
            max_tokens: Maximum tokens
            **kwargs: Additional parameters

        Raises:
            NotImplementedError: Always raised
        """
        self._check_mode_supported("multimodal")
        # This line should never be reached due to the check above
        raise NotImplementedError(f"{self.provider_id} does not support multimodal mode")

    def get_token_estimate(self, mode: str, avg_request_tokens: int = 500) -> Dict[str, int]:
        """Estimate token usage for DeepSeek.

        DeepSeek is generally efficient with tokens, output is typically
        50% of input length.

        Args:
            mode: Call mode (only "text" supported)
            avg_request_tokens: Average request tokens

        Returns:
            Dict: Token estimates
        """
        if mode != "text":
            self._check_mode_supported(mode)

        # DeepSeek efficiency: output ~50% of input
        return {
            "input": avg_request_tokens,
            "output": avg_request_tokens // 2,
            "total": avg_request_tokens + avg_request_tokens // 2,
        }

    def get_performance_rating(self, mode: str) -> Dict[str, float]:
        """Get performance rating for DeepSeek.

        DeepSeek is rated as:
        - High latency (fast responses)
        - Medium quality
        - High token efficiency

        Args:
            mode: Call mode

        Returns:
            Dict: Performance ratings
        """
        if mode != "text":
            return {"latency": 0.0, "quality": 0.0, "efficiency": 0.0}

        return {
            "latency": 0.8,  # Fast
            "quality": 0.7,  # Good quality
            "efficiency": 0.9,  # Very efficient
        }
