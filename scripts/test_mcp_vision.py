"""Test script for MCP Provider vision analysis."""

import asyncio
import base64
import json
import os
import sys
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from src.ai.providers.mcp import MCPProvider
from src.ai.providers.base import AIProviderConfig


async def test_vision_analysis():
    """Test vision analysis with MCP provider."""

    # Get configuration from environment
    api_key = os.environ.get("ANTHROPIC_AUTH_TOKEN") or os.environ.get("ANTHROPIC_API_KEY")
    base_url = os.environ.get("ANTHROPIC_BASE_URL", "https://api.anthropic.com")

    print(f"Using base_url: {base_url}")
    print(f"Using api_key: {api_key[:20]}..." if api_key else "No API key found!")

    # Create provider config
    config = AIProviderConfig(
        api_key=api_key or "test_key",
        model="claude-3-5-sonnet-20241022",
        base_url=base_url,
        request_timeout=60.0,
    )

    # Create provider
    provider = MCPProvider(config)

    # Read test image
    image_path = project_root / "tests/assets/images/settings_home.jpg"
    with open(image_path, "rb") as f:
        image_data = f.read()

    print(f"\nImage size: {len(image_data)} bytes")

    # Vision analysis prompt
    prompt = """Analyze this mobile app screenshot and extract the UI elements in JSON format.

Return a JSON object with this structure:
{
  "level1_dir": "top|left|right|bottom",
  "level1_menus": [{"name": "tab name", "coordinate": {"x": 0.5, "y": 0.5}, "active": true}],
  "level2_dir": "top|left|right|bottom|null",
  "level2_menus": [{"name": "submenu name", "coordinate": {"x": 0.5, "y": 0.5}}],
  "current_path": ["current", "menu", "path"],
  "items": [
    {
      "name": "element name",
      "type": "button|toggle|menu_item|slider|input|text|checkbox",
      "coordinate": {"x": 0.5, "y": 0.5},
      "parent": "parent name or null",
      "description": "brief description",
      "expected_action": "tap|toggle|navigate|input|scroll",
      "expects_page_change": false,
      "expects_state_change": false
    }
  ],
  "is_popup": false,
  "popup_info": null,
  "close_button": null,
  "back_button": {"x": 0.05, "y": 0.08},
  "has_scroll": true,
  "is_end_of_list": false
}

Only return the JSON, no other text."""

    print(f"\nCalling vision analysis...")
    print(f"Provider ID: {provider.provider_id}")
    print(f"Supported modes: {provider.supported_modes}")

    try:
        # Call vision analysis
        response = await provider.complete_vision(
            prompt=prompt,
            image_data=image_data,
            max_tokens=4096,
        )

        print(f"\n=== SUCCESS ===")
        print(f"Provider: {response.provider_id}")
        print(f"Mode: {response.mode}")
        print(f"Input tokens: {response.input_tokens}")
        print(f"Output tokens: {response.output_tokens}")
        print(f"Latency: {response.latency_ms:.0f}ms")
        print(f"Model: {response.model}")
        print(f"Success: {response.success}")

        print(f"\n=== Content ===")
        print(response.content[:1000])

        # Try to parse as JSON
        try:
            result = json.loads(response.content)
            print(f"\n=== Parsed JSON ===")
            print(json.dumps(result, indent=2, ensure_ascii=False))
        except json.JSONDecodeError:
            print("\n⚠️  Response is not valid JSON")

    except Exception as e:
        print(f"\n=== ERROR ===")
        print(f"Type: {type(e).__name__}")
        print(f"Message: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(test_vision_analysis())
