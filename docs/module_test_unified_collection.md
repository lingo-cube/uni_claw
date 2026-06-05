# 模块独立测试 + 统一结果收集方案

**版本**: 1.0  
**创建**: 2026-06-05  
**设计**: 每模块管自己的运行，统一收集最新测试结果

---

## 🎯 核心设计原则

```
各模块独立运行 → 保存到统一路径 → validation统一收集
     ↓                    ↓                  ↓
保持模块自治      标准化JSON格式      集中validation报告
```

### 关键优势
- ✅ **保持模块自治** - 每个模块仍可独立测试
- ✅ **最小改动** - 只需修改现有脚本输出格式
- ✅ **统一数据源** - validation有标准化的输入
- ✅ **易于扩展** - 新模块只需遵循输出格式

---

## 📂 统一测试结果目录结构

```
test_results/
├── ai_module.json              # AI模块测试结果
├── adb_module.json             # ADB模块测试结果  
├── traversal_module.json      # 遍历引擎测试结果
├── simulation_module.json     # 仿真模块测试结果
├── state_machine_module.json  # 状态机测试结果
├── graph_engine_module.json   # 图引擎测试结果
├── models_module.json          # 核心模型测试结果
├── integration.json             # 集成测试结果
├── .gitkeep                     # Git保留目录
└── README.md                    # 格式说明文档
```

---

## 📋 标准化测试结果JSON格式

### 完整JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Unified Test Result Format",
  "type": "object",
  "required": ["module", "timestamp", "format_version", "summary", "tests"],
  "properties": {
    "module": {
      "type": "string",
      "description": "模块名称",
      "examples": ["ai", "adb", "simulation", "state_machine"]
    },
    "timestamp": {
      "type": "string",
      "format": "date-time",
      "description": "测试执行时间（ISO 8601格式）"
    },
    "format_version": {
      "type": "string",
      "description": "JSON格式版本",
      "enum": ["1.0"]
    },
    "python_version": {
      "type": "string",
      "description": "Python版本",
      "examples": ["3.10.0", "3.11.0"]
    },
    "pytest_version": {
      "type": "string",
      "description": "pytest版本",
      "examples": ["7.4.0", "8.0.0"]
    },
    "environment": {
      "type": "object",
      "description": "测试环境信息",
      "properties": {
        "platform": {"type": "string"},
        "architecture": {"type": "string"},
        "test_duration_seconds": {"type": "number"}
      }
    },
    "summary": {
      "type": "object",
      "required": ["total", "passed", "failed", "skipped"],
      "properties": {
        "total": {
          "type": "integer",
          "description": "总测试数",
          "minimum": 0
        },
        "passed": {
          "type": "integer", 
          "description": "通过的测试数",
          "minimum": 0
        },
        "failed": {
          "type": "integer",
          "description": "失败的测试数",
          "minimum": 0
        },
        "skipped": {
          "type": "integer",
          "description": "跳过的测试数",
          "minimum": 0
        },
        "errors": {
          "type": "integer",
          "description": "错误的测试数",
          "minimum": 0
        },
        "pass_rate": {
          "type": "number",
          "description": "通过率（百分比）",
          "minimum": 0.0,
          "maximum": 100.0
        }
      }
    },
    "tests": {
      "type": "array",
      "description": "详细测试结果列表",
      "items": {
        "type": "object",
        "required": ["file", "test", "outcome"],
        "properties": {
          "file": {
            "type": "string",
            "description": "测试文件路径"
          },
          "class": {
            "type": "string",
            "description": "测试类名"
          },
          "test": {
            "type": "string", 
            "description": "测试函数名"
          },
          "outcome": {
            "type": "string",
            "enum": ["PASSED", "FAILED", "SKIPPED", "ERROR"],
            "description": "测试结果"
          },
          "duration": {
            "type": "number",
            "description": "测试执行时间（秒）"
          },
          "message": {
            "type": "string",
            "description": "失败或错误信息"
          },
          "traceback": {
            "type": "string",
            "description": "错误堆栈信息"
          }
        }
      }
    },
    "files_tested": {
      "type": "array",
      "description": "被测试的源代码文件列表",
      "items": {"type": "string"}
    },
    "coverage": {
      "type": "object",
      "description": "代码覆盖率信息（可选）",
      "properties": {
        "percent_covered": {"type": "number"},
        "lines_covered": {"type": "integer"},
        "lines_total": {"type": "integer"},
        "files_covered": {"type": "array"}
      }
    }
  }
}
```

### 实际示例

```json
{
  "module": "simulation",
  "timestamp": "2026-06-05T10:30:42Z",
  "format_version": "1.0",
  "python_version": "3.10.0",
  "pytest_version": "7.4.0",
  "environment": {
    "platform": "Windows",
    "architecture": "AMD64",
    "test_duration_seconds": 5.23
  },
  "summary": {
    "total": 33,
    "passed": 33,
    "failed": 0,
    "skipped": 0,
    "errors": 0,
    "pass_rate": 100.0
  },
  "tests": [
    {
      "file": "tests/v6/test_simulation.py",
      "class": "TestMockVisionService",
      "test": "test_create_with_virtual_pages",
      "outcome": "PASSED",
      "duration": 0.123
    },
    {
      "file": "tests/v6/test_simulation.py", 
      "class": "TestMockVisionService",
      "test": "test_mock_response_consistency",
      "outcome": "PASSED",
      "duration": 0.089
    }
  ],
  "files_tested": [
    "src/simulation/mock_vision.py",
    "src/simulation/visualizer.py",
    "src/simulation/runner.py"
  ],
  "coverage": {
    "percent_covered": 92.5,
    "lines_covered": 485,
    "lines_total": 524,
    "files_covered": [
      "src/simulation/mock_vision.py",
      "src/simulation/visualizer.py"
    ]
  }
}
```

---

## 🔧 模块级测试脚本改造

### 通用测试结果导出器

创建 `scripts/test_result_exporter.py`:

```python
#!/usr/bin/env python3
"""
统一测试结果导出器

为所有模块提供标准化的测试结果导出功能
"""

