# uni-claw 车机菜单遍历测试引擎 - PRD V5.1

> **文档版本**: V5.1
> **基于版本**: PRD_V5 (2025-06-01)
> **当前更新**: 2026-05-31
> **状态**: 实施中（核心模块已完成，AI 模块待实施）
> **变更类型**: 实施进度更新 + 差异记录

---

## 文档说明

PRD V5.1 是 PRD_V5 的实施进度追踪版本。本文档记录了：
1. **原始提案**（来自 PRD_V5）
2. **当前实施状态**（已完成/未完成）
3. **提案与实施的差异**
4. **后续实施计划**

---

# 1. 产品概述（继承自 PRD_V5）

## 1.1 定位

**uni-claw V5.0** 是一个图驱动的智能遍历引擎，核心能力包括：
- **可规划的图模型**：静态节点 + 动态模板
- **严谨的状态机控制**：三层状态机（全局/遍历/栈）
- **可观测的 Trace**：全链路记录与回放
- **AI 增强与安全**：AIProvider + 安全策略（未实施）

---

# 2. 实施状态总览

## 2.1 实施进度表

| 模块 | PRD_V5 状态 | 当前实施状态 | 完成度 | 备注 |
|------|-------------|--------------|--------|------|
| **图模型** | ✅ 提案完整 | ✅ 已实施 | 100% | 与提案一致 |
| **三层状态机** | ✅ 提案完整 | ✅ 已实施 | 100% | 与提案一致 |
| **模板注册表** | ✅ 提案完整 | ✅ 已实施 | 100% | 与提案一致 |
| **Trace 系统** | ✅ 提案完整 | ✅ 已实施 | 100% | 与提案一致 |
| **异常处理链** | ✅ 提案完整 | ✅ 已实施 | 100% | 规则型处理 |
| **AIProvider** | ✅ 提案完整 | ❌ 未实施 | 0% | 仅有 NoOpAIProvider |
| **安全策略** | ✅ 提案完整 | ❌ 未实施 | 0% | 未集成 |
| **自然语言 API** | ❌ 无提案 | ✅ 已设计 | 设计 | 已完成设计文档 |
| **AI 异常处理** | ❌ 无提案 | ✅ 已设计 | 设计 | Phase 2 设计 |
| **截图存储** | ✅ 提案完整 | 🟡 部分实施 | 50% | 仅本地存储 |

**整体完成度**: 约 60% (核心功能已完成，AI 功能待实施)

---

## 2.2 已实施模块详情

### 2.2.1 图模型 (src/graph/)

**文件结构**:
```
src/graph/
├── node.py      # TraversalNode 及相关数据类
├── template.py  # 模板注册表与实例化
└── matcher.py   # 动态匹配逻辑
```

**实施内容**:
- ✅ `TraversalNode` - 统一节点抽象
- ✅ `NodeType` - container, leaf_switch, leaf_slider, leaf_action, leaf_info
- ✅ `Operation` - 操作定义（action, target, params, restore）
- ✅ `Precondition` - 前置条件
- ✅ `ChildrenStrategy` - 子节点生成策略（STATIC/DYNAMIC_MATCH/NONE）
- ✅ `DynamicRule` - 动态匹配规则
- ✅ `ErrorPolicy` - 错误处理策略

**与 PRD_V5 的一致性**: 100% 一致，无差异

---

### 2.2.2 三层状态机 (src/state_machine/)

**文件结构**:
```
src/state_machine/
├── global_fsm.py      # 全局状态机
├── traversal_fsm.py   # 遍历状态机
├── node_stack.py      # 节点栈
└── interaction.py     # 状态机协调器
```

**实施内容**:

#### 全局状态机
- ✅ 8 个状态: IDLE, INITIALIZING, TRAVERSING, PAUSED, ERROR, RECOVERING, COMPLETED, TERMINATED
- ✅ 状态转换验证
- ✅ 转换历史记录
- ✅ 状态回调机制

#### 遍历状态机
- ✅ 5 个状态: NODE_SELECT, PRECONDITION_CHECK, EXECUTE, RESULT_VERIFY, BRANCH
- ✅ 转换验证与历史
- ✅ 执行结果与前置条件结果存储

