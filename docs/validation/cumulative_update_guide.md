# Validation Documentation 累积式更新指南

**问题**: 在多模块测试场景中，如何避免后面的测试覆盖前面的结果？

**解决方案**: 使用累积式更新（Merge而非Overwrite）

---

## 问题场景

在一个OpenSpec任务中按顺序测试多个模块时：

```bash
# ❌ 问题场景：简单覆盖
1. 测试simulation模块 → 生成 unit_test_status.md (只包含simulation)
2. 测试state_machine模块 → 覆盖 unit_test_status.md (只包含state_machine)
3. 测试graph_engine模块 → 覆盖 unit_test_status.md (只包含graph_engine)

# 最终结果：只有最后一个模块的结果！前面的都丢失了！
```

## 解决方案：累积式更新

### 方法1：使用合并工具

**推荐使用** `scripts/merge_validation_docs.py` 工具：

```python
from scripts.merge_validation_docs import ValidationDocMerger

merger = ValidationDocMerger()

# 模块1测试完成
simulation_results = {
    "total": 33,
    "passed": 33,
    "failed": 0
}
merger.append_module_result("simulation", simulation_results)

# 模块2测试完成
state_machine_results = {
    "total": 20,
    "passed": 20,
    "failed": 0
}
merger.append_module_result("state_machine", state_machine_results)

# 模块3测试完成
graph_engine_results = {
    "total": 31,
    "passed": 31,
    "failed": 0
}
merger.append_module_result("graph_engine", graph_engine_results)

# 最终结果：unit_test_status.md 包含所有3个模块的结果！
```

### 方法2：手动合并

如果没有工具支持，可以手动处理：

```markdown
# 1. 读取现有文档
existing_content = read_file("docs/validation/unit_test_status.md")

# 2. 解析现有模块结果
existing_modules = parse_existing_modules(existing_content)

# 3. 添加新模块结果
existing_modules["new_module"] = new_module_results

# 4. 重新生成文档
updated_content = generate_updated_doc(existing_modules)

# 5. 保存（覆盖模式）
write_file("docs/validation/unit_test_status.md", updated_content)
```

### 方法3：Hook自动处理

在最新的`validation_documentation_hook`中，Hook会自动检测文档是否存在并提示使用累积式更新：

```
[INFO] 文档已存在: unit_test_status.md
[ACTION] 请使用累积式更新，读取并合并现有内容
[INFO] 现有文档大小: 9440 bytes
[INFO] 现有文档修改时间: 2026-06-05 09:38:50
```

## 工作流程对比

### ❌ 错误的工作流程（覆盖模式）

```bash
# 模块1测试
pytest tests/simulation/ -v
# 直接生成新文档，覆盖旧内容
generate_unit_test_report("simulation", output="unit_test_status.md")  # ❌ 覆盖

# 模块2测试
pytest tests/state_machine/ -v
# 直接生成新文档，覆盖模块1的结果
generate_unit_test_report("state_machine", output="unit_test_status.md")  # ❌ 覆盖

# 模块3测试
pytest tests/graph_engine/ -v
# 直接生成新文档，覆盖模块1+2的结果
generate_unit_test_report("graph_engine", output="unit_test_status.md")  # ❌ 覆盖

# 结果：只有graph_engine的结果！
```

### ✅ 正确的工作流程（累积式更新）

```bash
# 模块1测试
pytest tests/simulation/ -v
# 使用合并工具追加结果
merge_validation_docs.py --module simulation --results simulation_results.json  # ✅ 追加

# 模块2测试
pytest tests/state_machine/ -v
# 使用合并工具追加结果（自动读取并合并现有内容）
merge_validation_docs.py --module state_machine --results sm_results.json  # ✅ 追加

# 模块3测试
pytest tests/graph_engine/ -v
# 使用合并工具追加结果
merge_validation_docs.py --module graph_engine --results ge_results.json  # ✅ 追加

# 结果：unit_test_status.md 包含所有3个模块的结果！
```

## 文档结构示例

累积式更新后的文档结构：

```markdown
# Unit Test Status

**Generated**: 2026-06-05 14:30
**Status**: COMPLETE

## Executive Summary
- Total Tests: 84/84 passing (100%)
- Modules Tested: 3

## Latest Test Run (2026-06-05 14:30)

### Simulation Module
- Total: 33 tests
- Passed: 33
- Failed: 0
- Test Time: 2026-06-05 14:00

### State Machine Module
- Total: 20 tests
- Passed: 20
- Failed: 0
- Test Time: 2026-06-05 14:15

### Graph Engine Module
- Total: 31 tests
- Passed: 31
- Failed: 0
- Test Time: 2026-06-05 14:30

## Previous Test Runs (历史对比)
# ... 如果有历史数据
```

## 实现细节

### 合并算法

1. **检测现有文档**
   ```python
   doc_path = "docs/validation/unit_test_status.md"
   if doc_path.exists():
       # 读取现有内容
       existing_content = doc_path.read_text()
   ```