import json
import subprocess
import sys
import re
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Any


class TestResultExporter:
    """统一测试结果导出器"""
    
    def __init__(self, module_name: str, project_root: Path = None):
        self.module_name = module_name
        self.project_root = project_root or Path.cwd()
        self.results_dir = self.project_root / "test_results"
        self.output_file = self.results_dir / f"{module_name}_module.json"
        
    def run_and_export(self, test_paths: List[str], env: Dict[str, str] = None) -> Dict[str, Any]:
        """
        运行测试并导出标准化结果
        
        Args:
            test_paths: 测试路径列表
            env: 环境变量（可选）
        
        Returns:
            标准化的测试结果字典
        """
        print(f"🧪 运行 {self.module_name} 模块测试...")
        
        # 确保结果目录存在
        self.results_dir.mkdir(parents=True, exist_ok=True)
        
        all_test_results = []
        total_summary = {
            "total": 0, "passed": 0, "failed": 0, 
            "skipped": 0, "errors": 0
        }
        
        start_time = datetime.now()
        
        # 运行每个测试路径
        for test_path in test_paths:
            print(f"  📋 测试路径: {test_path}")
            
            # 运行pytest
            result = subprocess.run(
                [sys.executable, "-m", "pytest", test_path, "-v", "--tb=short"],
                capture_output=True,
                text=True,
                cwd=self.project_root,
                env=env or {}
            )
            
            # 解析结果
            parsed = self._parse_pytest_output(result.stdout, test_path)
            all_test_results.extend(parsed['tests'])
            
            # 累加统计
            for key in ['total', 'passed', 'failed', 'skipped', 'errors']:
                total_summary[key] += parsed['summary'][key]
        
        end_time = datetime.now()
        duration = (end_time - start_time).total_seconds()
        
        # 计算通过率
        if total_summary['total'] > 0:
            pass_rate = (total_summary['passed'] / total_summary['total']) * 100
        else:
            pass_rate = 0.0
        
        # 构建标准化结果
        standard_result = {
            "module": self.module_name,
            "timestamp": start_time.isoformat(),
            "format_version": "1.0",
            "python_version": f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}",
            "pytest_version": self._get_pytest_version(),
            "environment": {
                "platform": sys.platform,
                "architecture": sys.platform.architecture if hasattr(sys.platform, 'architecture') else "unknown",
                "test_duration_seconds": duration
            },
            "summary": {
                "total": total_summary['total'],
                "passed": total_summary['passed'],
                "failed": total_summary['failed'],
                "skipped": total_summary['skipped'],
                "errors": total_summary['errors'],
                "pass_rate": round(pass_rate, 1)
            },
            "tests": all_test_results,
            "files_tested": self._extract_tested_files(all_test_results)
        }
        
        # 保存到统一路径
        self._save_result(standard_result)
        
        return standard_result
    
    def _parse_pytest_output(self, output: str, test_path: str) -> Dict[str, Any]:
        """解析pytest输出"""
        lines = output.split('\n')
        
        tests = []
        passed, failed, skipped, errors = 0, 0, 0, 0
        
        # 解析测试行
        test_pattern = re.compile(r'(.+\.py)::(.+)::(.+)\s+(PASSED|FAILED|ERROR|SKIPPED)')
        duration_pattern = re.compile(r'(\d+\.?\d*)s')
        
        for line in lines:
            match = test_pattern.match(line)
            if match:
                file_path, test_class, test_name, outcome = match.groups()
                
                # 尝试提取执行时间
                duration = 0.0
                duration_match = duration_pattern.search(line)
                if duration_match:
                    duration = float(duration_match.group(1))
                
                tests.append({
                    "file": file_path,
                    "class": test_class,
                    "test": test_name,
                    "outcome": outcome,
                    "duration": duration
                })
                
                if outcome == "PASSED":
                    passed += 1
                elif outcome == "FAILED":
                    failed += 1
                elif outcome == "ERROR":
                    errors += 1
                elif outcome == "SKIPPED":
                    skipped += 1
        
        # 解析摘要行
        summary_pattern = re.compile(r'(\d+)\s+passed(?:\s+(\d+)\s+failed)?(?:\s+(\d+)\s+skipped)?(?:\s+(\d+)\s+error)?')
        for line in lines:
            match = summary_pattern.search(line)
            if match:
                passed = int(match.group(1))
                if match.group(2):
                    failed = int(match.group(2))
                if match.group(3):
                    skipped = int(match.group(3))
                if match.group(4):
                    errors = int(match.group(4))
        
        total = passed + failed + skipped + errors
        
        return {
            "test_path": test_path,
            "summary": {
                "total": total,
                "passed": passed,
                "failed": failed,
                "skipped": skipped,
                "errors": errors
            },
            "tests": tests,
            "status": "passed" if failed == 0 and errors == 0 else "failed"
        }
    
    def _extract_tested_files(self, tests: List[Dict]) -> List[str]:
        """从测试结果中提取被测试的源文件"""
        files = set()
        for test in tests:
            # 从测试文件路径推断源文件
            test_file = test.get('file', '')
            if 'tests/' in test_file:
                # 将 tests/v6/test_simulation.py 转换为 src/simulation/*.py
                parts = test_file.replace('tests/', '').replace('test_', '').split('/')
                if len(parts) >= 2:
                    module = parts[1].replace('.py', '')
                    files.add(f"src/{module}/*.py")
        
        return sorted(list(files))
    
    def _get_pytest_version(self) -> str:
        """获取pytest版本"""
        try:
            result = subprocess.run(
                [sys.executable, "-m", "pytest", "--version"],
                capture_output=True,
                text=True
            )
            # 提取版本号
            match = re.search(r'(\d+\.\d+\.\d+)', result.stdout)
            if match:
                return match.group(1)
        except:
            pass
        return "unknown"
    
    def _save_result(self, result: Dict[str, Any]):
        """保存测试结果到统一路径"""
        with open(self.output_file, 'w', encoding='utf-8') as f:
            json.dump(result, f, indent=2, ensure_ascii=False)
        
        print(f"✅ 测试结果已保存: {self.output_file}")
        print(f"📊 通过率: {result['summary']['pass_rate']}% ({result['summary']['passed']}/{result['summary']['total']})")


