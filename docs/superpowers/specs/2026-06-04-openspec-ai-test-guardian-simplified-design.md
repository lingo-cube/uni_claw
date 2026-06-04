# 简化的OpenSpec测试守护方案

**核心理念**: 设计文档驱动 + 标准化执行流程

---

## 🎯 方案概述

### 简化流程

```
1. 模块设计文档 → 定义测试要求
2. 单元测试集 → 实现具体测试
3. 统一执行脚本 → 标准化测试运行
4. AI可读报告 → 简单结果解读
5. Skill约束 → AI执行纪律
```

### 与复杂方案的区别

| 复杂方案 | 简化方案 |
|---------|----------|
| AI分析测试修改 | AI生成测试时负责质量 |
| 复杂异常检测 | 基于设计文档的验证 |
| 多层检测机制 | 统一执行+报告解读 |
| Token消耗大 | Token消耗小 |

---

## 📁 标准文件结构

```
src/{module}/
├── DESIGN.md                    # 模块设计文档（测试要求来源）
├── test/
│   ├── test_{feature}.py       # 单元测试集
│   └── test_config.yaml        # 测试配置（可选）
└── run_tests.py                 # 统一执行脚本

openspec/
├── skills/
│   └── test-discipline-skill.md  # 测试纪律skill
└── hooks/
    └── simple_test_runner.py     # 简单测试运行器
```

---

## 🔧 核心组件

### 1. 模块设计文档 (DESIGN.md)

```markdown
# Graph模块设计文档

## 功能要求
- TraversalPlan: 遍历计划
- TraversalNode: 遍历节点
- ...

## 测试要求
### 单元测试覆盖率
- 最低覆盖率: 80%
- 关键路径覆盖率: 95%

### 测试类型
- 正常路径测试
- 边界条件测试
- 异常处理测试

### 测试断言标准
- 每个测试至少3个断言
- 包含边界值验证
- 异常情况验证
```

### 2. 统一执行脚本 (run_tests.py)

```python
#!/usr/bin/env python3
"""
统一的测试执行脚本

功能:
1. 运行模块测试
2. 生成AI可读报告
3. 返回明确的退出码
"""

import sys
import subprocess
import json
from pathlib import Path

def main():
    """执行测试并生成报告"""
    module = Path(__file__).parent.name
    test_dir = Path(__file__).parent / "test"

    # 运行测试
    result = subprocess.run([
        sys.executable, "-m", "pytest",
        str(test_dir), "-v", "--tb=short"
    ], capture_output=True, text=True)

    # 生成AI可读报告
    report = generate_ai_report(result, module)

    # 保存报告
    report_file = test_dir / "test_report.json"
    with open(report_file, 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=2, ensure_ascii=False)

    # 输出结果（AI可读）
    print(json.dumps(report, ensure_ascii=False))

    # 返回退出码
    return 0 if report['status'] == 'passed' else 1

def generate_ai_report(result, module):
    """生成AI可读的测试报告"""
    # 简单解析pytest输出
    lines = result.stdout.split('\n')

    passed = lines.count('PASSED')
    failed = lines.count('FAILED')
    errors = lines.count('ERRORS')

    return {
        "module": module,
        "status": "passed" if (failed == 0 and errors == 0) else "failed",
        "summary": {
            "total": passed + failed + errors,
            "passed": passed,
            "failed": failed,
            "errors": errors
        },
        "ai_interpretation": f"测试{'通过' if failed == 0 and errors == 0 else '失败，需要修复'}"
    }

if __name__ == "__main__":
    sys.exit(main())
```

### 3. 测试纪律Skill (test-discipline-skill.md)

