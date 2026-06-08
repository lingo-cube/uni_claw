#!/usr/bin/env python3
"""
测试场景提取器 - 自动从设计文档提取测试场景

Usage:
    python scripts/extract_test_scenarios.py <module_name>
    python scripts/extract_test_scenarios.py --list

Example:
    python scripts/extract_test_scenarios.py traversal
    python scripts/extract_test_scenarios.py --list
"""

import sys
import re
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Any


# 模块映射
MODULE_ALIASES = {
    'graph': 'graph',
    'state-machine': 'state-machine',
    'state_machine': 'state-machine',
    'traversal': 'traversal',
    'exception': 'exception',
    'adb': 'adb',
    'config': 'config',
    'analysis': 'analysis',
    'safety': 'safety',
    'ai': 'ai',
    'simulation': 'simulation',
    'trace': 'trace',
    'vision': 'vision',
    'context': 'context',
}


def find_design_document(module_name: str) -> Path:
    """查找设计文档"""
    base_path = Path("docs/architecture")

    # 可能的路径
    possible_paths = [
        base_path / "modules" / f"{module_name}-design.md",
        base_path / "modules" / f"{module_name}_design.md",
        base_path / "concepts" / f"{module_name}-design.md",
        base_path / "concepts" / f"{module_name}_design.md",
    ]

    for path in possible_paths:
        if path.exists():
            return path

    return None


def read_design_document(doc_path: Path) -> str:
    """读取设计文档"""
    if not doc_path or not doc_path.exists():
        return None

    with open(doc_path, 'r', encoding='utf-8') as f:
        return f.read()


def extract_enums(content: str) -> List[Dict[str, Any]]:
    """提取枚举类型定义"""
    enums = []

    # 匹配枚举定义模式
    enum_patterns = [
        r'###\s+(\w+)\s*(?:\n|$)',  # ### EnumName
        r'##\s+.*?Enum.*?\n(.*?)(?=##|\Z)',  ## Enum section
    ]

    # 简化实现：查找表格中的枚举值
    table_pattern = r'\|\s*(\w+)\s*\|.*?\n(?:\|[^|\n]*\|[^|\n]*\|.*?\n)+'
    tables = re.finditer(table_pattern, content)

    for table in tables:
        lines = table.group(0).split('\n')
        if len(lines) > 2:  # 至少有表头和一行数据
            header = lines[1]
            if 'Type' in header or 'Value' in header or '类型' in header:
                enum_name = "Enum_" + str(len(enums))
                values = []
                for line in lines[2:]:
                    match = re.match(r'\|\s*`?(\w+)`?\s*\|', line)
                    if match:
                        values.append(match.group(1))

                if values:
                    enums.append({
                        'name': enum_name,
                        'values': values,
                        'count': len(values)
                    })

    return enums


def extract_classes(content: str) -> List[Dict[str, Any]]:
    """提取数据类定义"""
    classes = []

    # 查找 dataclass 定义
    class_pattern = r'class\s+(\w+).*?:'
    for match in re.finditer(class_pattern, content):
        class_name = match.group(1)
        # 简单统计：查找类中的属性
        classes.append({
            'name': class_name,
            'attributes': [],  # 可以进一步解析
        })

    return classes


def extract_boundaries(content: str) -> List[Dict[str, Any]]:
    """提取边界值定义"""
    boundaries = []

    # 查找 max_*, min_*, timeout 等模式
    patterns = [
        (r'max_(\w+)\s*[=:]\s*(\d+)', 'max'),
        (r'min_(\w+)\s*[=:]\s*(\d+)', 'min'),
        (r'timeout\s*[=:]\s*([\d.]+)', 'timeout'),
        (r'limit\s*[=:]\s*(\d+)', 'limit'),
    ]

    for pattern, boundary_type in patterns:
        for match in re.finditer(pattern, content):
            boundaries.append({
                'type': boundary_type,
                'name': match.group(1) if match.groups() else match.group(0),
                'value': match.group(2),
            })

    return boundaries


def extract_operations(content: str) -> List[str]:
    """提取操作/方法定义"""
    operations = []

    # 查找 def 定义
    def_pattern = r'def\s+(\w+)\s*\('
    for match in re.finditer(def_pattern, content):
        operations.append(match.group(1))

    # 查找 action 枚举值
    action_pattern = r'action\s*[=:]\s*["\'](\w+)["\']'
    for match in re.finditer(action_pattern, content):
        if match.group(1) not in operations:
            operations.append(match.group(1))

    return list(set(operations))


