#!/usr/bin/env python3
"""
E2E仿真测试一键运行脚本
支持多种模式：simple, detailed, clean
"""
import sys
import argparse
from pathlib import Path

# 添加项目路径
sys.path.insert(0, str(Path(__file__).parent))

def run_simple_mode():
    """简单模式 - 快速执行"""
    print("[MODE] Running in SIMPLE mode...")
    from run_e2e_simple import main as simple_main
    return simple_main()

def run_detailed_mode():
    """详细模式 - 完整输入输出"""
    print("[MODE] Running in DETAILED mode...")
    from run_e2e_detailed import main as detailed_main
    return detailed_main()

def run_clean_mode():
    """干净模式 - 无特殊字符"""
    print("[MODE] Running in CLEAN mode...")
    from run_e2e_clean import main as clean_main
    return clean_main()

def main():
    """主函数"""
    parser = argparse.ArgumentParser(
        description='E2E Simulation Test Runner',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python run_e2e.py              # Default: clean mode
  python run_e2e.py simple       # Simple mode with emoji
  python run_e2e.py detailed     # Detailed mode with full I/O
  python run_e2e.py clean        # Clean mode without special chars

Modes:
  simple   - Fast execution with emoji display
  detailed - Full input/output display for debugging
  clean    - No special characters, maximum compatibility
        """
    )

    parser.add_argument(
        'mode',
        nargs='?',
        default='clean',
        choices=['simple', 'detailed', 'clean'],
        help='Execution mode (default: clean)'
    )

    parser.add_argument(
        '-v', '--version',
        action='version',
        version='E2E Test Runner v1.0'
    )

    args = parser.parse_args()

    print("=" * 70)
    print("E2E Simulation Test Runner")
    print("=" * 70)
    print(f"Mode: {args.mode.upper()}")
    print(f"Python: {sys.version.split()[0]}")
    print("=" * 70)
    print()

    # 根据模式执行
    try:
        if args.mode == 'simple':
            exit_code = run_simple_mode()
        elif args.mode == 'detailed':
            exit_code = run_detailed_mode()
        elif args.mode == 'clean':
            exit_code = run_clean_mode()
        else:
            print(f"[ERROR] Unknown mode: {args.mode}")
            return 2

        return exit_code

    except Exception as e:
        print(f"[ERROR] Execution failed: {e}")
        import traceback
        traceback.print_exc()
        return 2

if __name__ == "__main__":
    exit_code = main()
    print(f"\n[RESULT] Exit Code: {exit_code}")

    # 提供退出码含义
    if exit_code == 0:
        print("[SUCCESS] All tests passed!")
    elif exit_code == 1:
        print("[FAILURE] Tests failed - check output above")
    elif exit_code == 2:
        print("[ERROR] Execution error - check environment")

    sys.exit(exit_code)