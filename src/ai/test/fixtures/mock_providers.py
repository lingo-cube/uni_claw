"""Mock Provider implementations for testing AI providers without real API calls.

This module provides mock implementations of AI providers that simulate
real API responses without making actual API calls, ensuring zero-cost testing.
"""

import json
import time
from typing import Dict, List, Optional, Any
from dataclasses import dataclass, field


@dataclass
class MockResponse:
    """Mock API response."""
    content: str
    provider_id: str = "mock"
    mode: str = "text"
    input_tokens: int = 10
    output_tokens: int = 20
    latency_ms: float = 50.0
    model: str = "mock-model"
    success: bool = True


class RecordedResponses:
    """Container for pre-recorded API responses.

    This allows testing with realistic response data without making real API calls.
    """

    def __init__(self):
        self._responses: Dict[str, Dict[str, Any]] = {
            # DeepSeek text responses
            "deepseek_parse_instruction": {
                "input": "Go to WiFi settings",
                "response": {
                    "content": json.dumps({
                        "entry_app": "Settings",
                        "root_node": {
                            "node_id": "root",
                            "name": "Navigate to WiFi",
                            "node_type": "container",
                            "operation": {"action": "navigate", "target": {"by": "app_name", "value": "Settings"}},
                            "children_strategy": {"type": "dynamic"}
                        },
                        "mode": "hybrid",
                        "confidence": 0.9
                    }),
                    "input_tokens": 15,
                    "output_tokens": 120,
                }
            },
            "deepseek_verify_page": {
                "input": "menu_list",
                "response": {
                    "content": json.dumps({
                        "is_match": True,
                        "confidence": 0.95,
                        "actual_type": "menu_list",
                        "reasoning": "Page structure matches menu_list pattern"
                    }),
                    "input_tokens": 20,
                    "output_tokens": 50,
                }
            },
            "deepseek_safety": {
                "input": "screen_elements",
                "response": {
                    "content": json.dumps({
                        "evaluations": [
                            {"name": "WiFi", "safety_tag": "safe", "confidence": 0.95}
                        ],
                        "page_level_guidance": {
                            "overall_safe_to_proceed": True
                        }
                    }),
                    "input_tokens": 30,
                    "output_tokens": 80,
                }
            },
            "deepseek_decision": {
                "input": "next_action",
                "response": {
                    "content": json.dumps({
                        "result": "success",
                        "action": "click",
                        "target": {"by": "text", "value": "WiFi"},
                        "confidence": 0.85
                    }),
                    "input_tokens": 25,
                    "output_tokens": 60,
                }
            },

            # Claude vision responses
            "claude_analyze_visual": {
                "input": "screenshot",
                "response": {
                    "content": json.dumps({
                        "current_path": ["Home", "Settings"],
                        "page_type": "menu_list",
                        "items": [
                            {
                                "id": 1,
                                "name": "WiFi",
                                "bbox": {"x": 0.1, "y": 0.3, "w": 0.8, "h": 0.1},
                                "type": "settings_item"
                            }
                        ],
                        "confidence": 0.9
                    }),
                    "input_tokens": 1100,
                    "output_tokens": 350,
                }
            },
            "claude_verify_with_vision": {
                "input": "verify_screenshot",
                "response": {
                    "content": json.dumps({
                        "is_match": True,
                        "confidence": 0.92,
                        "actual_type": "settings_group"
                    }),
                    "input_tokens": 1150,
                    "output_tokens": 280,
                }
            },

            # MiMo multimodal responses
            "mimo_analyze_visual": {
                "input": "screenshot",
                "response": {
                    "content": json.dumps({
                        "current_path": ["Home"],
                        "page_type": "home_desktop",
                        "items": [
                            {
                                "id": 1,
                                "name": "Settings",
                                "bbox": {"x": 0.2, "y": 0.4, "w": 0.6, "h": 0.15},
                                "type": "app_icon"
                            }
                        ],
                        "confidence": 0.88
                    }),
                    "input_tokens": 950,
                    "output_tokens": 300,
                }
            },
        }

    def get_response(self, key: str) -> Optional[Dict[str, Any]]:
        """Get a recorded response by key."""
        return self._responses.get(key)

    def list_keys(self) -> List[str]:
        """List all available response keys."""
        return list(self._responses.keys())


