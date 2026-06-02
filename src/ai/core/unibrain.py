"""UniBrain - Unified AI service for vision reasoning and decision making.

This service provides semantic interfaces aligned with business logic:
- Vision reasoning (视觉推理): Analyze screenshots to extract visual elements
- Decision making (决策): Infer page structure and next actions
"""

import base64
import logging
from typing import Dict, List, Optional, Union

import aiohttp

from src.ai.core.config import AIProviderConfig


logger = logging.getLogger(__name__)


class ReasoningResult:
    """Result from vision reasoning.

    Attributes:
        content: Extracted information
        input_tokens: Input tokens consumed
        output_tokens: Output tokens generated
        model: Model used
    """
    def __init__(self, content: str, input_tokens: int = 0, output_tokens: int = 0, model: str = ""):
        self.content = content
        self.input_tokens = input_tokens
        self.output_tokens = output_tokens
        self.model = model


class DecisionResult:
    """Result from decision making.

    Attributes:
        content: Decision result
        input_tokens: Input tokens consumed
        output_tokens: Output tokens generated
        model: Model used
    """
    def __init__(self, content: str, input_tokens: int = 0, output_tokens: int = 0, model: str = ""):
        self.content = content
        self.input_tokens = input_tokens
        self.output_tokens = output_tokens
        self.model = model