def main():
    """命令行入口"""
    import argparse
    
    parser = argparse.ArgumentParser(description="统一测试结果导出器")
    parser.add_argument("module", help="模块名称")
    parser.add_argument("test_paths", nargs="+", help="测试路径列表")
    
    args = parser.parse_args()
    
    exporter = TestResultExporter(args.module)
    result = exporter.run_and_export(args.test_paths)
    
    # 返回退出码
    sys.exit(0 if result['summary']['failed'] == 0 else 1)


if __name__ == "__main__":
    main()
```

---

## 🔨 各模块脚本改造

### 1. AI模块 (`src/ai/run_tests.py`)

```python
#!/usr/bin/env python3
"""AI模块测试脚本"""

import sys
from pathlib import Path

# 添加项目根目录到路径
project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root / "scripts"))

from test_result_exporter import TestResultExporter


def main():
    """运行AI模块测试并导出结果"""
    exporter = TestResultExporter(
        module_name="ai",
        project_root=project_root
    )
    
    # AI模块的测试路径
    test_paths = [
        "src/ai/test/",           # AI核心测试
        "tests/ai/providers/",    # Provider测试
        "tests/ai/prompts/",      # Prompts测试
        "tests/ai/trace/"         # Trace测试
    ]
    
    result = exporter.run_and_export(test_paths)
    
    # 显示简要结果
    print("\n" + "=" * 70)
    print("📊 AI模块测试汇总")
    print("=" * 70)
    print(f"✅ 通过: {result['summary']['passed']}")
    print(f"❌ 失败: {result['summary']['failed']}")
    print(f"⏭️  跳过: {result['summary']['skipped']}")
    print(f"📈 通过率: {result['summary']['pass_rate']}%")


