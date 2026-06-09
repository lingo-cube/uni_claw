# PRD V6.15.0: 状态机与缓存管理测试迁移

## 概述
迁移 V6.11.0 引擎重构后的状态机和缓存管理测试。

## 背景
V6.11.0 引擎重构引入了以下变化：
- PageCacheManager 替代了直接缓存管理
- StepOrchestrator 替代了原有的状态机逻辑
- 异常处理链机制变化

## 需要迁移的测试

### 1. test_executor.py (3个) - 优先级: 高
- **TestCacheManagement::test_update_page_cache** - 测试已移除的 `_update_page_cache` 方法
- **TestCacheManagement::test_restore_from_cache** - 测试已移除的 `_restore_from_cache` 方法  
- **TestCacheManagement::test_restore_from_cache_miss** - 测试已移除的 `_restore_from_cache` 方法

**行动**: 删除这些测试（测试已移除功能）

### 2. test_state_machine_intelligence.py (8个) - 优先级: 中

#### PreconditionHandler (1个)
- **test_deeper_executes_back** - NameError: `action` 未定义

#### FrameCompleteHandler (2个)
- **test_auto_escape_clicks_unvisited_menu** - 状态期望 `NODE_SELECT`，实际 `ERROR_HANDLING`
- **test_auto_escape_fallback_to_back_when_no_unvisited** - 状态期望 `NODE_SELECT`，实际 `ERROR_HANDLING`

#### ErrorHandler (3个)
- **test_retry_with_remaining_retries** - 状态期望 `EXECUTE`，实际 `NODE_SELECT`
- **test_backtrack_pops_stack** - Mock.pop() 未被调用
- **test_abort_sets_terminated** - 状态期望 `BRANCH`，实际 `NODE_SELECT`

#### StepExceptionHandling (2个)
- **test_catches_handler_exception_and_routes_to_error_handling** - `last_error` 为 None
- **test_preserves_error_type_in_metadata** - 元数据缺少 `error_type`

**行动**: 需要检查测试逻辑是否仍然有效，修复或删除

## 实施计划

### 阶段 1: 删除已移除功能的测试
- [ ] 删除 test_executor.py 中的 3 个缓存管理测试

### 阶段 2: 分析状态机测试
- [ ] 分析状态机逻辑变化
- [ ] 确定哪些测试需要修复，哪些需要删除

### 阶段 3: 修复或删除
- [ ] 修复仍然有效的测试
- [ ] 删除过时的测试

## 依赖
- V6.11.0 引擎重构设计文档
- 状态机转换规范

## 验收标准
- [ ] 所有相关测试通过或已正确标记
- [ ] 无测试测试已移除的功能
