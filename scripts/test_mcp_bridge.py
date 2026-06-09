"""Test script for MCP Bridge Server solution.

This script demonstrates the portable MCP provider solution:
1. Run the bridge server in Claude Code environment
2. Call it from external Python code
3. No API key required - uses Claude Code's existing MCP connection
"""

import asyncio
import base64
import json
import os
import sys
import time
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from src.ai.providers.mcp import MCPProvider
from src.ai.providers.base import AIProviderConfig


def print_section(title: str):
    """Print a section header."""
    print(f"\n{'='*60}")
    print(f"  {title}")
    print(f"{'='*60}")


async def test_bridge_server_direct():
    """Test calling the bridge server directly via HTTP."""
    print_section("Test 1: Direct Bridge Server Call")

    import aiohttp

    bridge_url = os.environ.get("MCP_BRIDGE_URL", "http://127.0.0.1:8765")

    # Read test image
    image_path = project_root / "tests/assets/images/settings_home.jpg"
    with open(image_path, "rb") as f:
        image_data = f.read()

    # Encode to base64
    image_base64 = base64.b64encode(image_data).decode("utf-8")

    prompt = "Analyze this mobile app screenshot and extract UI elements in JSON format."

    print(f"Bridge URL: {bridge_url}")
    print(f"Image size: {len(image_data)} bytes")
    print(f"Prompt: {prompt[:50]}...")

    try:
        async with aiohttp.ClientSession() as session:
            async with session.post(
                f"{bridge_url}/mcp/analyze_image",
                json={
                    "image_data": image_base64,
                    "prompt": prompt,
                },
                timeout=aiohttp.ClientTimeout(total=30)
            ) as response:
                if response.status >= 400:
                    error_text = await response.text()
                    print(f"[ERROR] Bridge server error: {response.status} - {error_text}")
                    return False

                data = await response.json()

                if not data.get("success"):
                    print(f"[ERROR] Bridge server returned error: {data.get('error')}")
                    return False

                content = data.get("content", "")
                print(f"[OK] Bridge server call successful!")
                print(f"   Content length: {len(content)} chars")

                # Try to parse JSON
                try:
                    result = json.loads(content)
                    print(f"   Items found: {len(result.get('items', []))}")
                    print(f"   Level 1 menus: {len(result.get('level1_menus', []))}")
                except json.JSONDecodeError:
                    print(f"   [WARNING]  Content is not valid JSON")

                return True

    except Exception as e:
        print(f"[ERROR] Error calling bridge server: {e}")
        return False


async def test_mcp_provider_with_bridge():
    """Test MCPProvider configured to use bridge server."""
    print_section("Test 2: MCPProvider with Bridge Server")

    # Configure provider to use bridge
    config = AIProviderConfig(
        api_key="not_required_for_bridge",
        model="claude-3-5-sonnet-20241022",
        base_url="mcp://local",  # Use bridge server
        request_timeout=60.0,
    )

    provider = MCPProvider(config)

    # Read test image
    image_path = project_root / "tests/assets/images/settings_home.jpg"
    with open(image_path, "rb") as f:
        image_data = f.read()

    print(f"Provider ID: {provider.provider_id}")
    print(f"Supported modes: {provider.supported_modes}")
    print(f"Image size: {len(image_data)} bytes")

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

    try:
        # Use bridge mode
        response = await provider.complete_vision(
            prompt=prompt,
            image_data=image_data,
            max_tokens=4096,
            use_bridge=True,
        )

        print(f"[OK] MCPProvider call successful!")
        print(f"   Provider: {response.provider_id}")
        print(f"   Mode: {response.mode}")
        print(f"   Input tokens: {response.input_tokens}")
        print(f"   Output tokens: {response.output_tokens}")
        print(f"   Latency: {response.latency_ms:.0f}ms")

        # Try to parse JSON
        try:
            result = json.loads(response.content)
            print(f"   Items found: {len(result.get('items', []))}")
            print(f"   Level 1 menus: {len(result.get('level1_menus', []))}")
        except json.JSONDecodeError:
            print(f"   [WARNING]  Content is not valid JSON")
            print(f"   Content preview: {response.content[:200]}...")

        return True

    except Exception as e:
        print(f"[ERROR] MCPProvider error: {e}")
        import traceback
        traceback.print_exc()
        return False


async def main():
    """Run all tests."""
    print_section("MCP Bridge Server Solution Test")
    print("This test demonstrates the portable MCP provider solution.")
    print("")
    print("Prerequisites:")
    print("  1. MCP Bridge Server must be running on http://127.0.0.1:8765")
    print("     Run it with: python scripts/mcp_bridge.py")
    print("  2. The bridge server should be run within Claude Code environment")
    print("")

    # Check if bridge server is available
    print("Checking bridge server availability...")
    try:
        import aiohttp
        async with aiohttp.ClientSession() as session:
            async with session.get("http://127.0.0.1:8765/health", timeout=aiohttp.ClientTimeout(total=5)) as response:
                if response.status == 200:
                    data = await response.json()
                    print(f"[OK] Bridge server is running: {data}")
                else:
                    print(f"[ERROR] Bridge server returned status: {response.status}")
                    print("\n[WARNING] Please start the bridge server first:")
                    print("   python scripts/mcp_bridge.py")
                    return
    except Exception as e:
        print(f"[ERROR] Cannot reach bridge server: {e}")
        print("\n[WARNING] Please start the bridge server first:")
        print("   python scripts/mcp_bridge.py")
        return

    # Run tests
    results = []

    results.append(await test_bridge_server_direct())
    await asyncio.sleep(1)  # Brief pause between tests

    results.append(await test_mcp_provider_with_bridge())

    # Summary
    print_section("Test Summary")
    total = len(results)
    passed = sum(results)
    print(f"Tests passed: {passed}/{total}")

    if passed == total:
        print("[OK] All tests passed!")
    else:
        print("[WARNING]  Some tests failed")

    print("\n" + "="*60)
    print("Solution Overview:")
    print("="*60)
    print("""
This solution provides a portable way to use MCP tools without API keys:

1. Bridge Server (scripts/mcp_bridge.py)
   - Runs within Claude Code environment
   - Exposes MCP tools via HTTP on localhost
   - No API key required - uses Claude Code's existing connection

2. MCPProvider (src/ai/providers/mcp.py)
   - Standard AIProvider interface
   - Can call bridge server when configured
   - Falls back to direct API if bridge unavailable

3. Usage:
   # On machine with Claude Code:
   export MCP_USE_BRIDGE=true
   python -c "import asyncio; from scripts.test_mcp_bridge import main; asyncio.run(main())"

   # On other machines:
   # 1. Run bridge server in Claude Code
   # 2. Set MCP_BRIDGE_URL if needed
   # 3. Use MCP_USE_BRIDGE=true environment variable
    """)


if __name__ == "__main__":
    asyncio.run(main())