if __name__ == "__main__":
    main()
```

### 2. ADB模块 (`src/adb/run_tests.py`)

```python
#!/usr/bin/env python3
"""ADB模块测试脚本"""

import sys
from pathlib import Path

project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root / "scripts"))

from test_result_exporter import TestResultExporter


def main():
    """运行ADB模块测试并导出结果"""
    exporter = TestResultExporter(
        module_name="adb",
        project_root=project_root
    )
    
    test_paths = [
        "tests/adb/",
        "tests/integration/test_adb_integration.py"
    ]
    
    result = exporter.run_and_export(test_paths)
    
    print("\n" + "=" * 70)
    print("📊 ADB模块测试汇总")
    print("=" * 70)
    print(f"✅ 通过: {result['summary']['passed']}")
    print(f"❌ 失败: {result['summary']['failed']}")


if __name__ == "__main__":
    main()
```

### 3. 仿真模块 (`src/simulation/run_tests.py`)

```python
#!/usr/bin/env python3
"""仿真模块测试脚本"""

import sys
from pathlib import Path

project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root / "scripts"))

from test_result_exporter import TestResultExporter


def main():
    """运行仿真模块测试并导出结果"""
    exporter = TestResultExporter(
        module_name="simulation",
        project_root=project_root
    )
    
    test_paths = [
        "tests/v6/test_simulation.py",
        "tests/v6/test_examples.py"
    ]
    
    result = exporter.run_and_export(test_paths)
    
    print("\n" + "=" * 70)
    print("📊 仿真模块测试汇总")
    print("=" * 70)
    print(f"✅ 通过: {result['summary']['passed']}")
    print(f"❌ 失败: {result['summary']['failed']}")


