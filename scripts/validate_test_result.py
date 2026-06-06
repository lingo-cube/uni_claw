#!/usr/bin/env python3
"""
测试结果JSON结构验证工具（可选）

验证test_results/{module}_unit.json文件是否符合最小契约定义。
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Dict, Any


def validate_json_structure(json_file: Path) -> bool:
    """验证JSON结构符合契约

    Args:
        json_file: JSON文件路径

    Returns:
        True if valid, False otherwise
    """
    try:
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
    except Exception as e:
        print(f"❌ JSON解析失败 {json_file}: {e}")
        return False

    # 必需字段检查
    required_fields = ['module', 'timestamp', 'summary', 'failures']
    for field in required_fields:
        if field not in data:
            print(f"❌ 缺少必需字段: {field}")
            return False

    # summary字段检查
    summary = data['summary']
    required_summary = ['total', 'passed', 'failed', 'error', 'skipped']
    for field in required_summary:
        if field not in summary:
            print(f"❌ summary缺少字段: {field}")
            return False

    # 数据一致性检查
    if summary['total'] != (summary['passed'] + summary['failed'] +
                             summary['error'] + summary['skipped']):
        print(f"❌ 统计不一致")
        return False

    # failures数组一致性检查
    if (summary['failed'] + summary['error']) == 0 and len(data['failures']) != 0:
        print(f"❌ failures应为空数组")
        return False

    print(f"✅ 结构验证通过: {json_file.name}")
    return True


def main():
    parser = argparse.ArgumentParser(description="验证测试结果JSON结构")
    parser.add_argument("module", nargs='?', help="模块名称（可选）")

    args = parser.parse_args()

    results_dir = Path.cwd() / "test_results"

    if not results_dir.exists():
        print(f"❌ 测试结果目录不存在: {results_dir}")
        return 1

    # 查找JSON文件
    if args.module:
        json_files = [results_dir / f"{args.module}_unit.json"]
    else:
        json_files = list(results_dir.glob("*_unit.json"))

    if not json_files:
        print(f"❌ 未找到测试结果文件")
        return 1

    # 验证文件
    all_valid = all(validate_json_structure(f) for f in json_files)

    if all_valid:
        print(f"\n✅ 所有{len(json_files)}个文件验证通过")
        return 0
    else:
        print("\n❌ 验证失败")
        return 1


if __name__ == "__main__":
    sys.exit(main())
