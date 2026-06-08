# V6.10.2 状态机逻辑与可观测性增强 - 提案

> **变更**: v6-10-2-state-machine-logic
> **创建日期**: 2026-06-08
> **状态**: 提案阶段
> **优先级**: P1
> **预计工时**: 5h

---

## 1. 问题陈述

### 1.1 当前问题

V6.10.1 实施后，调试工具和测试覆盖已得到增强，但状态机核心逻辑仍存在以下问题：

| 类别 | 具体问题 | 影响 |
|------|----------|------|
| **逻辑分散** | DYNAMIC_MATCH 节点处理逻辑分散在 `_handle_branch` 和 `_get_next_unvisited_child` 中 | 难以维护和测试 |
| **错误信息不足** | 状态转换错误缺少调试上下文（堆栈、历史、有效转换） | 定位问题耗时长 |
| **Trace 不完整** | 部分状态转换没有 Trace 记录 | 可观测性不足 |

### 1.2 根本原因

1. **未访问子节点检查逻辑未集中**：`_handle_branch` 对 DYNAMIC_MATCH 节点总是假设有未访问子节点
2. **状态转换断言信息简陋**：`transition_to()` 方法的错误信息缺少调试上下文
3. **状态转换 Trace 记录不统一**：部分转换有记录，部分没有

### 1.3 改进目标

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **代码组织** | 逻辑分散，难以测试 | 集中在独立方法，易于测试 |
| **错误信息** | 简单的 ValueError | 包含堆栈/历史/有效转换 |
| **Trace 完整性** | 部分转换有记录 | 所有转换都有记录 |

---

## 2. 解决方案概述

### 核心方案

1. **提取未访问子节点检查方法**：将 DYNAMIC_MATCH 节点处理逻辑集中到独立方法
2. **状态转换断言增强**：错误信息包含完整调试上下文
3. **状态转换 Trace 标准化**：确保所有状态转换都有 Trace 记录

### 预期成果

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **代码可维护性** | 逻辑分散 | 集中在独立方法 |
| **调试效率** | 错误信息简陋 | 包含完整上下文 |
| **可观测性** | 部分 trace | 完整 trace 记录 |

---

## 3. 范围

### 包含内容

- 在 `GraphTraversalEngine` 中新增 `_has_unvisited_children()` 私有辅助方法
- 优化 `TraversalStateMachine.transition_to()` 方法的错误信息
- 在 `transition_to()` 中添加 Trace 记录
- 在 `GraphTraversalEngine.__init__` 中注入 trace_recorder 给 state_machine
- 创建单元测试覆盖新增方法

### 排除内容

- 状态机核心逻辑重构（留待后续版本）
- 其他状态处理的优化
- 文档更新（V6.10.4 处理）

### 架构调整说明

**重要变更**：

原计划将 `has_unvisited_children()` 作为 `TraversalStateMachine` 的方法，但审阅发现这违反单一职责原则（StateMachine 不应负责图的逻辑检查，也不应依赖 GraphTraversalEngine）。

**调整方案**：将此方法移至 `GraphTraversalEngine` 作为私有辅助方法 `_has_unvisited_children()`。

---

## 4. 依赖关系

### 前置依赖

- **V6.10.1 debugging-tools**：调试工具已实现，本变更依赖其测试覆盖

### 后续变更

- **V6.10.3**：代码质量改进（可并行开始）
- **V6.10.4**：调试文档（可并行开始）

---

## 5. 成功标准

### 功能验证

- ✅ `_has_unvisited_children()` 方法能正确处理 STATIC 和 DYNAMIC_MATCH 策略
- ✅ `_has_unvisited_children()` 对 DYNAMIC_MATCH 节点不总是返回 True
- ✅ `transition_to()` 的错误信息包含：当前节点、目标节点、最近转换、有效转换列表
- ✅ 所有状态转换都有 Trace 记录（当 trace_recorder 存在时）

### 代码质量

- ✅ `_has_unvisited_children()` 方法通过 **mypy strict** 类型检查
- ✅ 所有新增/修改方法有完整类型注解（参数 + 返回值）
- ✅ 禁用 `Any` 类型（除 metadata 参数）
- ✅ 通过 **ruff** linting（零警告）
- ✅ 符合强类型要求（CLAUDE_CONVENTIONS.md §1）
- ✅ `_has_unvisited_children()` 方法圈复杂度 < 10

### 测试覆盖

- ✅ `test_has_unvisited_children.py` 覆盖率 **> 90%**
- ✅ `test_transition_to.py` 覆盖率 **> 85%**
- ✅ 所有测试命名符合 `test_<method>_<scenario>_<expected>` 格式
- ✅ 测试文件放置在 `tests/state_machine/`
- ✅ 测试使用 Given-When-Then 格式的 docstring

---

## 6. 实施估算

- **预计工时**: 5 小时
- **实施阶段**: P1（逻辑优化）
- **风险等级**: 中（修改核心逻辑）

---

## 7. 提案历史

| 日期 | 版本 | 变更内容 |
|------|------|----------|
| 2026-06-08 | 1.0 | 初始提案 |