if __name__ == "__main__":
    main()
```

### 4. 状态机模块 (`src/state_machine/run_tests.py`)

```python
#!/usr/bin/env python3
"""状态机模块测试脚本"""

import sys
from pathlib import Path

project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root / "scripts"))

from test_result_exporter import TestResultExporter


def main():
    """运行状态机模块测试并导出结果"""
    exporter = TestResultExporter(
        module_name="state_machine",
        project_root=project_root
    )
    
    test_paths = [
        "tests/v6/test_state_machine.py",
        "tests/v6/test_state_machine_popup_integration.py",
        "tests/v6/test_state_machine_error_integration.py"
    ]
    
    result = exporter.run_and_export(test_paths)
    
    print("\n" + "=" * 70)
    print("📊 状态机模块测试汇总")
    print("=" * 70)
    print(f"✅ 通过: {result['summary']['passed']}")
    print(f"❌ 失败: {result['summary']['failed']}")


if __name__ == "__main__":
    main()
```

### 5. 集成测试 (`tests/integration/run_tests.py`)

```python
#!/usr/bin/env python3
"""集成测试脚本"""

import sys
from pathlib import Path

project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root / "scripts"))

from test_result_exporter import TestResultExporter


def main():
    """运行集成测试并导出结果"""
    exporter = TestResultExporter(
        module_name="integration",
        project_root=project_root
    )
    
    test_paths = [
        "tests/integration/",
        "tests/v6/test_examples.py"
    ]
    
    result = exporter.run_and_export(test_paths)
    
    print("\n" + "=" * 70)
    print("📊 集成测试汇总")
    print("=" * 70)
    print(f"✅ 通过: {result['summary']['passed']}")
    print(f"❌ 失败: {result['summary']['failed']}")


if __name__ == "__main__":
    main()
```

---

## 📝 Validation统一收集器

### 创建 `scripts/collect_test_results.py`

```python
#!/usr/bin/env python3
"""
Validation统一测试结果收集器

从test_results/目录收集所有模块的测试结果，生成综合validation报告
"""

import json
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Any


