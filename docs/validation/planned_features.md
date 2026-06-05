# V6 未实现功能追踪

> **创建日期**: 2026-06-04  
> **状态**: 待实现功能清单  
> **搜索标签**: `V6_UNIMPLEMENTED_FEATURES`

---

## 📊 功能实现状态概览

| 类别 | 已实现 | 未实现 | 总计 | 完成率 |
|------|--------|--------|------|--------|
| 状态定义 | 8/8 | 0/8 | 8 | 100% |
| 状态转换规则 | 8/8 | 0/8 | 8 | 100% |
| 便捷入口方法 | 0/3 | 3/3 | 3 | 0% |
| 状态转换方法 | 0/8 | 8/8 | 8 | 0% |
| **总计** | **16** | **11** | **27** | **59%** |

---

## 🔴 未实现的便捷入口方法 (3个)

### 1. `handle_frame_complete()`
**功能**: 容器框架完成处理
**描述**: 当容器节点执行框架完成时，决定下一步fallback操作
**Fallback策略**: BACK, AUTO_ESCAPE, SKIP, ABORT
**测试位置**: `tests/v6/test_state_machine.py::TestStateHandlerMethods::test_handle_frame_complete`
**优先级**: HIGH - 容器节点遍历的关键功能

**实现参考**:
```python
def handle_frame_complete(self) -> bool:
    """Handle container frame completion and decide fallback action."""
    self.transition_to(TraversalState.FRAME_COMPLETE)
    # Implementation pending: fallback decision logic
    return True
```

### 2. `handle_error()`
**功能**: 错误处理入口
**描述**: 任何状态发生错误时，进入错误处理状态
**错误策略**: SKIP, RETRY, BACKTRACK, CONTINUE
**测试位置**: `tests/v6/test_state_machine.py::TestStateHandlerMethods::test_handle_error`
**优先级**: HIGH - 错误恢复的核心功能

**实现参考**:
```python
def handle_error(self, error: Exception) -> bool:
    """Handle error and enter error handling state."""
    self.transition_to(TraversalState.ERROR_HANDLING)
    # Implementation pending: error classification and strategy selection
    return True
```

### 3. `handle_popup()`
**功能**: 弹窗检测和处理
**描述**: 检测到弹窗时，进入弹窗处理状态
**弹窗策略**: 识别 → 关闭 → 验证 → 恢复
**测试位置**: `tests/v6/test_state_machine.py::TestStateHandlerMethods::test_handle_popup`
**优先级**: MEDIUM - 弹窗处理的便捷入口

**实现参考**:
```python
def handle_popup(self) -> bool:
    """Handle popup detection and enter popup handling state."""
    self.transition_to(TraversalState.POPUP_HANDLING)
    # Implementation pending: popup detection and handling logic
    return True
```

---

## 🔴 未实现的状态转换方法 (8个)

### FRAME_COMPLETE状态转换方法

#### 4. `frame_complete_to_node_select()`
**功能**: 完成后选择下一个节点
**测试位置**: `tests/v6/test_state_machine.py::TestFallbackActions::test_frame_complete_to_node_select`
**优先级**: HIGH

#### 5. `frame_complete_failed()`
**功能**: 框架处理失败，进入错误处理
**测试位置**: `tests/v6/test_state_machine.py::TestFallbackActions::test_frame_complete_failed`
**优先级**: HIGH

### ERROR_HANDLING状态转换方法

#### 6. `error_to_node_select()`
**功能**: SKIP策略 - 跳过错误继续下一个
**测试位置**: `tests/v6/test_state_machine.py::TestErrorHandlingMethods::test_error_to_node_select`
**优先级**: HIGH

#### 7. `error_to_execute()`
**功能**: RETRY策略 - 重试当前操作
**测试位置**: `tests/v6/test_state_machine.py::TestErrorHandlingMethods::test_error_to_execute`
**优先级**: HIGH

#### 8. `error_to_frame_complete()`
**功能**: BACKTRACK策略 - 回溯到容器状态
**测试位置**: `tests/v6/test_state_machine.py::TestErrorHandlingMethods::test_error_to_frame_complete`
**优先级**: HIGH

#### 9. `error_to_branch()`
**功能**: CONTINUE策略 - 继续分支决策
**测试位置**: `tests/v6/test_state_machine.py::TestErrorHandlingMethods::test_error_to_branch`
**优先级**: HIGH

### POPUP_HANDLING状态转换方法

#### 10. `popup_handled()`
**功能**: 弹窗处理成功，恢复到被中断状态
**测试位置**: `tests/v6/test_state_machine.py::TestPopupHandlingMethods::test_popup_handled`
**优先级**: MEDIUM

#### 11. `popup_handling_failed()`
**功能**: 弹窗处理失败，进入错误处理
**测试位置**: `tests/v6/test_state_machine.py::TestPopupHandlingMethods::test_popup_handling_failed`
**优先级**: MEDIUM

---

## 🎯 实现优先级建议

### Phase 1: 核心错误处理 (HIGH优先级)
1. `handle_error()` - 错误处理入口
2. `error_to_node_select()` - SKIP策略
3. `error_to_execute()` - RETRY策略  
4. `error_to_frame_complete()` - BACKTRACK策略
5. `error_to_branch()` - CONTINUE策略

**理由**: 错误处理是遍历引擎稳定性的基础

### Phase 2: 容器节点支持 (HIGH优先级)
6. `handle_frame_complete()` - 容器框架处理
7. `frame_complete_to_node_select()` - 完成后继续
8. `frame_complete_failed()` - 容器失败处理

**理由**: 容器节点是复杂应用结构的基础

### Phase 3: 弹窗处理 (MEDIUM优先级)
9. `handle_popup()` - 弹窗处理入口
10. `popup_handled()` - 弹窗成功恢复
11. `popup_handling_failed()` - 弹窗失败处理

**理由**: 弹窗处理是遍历稳定性的增强功能

---

## 🔍 如何搜索和识别

### 在代码中搜索
```bash
# 搜索所有未实现功能标记
grep -r "V6_UNIMPLEMENTED_FEATURES" tests/

# 搜索pytest skip标记
grep -r "@pytest.mark.skip" tests/v6/test_state_machine.py
```

### 在测试报告中查看
```bash
# 查看跳过的测试
pytest tests/v6/test_state_machine.py -v --tb=no

# 只统计跳过的测试
pytest tests/v6/test_state_machine.py -v --tb=no | grep SKIPPED
```

### 在此文档中更新
当实现功能时：
1. 从未实现列表中移除
2. 更新功能实现状态概览表
3. 在实现记录中添加完成日期和备注

---

## 📝 设计参考文档

这些功能在以下设计文档中定义：

- **架构文档**: `docs/architecture/ARCHITECTURE_V6.md`
- **PRD V6.0**: `docs/archive/prd/PRD_V6_0-simulation-testing.md`
- **状态机设计**: `docs/architecture/concepts/hierarchical-state-machine.md`

---

## ✅ 实现记录

### 已完成功能
无 - 本文档创建时，所有功能都处于待实现状态

### 正在实现的功能
(如有功能正在实现，在此记录开始日期)

### 待实现功能
见上述11个未实现功能列表

---

## 🚀 下一步行动

1. **创建OpenSpec变更**: "V6 State Machine Enhancement"
2. **制定实现计划**: 按优先级分阶段实现
3. **逐步启用测试**: 每实现一个功能，移除对应的skip标记
4. **更新此文档**: 实时追踪实现进度

---

**最后更新**: 2026-06-04  
**下次审查**: 当开始实现V6功能时