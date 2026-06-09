## ADDED Requirements

### Requirement: V6 Test API Synchronization
V6 测试代码 SHALL 与实现代码的 API 保持同步。

#### Scenario: Compiler tests use correct method names
- **WHEN** compiler 测试执行
- **THEN** 测试使用 `_build_completion_policy` 方法名（而非 `_map_completion_policy`）

#### Scenario: Dynamic matching tests use correct enum values
- **WHEN** dynamic_matching 测试执行
- **THEN** 测试使用 `MatchAction.GENERATE_CHILD` 枚举值（而非 `MatchAction.CLICK`）

#### Scenario: Popup handler tests use correct imports
- **WHEN** popup_handler 测试执行
- **THEN** 测试使用正确的模块导入和类结构

### Requirement: Test Fix Verification
修复后的测试 SHALL 能够成功运行并通过所有断言。

#### Scenario: All compiler tests pass
- **WHEN** 运行 `test_compiler.py`
- **THEN** 所有 21 个测试用例通过

#### Scenario: All dynamic matching tests pass
- **WHEN** 运行 `test_v6_9_dynamic_matching.py`
- **THEN** 所有 12 个测试用例通过

#### Scenario: All popup handler tests pass
- **WHEN** 运行 `test_popup_handler.py`
- **THEN** 所有 9 个测试用例通过

### Requirement: Test Coverage Maintenance
测试修复 SHALL 不降低整体测试覆盖率。

#### Scenario: Coverage remains stable
- **WHEN** 测试修复完成
- **THEN** V6 测试覆盖率保持或提高