class ValidationResultCollector:
    """Validation结果收集器"""
    
    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.results_dir = self.project_root / "test_results"
        self.validation_dir = self.project_root / "docs" / "validation"
        
    def collect_all_results(self) -> Dict[str, Any]:
        """收集所有模块的测试结果"""
        print("📊 收集统一测试结果...")
        
        # 确保目录存在
        self.validation_dir.mkdir(parents=True, exist_ok=True)
        
        # 收集所有模块结果
        module_results = {}
        total_summary = {
            "total_tests": 0,
            "total_passed": 0,
            "total_failed": 0,
            "total_skipped": 0,
            "total_errors": 0,
            "modules_tested": 0,
            "modules_passed": 0,
            "modules_failed": 0
        }
        
        # 遍历test_results目录
        if not self.results_dir.exists():
            print("⚠️  test_results目录不存在")
            return self._empty_collection_result()
        
        result_files = list(self.results_dir.glob("*_module.json"))
        
        if not result_files:
            print("⚠️  未找到测试结果文件")
            return self._empty_collection_result()
        
        print(f"📋 找到 {len(result_files)} 个模块结果文件")
        
        for result_file in result_files:
            try:
                with open(result_file, 'r', encoding='utf-8') as f:
                    module_data = json.load(f)
                
                module_name = module_data.get('module', result_file.stem)
                module_results[module_name] = module_data
                
                # 累加统计
                summary = module_data.get('summary', {})
                total_summary['total_tests'] += summary.get('total', 0)
                total_summary['total_passed'] += summary.get('passed', 0)
                total_summary['total_failed'] += summary.get('failed', 0)
                total_summary['total_skipped'] += summary.get('skipped', 0)
                total_summary['total_errors'] += summary.get('errors', 0)
                
                total_summary['modules_tested'] += 1
                
                # 检查模块是否通过
                if summary.get('failed', 0) == 0 and summary.get('errors', 0) == 0:
                    total_summary['modules_passed'] += 1
                else:
                    total_summary['modules_failed'] += 1
                
                print(f"  ✅ {module_name}: {summary.get('passed', 0)}/{summary.get('total', 0)} 通过")
                
            except Exception as e:
                print(f"  ❌ 读取 {result_file.name} 失败: {e}")
        
        # 计算整体通过率
        if total_summary['total_tests'] > 0:
            total_summary['overall_pass_rate'] = (
                total_summary['total_passed'] / total_summary['total_tests'] * 100
            )
        else:
            total_summary['overall_pass_rate'] = 0.0
        
        # 生成综合报告
        collection_result = {
            "collection_timestamp": datetime.now().isoformat(),
            "format_version": "1.0",
            "summary": total_summary,
            "modules": module_results,
            "status": "all_passed" if total_summary['total_failed'] == 0 else "has_failures"
        }
        
        # 生成validation报告
        self._generate_validation_reports(collection_result)
        
        return collection_result
    
    def _generate_validation_reports(self, collection_result: Dict[str, Any]):
        """生成标准化validation报告"""
        print("📝 生成validation报告...")
        
        # 1. 生成unit_test_status.md
        self._generate_unit_test_status(collection_result)
        
        # 2. 生成integration_test_status.md（如果有集成测试）
        if 'integration' in collection_result.get('modules', {}):
            self._generate_integration_test_status(collection_result)
        
        # 3. 生成综合状态报告
        self._generate_comprehensive_status(collection_result)
        
        print("✅ Validation报告生成完成")
    
    def _generate_unit_test_status(self, collection_result: Dict[str, Any]):
        """生成单元测试状态报告"""
        summary = collection_result['summary']
        modules = collection_result['modules']
        
        content = f"""# Unit Test Status

**Generated**: {collection_result['collection_timestamp']}
**Status**: {'COMPLETE' if summary['total_failed'] == 0 else 'HAS_FAILURES'}
**Collector**: ValidationResultCollector
**Format Version**: {collection_result['format_version']}

---

## Executive Summary

- **Modules Tested**: {summary['modules_tested']}
- **Modules Passed**: {summary['modules_passed']}
- **Modules Failed**: {summary['modules_failed']}
- **Total Tests**: {summary['total_tests']}
- **Passed**: {summary['total_passed']} ({summary['overall_pass_rate']:.1f}%)
- **Failed**: {summary['total_failed']}
- **Skipped**: {summary['total_skipped']}

---

## Detailed Results by Module

"""
        
        for module_name, module_data in modules.items():
            if module_name == 'integration':
                continue  # 集成测试单独处理
                
            module_summary = module_data.get('summary', {})
            passed = module_summary.get('passed', 0)
            total = module_summary.get('total', 0)
            failed = module_summary.get('failed', 0)
            pass_rate = (passed / total * 100) if total > 0 else 0
            
            status_icon = "✅" if failed == 0 else "❌"
            
            content += f"""
### {status_icon} {module_name.replace('_', ' ').title()} Module ({passed}/{total} - {pass_rate:.1f}%)

**Test Duration**: {module_data.get('environment', {}).get('test_duration_seconds', 0):.2f}s
**Python Version**: {module_data.get('python_version', 'unknown')}
**Timestamp**: {module_data.get('timestamp', 'unknown')}

"""
            
            # 列出前几个失败测试（如果有）
            if failed > 0:
                content += "**Failed Tests:**\n"
                failed_tests = [
                    test for test in module_data.get('tests', []) 
                    if test.get('outcome') in ['FAILED', 'ERROR']
                ][:5]
                
                for test in failed_tests:
                    icon = "❌" if test.get('outcome') == 'FAILED' else "⚠️"
                    content += f"- {icon} `{test.get('class', '')}::{test.get('test', '')}`\n"
                
                content += "\n"
        
        content += """
---

## Module List

"""
        for module_name in modules.keys():
            if module_name != 'integration':
                content += f"- `{module_name}_module.json`\n"
        
        content += """
---

*This report was automatically generated by ValidationResultCollector*
*Data source: test_results/ directory*
"""
        
        output_path = self.validation_dir / "unit_test_status.md"
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(content)
        
        print(f"  ✅ 生成: {output_path}")
    
    def _generate_integration_test_status(self, collection_result: Dict[str, Any]):
        """生成集成测试状态报告"""
        integration_data = collection_result['modules'].get('integration', {})
        summary = integration_data.get('summary', {})
        tests = integration_data.get('tests', [])
        
        content = f"""# Integration Test Status

**Generated**: {collection_result['collection_timestamp']}
**Status**: {'COMPLETE' if summary.get('failed', 0) == 0 else 'HAS_FAILURES'}
**Collector**: ValidationResultCollector

---

## Executive Summary

- **Total Tests**: {summary.get('total', 0)}
- **Passed**: {summary.get('passed', 0)}
- **Failed**: {summary.get('failed', 0)}
- **Success Rate**: {summary.get('pass_rate', 0):.1f}%

---

## Test Results

"""
        
        for test in tests:
            icon = {"PASSED": "✅", "FAILED": "❌", "SKIPPED": "⏭️", "ERROR": "⚠️"}.get(
                test.get('outcome', ''), '❓'
            )
            content += f"{icon} `{test.get('class', '')}::{test.get('test', '')}` - {test.get('outcome', '')}\n"
        
        content += f"""
---

## Execution Details

**Test Duration**: {integration_data.get('environment', {}).get('test_duration_seconds', 0):.2f}s
**Timestamp**: {integration_data.get('timestamp', 'unknown')}
**Source File**: integration_module.json

---

*This report was automatically generated by ValidationResultCollector*
"""
        
        output_path = self.validation_dir / "integration_test_status.md"
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(content)
        
        print(f"  ✅ 生成: {output_path}")
    
    def _generate_comprehensive_status(self, collection_result: Dict[str, Any]):
        """生成综合状态报告"""
        summary = collection_result['summary']
        
        content = f"""# Comprehensive Test Status

**Generated**: {collection_result['collection_timestamp']}
**Status**: {collection_result['status'].upper()}
**Overall Pass Rate**: {summary['overall_pass_rate']:.1f}%

---

## Summary

- **Modules**: {summary['modules_passed']}/{summary['modules_tested']} passed
- **Tests**: {summary['total_passed']}/{summary['total_tests']} passed ({summary['overall_pass_rate']:.1f}%)
- **Failures**: {summary['total_failed']}
- **Skipped**: {summary['total_skipped']}

---

## Module Breakdown

"""
        
        for module_name, module_data in collection_result['modules'].items():
            module_summary = module_data.get('summary', {})
            passed = module_summary.get('passed', 0)
            total = module_summary.get('total', 0)
            failed = module_summary.get('failed', 0)
            status = "✅ PASS" if failed == 0 else "❌ FAIL"
            
            content += f"- **{module_name}**: {status} ({passed}/{total})\n"
        
        content += """
---

*Comprehensive status report from unified test collection*
"""
        
        output_path = self.validation_dir / "comprehensive_status.md"
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(content)
        
        print(f"  ✅ 生成: {output_path}")
    
    def _empty_collection_result(self) -> Dict[str, Any]:
        """返回空的收集结果"""
        return {
            "collection_timestamp": datetime.now().isoformat(),
            "summary": {
                "total_tests": 0,
                "total_passed": 0,
                "total_failed": 0,
                "total_skipped": 0,
                "modules_tested": 0,
                "overall_pass_rate": 0.0
            },
            "modules": {},
            "status": "no_results"
        }


