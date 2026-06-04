#!/usr/bin/env python3
"""
测试执行管理脚本

统一管理模块测试的执行、依赖分析、环境准备等功能。
"""

import argparse
import subprocess
import sys
import re
from pathlib import Path
from typing import Dict, List, Any, Optional, Set
import json


class TestRunner:
    """测试执行管理器"""

    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.config = self._load_config()
        self.test_results = {}

    def run_tests(self, modules: List[str]) -> Dict[str, Any]:
        """运行指定模块的测试"""
        print(f"🧪 运行模块测试: {', '.join(modules)}")

        # 环境准备
        self._prepare_environment()

        # 扩展测试范围（基于依赖关系）
        extended_modules = self._expand_test_scope(modules)

        # 执行测试
        for module in extended_modules:
            print(f"\n=== 测试模块: {module} ===")
            result = self._run_single_module(module)
            self.test_results[module] = result

        # 生成汇总报告
        return self._generate_summary()

    def _load_config(self) -> Dict[str, Any]:
        """加载测试配置"""
        config_file = self.project_root / ".test-config.yaml"

        if config_file.exists():
            try:
                import yaml
                with open(config_file, 'r') as f:
                    return yaml.safe_load(f)
            except:
                print("⚠️  YAML解析失败，使用默认配置")
                return self._get_default_config()
        else:
            return self._get_default_config()

    def _get_default_config(self) -> Dict[str, Any]:
        """获取默认配置"""
        return {
            'test_runner': 'auto',
            'coverage_threshold': 80,
            'parallel_execution': False,
            'flaky_tests': {'reruns': 0},
            'dependencies': []
        }

    def _prepare_environment(self):
        """准备测试环境"""
        print("🧹 准备测试环境...")

        # 清理缓存
        self._cleanup_cache()

        # 检查依赖
        self._check_dependencies()

        # 检查外部服务
        self._check_external_services()

        print("✅ 环境准备完成")

    def _cleanup_cache(self):
        """清理测试缓存"""
        print("  🗑️  清理测试缓存...")

        cache_dirs = ['__pycache__', '.pytest_cache', '.test_cache', '*.egg-info']
        cache_files = ['*.pyc', '.coverage']

        for pattern in cache_dirs:
            try:
                subprocess.run(
                    ['find', '.', '-type', 'd', '-name', pattern, '-exec', 'rm', '-rf', '{}', '+'],
                    capture_output=True,
                    cwd=self.project_root
                )
            except:
                pass

        for pattern in cache_files:
            try:
                subprocess.run(
                    ['find', '.', '-name', pattern, '-delete'],
                    capture_output=True,
                    cwd=self.project_root
                )
            except:
                pass

    def _check_dependencies(self):
        """检查测试依赖"""
        print("  📦 检查测试依赖...")

        # 检查pytest
        try:
            result = subprocess.run(
                ['python', '-c', 'import pytest; print(pytest.__version__)'],
                capture_output=True,
                text=True
            )
            if result.returncode == 0:
                print(f"    ✅ pytest {result.stdout.strip()}")
        except:
            print("    ⚠️  pytest未安装")

        # 检查其他测试工具
        for tool in ['pytest-cov', 'pytest-xdist', 'pytest-rerunfailures']:
            try:
                subprocess.run(
                    ['python', '-c', f'import {tool}; print("installed")'],
                    capture_output=True,
                    text=True
                )
                print(f"    ✅ {tool}")
            except:
                print(f"    ⚠️  {tool} 未安装（可选）")

    def _check_external_services(self):
        """检查外部服务依赖"""
        print("  🌐 检查外部服务依赖...")

        # 扫描代码中的外部服务使用
        src_files = list(self.project_root.glob('src/**/*.py'))

        external_services = set()
        for file in src_files:
            try:
                content = file.read_text()
                if 'requests.' in content or 'urllib.' in content or 'http.' in content:
                    external_services.add('network')
                if 'sqlite3.connect' in content or 'psycopg2' in content or 'pymongo' in content:
                    external_services.add('database')
            except:
                pass

        if external_services:
            print(f"    ⚠️  发现外部服务依赖: {', '.join(external_services)}")
            print("    💡 建议使用mock或fixture隔离外部服务")
        else:
            print("    ✅ 无外部服务依赖")

    def _expand_test_scope(self, modules: List[str]) -> List[str]:
        """扩展测试范围（基于依赖关系）"""
        print("🔗 分析模块依赖关系...")

        # 从配置文件读取依赖关系
        config_deps = self.config.get('dependencies', [])

        # 检测import依赖关系
        detected_deps = self._detect_import_dependencies(modules)

        # 合并依赖关系
        all_modules = set(modules)

        for dep_spec in config_deps:
            if '->' in dep_spec:
                source, target = dep_spec.split('->')
                source = source.strip()
                target = target.strip()

                if source in modules:
                    all_modules.add(target)

        # 添加检测到的依赖
        for dep in detected_deps:
            all_modules.add(dep)

        if all_modules != set(modules):
            print(f"  📋 扩展测试范围: {', '.join(all_modules - set(modules))}")

        return sorted(list(all_modules))

    def _detect_import_dependencies(self, modules: List[str]) -> List[str]:
        """检测import依赖关系"""
        dependencies = set()

        for module in modules:
            # 查找import此模块的其他模块
            module_path = self.project_root / 'src' / module
            if not module_path.exists():
                continue

            try:
                result = subprocess.run(
                    ['grep', '-r', f'from.*{module}\\s*import\\|import.*{module}',
                     f'{self.project_root}/src/*/', '--include=*.py'],
                    capture_output=True,
                    text=True
                )

                if result.stdout:
                    matches = result.stdout.strip().split('\n')
                    for match in matches:
                        # 提取模块名
                        import_match = re.search(r'src/(\w+)/', match)
                        if import_match:
                            dep_module = import_match.group(1)
                            if dep_module != module:
                                dependencies.add(dep_module)
            except:
                pass

        return list(dependencies)

    def _run_single_module(self, module: str) -> Dict[str, Any]:
        """运行单个模块的测试"""
        try:
            test_path = self._find_test_path(module)

            if not test_path:
                return {
                    'module': module,
                    'status': 'skipped',
                    'reason': 'no_tests_found'
                }

            # 检测测试框架
            test_framework = self._detect_test_framework()

            # 构建测试命令
            cmd = self._build_test_command(test_framework, test_path)

            print(f"  🔧 使用框架: {test_framework}")
            print(f"  📋 测试路径: {test_path}")

            # 执行测试
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            # 解析结果
            test_result = self._parse_test_result(result.stdout, result.stderr, module)

            # 检查覆盖率
            if self.config.get('coverage', {}).get('enabled', False):
                coverage_result = self._check_coverage(module, test_path)
                test_result['coverage'] = coverage_result

            return test_result

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

    def _detect_test_framework(self) -> str:
        """检测测试框架"""
        config = self.config.get('test_runner', 'auto')

        if config != 'auto':
            return config

        # 自动检测
        # 检查pytest
        if (self.project_root / 'pytest.ini').exists() or \
           (self.project_root / 'setup.cfg').exists() or \
           (self.project_root / 'pyproject.toml').exists():
            return 'pytest'

        # 检查unittest
        test_files = list(self.project_root.glob('**/test_*.py'))
        for file in test_files:
            try:
                content = file.read_text()
                if 'import unittest' in content:
                    return 'unittest'
            except:
                pass

        # 默认使用pytest
        return 'pytest'

    def _build_test_command(self, framework: str, test_path: Path) -> List[str]:
        """构建测试命令"""
        if framework == 'pytest':
            cmd = [sys.executable, '-m', 'pytest', str(test_path), '-v', '--tb=short']

            # 添加覆盖率
            if self.config.get('coverage', {}).get('enabled', False):
                cmd.extend(['--cov', f'src.{test_path.parent.parent.name}'])
                cmd.extend(['--cov-report', 'term-missing'])

            # 添加并行执行
            if self.config.get('parallel_execution', False):
                cmd.extend(['-n', 'auto'])

            # 添加重试
            flaky_config = self.config.get('flaky_tests', {})
            if flaky_config.get('reruns', 0) > 0:
                cmd.extend(['--reruns', str(flaky_config['reruns'])])

            return cmd

        elif framework == 'unittest':
            return [sys.executable, '-m', 'unittest', 'discover', '-s', str(test_path), '-v']

        else:
            return [sys.executable, '-m', 'pytest', str(test_path), '-v']

    def _parse_test_result(self, stdout: str, stderr: str, module: str) -> Dict[str, Any]:
        """解析测试结果"""
        lines = stdout.split('\n')

        # 统计测试结果
        passed = sum(1 for line in lines if 'PASSED' in line)
        failed = sum(1 for line in lines if 'FAILED' in line)
        errors = sum(1 for line in lines if 'ERRORS' in line or 'ERROR' in line)

        total = passed + failed + errors

        if failed > 0 or errors > 0:
            status = 'failed'
        elif passed == 0 and total == 0:
            status = 'no_tests'
        else:
            status = 'passed'

        return {
            'module': module,
            'status': status,
            'summary': {
                'total': total,
                'passed': passed,
                'failed': failed,
                'errors': errors,
                'skipped': sum(1 for line in lines if 'SKIPPED' in line)
            }
        }

    def _check_coverage(self, module: str, test_path: Path) -> Dict[str, Any]:
        """检查测试覆盖率"""
        try:
            cmd = [sys.executable, '-m', 'pytest', str(test_path),
                   '--cov', f'src.{module}',
                   '--cov-report', 'term',
                   '--cov-report', 'json',
                   '--cov-report', 'json']

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            # 解析覆盖率
            coverage_file = self.project_root / 'coverage.json'
            if coverage_file.exists():
                with open(coverage_file) as f:
                    coverage_data = json.load(f)

                percent = coverage_data.get('totals', {}).get('percent_covered', 0)
                threshold = self.config.get('coverage', {}).get('threshold', 80)

                return {
                    'percent': percent,
                    'threshold': threshold,
                    'meets_threshold': percent >= threshold,
                    'status': 'passed' if percent >= threshold else 'failed'
                }

        except Exception as e:
            return {
                'status': 'error',
                'error': str(e)
            }

        return {'status': 'skipped'}

    def _generate_summary(self) -> Dict[str, Any]:
        """生成测试汇总报告"""
        total_modules = len(self.test_results)
        passed_modules = sum(1 for r in self.test_results.values() if r.get('status') == 'passed')
        failed_modules = sum(1 for r in self.test_results.values() if r.get('status') == 'failed')
        skipped_modules = sum(1 for r in self.test_results.values() if r.get('status') in ['skipped', 'no_tests'])

        return {
            'total_modules': total_modules,
            'passed_modules': passed_modules,
            'failed_modules': failed_modules,
            'skipped_modules': skipped_modules,
            'details': self.test_results
        }


def main():
    """主函数"""
    parser = argparse.ArgumentParser(
        description="测试执行管理脚本",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument('modules', nargs='+', help='要测试的模块列表')
    parser.add_argument('--config', help='配置文件路径（可选）')

    args = parser.parse_args()

    runner = TestRunner()

    results = runner.run_tests(args.modules)

    print(f"\n{'='*70}")
    print("📊 测试执行汇总")
    print('='*70)
    print(f"总模块数: {results['total_modules']}")
    print(f"通过: {results['passed_modules']}")
    print(f"失败: {results['failed_modules']}")
    print(f"跳过: {results['skipped_modules']}")

    if results['failed_modules'] > 0:
        print("\n❌ 有模块测试失败，建议使用诊断脚本:")
        for module, result in results['details'].items():
            if result.get('status') == 'failed':
                print(f"  python scripts/test_diagnostic.py --module {module}")

    print(f"\n📝 结果已记录，可用于进一步分析")


if __name__ == "__main__":
    main()