"""Unified AI provider supporting both multimodal and text interfaces.

This provider wraps both MiMo-CC (for vision) and DeepSeek (for text)
into a single provider with dual interface support.
"""

import base64
import logging
from typing import Dict, List, Optional, Union

import aiohttp

from src.ai.core.config import AIProviderConfig


logger = logging.getLogger(__name__)


class MockResponse:
    """Mock response object mimicking API response."""

    def __init__(self, content: str, input_tokens: int = 0, output_tokens: int = 0):
        self.content = content
        self.usage = MockUsage(input_tokens, output_tokens)


class MockUsage:
    """Mock usage object."""

    def __init__(self, input_tokens: int, output_tokens: int):
        self.input_tokens = input_tokens
        self.output_tokens = output_tokens


class UnifiedAIProvider:
    """Unified AI provider supporting both multimodal and text interfaces.

    Provides two interfaces:
    - complete(prompt, image_data, ...) - Multimodal interface (for vision)
    - call(messages, schema) / call_with_prompt(...) - Text interface (for assembly)

    Routes to appropriate backend:
    - With image_data → MiMo-CC (Anthropic protocol, multimodal)
    - Without image_data → DeepSeek (text-only, cheaper)
    """

    def __init__(
        self,
        multimodal_config: AIProviderConfig = None,
        text_config: AIProviderConfig = None,
    ):
        """Initialize the unified AI provider.

        Args:
            multimodal_config: Config for multimodal (MiMo-CC)
            text_config: Config for text (DeepSeek)
        """
        self.multimodal_config = multimodal_config
        self.text_config = text_config
        self._session: Optional[aiohttp.ClientSession] = None

    async def _get_session(self, base_url: str, headers: Dict) -> aiohttp.ClientSession:
        """Get or create HTTP session with specific headers."""
        if self._session is None or self._session.closed:
            timeout = aiohttp.ClientTimeout(total=30.0)
            self._session = aiohttp.ClientSession(timeout=timeout, headers=headers)
        return self._session

    def _encode_image_base64(self, image_data: bytes) -> str:
        """Encode image bytes to base64."""
        return base64.b64encode(image_data).decode('utf-8')

    # ========== Multimodal Interface (for Vision) ==========

    async def complete(
        self,
        prompt: str,
        image_data: Optional[bytes] = None,
        model: Optional[str] = None,
        response_format: Optional[Dict] = None,
        max_tokens: int = 4096,
    ) -> MockResponse:
        """Complete a prompt with optional image input.

        This is the multimodal interface used by vision analysis.

        Args:
            prompt: Text prompt
            image_data: Optional PNG image bytes
            model: Model override
            response_format: Response format spec
            max_tokens: Max tokens in response

        Returns:
            MockResponse with content and usage
        """
        if image_data:
            # Use MiMo-CC for multimodal
            return await self._complete_multimodal(
                prompt, image_data, model, response_format, max_tokens
            )
        else:
            # Use DeepSeek for text-only
            return await self._complete_text(
                prompt, model, response_format, max_tokens
            )

    async def _complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        model: Optional[str],
        response_format: Optional[Dict],
        max_tokens: int,
    ) -> MockResponse:
        """Multimodal completion via MiMo-CC (Anthropic protocol)."""
        if not self.multimodal_config:
            raise RuntimeError("Multimodal config not provided")

        config = self.multimodal_config
        model = model or config.model

        # Build content with image
        content = [{"type": "text", "text": prompt}]
        image_base64 = self._encode_image_base64(image_data)
        content.append({
            "type": "image",
            "source": {
                "type": "base64",
                "media_type": "image/png",
                "data": image_base64,
            },
        })

        # Build payload for Anthropic protocol
        payload = {
            "model": model,
            "max_tokens": max_tokens,
            "messages": [{"role": "user", "content": content}],
        }

        # Prepare headers for Anthropic protocol
        headers = {
            "x-api-key": config.api_key,
            "anthropic-version": "2023-06-01",
            "content-type": "application/json",
        }

        try:
            timeout = aiohttp.ClientTimeout(total=30.0)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    f"{config.base_url}/v1/messages",
                    headers=headers,
                    json=payload,
                ) as response:
                    if response.status >= 400:
                        error_text = await response.text()
                        raise RuntimeError(f"Multimodal API error {response.status}: {error_text}")

                    data = await response.json()

                    if "error" in data:
                        raise RuntimeError(f"API error: {data['error']}")

                    content_block = data.get("content", [{}])[0]
                    response_text = content_block.get("text", "")

                    usage = data.get("usage", {})
                    return MockResponse(
                        content=response_text,
                        input_tokens=usage.get("input_tokens", 0),
                        output_tokens=usage.get("output_tokens", 0),
                    )
        except Exception as e:
            logger.error(f"Multimodal completion failed: {e}")
            raise

    async def _complete_text(
        self,
        prompt: str,
        model: Optional[str],
        response_format: Optional[Dict],
        max_tokens: int,
    ) -> MockResponse:
        """Text-only completion via DeepSeek."""
        if not self.text_config:
            raise RuntimeError("Text config not provided")

        config = self.text_config
        model = model or config.model

        # Build messages for DeepSeek
        messages = [{"role": "user", "content": prompt}]

        # Build payload
        payload = {
            "model": model,
            "messages": messages,
            "max_tokens": max_tokens,
        }

        # Prepare headers for OpenAI protocol
        headers = {
            "Authorization": f"Bearer {config.api_key}",
            "Content-Type": "application/json",
        }

        try:
            timeout = aiohttp.ClientTimeout(total=30.0)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    f"{config.base_url}/chat/completions",
                    headers=headers,
                    json=payload,
                ) as response:
                    if response.status >= 400:
                        error_text = await response.text()
                        raise RuntimeError(f"Text API error {response.status}: {error_text}")

                    data = await response.json()

                    message = data["choices"][0]["message"]
                    response_text = message["content"]

                    usage = data.get("usage", {})
                    return MockResponse(
                        content=response_text,
                        input_tokens=usage.get("prompt_tokens", 0),
                        output_tokens=usage.get("completion_tokens", 0),
                    )
        except Exception as e:
            logger.error(f"Text completion failed: {e}")
            raise

    # ========== Text Interface (for Assembly) ==========

    async def call(
        self,
        messages: List[Dict],
        schema: Optional[Dict] = None,
    ) -> Dict:
        """Text interface - compatible with LLMClient.

        This interface is used by PageAnalysisAssembler.

        Args:
            messages: List of message dicts
            schema: JSON schema for structured output

        Returns:
            Parsed JSON response as dict
        """
        # Extract prompt from messages
        prompt = messages[-1]["content"]

        # Use complete interface
        response = await self.complete(prompt)

        # Parse JSON response
        import json
        try:
            return json.loads(response.content)
        except json.JSONDecodeError:
            raise RuntimeError(f"Failed to parse response as JSON: {response.content[:200]}")

    async def call_with_prompt(
        self,
        prompt: str,
        schema: Optional[Dict] = None,
        variables: Optional[Dict] = None,
    ) -> Dict:
        """Text interface with template - compatible with LLMClient.

        Args:
            prompt: Prompt template with {variable} placeholders
            schema: JSON schema
            variables: Variables to inject

        Returns:
            Parsed JSON response as dict
        """
        formatted = self._inject_variables(prompt, variables or {})
        return await self.call([{"role": "user", "content": formatted}], schema)

    def _inject_variables(self, template: str, variables: Dict) -> str:
        """Inject variables into template."""
        result = template
        for key, value in variables.items():
            result = result.replace(f"{{{key}}}", str(value))
        return result

    async def close(self):
        """Close the session."""
        if self._session and not self._session.closed:
            await self._session.close()

    async def __aenter__(self):
        """Async context manager entry."""
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit."""
        await self.close()


def create_unified_provider(
    mimo_api_key: str,
    mimo_base_url: str = "https://token-plan-cn.xiaomimimo.com/anthropic",
    mimo_model: str = "mimo-v2.5",
    deepseek_api_key: str = "",
    deepseek_model: str = "deepseek-v4-flash",
) -> UnifiedAIProvider:
    """Create a unified AI provider with both interfaces.

    Args:
        mimo_api_key: API key for MiMo-CC
        mimo_base_url: Base URL for MiMo-CC
        mimo_model: Model name for MiMo
        deepseek_api_key: API key for DeepSeek
        deepseek_model: Model name for DeepSeek

    Returns:
        UnifiedAIProvider instance
    """
    from src.ai.core.config import AIProviderConfig

    multimodal_config = AIProviderConfig(
        api_key=mimo_api_key,
        model=mimo_model,
        base_url=mimo_base_url,
    )

    text_config = None
    if deepseek_api_key:
        text_config = AIProviderConfig(
            api_key=deepseek_api_key,
            model=deepseek_model,
            base_url="https://api.deepseek.com/v1",
        )

    return UnifiedAIProvider(
        multimodal_config=multimodal_config,
        text_config=text_config,
    )


__all__ = [
    "UnifiedAIProvider",
    "create_unified_provider",
    "MockResponse",
    "MockUsage",
]
