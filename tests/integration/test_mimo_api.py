#!/usr/bin/env python3
"""Debug MiMo API connection."""

import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

def test_mimo_direct():
    """Test MiMo API directly."""
    print("🔍 Testing MiMo API directly...")

    from openai import OpenAI

    api_key = os.environ.get("MIMO_API_KEY", "tp-c0xl11fllcgwm1m5xafrqbwz64adsrenvy7ymg5xod4u8w8n")

    print(f"API Key: {api_key[:20]}...")
    print(f"Base URL: https://api.xiaomimimo.com/v1")

    client = OpenAI(
        api_key=api_key,
        base_url="https://api.xiaomimimo.com/v1"
    )

    # Test with a simple text request first
    try:
        print("\n📡 Sending test request...")
        response = client.chat.completions.create(
            model="mimo-v2.5",
            messages=[
                {
                    "role": "system",
                    "content": "You are a helpful assistant."
                },
                {
                    "role": "user",
                    "content": "Say 'API works!' if you receive this."
                }
            ],
            max_tokens=50
        )

        print(f"✅ API Response: {response.choices[0].message.content}")
        return True

    except Exception as e:
        print(f"❌ API Error: {e}")
        return False


if __name__ == "__main__":
    test_mimo_direct()