#### 节点栈
- ✅ 深度优先遍历支持
- ✅ StackFrame 结构（node, child_queue, current_child_idx, pending_restore）
- ✅ push/pop/top 操作

**与 PRD_V5 的一致性**: 100% 一致，无差异

---

### 2.2.3 模板注册表 (src/graph/template.py)

**实施内容**:
- ✅ `TemplateRegistry` - 模板管理
- ✅ `TemplateInstantiator` - 节点实例化
- ✅ `PlaceholderResolver` - 占位符解析（{{item_text}}, {{item_index}}, {{coordinate_x}}, {{coordinate_y}}, {{parent_id}}）
- ✅ `TemplateValidator` - 模板验证
- ✅ 内置模板: menu_container, switch_leaf, slider_leaf
- ✅ JSON 文件加载

**与 PRD_V5 的一致性**: 100% 一致，无差异

---

### 2.2.4 Trace 系统 (src/trace/)

**文件结构**:
```
src/trace/
├── models.py    # TraceStep, StateSnapshot, SessionInfo, TraversalTrace
├── recorder.py  # TraceRecorder
└── replay.py    # ReplayEngine
```

**实施内容**:
- ✅ `TraceRecorder` - 会话录制
- ✅ `TraceStep` - 步骤记录（全局状态、遍历状态、决策、执行结果、截图引用）
- ✅ `StateSnapshot` - 定期状态快照
- ✅ `SessionInfo` - 会话信息
- ✅ JSON Lines 格式输出
- ✅ 截图存储（本地）
- ✅ 三种回放模式: STRICT, DECISION, SIMULATION

**与 PRD_V5 的一致性**: 100% 一致，无差异

---

### 2.2.5 异常处理链 (src/exception/)

**文件结构**:
```
src/exception/
├── exceptions.py    # 异常定义
├── context.py       # ExceptionContext, ExceptionHandlingResult
├── chain.py         # ExceptionHandlingChain
└── handlers.py      # 各种 Handler
```

**实施内容**:
- ✅ `ExceptionHandlingChain` - 责任链模式
- ✅ 5 个 Handler: FatalExceptionHandler, DeviceExceptionHandler, UIExceptionHandler, RetryHandler, BacktrackHandler
- ✅ 异常类型: ElementNotFoundException, PathMismatchException, ClickFailedException, ADBDisconnectedException, AppCrashException, PopupDetectedException, etc.
- ✅ 异常历史记录

**与 PRD_V5 的一致性**: 100% 一致，实现了规则型异常处理

---

## 2.3 未实施模块详情

### 2.3.1 AIProvider 四项能力

**PRD_V5 提案**:
1. 能力1: 自然语言 → 遍历计划 (`parse_task_to_plan`)
2. 能力2: 页面类型验证 (`verify_page_type`)
3. 能力3: 元素安全预筛 (`screen_elements`)
4. 能力4: 上下文决策 (`make_decision`)

**当前状态**:
- ❌ 仅 `NoOpAIProvider` 占位实现
- ❌ 四项能力均未实现
- ❌ SafetyPolicy 未集成

**影响**:
- 无法使用 AI 辅助遍历
- 无法智能决策
- 无法进行元素安全预筛

---

### 2.3.2 安全策略 (SafetyPolicy)

**PRD_V5 提案**:
- 四层安全防护（元素预筛、AI 内部、全局过滤器、设备驱动层）
- 危险文本黑名单
- 操作白名单
- 坐标越界检查

**当前状态**:
- ❌ 未实施
- ❌ 未与 AIProvider 集成

---

### 2.3.3 自然语言测试 API

**PRD_V5 提案**: 未包含

**当前状态**:
- ✅ 设计文档完成 (`docs/natural_language_test_api.md`)
- ❌ 未实施

**设计内容**:
- `NaturalLanguageExecutor` - 自然语言命令执行器
- `CommandParser` - 命令解析器
- 支持操作: 点击、输入、等待、验证、滑动、返回
- 支持层级路径: "点击车辆设置/DiLink/互联/移动数据"

---

### 2.3.4 AI 驱动异常处理

**PRD_V5 提案**: 未包含（仅 Phase 2 提及）

**当前状态**:
- ✅ 设计文档完成 (`docs/ai_driven_exception_handling.md`)
- ❌ 未实施