```markdown
---
name: test-discipline
description: 确保代码变更时测试完整性得到保障
---

# 测试纪律Skill

## 何时使用

当执行OpenSpec任务涉及代码变更时，必须使用此skill确保测试完整性。

## 执行步骤

### 1. 识别相关模块
根据变更的文件路径，确定受影响的模块：
- `src/graph/*` → graph模块
- `src/ai/*` → ai模块
- `src/models/*` → models模块

### 2. 运行模块测试
```bash
python src/{module}/run_tests.py
```

### 3. 读取测试报告
```bash
cat src/{module}/test/test_report.json
```

### 4. 解读测试结果
检查报告中的`status`字段：
- `"status": "passed"` → 测试通过，可以继续
- `"status": "failed"` → 测试失败，必须修复

### 5. 处理测试失败
如果测试失败：
1. 查看`summary.failed`和`summary.errors`数量
2. 阅读测试输出，确定失败原因
3. 修复导致测试失败的问题
4. 重新运行测试直到通过
5. **禁止**修改测试数据来达成通过

## 约束规则

### ❌ 禁止行为
- 修改测试断言值来修复失败
- 删除或注释掉失败的测试
- 修改测试输入数据来通过测试
- 忽略测试失败继续任务

### ✅ 正确行为
- 修复实际代码中的bug
- 补充缺失的测试用例
- 更新过时的测试逻辑
- 确保所有测试通过

## 任务完成条件

只有满足以下条件时，才能标记任务为完成：
1. 测试状态为`"passed"`
2. 没有failed或error的测试
3. 测试覆盖率不低于基线
```

### 4. 简单Hook集成 (simple_test_runner.py)

```python
"""
简单的测试运行Hook

集成到OpenSpec工作流中
"""

import subprocess
import json
import sys
from pathlib import Path

def run_module_tests(module):
    """运行模块测试"""
    script_path = Path.cwd() / f"src/{module}/run_tests.py"

    if not script_path.exists():
        print(f"⚠️  模块 {module} 没有测试脚本")
        return {"status": "no_tests", "acceptable": True}

    result = subprocess.run(
        [sys.executable, str(script_path)],
        capture_output=True,
        text=True
    )

    # 解析JSON报告
    try:
        report = json.loads(result.stdout)
        return {
            "status": report["status"],
            "acceptable": report["status"] == "passed",
            "report": report
        }
    except:
        return {"status": "error", "acceptable": False}

def post_task_hook(task_info, changes):
    """任务后测试检查"""

    # 1. 识别模块
    modified_files = changes.get("modified_files", [])
    modules = set()
    for file in modified_files:
        if "src/" in file:
            parts = Path(file).parts
            if len(parts) > 1 and "src" in parts:
                src_idx = parts.index("src")
                if src_idx + 1 < len(parts):
                    modules.add(parts[src_idx + 1])

    # 2. 运行所有相关模块的测试
    results = {}
    for module in modules:
        print(f"🧪 运行 {module} 模块测试...")
        results[module] = run_module_tests(module)

    # 3. 评估结果
    failed_modules = [m for m, r in results.items() if not r.get("acceptable", True)]

    if failed_modules:
        print(f"❌ 以下模块测试失败: {', '.join(failed_modules)}")
        return {"status": "failed", "failed_modules": failed_modules}
    else:
        print(f"✅ 所有模块测试通过")
        return {"status": "passed"}
```

---

## 🚀 实施步骤

### 阶段1: 建立标准 (1周)

1. **创建设计文档模板**
   - 包含测试要求部分
   - 定义覆盖率标准
   - 明确测试类型

2. **统一测试执行脚本**
   - 标准化run_tests.py
   - 生成统一格式的报告
   - 明确的退出码约定

3. **创建测试纪律Skill**
   - 定义执行流程
   - 明确约束规则
   - 提供使用示例

### 阶段2: Graph模块试点 (1周)

1. **完善Graph模块设计文档**
   - 补充测试要求
   - 明确关键功能点

2. **验证现有测试集**
   - 确保所有测试通过
   - 检查覆盖率达标
   - 补充缺失测试

3. **测试标准化流程**
   - 运行run_tests.py
   - 检查报告格式
   - 验证Skill可用性

### 阶段3: 其他模块推广 (持续)

1. **逐步完善设计文档**
   - 每个模块补充测试要求
   - 建立质量标准

2. **统一测试脚本**
   - 所有模块采用相同格式
   - 确保报告一致性

3. **建立测试文化**
   - AI生成测试时参考设计文档
   - 执行时使用标准化流程
   - 完成时确保测试通过

---

## 📊 预期效果

### 优势

1. **简单明了**
   - 流程清晰：设计文档→测试集→执行脚本→报告
   - AI理解容易：只需运行脚本，读取报告
   - 维护成本低：标准化组件

2. **质量可控**
   - 设计文档定义标准
   - 测试集验证实现
   - Skill约束AI行为

3. **扩展性好**
   - 新模块只需遵循标准
   - 统一格式便于自动化
   - 技能复用性强

### 与复杂方案对比

| 复杂方案 | 简化方案 |
|---------|----------|
| 需要复杂的异常检测 | 只需解读测试报告 |
| AI分析消耗大量token | AI执行简单流程 |
| 规则需要持续维护 | 设计文档自然演进 |
| 集成复杂 | 集成简单 |

---

## 🎯 成功指标

1. **流程标准化**
   - 所有模块有设计文档
   - 所有模块有run_tests.py
   - 所有测试报告格式统一

2. **质量保障**
   - 测试覆盖率达标
   - AI遵循测试纪律
   - 变更时测试完整性

3. **效率提升**
   - AI执行测试简单直接
   - 测试报告易于解读
   - 问题定位快速准确

---

**结论**: 这个简化方案通过标准化流程和明确约束，在不牺牲质量的前提下大大降低了复杂性，更适合实际实施。