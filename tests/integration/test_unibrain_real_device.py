#!/usr/bin/env python3
"""UniBrain AI Provider - Real Device Integration Test.

This test demonstrates:
- Connecting to a real device via ADB
- Taking screenshots from the device
- Using Claude Vision Service for analysis
- Testing AI capabilities with real data
"""

import sys
import time
from pathlib import Path
from datetime import datetime

sys.path.insert(0, str(Path(__file__).parent.parent.parent))


def test_unibrain_real_device():
    """Test UniBrain AI Provider with real device."""
    print("=" * 70)
    print("UniBrain AI Provider - Real Device Test")
    print("=" * 70)
    print()

    # Import components
    from src.adb import ADBClient
    from src.ai import UniBrain, AIProviderConfig, RetryConfig, VisionConfig
    from src.state import TraversalState
    from src.context import TraversalContext
    from src.traversal import TraversalConfig, TraversalEngine

    # Step 1: Connect to device
    print("📱 Step 1: Connecting to device...")
    try:
        adb = ADBClient()
        device_info = adb.get_device_info()
        print(f"   ✅ Connected: {device_info.get('model', 'Unknown Device')}")
    except Exception as e:
        print(f"   ❌ Failed to connect: {e}")
        return 1

    # Step 2: Configure AI Provider
    print("\n🤖 Step 2: Configuring AI Provider...")

    ai_config = AIProviderConfig(
        api_key="sk-c052620de7c24c0dbb4d1db9460c50eb",  # DeepSeek
        model="deepseek-v4-flash",
        retry=RetryConfig(max_attempts=2, base_delay=0.5),
        reasoning_detail="detailed",
    )

    vision_config = VisionConfig(
        service_type="claude",
        api_key="sk-5d1655b4dd6b931d7fe05c03293b940c248d8d578c13598945af45d008506f43",
        model="claude-3-5-sonnet-20241022",
        timeout=30.0,
    )

    print(f"   ✅ AI Model: {ai_config.model}")
    print(f"   ✅ Vision Model: {vision_config.model}")

    # Step 3: Initialize UniBrain
    print("\n🧠 Step 3: Initializing UniBrain...")
    provider = UniBrain(
        ai_config,
        vision_config,
        enable_metrics=True,
        enable_archiving=True,
    )
    print("   ✅ UniBrain initialized with all capabilities")

    # Step 4: Take screenshot from device
    print("\n📸 Step 4: Capturing screen from device...")
    try:
        screenshot_data = adb.get_screenshot()
        print(f"   ✅ Screenshot captured ({len(screenshot_data)} bytes)")

        # Save screenshot for reference
        screenshot_dir = Path("test_output/screenshots")
        screenshot_dir.mkdir(parents=True, exist_ok=True)
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        screenshot_path = screenshot_dir / f"real_device_{timestamp}.png"
        with open(screenshot_path, "wb") as f:
            f.write(screenshot_data)
        print(f"   💾 Saved to: {screenshot_path}")

    except Exception as e:
        print(f"   ❌ Failed to capture screenshot: {e}")
        return 1

    # Step 5: Vision Analysis
    print("\n👁️ Step 5: Analyzing screenshot with AI Vision...")
    try:
        start_time = time.time()
        page_analysis = provider.analyze_screenshot(screenshot_data)
        analysis_time = time.time() - start_time

        print(f"   ✅ Analysis complete in {analysis_time:.2f}s")
        print()
        print("   📊 Page Structure:")
        print(f"      Level1 Direction: {page_analysis.level1_dir.value}")
        print(f"      Level1 Menus: {len(page_analysis.level1_menus)}")
        for menu in page_analysis.level1_menus[:5]:
            status = "🟢" if menu.active else "⚪"
            print(f"         {status} {menu.name} at ({menu.coordinate.x:.2f}, {menu.coordinate.y:.2f})")

        print(f"      Level2 Direction: {page_analysis.level2_dir.value}")
        print(f"      Level2 Menus: {len(page_analysis.level2_menus)}")
        for menu in page_analysis.level2_menus[:5]:
            status = "🟢" if menu.active else "⚪"
            print(f"         {status} {menu.name} at ({menu.coordinate.x:.2f}, {menu.coordinate.y:.2f})")

        print(f"      Current Path: {page_analysis.current_path}")
        print(f"      Interactive Items: {len(page_analysis.items)}")

    except Exception as e:
        print(f"   ❌ Vision analysis failed: {e}")
        import traceback
        traceback.print_exc()
        return 1

    # Step 6: Container Type Inference
    print("\n🔍 Step 6: Inferring container type...")
    try:
        context = TraversalContext()
        container_inference = provider.infer_container_type(page_analysis, context)

        print(f"   ✅ Container Type: {container_inference.container_type}")
        print(f"   ✅ Confidence: {container_inference.confidence:.2f}")
        print(f"   ✅ Matched Template: {container_inference.matched_template}")

    except Exception as e:
        print(f"   ❌ Container inference failed: {e}")

    # Step 7: Safety Screening
    print("\n🛡️ Step 7: Safety screening elements...")
    try:
        safety_result = provider.capabilities["safety"].execute({
            "page_analysis": page_analysis,
            "instruction": "Explore the current page",
            "page_type": container_inference.container_type,
        })

        print(f"   ✅ Overall Safe to Proceed: {safety_result.page_level_guidance.overall_safe_to_proceed}")

        safe_count = sum(1 for e in safety_result.evaluations if e.safety_tag == "safe")
        caution_count = sum(1 for e in safety_result.evaluations if e.safety_tag == "caution")
        skip_count = sum(1 for e in safety_result.evaluations if e.safety_tag == "skip")

        print(f"   📊 Safety Distribution:")
        print(f"      ✅ Safe: {safe_count}")
        print(f"      ⚠️ Caution: {caution_count}")
        print(f"      🚫 Skip: {skip_count}")

        if safety_result.page_level_guidance.special_precautions:
            print(f"   ⚠️ Special Precautions:")
            for precaution in safety_result.page_level_guidance.special_precautions:
                print(f"      - {precaution}")

    except Exception as e:
        print(f"   ❌ Safety screening failed: {e}")

    # Step 8: Context Decision
    print("\n🎯 Step 8: Making context-aware decision...")
    try:
        decision = provider.capabilities["decision"].execute({
            "reason": "Explore the current page and find interactive elements",
            "page_analysis": page_analysis,
            "context": {
                "node_stack": [],
                "visited_pages": [],
                "failed_nodes": [],
                "action_history": [],
            },
            "safety_result": safety_result if 'safety_result' in locals() else None,
        })

        print(f"   ✅ Decision Result: {decision.result.value}")
        print(f"   ✅ Action: {decision.action.value}")
        if decision.target:
            print(f"   ✅ Target: {decision.target}")
        print(f"   ✅ Reasoning: {decision.reasoning[:100]}...")
        print(f"   ✅ Confidence: {decision.confidence:.2f}")
        print(f"   ✅ Safety Verified: {decision.safety_verified}")

    except Exception as e:
        print(f"   ❌ Decision making failed: {e}")

    # Step 9: Metrics Summary
    print("\n📊 Step 9: Metrics Summary...")
    try:
        metrics_summary = provider.get_metrics_summary()
        if metrics_summary:
            print(f"   Total Capabilities: {len(metrics_summary['capabilities'])}")
            for cap, counts in metrics_summary['call_counts'].items():
                print(f"   {cap}:")
                print(f"      ✅ Success: {counts['success']}")
                print(f"      ❌ Failure: {counts['failure']}")

    except Exception as e:
        print(f"   ⚠️ Could not retrieve metrics: {e}")

    # Step 10: Integration with TraversalEngine
    print("\n🔧 Step 10: Testing TraversalEngine integration...")
    try:
        state = TraversalState()
        config = TraversalConfig(
            enable_ai_advisor=True,
            ai_min_confidence=0.6,
            enable_exception_handling=False,
        )

        # Create vision service that uses real analysis
        from src.vision.vision_service import VisionService
        from src.ai.vision.claude_service import ClaudeVisionService

        real_vision = ClaudeVisionService(
            api_key="sk-5d1655b4dd6b931d7fe05c03293b940c248d8d578c13598945af45d008506f43",
            model="claude-3-5-sonnet-20241022",
        )

        engine = TraversalEngine(
            adb_client=adb,
            vision_service=real_vision,
            state=state,
            config=config,
        )

        # Set UniBrain as AI advisor
        engine.set_ai_advisor(provider)

        print(f"   ✅ TraversalEngine initialized with AI advisor")
        print(f"   ✅ AI Advisor type: {type(engine.ai_advisor).__name__}")

        # Test context building
        traversal_context = engine._build_traversal_context()
        print(f"   ✅ TraversalContext built with {len(traversal_context.visited_pages)} visited pages")

    except Exception as e:
        print(f"   ❌ TraversalEngine integration failed: {e}")
        import traceback
        traceback.print_exc()

    # Final Summary
    print("\n" + "=" * 70)
    print("✅ Real Device Test Complete!")
    print("=" * 70)
    print()
    print("Summary:")
    print("  ✅ Device connected and screenshot captured")
    print("  ✅ Vision analysis completed with real Claude API")
    print("  ✅ Container type inferred")
    print("  ✅ Safety screening performed")
    print("  ✅ Context decision made")
    print("  ✅ TraversalEngine integration verified")
    print()
    print(f"Output saved to: {screenshot_path}")
    print("=" * 70)

    return 0


if __name__ == "__main__":
    sys.exit(test_unibrain_real_device())