def main():
    """命令行入口"""
    collector = ValidationResultCollector()
    result = collector.collect_all_results()
    
    print("\n" + "=" * 70)
    print("📊 测试结果收集汇总")
    print("=" * 70)
    print(f"📋 模块测试: {result['summary']['modules_passed']}/{result['summary']['modules_tested']} 通过")
    print(f"✅ 测试通过: {result['summary']['total_passed']}")
    print(f"❌ 测试失败: {result['summary']['total_failed']}")
    print(f"📈 整体通过率: {result['summary']['overall_pass_rate']:.1f}%")


if __name__ == "__main__":
    main()
```

---

## 🚀 统一工作流程

### 完整测试流程

```bash
# 1. 各模块独立运行测试
python src/ai/run_tests.py           # 生成 test_results/ai_module.json
python src/adb/run_tests.py          # 生成 test_results/adb_module.json  
python src/simulation/run_tests.py   # 生成 test_results/simulation_module.json
python src/state_machine/run_tests.py # 生成 test_results/state_machine_module.json
python tests/integration/run_tests.py # 生成 test_results/integration.json

# 2. 统一收集结果
python scripts/collect_test_results.py

# 3. 自动生成validation报告
# ✅ docs/validation/unit_test_status.md
# ✅ docs/validation/integration_test_status.md
# ✅ docs/validation/comprehensive_status.md
```

### OpenSpec集成流程

```bash
# 在OpenSpec工作流中
/opsx:apply some-change

