#!/usr/bin/env python3
"""
覆盖率检查脚本

检查模块测试覆盖率是否达到要求，提供改进建议。
"""

import argparse
import subprocess
import sys
import json
from pathlib import Path
from typing import Dict, List, Any, Optional


class CoverageChecker:
    """覆盖率检查器"""

    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.threshold = 80  # 默认覆盖率阈值

    def check_coverage(self, modules: List[str]) -> Dict[str, Any]:
        """检查指定模块的覆盖率"""
        print(f"📈 检查模块覆盖率: {', '.join(modules)}")

        results = {}

        for module in modules:
            result = self._check_single_module_coverage(module)
            results[module] = result

        return self._generate_summary(results)

    def _check_single_module_coverage(self, module: str) -> Dict[str, Any]:
        """检查单个模块的覆盖率"""
        print(f"\n=== {module} 覆盖率检查 ===")

        try:
            test_path = self._find_test_path(module)
            if not test_path:
                return {
                    'module': module,
                    'status': 'skipped',
                    'reason': 'no_tests_found'
                }

            # 运行覆盖率测试
            cmd = [
                sys.executable, '-m', 'pytest', str(test_path),
                '--cov', f'src.{module}',
                '--cov-report', 'term',
                '--cov-report', 'json'
            ]

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            # 解析覆盖率
            coverage_data = self._parse_coverage_report(result.stdout, result.stderr)

            # 评估覆盖率
            assessment = self._assess_coverage(coverage_data)

            print(f"  覆盖率: {coverage_data.get('percent', 0):.1f}%")
            print(f"  阈值: {self.threshold}%")
            print(f"  状态: {'✅ 达标' if assessment['meets_threshold'] else '❌ 不足'}")

            return {
                'module': module,
                'coverage': coverage_data,
                'assessment': assessment
            }

        except Exception as e:
            return {
                'module': module,
                'status': 'error',
                'error': str(e)
            }

    def _find_test_path(self, module: str) -> Optional[Path]:
        """查找模块的测试路径"""
        possible_paths = [
            self.project_root / f'src/{module}/test',
            self.project_root / f'tests/{module}',
            self.project_root / f'test/{module}',
            self.project_root / f'src/{module}/tests',
        ]

        for path in possible_paths:
            if path.exists() and any(path.glob('test_*.py')):
                return path

        return None

    def _parse_coverage_report(self, stdout: str, stderr: str) -> Dict[str, Any]:
        """解析覆盖率报告"""
        # 尝试从JSON文件读取
        coverage_file = self.project_root / 'coverage.json'

        if coverage_file.exists():
            try:
                with open(coverage_file) as f:
                    return json.load(f)
            except:
                pass

        # 从stdout解析
        coverage_data = {}
        lines = stdout.split('\n')

        for line in lines:
            # 匹配类似 "TOTAL 100 50 50%" 的行
            match = re.search(r'TOTAL\s+(\d+)\s+(\d+)\s+(\d+)%', line)
            if match:
                coverage_data = {
                    'total_statements': int(match.group(1)),
                    'covered_statements': int(match.group(2)),
                    'percent_covered': float(match.group(3))
                }
                break

        return coverage_data

    def _assess_coverage(self, coverage_data: Dict[str, Any]) -> Dict[str, Any]:
        """评估覆盖率"""
        percent = coverage_data.get('percent_covered', 0)

        meets_threshold = percent >= self.threshold
        status = 'passed' if meets_threshold else 'failed'

        # 计算差距
        gap = max(0, self.threshold - percent)

        # 生成建议
        suggestions = []
        if not meets_threshold:
            suggestions.append(f"需要提升 {gap:.1f}% 覆盖率")

        if gap > 20:
            suggestions.append("覆盖率差距较大，建议优先补充关键路径测试")
        elif gap > 10:
            suggestions.append("建议补充边界条件和异常处理测试")

        return {
            'meets_threshold': meets_threshold,
            'threshold': self.threshold,
            'gap': gap,
            'status': status,
            'suggestions': suggestions
        }

    def _generate_summary(self, results: Dict[str, Any]) -> Dict[str, Any]:
        """生成覆盖率检查汇总"""
        total_modules = len(results)
        passed_modules = sum(1 for r in results.values()
                            if r.get('assessment', {}).get('meets_threshold', False))
        failed_modules = sum(1 for r in results.values()
                            if r.get('assessment', {}).get('status') == 'failed')

        return {
            'total_modules': total_modules,
            'passed_modules': passed_modules,
            'failed_modules': failed_modules,
            'overall_status': 'passed' if failed_modules == 0 else 'failed',
            'details': results
        }


def main():
    """主函数"""
    parser = argparse.ArgumentParser(
        description="覆盖率检查脚本",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument('modules', nargs='+', help='要检查覆盖率的模块列表')
    parser.add_argument('--threshold', type=int, default=80, help='覆盖率阈值（默认80）')
    parser.add_argument('--verbose', action='store_true', help='详细输出')

    args = parser.parse_args()

    checker = CoverageChecker()
    checker.threshold = args.threshold

    results = checker.check_coverage(args.modules)

    print(f"\n{'='*70}")
    print("📊 覆盖率检查汇总")
    print('='*70)
    print(f"总模块数: {results['total_modules']}")
    print(f"达标模块: {results['passed_modules']}")
    print(f"不达标模块: {results['failed_modules']}")
    print(f"整体状态: {'✅ 通过' if results['overall_status'] == 'passed' else '❌ 失败'}")

    if args.verbose:
        print("\n📋 详细结果:")
        for module, result in results['details'].items():
            if 'assessment' in result:
                assessment = result['assessment']
                print(f"\n{module}:")
                print(f"  覆盖率: {result['coverage'].get('percent_covered', 0):.1f}%")
                print(f"  阈值: {assessment['threshold']}%")
                print(f"  状态: {assessment['status']}")
                if assessment.get('suggestions'):
                    print("  建议:")
                    for suggestion in assessment['suggestions']:
                        print(f"    - {suggestion}")

    if results['failed_modules'] > 0:
        print(f"\n💡 提示: 可以使用以下命令查看详细的覆盖率报告:")
        print("python -m pytest src/<module>/test/ --cov=src.<module> --cov-report=html")
        print("然后在 htmlcov/index.html 查看详细报告")


if __name__ == "__main__":
    main()