class MockProvider:
    """Mock AI Provider for testing.

    This provider simulates AI responses without making actual API calls.
    It supports three modes: text, vision, and multimodal.
    """

    def __init__(self, provider_id: str = "mock", use_recorded_data: bool = True):
        """Initialize the mock provider.

        Args:
            provider_id: Identifier for this mock provider
            use_recorded_data: Whether to use pre-recorded responses
        """
        self.provider_id = provider_id
        self.use_recorded_data = use_recorded_data
        self.recorded_data = RecordedResponses()
        self._call_count = 0
        self._calls: List[Dict[str, Any]] = []

    @property
    def supported_modes(self) -> List[str]:
        """Return supported modes."""
        return ["text", "vision", "multimodal"]

    def _record_call(self, method: str, **kwargs) -> None:
        """Record a call for testing verification."""
        self._call_count += 1
        self._calls.append({
            "method": method,
            "kwargs": kwargs,
            "timestamp": time.time()
        })

    def _find_recorded_response(self, method: str, **kwargs) -> Optional[MockResponse]:
        """Find a matching recorded response."""
        if not self.use_recorded_data:
            return None

        # Simple matching logic based on method and key hints
        if method == "complete_text":
            prompt = kwargs.get("prompt", "")
            if "WiFi" in prompt or "instruction" in str(kwargs).lower():
                data = self.recorded_data.get_response("deepseek_parse_instruction")
            elif "verify" in prompt.lower():
                data = self.recorded_data.get_response("deepseek_verify_page")
            elif "safe" in prompt.lower():
                data = self.recorded_data.get_response("deepseek_safety")
            elif "action" in prompt.lower() or "decision" in prompt.lower():
                data = self.recorded_data.get_response("deepseek_decision")
            else:
                data = None

            if data:
                return MockResponse(
                    content=data["response"]["content"],
                    provider_id=self.provider_id,
                    mode="text",
                    input_tokens=data["response"]["input_tokens"],
                    output_tokens=data["response"]["output_tokens"],
                    latency_ms=50.0 + (hash(data["response"]["content"]) % 100),
                )

        elif method in ("complete_vision", "complete_multimodal"):
            if "claude" in self.provider_id.lower():
                data = self.recorded_data.get_response("claude_analyze_visual")
            elif "mimo" in self.provider_id.lower():
                data = self.recorded_data.get_response("mimo_analyze_visual")
            else:
                data = self.recorded_data.get_response("claude_analyze_visual")

            if data:
                return MockResponse(
                    content=data["response"]["content"],
                    provider_id=self.provider_id,
                    mode="vision" if method == "complete_vision" else "multimodal",
                    input_tokens=data["response"]["input_tokens"],
                    output_tokens=data["response"]["output_tokens"],
                    latency_ms=100.0 + (hash(data["response"]["content"]) % 200),
                )

        return None

    async def complete_text(
        self,
        prompt: str,
        schema: Optional[Dict] = None,
        max_tokens: int = 2048,
        **kwargs
    ) -> MockResponse:
        """Mock text completion.

        Args:
            prompt: Text prompt
            schema: Optional JSON schema for response validation
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            MockResponse: Simulated API response
        """
        self._record_call("complete_text", prompt=prompt, schema=schema, max_tokens=max_tokens, **kwargs)

        # Try to find a recorded response
        recorded = self._find_recorded_response("complete_text", prompt=prompt, **kwargs)
        if recorded:
            return recorded

        # Default mock response
        return MockResponse(
            content='{"result": "mock_response"}',
            provider_id=self.provider_id,
            mode="text",
            input_tokens=len(prompt.split()),
            output_tokens=20,
            latency_ms=50.0,
        )

    async def complete_vision(
        self,
        prompt: str,
        image_data: bytes,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> MockResponse:
        """Mock vision completion.

        Args:
            prompt: Text prompt
            image_data: PNG image data
            schema: Optional JSON schema for response validation
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            MockResponse: Simulated API response
        """
        self._record_call(
            "complete_vision",
            prompt=prompt,
            image_size=len(image_data),
            schema=schema,
            max_tokens=max_tokens,
            **kwargs
        )

        # Try to find a recorded response
        recorded = self._find_recorded_response("complete_vision", prompt=prompt, **kwargs)
        if recorded:
            return recorded

        # Default mock response for vision
        return MockResponse(
            content=json.dumps({
                "current_path": ["Mock", "Path"],
                "page_type": "mock_page",
                "items": [],
                "confidence": 0.8
            }),
            provider_id=self.provider_id,
            mode="vision",
            input_tokens=1000,
            output_tokens=100,
            latency_ms=200.0,
        )

    async def complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        additional_context: Optional[Dict] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> MockResponse:
        """Mock multimodal completion.

        Args:
            prompt: Text prompt
            image_data: PNG image data
            additional_context: Additional context information
            schema: Optional JSON schema for response validation
            max_tokens: Maximum output tokens
            **kwargs: Additional parameters

        Returns:
            MockResponse: Simulated API response
        """
        self._record_call(
            "complete_multimodal",
            prompt=prompt,
            image_size=len(image_data),
            additional_context=additional_context,
            schema=schema,
            max_tokens=max_tokens,
            **kwargs
        )

        # Try to find a recorded response
        recorded = self._find_recorded_response("complete_multimodal", prompt=prompt, **kwargs)
        if recorded:
            return recorded

        # Default mock response for multimodal
        return MockResponse(
            content=json.dumps({
                "current_path": ["Mock", "Multimodal"],
                "page_type": "mock_page",
                "items": [],
                "confidence": 0.85
            }),
            provider_id=self.provider_id,
            mode="multimodal",
            input_tokens=1200,
            output_tokens=150,
            latency_ms=250.0,
        )

    def get_token_estimate(self, mode: str, avg_request_tokens: int = 500) -> Dict[str, int]:
        """Estimate token usage.

        Args:
            mode: Call mode (text, vision, multimodal)
            avg_request_tokens: Average request tokens

        Returns:
            Dict with input, output, and total token estimates
        """
        if mode in ("vision", "multimodal"):
            return {
                "input": avg_request_tokens * 2,
                "output": avg_request_tokens // 2,
                "total": avg_request_tokens * 2 + avg_request_tokens // 2
            }
        return {
            "input": avg_request_tokens,
            "output": avg_request_tokens // 2,
            "total": avg_request_tokens + avg_request_tokens // 2
        }

    def get_performance_rating(self, mode: str) -> Dict[str, float]:
        """Get performance rating.

        Args:
            mode: Call mode

        Returns:
            Dict with latency, quality, and efficiency ratings
        """
        return {
            "latency": 0.9,    # Mock is very fast
            "quality": 0.8,    # Good quality from recorded data
            "efficiency": 1.0  # Perfect efficiency (no cost)
        }

    async def health_check(self) -> bool:
        """Mock health check - always returns True."""
        return True

    def get_call_history(self) -> List[Dict[str, Any]]:
        """Get call history for testing verification."""
        return self._calls.copy()

    def reset_call_history(self) -> None:
        """Reset call history."""
        self._calls.clear()
        self._call_count = 0

    @property
    def call_count(self) -> int:
        """Get total call count."""
        return self._call_count


class MockDeepSeekProvider(MockProvider):
    """Mock DeepSeek provider."""

    def __init__(self):
        super().__init__(provider_id="mock_deepseek", use_recorded_data=True)

    @property
    def supported_modes(self) -> List[str]:
        """DeepSeek only supports text."""
        return ["text"]


class MockClaudeProvider(MockProvider):
    """Mock Claude provider."""

    def __init__(self):
        super().__init__(provider_id="mock_claude", use_recorded_data=True)


class MockMiMoProvider(MockProvider):
    """Mock MiMo provider."""

    def __init__(self):
        super().__init__(provider_id="mock_mimo", use_recorded_data=True)

    @property
    def supported_modes(self) -> List[str]:
        """MiMo supports vision and multimodal."""
        return ["vision", "multimodal"]
