#!/usr/bin/env python3
"""
测试失败诊断脚本

根据测试失败信息进行智能诊断，提供处理建议。
"""

import argparse
import subprocess
import sys
import re
from pathlib import Path
from typing import Dict, List, Any, Optional


class TestDiagnostic:
    """测试失败诊断器"""

    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.design_docs = {}

    def diagnose_failure(self, module: str, test_name: str = None) -> Dict[str, Any]:
        """诊断测试失败"""
        print(f"🔍 诊断 {module} 模块的测试失败...")

        # 获取测试输出
        test_output = self._get_test_output(module)

        if test_name:
            return self._diagnose_specific_test(module, test_name, test_output)
        else:
            return self._diagnose_all_failures(module, test_output)

    def _get_test_output(self, module: str) -> str:
        """获取测试输出"""
        # 重新运行测试获取最新输出
        test_path = self._find_test_path(module)

        if not test_path:
            return f"未找到 {module} 模块的测试目录"

        try:
            result = subprocess.run(
                [sys.executable, "-m", "pytest", str(test_path), "-v", "--tb=short"],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )
            return result.stdout + result.stderr
        except Exception as e:
            return f"获取测试输出失败: {e}"

    def _find_test_path(self, module: str) -> Optional[Path]:
        """查找模块的测试路径"""
        possible_paths = [
            self.project_root / f"src/{module}/test",
            self.project_root / f"tests/{module}",
            self.project_root / f"test/{module}",
            self.project_root / f"src/{module}/tests",
        ]

        for path in possible_paths:
            if path.exists() and any(path.glob("test_*.py")):
                return path

        return None

    def _diagnose_specific_test(self, module: str, test_name: str, test_output: str) -> Dict[str, Any]:
        """诊断特定测试失败"""
        print(f"  📋 分析测试: {test_name}")

        # 提取错误信息
        error_info = self._extract_error_info(test_output, test_name)

        if not error_info:
            return {
                "status": "no_error_found",
                "message": f"未找到 {test_name} 的失败信息"
            }

        print(f"  ❌ 错误类型: {error_info['type']}")
        print(f"  📄 错误位置: {error_info['location']}")

        # 根据错误类型进行诊断
        diagnosis = self._analyze_error_type(module, test_name, error_info)

        return {
            "module": module,
            "test_name": test_name,
            "error_info": error_info,
            "diagnosis": diagnosis,
            "recommendations": self._generate_recommendations(diagnosis)
        }

    def _diagnose_all_failures(self, module: str, test_output: str) -> Dict[str, Any]:
        """诊断所有测试失败"""
        failed_tests = self._extract_failed_tests(test_output)

        if not failed_tests:
            return {
                "status": "no_failures",
                "message": f"{module} 模块无测试失败"
            }

        all_diagnoses = []

        for test_name in failed_tests:
            diagnosis = self._diagnose_specific_test(module, test_name, test_output)
            all_diagnoses.append(diagnosis)

        return {
            "module": module,
            "failed_count": len(failed_tests),
            "diagnoses": all_diagnoses
        }

    def _extract_failed_tests(self, test_output: str) -> List[str]:
        """提取失败的测试名称"""
        # 匹配类似 "test_xxx FAILED" 的行
        pattern = r'(\w+)\s+FAILED'
        matches = re.findall(pattern, test_output)
        return list(set(matches))

    def _extract_error_info(self, test_output: str, test_name: str) -> Optional[Dict[str, str]]:
        """提取错误信息"""
        # 查找测试失败相关的错误信息
        lines = test_output.split('\n')

        error_found = False
        error_info = None

        for i, line in enumerate(lines):
            if test_name in line and 'FAILED' in line:
                # 从这里开始收集错误信息
                error_lines = []
                for j in range(i, min(i + 20, len(lines))):
                    error_lines.append(lines[j])
                    if 'AssertionError' in lines[j] or 'Error' in lines[j]:
                        break

                error_text = '\n'.join(error_lines)

                # 解析错误类型
                error_type = self._parse_error_type(error_text)
                error_location = self._parse_error_location(error_text)

                error_info = {
                    'type': error_type,
                    'location': error_location,
                    'text': error_text
                }
                error_found = True
                break

        return error_info if error_found else None

    def _parse_error_type(self, error_text: str) -> str:
        """解析错误类型"""
        # 常见错误类型
        error_types = [
            'AssertionError', 'ValueError', 'TypeError', 'AttributeError',
            'KeyError', 'ImportError', 'ModuleNotFoundError',
            'FileNotFoundError', 'TimeoutError', 'RuntimeError'
        ]

        for error_type in error_types:
            if error_type in error_text:
                return error_type

        return 'UnknownError'

    def _parse_error_location(self, error_text: str) -> str:
        """解析错误位置"""
        # 查找文件:行号格式
        match = re.search(r'(\w+\.py):\d+', error_text)
        if match:
            return match.group(1)

        return "未知位置"

    def _analyze_error_type(self, module: str, test_name: str, error_info: Dict[str, str]) -> Dict[str, Any]:
        """分析错误类型并提供建议"""
        error_type = error_info['type']

        analysis = {
            'error_type': error_type,
            'possible_causes': [],
            'recommended_checks': [],
            'priority_level': self._get_priority_level(error_type)
        }

        # 根据错误类型提供具体的分析
        if error_type == 'ImportError':
            analysis['possible_causes'] = [
                "测试依赖未安装",
                "模块路径配置问题",
                "Python路径环境变量问题"
            ]
            analysis['recommended_checks'] = [
                "检查requirements.txt或requirements-dev.txt",
                "验证模块路径是否正确",
                "确认虚拟环境是否激活"
            ]

        elif error_type == 'AssertionError':
            analysis['possible_causes'] = [
                "代码实现逻辑错误",
                "边界条件处理不当",
                "状态机状态错误",
                "测试期望值可能需要更新"
            ]
            analysis['recommended_checks'] = [
                "检查相关代码实现",
                "验证边界条件处理",
                "对比设计文档要求",
                "考虑是否需要更新设计文档"
            ]

        elif error_type == 'AttributeError':
            analysis['possible_causes'] = [
                "对象属性未初始化",
                "对象类型错误",
                "属性名称拼写错误"
            ]
            analysis['recommended_checks'] = [
                "检查对象初始化代码",
                "验证对象类型和属性",
                "检查继承关系"
            ]

        elif error_type == 'ValueError':
            analysis['possible_causes'] = [
                "数值验证失败",
                "参数验证逻辑错误",
                "枚举值不在有效范围内"
            ]
            analysis['recommended_checks'] = [
                "检查数值验证逻辑",
                "验证参数范围约束",
                "检查枚举定义"
            ]

        elif error_type == 'TimeoutError':
            analysis['possible_causes'] = [
                "测试执行时间过长",
                "存在死锁或无限循环",
                "外部服务响应慢"
            ]
            analysis['recommended_checks'] = [
                "检查性能问题",
                "验证并发和锁机制",
                "考虑增加超时时间或使用mock"
            ]

        return analysis

    def _get_priority_level(self, error_type: str) -> str:
        """获取错误的优先级"""
        high_priority = ['ImportError', 'FileNotFoundError', 'ModuleNotFoundError']
        medium_priority = ['AssertionError', 'ValueError', 'TypeError', 'AttributeError', 'KeyError']

        if error_type in high_priority:
            return 'P0 - 环境/导入问题'
        elif error_type in medium_priority:
            return 'P1 - 代码逻辑问题'
        else:
            return 'P2 - 其他问题'

    def _generate_recommendations(self, analysis: Dict[str, Any]) -> List[str]:
        """生成处理建议"""
        recommendations = []

        priority = analysis.get('priority_level', 'P2')

        if 'P0' in priority:
            recommendations.append("🔧 优先检查环境问题")
            recommendations.append("📦 确保所有依赖已安装")
            recommendations.append("🛠️ 验证模块路径配置正确")

        elif 'P1' in priority:
            recommendations.append("💻 检查代码实现逻辑")
            recommendations.append("📋 对比设计文档要求")
            recommendations.append("🔍 验证边界条件处理")
            recommendations.append("❓ 考虑是否需要更新设计文档")

        else:
            recommendations.append("🔍 查看详细错误信息")
            recommendations.append("📝 记录问题并寻求帮助")

        return recommendations


def main():
    """主函数"""
    parser = argparse.ArgumentParser(
        description="测试失败诊断脚本",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument('--module', required=True, help='模块名称')
    parser.add_argument('--test', help='特定测试名称（可选）')

    args = parser.parse_args()

    diagnostic = TestDiagnostic()

    if args.test:
        result = diagnostic.diagnose_failure(args.module, args.test)
    else:
        result = diagnostic.diagnose_failure(args.module)

    print(f"\n{'='*70}")
    print("📊 诊断结果摘要")
    print('='*70)

    if result.get('status') == 'no_failures':
        print("✅ 未发现测试失败")
    elif result.get('status') == 'no_error_found':
        print(f"⚠️  {result['message']}")
    else:
        if 'diagnoses' in result:
            for diag in result['diagnoses']:
                print(f"\n测试: {diag['test_name']}")
                print(f"错误: {diag['error_info']['type']}")
                print(f"优先级: {diag['diagnosis']['priority_level']}")

                print("\n建议:")
                for rec in diag['recommendations']:
                    print(f"  {rec}")

    print(f"\n💡 详细信息已记录，请使用这些建议来处理测试失败")


if __name__ == "__main__":
    main()