2. **解析现有模块**
   ```python
   # 提取已有模块的测试结果
   existing_modules = parse_module_results(existing_content)
   # 结果: {"simulation": {...}, "state_machine": {...}}
   ```

3. **合并新结果**
   ```python
   # 更新或添加新模块
   all_modules = {**existing_modules, **new_modules}
   # Python字典合并：后面的覆盖前面的同键值
   ```

4. **重新生成文档**
   ```python
   # 基于所有模块结果生成新文档
   updated_content = generate_document(all_modules)
   ```

5. **保存更新**
   ```python
   # 覆盖式保存（文件名不变，内容已合并）
   doc_path.write_text(updated_content)
   ```

### 时间戳管理

每个模块测试都有独立的时间戳：

```python
{
    "simulation": {
        "total": 33,
        "passed": 33,
        "failed": 0,
        "timestamp": "2026-06-05 14:00:00"  # 模块测试时间
    },
    "state_machine": {
        "total": 20,
        "passed": 20,
        "failed": 0,
        "timestamp": "2026-06-05 14:15:00"  # 模块测试时间
    }
}
```

文档头部的总体时间戳反映最后更新时间：

```markdown
**Generated**: 2026-06-05 14:30:00  # 最后一个模块完成时间
```

## 最佳实践

### DO ✅

1. **使用合并工具**: 优先使用`merge_validation_docs.py`
2. **检查现有文档**: 生成前检查文档是否已存在
3. **保留历史数据**: 在文档中保留历史对比信息
4. **独立时间戳**: 每个模块使用独立的测试时间戳
5. **Git追踪**: 依赖Git历史追踪文档变化

### DON'T ❌

1. **不要简单覆盖**: 避免直接覆盖现有文档
2. **不要忽略现有内容**: 生成前必须读取现有文档
3. **不要混合不同类型**: 不要把单元测试和集成测试混在一起
4. **不要丢失模块结果**: 确保所有模块结果都被保留

## OpenSpec集成

### 在OpenSpec Tasks中使用

```markdown
### Task 2.3: Verify Unit Test Suite Completeness

**Steps**:
1. Test simulation module
2. Test state machine module
3. Test graph engine module

**Output**: `docs/validation/unit_test_status.md` (累积式更新)

**Validation Guidance**:
- Use cumulative update mode for multi-module testing
- Each module test should append results, not overwrite
- Use `scripts/merge_validation_docs.py` for result merging
- Final document should contain all module results
```

### Hook自动提示

当OpenSpec hook检测到多模块测试场景时：

```
[INFO] 检测到多模块测试任务
[ACTION] 建议使用累积式更新模式
[INFO] 第1/3模块：simulation
[INFO] 第2/3模块：state_machine
[INFO] 第3/3模块：graph_engine
[GUIDE] 使用 merge_validation_docs.py 工具处理结果合并
```

## 故障排除

### 问题1：文档内容混乱

**症状**: 文档中模块顺序错乱或格式不一致

**解决方案**: 使用标准化的合并工具，确保一致的文档结构

### 问题2：模块结果丢失

**症状**: 某些模块的测试结果没有出现在最终文档中

**解决方案**:
1. 检查是否使用了覆盖模式而非合并模式
2. 验证合并工具是否正确执行
3. 确认所有模块测试都调用了合并函数

### 问题3：时间戳错误

**症状**: 模块测试时间戳或文档生成时间不准确

**解决方案**:
1. 使用`datetime.now()`获取当前时间戳
2. 为每个模块记录独立的测试时间
3. 文档头部时间使用最后模块完成时间

## 工具API参考

### ValidationDocMerger类

```python
class ValidationDocMerger:
    def __init__(self, validation_dir: Path = None)
    def merge_unit_test_results(self, new_module_results: Dict) -> str
    def append_module_result(self, module_name: str, test_results: Dict) -> bool
```

### 使用示例

```python
# 初始化
merger = ValidationDocMerger()

# 方式1：批量合并
results = {
    "simulation": {"total": 33, "passed": 33, "failed": 0},
    "state_machine": {"total": 20, "passed": 20, "failed": 0}
}
updated_content = merger.merge_unit_test_results(results)

# 方式2：逐个追加
merger.append_module_result("simulation", {"total": 33, "passed": 33, "failed": 0})
merger.append_module_result("state_machine", {"total": 20, "passed": 20, "failed": 0})
```

## 总结

累积式更新是解决多模块测试覆盖问题的正确方法：

1. ✅ **避免数据丢失**: 所有模块结果都被保留
2. ✅ **保持历史**: 可以追踪每个模块的测试时间
3. ✅ **标准化输出**: 生成一致的文档格式
4. ✅ **自动化支持**: 提供工具和Hook自动处理
5. ✅ **Git友好**: 文件名固定，Git追踪完整历史

通过使用累积式更新，validation-documentation技能可以正确处理多模块测试场景，避免覆盖问题。

---

**维护者**: Uni-Claw开发团队  
**最后更新**: 2026-06-05  
**状态**: 生产就绪 ✅