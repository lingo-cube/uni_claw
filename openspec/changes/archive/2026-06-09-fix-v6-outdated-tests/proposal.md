## Why

V6 单元测试存在 42 个失败用例，主要原因是测试代码与实现代码 API 不同步。这些失败阻止了 CI/CD 流程，降低了测试可信度，且可能掩盖真实的功能回归问题。

## What Changes

修复以下三个测试模块的 API 不同步问题：

- **test_compiler.py** (21 failures): 更新方法名 `_map_completion_policy` → `_build_completion_policy`
- **test_v6_9_dynamic_matching.py** (12 failures): 更新枚举值 `MatchAction.CLICK` → `MatchAction.GENERATE_CHILD`
- **test_popup_handler.py** (9 failures): 更新导入和断言以匹配当前实现

## Capabilities

### New Capabilities
无新功能，仅修复现有测试。

### Modified Capabilities

- `v6-test-fixture`: V6 测试固件和断言更新
  - **变更**: 同步测试代码与实现代码的 API
  - **影响**: 仅测试代码，不影响生产代码

## Impact

**受影响的代码**:
- `tests/v6/unit/test_compiler.py`
- `tests/v6/test_v6_9_dynamic_matching.py`  
- `tests/v6/unit/test_popup_handler.py`

**受影响的模块**:
- `src/graph/compiler.py` - 测试将使用正确的方法名
- `src/graph/matcher.py` - 测试将使用正确的枚举值
- `src/state_machine/popup_handler.py` - 测试将使用正确的导入

**依赖**: 无新增依赖

**风险**: 低 - 仅修改测试代码，不改变生产代码行为