# 后置Hook自动执行：
# 1. 检测变更模块
# 2. 运行对应模块的 run_tests.py
# 3. 自动调用 collect_test_results.py
# 4. 触发validation-documentation技能
```

---

## 📊 数据流程图

```mermaid
graph LR
    A[各模块run_tests.py] --> B[TestResultExporter]
    B --> C[pytest执行]
    C --> D[解析输出]
    D --> E[标准化JSON]
    E --> F[test_results/xxx_module.json]
    
    F --> G[collect_test_results.py]
    G --> H[收集所有模块结果]
    H --> I[生成综合报告]
    I --> J[docs/validation/xxx.md]
    
    style A fill:#e1f5ff
    style B fill:#90ee90
    style F fill:#ffd1dc
    style G fill:#f9f9d9
    style J fill:#e8f5e8
```

---

## 📋 .gitignore 配置

```gitignore
# test_results目录中的JSON文件会被频繁更新
# 但需要保留目录结构
test_results/*.json

# 保留目录和README
!test_results/.gitkeep
!test_results/README.md
```

---

## 🎯 实施计划

### 阶段1: 基础设施（本周）
1. ✅ 创建 `test_results/` 目录
2. ✅ 创建 `scripts/test_result_exporter.py`
3. ✅ 创建 `scripts/collect_test_results.py`
4. ✅ 定义标准化JSON格式

### 阶段2: 模块改造（下周）
5. 改造现有 `run_tests.py` 脚本
6. 添加统一结果导出
7. 验证JSON格式正确性

### 阶段3: Validation集成（第三周）
8. 集成到validation-documentation技能
9. 集成到OpenSpec工作流
10. 完整测试验证

---

## 🔧 维护和扩展

### 添加新模块

1. 创建模块的 `run_tests.py`:
```python
from test_result_exporter import TestResultExporter

exporter = TestResultExporter("new_module")
result = exporter.run_and_export(["tests/new_module/"])
```

2. 自动生成 `test_results/new_module.json`

3. `collect_test_results.py` 自动包含新模块

### 格式升级

更新 `format_version` 并保持向后兼容:
```python
if result['format_version'] == '1.0':
    # 使用1.0格式解析
elif result['format_version'] == '2.0':
    # 使用2.0格式解析
```

---

**版本**: 1.0  
**最后更新**: 2026-06-05  
**状态**: 🎯 模块独立测试 + 统一结果收集方案确定