class UniBrain:
    """Unified AI service for vision reasoning and decision making.

    Provides two semantic interfaces:
    - reason_visual(): Vision reasoning with image input
    - decide(): Decision making with text input

    Routes to appropriate backend:
    - Vision reasoning → MiMo-CC (multimodal, supports images)
    - Decision making → DeepSeek (text-only, faster and cheaper)
    """

    def __init__(
        self,
        vision_config: AIProviderConfig = None,
        decision_config: AIProviderConfig = None,
    ):
        """Initialize UniBrain service.

        Args:
            vision_config: Config for vision reasoning (MiMo-CC)
            decision_config: Config for decision making (DeepSeek)
        """
        self.vision_config = vision_config
        self.decision_config = decision_config
        self._session: Optional[aiohttp.ClientSession] = None

    # ========== Vision Reasoning Interface (视觉推理) ==========

    async def reason_visual(
        self,
        prompt: str,
        image_data: bytes,
        model: Optional[str] = None,
        max_tokens: int = 4096,
    ) -> ReasoningResult:
        """Perform vision reasoning on screenshot.

        Analyzes the visual elements and structure from a screenshot.
        Uses MiMo-CC multimodal model.

        Args:
            prompt: Analysis prompt
            image_data: PNG image bytes
            model: Model override (default: vision_config.model)
            max_tokens: Max tokens in response

        Returns:
            ReasoningResult with extracted information and metrics

        Raises:
            RuntimeError: On API errors
        """
        if not self.vision_config:
            raise RuntimeError("Vision config not provided")

        config = self.vision_config
        model = model or config.model

        logger.info(f"[Vision Reasoning] Processing image with model: {model}")

        # Build content with image
        content = [{"type": "text", "text": prompt}]
        image_base64 = base64.b64encode(image_data).decode('utf-8')
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
                        logger.error(f"[Vision Reasoning] API error {response.status}: {error_text}")
                        raise RuntimeError(f"Vision reasoning API error {response.status}: {error_text}")

                    data = await response.json()

                    if "error" in data:
                        raise RuntimeError(f"Vision reasoning failed: {data['error']}")

                    content_block = data.get("content", [{}])[0]
                    response_text = content_block.get("text", "")

                    usage = data.get("usage", {})
                    logger.info(
                        f"[Vision Reasoning] Success. "
                        f"Tokens: {usage.get('input_tokens', 0)} in, "
                        f"{usage.get('output_tokens', 0)} out"
                    )

                    return ReasoningResult(
                        content=response_text,
                        input_tokens=usage.get("input_tokens", 0),
                        output_tokens=usage.get("output_tokens", 0),
                        model=model,
                    )
        except Exception as e:
            logger.error(f"[Vision Reasoning] Failed: {e}")
            raise RuntimeError(f"Vision reasoning failed: {e}") from e

    # ========== Decision Making Interface (决策) ==========

    async def decide(
        self,
        prompt: str,
        model: Optional[str] = None,
        max_tokens: int = 2048,
    ) -> DecisionResult:
        """Make decision based on context.

        Performs text-based reasoning and decision making.
        Uses DeepSeek for faster, cheaper text processing.

        Args:
            prompt: Decision prompt with context
            model: Model override (default: decision_config.model)
            max_tokens: Max tokens in response

        Returns:
            DecisionResult with decision and metrics

        Raises:
            RuntimeError: On API errors
        """
        if not self.decision_config:
            raise RuntimeError("Decision config not provided")

        config = self.decision_config
        model = model or config.model

        logger.info(f"[Decision] Processing with model: {model}")

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
                        logger.error(f"[Decision] API error {response.status}: {error_text}")
                        raise RuntimeError(f"Decision API error {response.status}: {error_text}")

                    data = await response.json()

                    message = data["choices"][0]["message"]
                    response_text = message["content"]

                    usage = data.get("usage", {})
                    logger.info(
                        f"[Decision] Success. "
                        f"Tokens: {usage.get('prompt_tokens', 0)} in, "
                        f"{usage.get('completion_tokens', 0)} out"
                    )

                    return DecisionResult(
                        content=response_text,
                        input_tokens=usage.get("prompt_tokens", 0),
                        output_tokens=usage.get("completion_tokens", 0),
                        model=model,
                    )
        except Exception as e:
            logger.error(f"[Decision] Failed: {e}")
            raise RuntimeError(f"Decision making failed: {e}") from e

    # ========== Compatibility Wrappers (兼容现有接口) ==========

    async def complete(
        self,
        prompt: str,
        image_data: Optional[bytes] = None,
        model: Optional[str] = None,
        response_format: Optional[Dict] = None,
        max_tokens: int = 4096,
    ) -> 'MockResponse':
        """Compatibility wrapper for legacy 'complete' interface.

        Deprecated: Use reason_visual() for vision, decide() for decisions.
        """
        if image_data:
            result = await self.reason_visual(prompt, image_data, model, max_tokens)
        else:
            result = await self.decide(prompt, model, max_tokens)

        # Convert to MockResponse format
        class MockResponse:
            def __init__(self, content, input_tokens, output_tokens):
                self.content = content
                self.usage = type('obj', (object,), {'input_tokens': input_tokens, 'output_tokens': output_tokens})()

        return MockResponse(
            result.content,
            result.input_tokens,
            result.output_tokens,
        )

    async def call(
        self,
        messages: List[Dict],
        schema: Optional[Dict] = None,
    ) -> Dict:
        """Compatibility wrapper for legacy 'call' interface.

        Deprecated: Use decide() for text-based decisions.
        """
        import json

        # Extract prompt from messages
        prompt = messages[-1]["content"]

        # Use decide interface
        result = await self.decide(prompt)

        # Parse JSON response
        try:
            return json.loads(result.content)
        except json.JSONDecodeError:
            raise RuntimeError(f"Failed to parse response as JSON: {result.content[:200]}")

    # ========== Session Management ==========

    async def close(self):
        """Close the HTTP session."""
        if self._session and not self._session.closed:
            await self._session.close()
            logger.info("[UniBrain] Session closed")

    async def __aenter__(self):
        """Async context manager entry."""
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit."""
        await self.close()


# ========== Factory Functions ==========

def create_unibrain(
    mimo_api_key: str,
    mimo_base_url: str = "https://token-plan-cn.xiaomimimo.com/anthropic",
    mimo_model: str = "mimo-v2.5",
    deepseek_api_key: str = "",
    deepseek_model: str = "deepseek-v4-flash",
) -> UniBrain:
    """Create UniBrain service with vision reasoning and decision making.

    Args:
        mimo_api_key: API key for MiMo-CC (vision reasoning)
        mimo_base_url: Base URL for MiMo-CC
        mimo_model: Model name for MiMo
        deepseek_api_key: API key for DeepSeek (decision making)
        deepseek_model: Model name for DeepSeek

    Returns:
        UniBrain instance with both capabilities

    Example:
        brain = create_unibrain(
            mimo_api_key="your-mimo-key",
            deepseek_api_key="your-deepseek-key",
        )

        # Vision reasoning
        result = await brain.reason_visual(prompt, image_data)

        # Decision making
        result = await brain.decide(prompt)
    """
    from src.ai.core.config import AIProviderConfig

    # Vision reasoning config (MiMo-CC)
    vision_config = AIProviderConfig(
        api_key=mimo_api_key,
        model=mimo_model,
        base_url=mimo_base_url,
    )

    # Decision making config (DeepSeek)
    decision_config = None
    if deepseek_api_key:
        decision_config = AIProviderConfig(
            api_key=deepseek_api_key,
            model=deepseek_model,
            base_url="https://api.deepseek.com/v1",
        )

    return UniBrain(
        vision_config=vision_config,
        decision_config=decision_config,
    )


def create_unibrain_from_settings(settings=None) -> UniBrain:
    """Create UniBrain from project settings.

    Args:
        settings: Optional Settings object (loads if None)

    Returns:
        UniBrain instance configured from settings

    Raises:
        RuntimeError: If required API keys are missing
    """
    if settings is None:
        from src.config.settings import get_settings
        settings = get_settings()

    return create_unibrain(
        mimo_api_key=settings.mimo_api_key or settings.anthropic_api_key,
        mimo_base_url=settings.mimo_cc_base_url,
        mimo_model=settings.mimo_cc_model or settings.vision_model,
        deepseek_api_key=settings.deepseek_api_key,
        deepseek_model=settings.vision.text_model,
    )


__all__ = [
    "UniBrain",
    "ReasoningResult",
    "DecisionResult",
    "create_unibrain",
    "create_unibrain_from_settings",
]