**设计内容**:
- `AIDrivenExceptionHandler` - AI 驱动的异常处理
- `AIDecision` - AI 决策数据结构
- `AIDecisionLearner` - 学习与优化
- 决策类型: RETRY, SKIP, BACKTRACK, NAVIGATE, RECOVER, WAIT_AND_RETRY

---

## 2.4 实施差异总结

### 2.4.1 命名差异

| PRD_V5 提案 | 实际实现 | 说明 |
|-------------|----------|------|
| `StrategyType` | `ChildrenStrategyType` | 更精确的命名 |
| 无 | `StackFrame` | 实现中新增的数据类 |

### 2.4.2 功能差异

1. **截图存储**: PRD_V5 提案了本地和 OSS 两种存储方式，当前仅实现本地存储
2. **Trace 更新机制**: 实现采用了重写文件的方式（非追加），存在优化空间

### 2.4.3 新增设计（PRD_V5 未包含）

1. **自然语言测试 API** - 作为新功能设计
2. **AI 驱动异常处理** - 作为 Phase 2 增强设计

---

# 3. 文件结构对照

## 3.1 PRD_V5 预期结构

```
src/
├── graph/              # 图模型
├── state_machine/      # 三层状态机
├── trace/              # Trace 系统
├── ai/                 # AIProvider (未实施)
├── safety/             # 安全策略 (未实施)
├── exception/          # 异常处理
└── traversal/          # 遍历引擎
```

## 3.2 实际结构

```
src/
├── graph/              ✅ 已实施
│   ├── node.py
│   ├── template.py
│   └── matcher.py
├── state_machine/      ✅ 已实施
│   ├── global_fsm.py
│   ├── traversal_fsm.py
│   ├── node_stack.py
│   └── interaction.py
├── trace/              ✅ 已实施
│   ├── models.py
│   ├── recorder.py
│   └── replay.py
├── exception/          ✅ 已实施（规则型）
│   ├── chain.py
│   ├── context.py
│   ├── handlers.py
│   └── exceptions.py
├── ai/                 ❌ 未实施
├── safety/             ❌ 未实施
└── traversal/          ✅ 已实施
```

---

# 4. 后续实施计划

## 4.1 短期计划（1-2 个迭代）

| 任务 | 优先级 | 预估工作量 |
|------|--------|------------|
| 完善 Trace 回放功能 | P0 | 2 天 |
| 实现自然语言测试 API | P1 | 3 天 |
| 完善异常处理与 Trace 集成 | P1 | 2 天 |

## 4.2 中期计划（3-6 个迭代）

| 任务 | 优先级 | 预估工作量 |
|------|--------|------------|
| 实施 AIProvider 接口 | P0 | 5 天 |
| 实现安全策略 (SafetyPolicy) | P0 | 3 天 |
| 实现元素安全预筛 | P1 | 3 天 |
| 实现上下文决策能力 | P1 | 4 天 |

## 4.3 长期计划（Phase 2）

| 任务 | 优先级 | 预估工作量 |
|------|--------|------------|
| AI 驱动异常处理 | P2 | 5 天 |
| AI 决策学习与优化 | P2 | 4 天 |
| 自然语言 → 遍历计划 | P2 | 5 天 |
| OSS 截图存储实现 | P3 | 2 天 |

---

# 5. 成功指标更新

## 5.1 已达成指标

- ✅ 图模型 + 状态机覆盖车机设置菜单
- ✅ 模板注册表支持动态节点生成
- ✅ Trace 记录完整执行历史
- ✅ 三种回放模式支持

## 5.2 待达成指标

- ❌ AIProvider 四项能力可用性
- ❌ 安全策略拦截率
- ❌ 元素预筛准确率
- ❌ AI 异常处理恢复率

---

# 6. 风险与依赖

## 6.1 技术风险

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| AI 决策准确性 | 中 | 先实现规则型兜底，AI 仅作为增强 |
| 安全策略覆盖不全 | 高 | 持续积累黑名单和白名单 |
| Trace 文件过大 | 中 | 实现 TTL 清理和压缩 |

## 6.2 外部依赖

- AI 服务可用性（Claude API / MiMo API）
- ADB 连接稳定性
- 设备截图速度

---

*本 PRD V5.1 文档记录了 PRD_V5 的实施进度，随开发进展持续更新。*