def estimate_scenarios(module_name: str, content: str) -> Dict[str, int]:
    """估算测试场景数量"""
    enums = extract_enums(content)
    classes = extract_classes(content)
    boundaries = extract_boundaries(content)
    operations = extract_operations(content)

    # 基于提取的元素估算测试场景数
    enum_scenarios = sum(len(e['values']) for e in enums) * 2  # 每个枚举值2个测试
    class_scenarios = len(classes) * 10  # 每个类10个测试
    boundary_scenarios = len(boundaries) * 3  # 每个边界3个测试
    operation_scenarios = len(operations) * 5  # 每个操作5个测试
    integration_scenarios = 10  # 固定10个集成测试

    total = (
        enum_scenarios +
        class_scenarios +
        boundary_scenarios +
        operation_scenarios +
        integration_scenarios
    )

    return {
        'total': total,
        'enums': enum_scenarios,
        'classes': class_scenarios,
        'boundaries': boundary_scenarios,
        'operations': operation_scenarios,
        'integration': integration_scenarios,
    }


def generate_scenarios_document(
    module_name: str,
    content: str,
    output_path: Path
) -> bool:
    """生成测试场景文档"""

    # 估算场景数
    estimates = estimate_scenarios(module_name, content)

    # 生成文档
    doc = f"""# {module_name.title()} Module Test Scenarios

> **Module**: {module_name}
> **Generated**: {datetime.now().strftime('%Y-%m-%d')}
> **Methodology**: docs/testing/TEST_EXTRACTION_METHODOLOGY.md

---

## Test Scenario Estimates

Based on design document analysis:

| Category | Estimated Scenarios |
|----------|---------------------|
| Enum Values | {estimates['enums']} |
| Data Classes | {estimates['classes']} |
| Boundary Conditions | {estimates['boundaries']} |
| Operations | {estimates['operations']} |
| Integration Tests | {estimates['integration']} |
| **Total Estimated** | **{estimates['total']}+** |

---

## Next Steps

To generate detailed test scenarios:

1. **Review the design document**: `{find_design_document(module_name) or 'N/A'}`
2. **Apply the 5-step methodology**: See TEST_EXTRACTION_METHODOLOGY.md
3. **Generate detailed scenarios**: For each test dimension
4. **Create test files**: Implement the scenarios
5. **Verify coverage**: Use `pytest tests/{module_name}/ --cov=src/{module_name}`

---

## Manual Extraction Required

This is an automated estimate. For complete test scenarios, run:

```bash
# Use Claude to analyze and extract full scenarios
# Reference: docs/testing/GRAPH_TEST_SCENARIOS.md for format example
```

---

**Generated by**: scripts/extract_test_scenarios.py
**See also**: TEST_EXTRACTION_METHODOLOGY.md, STANDARDS.md
"""

    try:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(doc)
        return True
    except Exception as e:
        print(f"❌ 写入文件失败: {e}")
        return False


def main():
    """主函数"""
    if len(sys.argv) < 2:
        print("Usage: extract_test_scenarios.py <module_name>")
        print("")
        print("Available modules:")
        for alias in sorted(set(MODULE_ALIASES.values())):
            design_doc = find_design_document(alias)
            status = "✓" if design_doc else "✗"
            print(f"  {status} {alias}")
        print("")
        print("Example:")
        print("  python scripts/extract_test_scenarios.py graph")
        return 1

    module_name = sys.argv[1].lower()

    # 标准化模块名
    if module_name in MODULE_ALIASES:
        module_name = MODULE_ALIASES[module_name]

    # 查找设计文档
    design_doc = find_design_document(module_name)

    if not design_doc:
        print(f"❌ 找不到 {module_name} 的设计文档")
        print(f"   尝试的路径: docs/architecture/modules/{module_name}-design.md")
        return 1

    print(f"✓ 找到设计文档: {design_doc}")

    # 读取设计文档
    content = read_design_document(design_doc)
    if not content:
        print(f"❌ 读取设计文档失败")
        return 1

    print(f"✓ 已读取 {len(content)} 字符")

    # 生成输出路径
    module_upper = module_name.replace('-', '_').upper()
    output_path = Path(f"docs/testing/{module_upper}_TEST_SCENARIOS.md")

    # 生成测试场景文档
    if generate_scenarios_document(module_name, content, output_path):
        print(f"✅ 已生成测试场景文档: {output_path}")

        # 显示估算
        estimates = estimate_scenarios(module_name, content)
        print(f"\n估算测试场景数: {estimates['total']}+")
        print(f"  - 枚举值: {estimates['enums']}")
        print(f"  - 数据类: {estimates['classes']}")
        print(f"  - 边界条件: {estimates['boundaries']}")
        print(f"  - 操作: {estimates['operations']}")
        print(f"  - 集成测试: {estimates['integration']}")

        return 0
    else:
        return 1


if __name__ == "__main__":
    sys.exit(main())
