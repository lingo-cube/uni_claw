# Proposal: V6.9.1 Test System Refactor

## Why

当前仿真测试体系存在三类问题导致测试无法正常运行：死引用导致导入错误、测试代码重复冗余、新增功能（V6.7-V6.9）无测试覆盖。需要清理死代码、补全测试覆盖，并建立能发现真实缺陷的测试体系。

## What Changes

- **清理死代码**：删除 `conftest.py` 中 7 个引用不存在模块的 fixture、删除重复文件 `test_v6_4_simulation_alignment.py`、重命名 `test_simulation.py` → `test_simulation_base.py`、修复 `test_simulation_e2e.py` 中的死引用
- **新建共享辅助模块**：创建 `tests/helpers/` 目录，添加工厂函数、状态验证、Trace 分析、随机化测试、边界测试、故障注入等工具
- **补全虚拟页面 Fixture**：创建 5 个新的 JSON fixture 文件（pages_dynamic.json、pages_correction.json、pages_entry.json、pages_boundary.json、pages_chaos.json）
- **扩展测试覆盖**：D/C/P/E2E/M 系列共 44 基础场景 + 20+ 复杂场景
- **修正 MockVisionService 解析格式**：使用 `coordinate.x/y` 替代 `bounds` 字段

## Capabilities

### New Capabilities

- `test-helpers`: 共享测试辅助模块，包括工厂函数、状态深度验证、Trace 分析、随机化测试、边界测试、故障注入、真实场景模拟、压力测试
- `dynamic-matching-tests`: D 系列动态匹配集成测试（10 基础 + 3 扩展场景）
- `compiler-tests`: C 系列计划编译器测试（12 场景）
- `smart-correction-tests`: P 系列状态机智能纠正测试（10 场景）
- `e2e-tests`: E2E 系列端到端测试（7 场景）
- `mock-validation-tests`: M 系列 Mock 服务映射验证（5 场景）

### Modified Capabilities

- 无

## Impact

- **代码变更**：`tests/` 目录下大量新增和修改文件，预计新增 2000+ 行测试代码
- **依赖变更**：无新增外部依赖，仅新增内部辅助模块
- **测试执行**：完成后 `pytest tests/v6/ tests/integration/` 应 100% 通过
- **时间估算**：5 周，分 9 个阶段实施
- **风险**：低风险，仅涉及测试代码，不影响生产代码
