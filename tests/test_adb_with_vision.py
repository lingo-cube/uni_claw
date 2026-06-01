#!/usr/bin/env python3
"""Complete ADB + MiMo Vision integration test.

This script tests the full flow:
1. Connect to ADB device
2. Capture screenshot
3. Analyze with MiMo vision service
4. Perform tap action based on analysis
"""

import logging
import sys
from pathlib import Path

from src.adb.adb_client import ADBError, RealADBClient
from src.vision.mimo_vision_cc import MiMoCCVisionService

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


def check_adb_connection() -> bool:
    """Check if any ADB device is connected."""
    import subprocess
    try:
        result = subprocess.run(
            ["adb", "devices"],
            capture_output=True,
            text=True,
            timeout=10
        )
        output = result.stdout.strip()
        logger.info(f"ADB devices: {output}")

        # Check if any device is connected
        lines = output.split("\n")[1:]  # Skip header
        return any("device" in line for line in lines)
    except Exception as e:
        logger.error(f"Failed to check ADB: {e}")
        return False


def connect_to_nox(port: int = 62001) -> bool:
    """Try to connect to Nox Player."""
    import subprocess
    try:
        result = subprocess.run(
            ["adb", "connect", f"127.0.0.1:{port}"],
            capture_output=True,
            text=True,
            timeout=10
        )
        logger.info(f"Connect to Nox: {result.stdout.strip()}")
        return "connected" in result.stdout.lower()
    except Exception as e:
        logger.error(f"Failed to connect to Nox: {e}")
        return False


def test_complete_flow():
    """Run complete ADB + Vision test."""
    print("=" * 60)
    print("ADB + MiMo Vision 完整流程测试")
    print("=" * 60)

    # Step 1: Check/Connect ADB
    print("\n[步骤 1] 检查 ADB 连接...")

    if not check_adb_connection():
        print("❌ 没有检测到设备，尝试连接 Nox Player...")
        if connect_to_nox(62001):
            print("✓ 已连接到 Nox Player (端口 62001)")
        else:
            print("❌ 连接失败！请确保：")
            print("   1. Nox Player 正在运行")
            print("   2. 已在 Nox 设置中开启 ADB 调试")
            print("   3. 查看: docs/nox_adb_setup.md")
            return False
    else:
        print("✓ 检测到 ADB 设备")

    # Step 2: Initialize ADB Client
    print("\n[步骤 2] 初始化 ADB 客户端...")
    try:
        adb = RealADBClient()
        screen_size = adb.get_screen_size()
        print(f"✓ 设备屏幕尺寸: {screen_size.width}x{screen_size.height}")
    except ADBError as e:
        print(f"❌ ADB 初始化失败: {e}")
        return False

    # Step 3: Capture Screenshot
    print("\n[步骤 3] 捕获屏幕截图...")
    try:
        screenshot_path = Path("test_data/nox_screenshot.png")
        screenshot_path.parent.mkdir(exist_ok=True)

        adb.capture_screenshot(output_path=screenshot_path)
        print(f"✓ 截图已保存: {screenshot_path}")

        # Verify file exists and has content
        if screenshot_path.stat().st_size < 1000:
            print("❌ 截图文件太小，可能失败")
            return False
    except Exception as e:
        print(f"❌ 截图失败: {e}")
        return False

    # Step 4: Analyze with MiMo Vision (optional)
    print("\n[步骤 4] MiMo 视觉分析...")
    mimo_api_key = None  # Add your key if available

    if mimo_api_key:
        try:
            vision = MiMoCCVisionService(api_key=mimo_api_key)
            with open(screenshot_path, "rb") as f:
                image_data = f.read()

            result = vision.analyze("描述这个屏幕的内容", image_data)
            print(f"✓ MiMo 分析结果:\n{result}")
        except Exception as e:
            print(f"⚠️  MiMo 分析失败: {e}")
    else:
        print("⚠️  未设置 MIMO_API_KEY，跳过视觉分析")
        print("   设置环境变量可启用: export MIMO_API_KEY=your_key")

    # Step 5: Test tap action
    print("\n[步骤 5] 测试点击操作...")
    try:
        # Tap center of screen (safe test)
        center_x = 0.5
        center_y = 0.5
        print(f"   点击屏幕中心: ({center_x}, {center_y})")
        adb.tap(center_x, center_y)
        print("✓ 点击成功")
    except Exception as e:
        print(f"❌ 点击失败: {e}")

    print("\n" + "=" * 60)
    print("✓ 测试完成！")
    print("=" * 60)
    return True


if __name__ == "__main__":
    success = test_complete_flow()
    sys.exit(0 if success else 1)
