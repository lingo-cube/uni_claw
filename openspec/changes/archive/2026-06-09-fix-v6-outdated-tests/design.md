## Context

V6 单元测试套件存在 42 个失败用例，分布在三个测试文件中。这些失败是由于测试代码与实现代码的 API 演进不同步造成的：

1. **PlanCompiler API 重构**: 编译器从 `_map_completion_policy` 重命名为 `_build_completion_policy`
2. **MatchAction 枚举更新**: 匹配动作枚举移除了 `CLICK` 值，使用 `GENERATE_CHILD` 代替
3. **PopupHandler 模块结构变化**: 弹窗处理器模块的导入和类结构发生变化

**当前状态**:
- 729 个 V6 测试通过，92 个失败
- 失败主要集中在 compiler (21), dynamic_matching (12), popup_handler (9)
- 所有测试的功能模块仍在使用中

**约束**:
- 仅修改测试代码，不改变生产代码行为
- 保持测试覆盖率不降低
- 确保修复后的测试能验证正确的 API 行为

## Goals / Non-Goals

**Goals**:
- 修复所有 42 个失败测试用例
- 同步测试代码与当前实现代码的 API
- 确保 V6 测试套件完全通过

**Non-Goals**:
- 不添加新测试用例
- 不重构测试结构
- 不修改生产代码

## Decisions

### 决策 1: 直接更新测试代码 vs 重写测试

**选择**: 直接更新测试代码

**理由**:
- 失败原因是 API 不同步，测试逻辑本身正确
- 直接更新更快速，风险更低
- 保留原有的测试结构和意图

### 决策 2: 修复顺序

**选择**: compiler → dynamic_matching → popup_handler

**理由**:
- compiler 失败最多 (21)，修复后能快速看到效果
- dynamic_matching 依赖 compiler 的正确性
- popup_handler 相对独立，可以最后处理

### 决策 3: 验证方式

**选择**: 每个模块修复后立即运行测试验证

**理由**:
- 快速发现问题
- 避免累积错误
- 确保每个修复都是正确的

## Risks / Trade-offs

**风险 1**: 测试断言可能与新的 API 行为不匹配
→ **缓解**: 逐个运行测试，检查断语义而非仅语法

**风险 2**: 修复一个测试可能导致其他测试失败
→ **缓解**: 每次修复后运行完整测试套件

**风险 3**: 某些测试可能已经过时，不再反映当前需求
→ **缓解**: 如果测试确实过时，标记为 skip 而非强制修复

## Migration Plan

无需迁移计划，因为仅修改测试代码：

1. 修复 `test_compiler.py` (21 tests)
2. 修复 `test_v6_9_dynamic_matching.py` (12 tests)
3. 修复 `test_popup_handler.py` (9 tests)
4. 运行完整 V6 测试套件验证
5. 提交代码

**回滚策略**: 如果测试修复导致问题，可以简单回滚到修复前的版本。

## Open Questions

无。这是一个直接的测试同步工作，技术决策明确。
