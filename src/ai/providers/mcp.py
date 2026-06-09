"""MCP AI provider implementation.

This provider implements vision analysis using Claude API with a portable,
cross-machine compatible configuration.
"""

import base64
import json
import logging
import time
import os
from typing import Dict, Optional, List
import aiohttp

from .base import AIProvider, AIResponse, AIProviderConfig

logger = logging.getLogger(__name__)


class MCPProvider(AIProvider):
    """MCP API provider - vision analysis using Claude API.

    This provider implements vision analysis similar to ClaudeProvider
    but designed for portability across different machines.

    Configuration:
    - Uses ANTHROPIC_API_KEY environment variable
    - Falls back to config file if env not set
    - Supports proxy configuration for testing
    """

    @property
    def provider_id(self) -> str:
        """Provider identifier."""
        return "mcp"

    @property
    def supported_modes(self) -> List[str]:
        """Supported modes - vision and multimodal only."""
        return ["vision", "multimodal"]

    def _get_api_key(self) -> str:
        """Get API key from environment or config.

        Priority:
        1. ANTHROPIC_AUTH_TOKEN environment variable (proxy token)
        2. ANTHROPIC_API_KEY environment variable
        3. config.api_key from configuration

        Returns:
            str: API key
        """
        # Try proxy auth token first
        env_token = os.environ.get("ANTHROPIC_AUTH_TOKEN")
        if env_token:
            return env_token

        # Try standard API key
        env_key = os.environ.get("ANTHROPIC_API_KEY")
        if env_key:
            return env_key

        # Fall back to config
        return self.config.api_key

    def _get_base_url(self) -> str:
        """Get base URL from environment or config.

        Returns:
            str: Base URL for API
        """
        # Check environment variable
        env_url = os.environ.get("ANTHROPIC_BASE_URL")
        if env_url:
            return env_url

        # Fall back to config, or default to official API
        if self.config.base_url and self.config.base_url != "mcp://local":
            return self.config.base_url

        return "https://api.anthropic.com"

    async def _call_claude_api(
        self,
        messages: List[Dict],
        max_tokens: int = 4096,
    ) -> Dict:
        """Call Claude API for vision analysis.

        Args:
            messages: Message list for the API
            max_tokens: Maximum output tokens

        Returns:
            Dict: API response data
        """
        api_key = self._get_api_key()
        base_url = self._get_base_url()

        payload = {
            "model": self.config.model,
            "max_tokens": max_tokens,
            "messages": messages,
        }

        headers = {
            "x-api-key": api_key,
            "anthropic-version": "2023-06-01",
            "content-type": "application/json",
            # For proxy compatibility, also add Authorization header
            "Authorization": f"Bearer {api_key}",
        }

        timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)

        logger.info(f"Calling Claude API at: {base_url}/v1/messages")

        async def make_request():
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    f"{base_url}/v1/messages",
                    headers=headers,
                    json=payload,
                ) as response:
                    if response.status >= 400:
                        error_text = await response.text()
                        raise RuntimeError(
                            f"API error {response.status}: {error_text}"
                        )

                    data = await response.json()

                    if "error" in data:
                        raise RuntimeError(f"API error: {data['error']}")

                    return data

        return await self._execute_with_semaphore(make_request())

    async def _call_bridge_server(
        self,
        image_data: bytes,
        prompt: str,
    ) -> str:
        """Call MCP bridge server for vision analysis.

        This method calls a local bridge server that runs within Claude Code
        environment and provides HTTP access to MCP tools.

        Args:
            image_data: PNG format image data
            prompt: Analysis prompt

        Returns:
            str: Analysis result in PageAnalysis JSON format
        """
        bridge_url = os.environ.get("MCP_BRIDGE_URL", "http://127.0.0.1:8765")

        # Encode image to base64
        image_base64 = base64.b64encode(image_data).decode("utf-8")

        payload = {
            "image_data": image_base64,
            "prompt": prompt,
        }

        timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)

        logger.info(f"Calling MCP bridge server at: {bridge_url}/mcp/analyze_image")

        async with aiohttp.ClientSession(timeout=timeout) as session:
            async with session.post(
                f"{bridge_url}/mcp/analyze_image",
                json=payload,
            ) as response:
                if response.status >= 400:
                    error_text = await response.text()
                    raise RuntimeError(
                        f"Bridge server error {response.status}: {error_text}"
                    )

                data = await response.json()

                if not data.get("success"):
                    error = data.get("error", "Unknown error")
                    raise RuntimeError(f"Bridge server error: {error}")

                return data.get("content", "")

    async def complete_text(
        self,
        prompt: str,
        schema: Optional[Dict] = None,
        max_tokens: int = 2048,
        **kwargs
    ) -> AIResponse:
        """Complete a text prompt - not supported.

        Args:
            prompt: Text prompt to complete
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            AIResponse: The completion response

        Raises:
            NotImplementedError: MCP tools don't support text-only mode
        """
        self._check_mode_supported("text")

    async def complete_vision(
        self,
        prompt: str,
        image_data: bytes,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a vision prompt using Claude API or MCP bridge server.

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

        # Check if bridge server mode is enabled
        use_bridge = kwargs.get("use_bridge", False) or os.environ.get("MCP_USE_BRIDGE", "").lower() == "true"

        if use_bridge:
            try:
                logger.info("Using MCP bridge server for vision analysis")
                content = await self._call_bridge_server(image_data, prompt)

                # Estimate tokens for bridge response
                input_tokens = len(prompt) // 4 + len(image_data) // 1000 + 768
                output_tokens = len(content) // 4
                latency_ms = (time.time() - start_time) * 1000

                logger.info(
                    f"[MCP] Bridge Success. Tokens: {input_tokens} in, {output_tokens} out, "
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
                logger.error(f"[MCP] Bridge server request failed: {e}")
                if kwargs.get("bridge_only", False):
                    raise RuntimeError(f"MCP bridge request failed: {e}") from e
                logger.info("Falling back to direct API")

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
            # Call Claude API
            data = await self._call_claude_api(messages, max_tokens)

            # Extract content from response
            content_block = data.get("content", [{}])[0]
            content = content_block.get("text", "")

            # Get actual token usage from API
            usage = data.get("usage", {})
            input_tokens = usage.get("input_tokens", 0)
            output_tokens = usage.get("output_tokens", 0)

            latency_ms = (time.time() - start_time) * 1000

            logger.info(
                f"[MCP] Vision Success. Tokens: {input_tokens} in, {output_tokens} out, "
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
            logger.error(f"[MCP] Vision request failed: {e}")
            raise RuntimeError(f"MCP vision request failed: {e}") from e

    async def complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        additional_context: Optional[Dict] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a multimodal prompt.

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
            content.append({
                "type": "text",
                "text": f"\nContext:\n{json.dumps(additional_context, indent=2)}"
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
            # Call Claude API
            data = await self._call_claude_api(messages, max_tokens)

            # Extract content from response
            content_block = data.get("content", [{}])[0]
            content = content_block.get("text", "")

            # Get actual token usage from API
            usage = data.get("usage", {})
            input_tokens = usage.get("input_tokens", 0)
            output_tokens = usage.get("output_tokens", 0)

            latency_ms = (time.time() - start_time) * 1000

            logger.info(
                f"[MCP] Multimodal Success. Tokens: {input_tokens} in, {output_tokens} out, "
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
            logger.error(f"[MCP] Multimodal request failed: {e}")
            raise RuntimeError(f"MCP multimodal request failed: {e}") from e

    def get_token_estimate(self, mode: str, avg_request_tokens: int = 500) -> Dict[str, int]:
        """Estimate token usage for MCP.

        Args:
            mode: Call mode
            avg_request_tokens: Average request tokens

        Returns:
            Dict: Token estimates
        """
        if mode in ("vision", "multimodal"):
            return {
                "input": avg_request_tokens * 2 + 768,
                "output": avg_request_tokens,
                "total": avg_request_tokens * 3 + 768,
            }
        return {
            "input": avg_request_tokens,
            "output": avg_request_tokens // 2,
            "total": avg_request_tokens + avg_request_tokens // 2,
        }

    def get_performance_rating(self, mode: str) -> Dict[str, float]:
        """Get performance rating for MCP.

        Args:
            mode: Call mode

        Returns:
            Dict: Performance ratings
        """
        if mode in ("vision", "multimodal"):
            return {
                "latency": 0.7,
                "quality": 0.95,
                "efficiency": 0.8,
            }
        return {"latency": 0.0, "quality": 0.0, "efficiency": 0.0}
