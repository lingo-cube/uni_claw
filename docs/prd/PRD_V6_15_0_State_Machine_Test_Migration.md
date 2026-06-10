# PRD V6.15.0: 状态机测试迁移

> **版本**: V6.15.0
> **日期**: 2026-06-10
> **依赖**: V6.11.0, V6.13.0, V6.14.0
> **状态**: 设计阶段

---

## 1. 目的与目标

### 1.1 目的

修复 V6.11.0 引擎重构后失败的状态机相关测试，确保测试套件准确反映当前架构。

### 1.2 目标

- [ ] 修复所有因测试设置问题而失败的测试（8个）
- [ ] 删除测试已移除功能的测试（0个）
- [ ] 确保测试覆盖面不降低
- [ ] 建立测试辅助层以降低未来API变更影响

---

## 2. 背景

### 2.1 V6.11.0 架构变化

V6.11.0 引擎重构引入了以下变化：

| 组件 | 变化 |
|------|------|
| **TraversalStateMachine** | 仍然存在，负责状态转换逻辑 |
| **StepOrchestrator** | 新增编排层，协调状态机执行 |
| **PageCacheManager** | 替代直接缓存管理 |
| **异常处理** | 异常链机制变化 |

**架构澄清**：StepOrchestrator **不是**状态机的替代品，而是**编排层**。TraversalStateMachine 仍然存在且活跃，负责核心状态转换。

### 2.2 依赖验证

| 依赖 | 状态 | 验证证据 |
|------|------|----------|
| V6.13.0 | ✅ 完成 | Commit `11bcf06`: 46 个导入更新，`src/state/` 目录删除 |
| V6.14.0 | ✅ 完成 | Commit `34aba4c`: 37 个过时测试删除，helper 层创建 |

### 2.3 测试失败情况

共 **8 个测试失败**，分布在 `test_state_machine_intelligence.py` 中：

| 测试类别 | 失败数 | 主要问题类型 |
|----------|--------|-------------|
| PreconditionHandler | 1 | 测试 bug (变量名错误) |
| FrameCompleteHandler | 2 | 测试设置问题 (缺少 mock 上下文) |
| ErrorHandler | 3 | 测试设置问题 (缺少 retry_count 设置) |
| StepExceptionHandling | 2 | API 理解问题 (metadata 快照时机) |

**关键发现**：初步分析显示多数失败为**测试设置问题**，需阶段0实际日志验证后确认。非产品逻辑变化。

---

## 3. 失败测试 AI 可执行分析

> **格式说明**: 每个测试包含 `root_cause` (代码证据)、`fix_strategy` (代码变更)、`verification_steps` (断言)

### 3.1 test_deeper_executes_back

```json
{
  "test_name": "test_deeper_executes_back",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "line": 229,
  "failure_type": "NameError: name 'action' is not defined",
  
  "root_cause": {
    "evidence": "测试代码第 229 行使用变量 'action'，但 fixture 定义为 'mock_action'",
    "code_reference": "line 229: result = handler.execute(action, context)",
    "conclusion": "简单笔误 - action 应为 mock_action"
  },
  
  "fix_strategy": {
    "action": "重命名变量",
    "change": "将第 229 行的 'action' 替换为 'mock_action'",
    "file": "tests/v6/test_state_machine_intelligence.py",
    "line": 229
  },
  
  "verification_steps": [
    "grep -n 'mock_action' tests/v6/test_state_machine_intelligence.py",
    "验证所有使用 handler.execute() 的地方都传入 mock_action",
    "运行 pytest tests/v6/test_state_machine_intelligence.py::test_deeper_executes_back -v"
  ]
}
```

---

### 3.2 test_auto_escape_clicks_unvisited_menu

```json
{
  "test_name": "test_auto_escape_clicks_unvisited_menu",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "AssertionError: 期望 NODE_SELECT，实际 ERROR_HANDLING",
  
  "root_cause": {
    "evidence": "traversal_fsm.py 第 1096-1105 行: _handle_frame_complete_state() 捕获所有异常并返回 ERROR_HANDLING",
    "code_reference": """
    def _handle_frame_complete_state(self, transition: Transition) -> State:
        try:
            # ... 检查是否有未访问子节点 ...
            return State.NODE_SELECT
        except Exception as e:
            self._last_error = e
            return State.ERROR_HANDLING
    """,
    "conclusion": "测试 mock 场景缺少必要的上下文属性，触发异常进入 ERROR_HANDLING"
  },
  
  "fix_strategy": {
    "action": "修复测试 mock 设置",
    "change": "确保 mock context 包含所有必需属性: current_page_analysis, node_stack, failed_nodes",
    "mock_example": "context.current_page_analysis = Mock(); context.node_stack = []; context.failed_nodes = {}"
  },
  
  "verification_steps": [
    "检查测试 fixture 是否包含完整 context 属性",
    "验证 _handle_frame_complete_state 访问的所有属性都已 mock",
    "运行单个测试验证修复"
  ]
}
```

---

### 3.3 test_auto_escape_fallback_to_back_when_no_unvisited

```json
{
  "test_name": "test_auto_escape_fallback_to_back_when_no_unvisited",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "AssertionError: 期望 NODE_SELECT，实际 ERROR_HANDLING",
  
  "root_cause": {
    "evidence": "与 3.2 相同 - _handle_frame_complete_state() 捕获异常",
    "code_reference": "traversal_fsm.py:1096-1105",
    "conclusion": "测试 mock 场景缺少必要的上下文属性"
  },
  
  "fix_strategy": {
    "action": "修复测试 mock 设置",
    "change": "与 3.2 相同 - 确保完整 context 属性",
    "note": "与 test_auto_escape_clicks_unvisited_menu 共享相同的修复"
  },
  
  "verification_steps": [
    "与 3.2 相同"
  ]
}
```

---

### 3.4 test_retry_with_remaining_retries

```json
{
  "test_name": "test_retry_with_remaining_retries",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "AssertionError: 期望 EXECUTE，实际 NODE_SELECT",
  
  "root_cause": {
    "evidence": "traversal_fsm.py 第 1147-1167 行: EXECUTE 只在 retry_count < policy.max_retries 时返回",
    "code_reference": """
    def _handle_error_state(self, transition: Transition) -> State:
        failed_info = self.context.failed_nodes.get(node_id)
        if failed_info and failed_info.get('retry_count', 0) < self.error_policy.max_retries:
            return State.EXECUTE
        return State.NODE_SELECT
    """,
    "conclusion": "测试未设置 context.failed_nodes[node_id]['retry_count']，默认返回 NODE_SELECT"
  },
  
  "fix_strategy": {
    "action": "添加 retry_count 到测试设置",
    "change": "在测试中设置: context.failed_nodes = {'test_node': {'retry_count': 0, 'last_error': ...}}",
    "requirement": "retry_count 必须小于 error_policy.max_retries（默认值通常为 3）"
  },
  
  "verification_steps": [
    "检查 context.failed_nodes 结构是否正确",
    "验证 retry_count < max_retries 条件",
    "运行测试验证返回 EXECUTE 状态"
  ]
}
```

---

### 3.5 test_backtrack_pops_stack

```json
{
  "test_name": "test_backtrack_pops_stack",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "MockAssertionError: pop() 未被调用",
  
  "root_cause": {
    "evidence": "需要检查回退逻辑是否使用 stack.pop() 或其他方法",
    "pending_analysis": "需要阶段 0 实际日志确认实现细节",
    "conclusion": "待定 - 可能是回退逻辑变化或测试设置问题"
  },
  
  "fix_strategy": {
    "action": "待阶段 0 分析后确定",
    "pending": true
  },
  
  "verification_steps": [
    "运行测试收集完整日志",
    "检查实际回退实现"
  ]
}
```

---

### 3.6 test_abort_sets_terminated

```json
{
  "test_name": "test_abort_sets_terminated",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "AssertionError: 期望 BRANCH，实际 NODE_SELECT",
  
  "root_cause": {
    "evidence": "需要检查终止条件逻辑",
    "pending_analysis": "需要阶段 0 实际日志确认",
    "conclusion": "待定 - 可能是终止条件变化或测试设置问题"
  },
  
  "fix_strategy": {
    "action": "待阶段 0 分析后确定",
    "pending": true
  },
  
  "verification_steps": [
    "运行测试收集完整日志",
    "检查终止条件实现"
  ]
}
```

---

### 3.7 test_catches_handler_exception_and_routes_to_error_handling

```json
{
  "test_name": "test_catches_handler_exception_and_routes_to_error_handling",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "AssertionError: last_error 为 None",
  
  "root_cause": {
    "evidence": "异常未被捕获或记录",
    "pending_analysis": "需要检查异常捕获机制和测试触发方式",
    "conclusion": "待定 - 可能是异常未正确触发或捕获机制变化"
  },
  
  "fix_strategy": {
    "action": "待阶段 0 分析后确定",
    "pending": true
  },
  
  "verification_steps": [
    "验证异常确实被触发",
    "检查 _last_error 记录位置"
  ]
}
```

---

### 3.8 test_preserves_error_type_in_metadata

```json
{
  "test_name": "test_preserves_error_type_in_metadata",
  "file": "tests/v6/test_state_machine_intelligence.py",
  "failure_type": "AssertionError: 元数据缺少 error_type",

  "root_cause": {
    "evidence": "traversal_fsm.py 第 1490-1514 行: error_type 在异常处理时添加到 metadata",
    "code_reference": """
    except Exception as e:
        self._last_error = e
        transition.metadata['error_type'] = type(e).__name__  # 这里添加
        return State.ERROR_HANDLING
    """,
    "class_definition": {
      "note": "TraversalStateTransition 是 dataclass，metadata 字段定义",
      "verification_required": "需验证 TraversalStateTransition 是否使用 field(default_factory=dict) 或普通字典",
      "code_check": "rg 'class TraversalStateTransition' src/state_machine/ -A 20",
      "implication": "如果使用 field(default_factory=dict)，每次创建新实例时 metadata 是新字典，但仍支持引用传递"
    },
    "conclusion": "transition.metadata 是异常处理前的快照，测试检查的是快照而非处理后的结果",
    "verification_needed": [
      "需在阶段0验证 TraversalStateTransition dataclass 定义",
      "验证 metadata 是 field(default_factory=dict) 还是普通字典赋值",
      "用 debugger 追踪 transition 对象生命周期",
      "确认 transition_to() 调用时 **metadata 展开语法是否复制字典"
    ],
    "contradiction_note": "如果 transition.metadata 是字典引用，则 mutation 应该在原对象可见。需验证 Transition 类是否在某个节点创建了副本"
  },
  
  "fix_strategy": {
    "action": "阶段0 dataclass 行为验证实验",
    "change": "创建小实验验证 field(default_factory=dict) 与 **metadata 展开语法的组合行为",
    "experiment_required": {
      "step_1": "验证 TraversalStateTransition 使用 field(default_factory=dict)",
      "step_2": "验证 transition_to() 调用方式 - 是否直接传递 transition 对象或创建副本",
      "step_3": "确认 **metadata 展开后，修改是否影响原对象",
      "expected_outcome": "确定测试应检查 transition 对象本身还是需要访问其他机制（如 _last_error）"
    },
    "pending_decision": "基于实验结果确定修复策略 - 可能需要调整测试访问点或修改 transition 传递方式"
  },

  "verification_steps": [
    "在阶段0执行 dataclass 行为验证实验",
    "根据实验结果确定修复方向",
    "应用修复并验证测试通过"
  ]
}
```

---

## 4. 测试保留与删除决策框架

### 4.1 决策标准

测试应**保留**当满足以下任一条件：
- ✅ 测试核心功能仍然存在
- ✅ 测试提供独特的覆盖面（无替代测试）
- ✅ 测试验证关键架构约束

测试应**删除**当满足以下所有条件：
- ❌ 测试功能已移除
- ❌ 有替代测试提供相同覆盖面
- ❌ 测试假设已被新架构取代

### 4.2 "正确标记"定义

| 标记类型 | 使用场景 | 行为 | 对指标的影响 |
|----------|----------|------|-------------|
| `pytest.mark.skip` | 测试暂时跳过（待修复） | 跳过，不计入覆盖率 | ❌ 仍计入"失败测试数" |
| `pytest.mark.xfail` | 已知失败，确认为产品 bug | 跳过，如通过则报错 | ✅ 不计入"失败测试数" |
| 删除 | 测试功能已移除且无替代测试 | 永久移除 | ✅ 不计入"失败测试数" |

**本 PRD 策略**:
- 优先修复，删除为最后手段
- 产品 bug 使用 `pytest.mark.xfail(reason="product bug: ...")`
- 删除前必须验证覆盖面影响（使用 `pytest --cov`）

---

## 5. 实施计划

### 阶段 0: 数据收集 (必须首先完成)

#### 0.0 失败模式分类学定义

在分析具体失败测试前，建立统一的失败分类标准：

| 失败模式 | 描述 | 典型原因 | 修复策略 |
|---------|------|---------|---------|
| **Mock设置问题** | 测试mock不完整或缺失 | fixture缺少必需字段 | 补充fixture属性 |
| **测试设计问题** | 测试逻辑或断言错误 | 变量名错误、断言位置错误 | 修正测试代码 |
| **产品逻辑变化** | 测试假设的功能已变化 | API变更、架构重构 | 更新测试或标记xfail |
| **测试覆盖面缺口** | 测试遗漏边界条件 | 正常路径通过但边界失败 | 添加缺失测试 |
| **时序问题** | 异步操作时序错误 | event未正确等待 | 调整时序或同步 |
| **状态管理问题** | 状态不一致或状态泄漏 | 全局状态污染 | 重置状态或隔离 |

**应用方式**: 阶段0.7专项测试数据收集后，使用此分类学归类3.5-3.7的pending测试，避免盲目修复。

#### 0.0 pytest 环境验证
- [ ] 验证 pytest 环境可用
  ```bash
  # 验证 pytest 版本
  pytest --version

  # 验证测试可收集
  pytest tests/v6/test_state_machine_intelligence.py --collect-only -q

  # 验证测试可运行（不关心结果）
  # Windows Git Bash: > /dev/null 2>&1 应该可以工作，如果不工作则使用 > NUL 2>&1
  pytest tests/v6/test_state_machine_intelligence.py -v > /dev/null 2>&1 && echo "Tests executable"
  ```
- [ ] 记录 pytest 版本和测试数量到附录 9.1

**Windows/Git Bash 兼容性说明**:
- 当前环境: platform: win32, shell: bash (Git Bash)
- `> /dev/null 2>&1` 在 Git Bash 中应该正常工作
- 如果遇到问题，使用 Windows 原生语法: `> NUL 2>&1`
- 阶段0的所有 bash 命令都需要验证在当前环境下的兼容性

**验收标准**: pytest 环境可用，测试可正常收集和运行

#### 0.1 代码静态分析
- [ ] 阅读源码验证实现
  ```bash
  # 验证状态转换逻辑
  rg "def _handle_.*_state" src/state_machine/traversal_fsm.py -A 20
  
  # 验证异常处理机制
  rg "except Exception" src/state_machine/traversal_fsm.py -B 5 -A 10
  
  # 验证 TraversalStateMachine 类结构
  rg "class TraversalStateMachine" src/state_machine/ -A 30
  ```
- [ ] 记录关键代码片段到附录 9.1

#### 0.2 架构验证
- [ ] 追踪调用链确认职责边界
  ```bash
  # StepOrchestrator 如何调用 TraversalStateMachine
  rg "state_machine\." src/traversal/step_orchestrator.py -B 3 -A 3
  
  # TraversalStateMachine 状态转换入口
  rg "def transition" src/state_machine/traversal_fsm.py -A 10
  ```
- [ ] 记录架构图到附录 9.2

#### 0.2.b Fixture 结构分析
- [ ] 收集并分析所有测试 fixture 定义
  ```bash
  # 查找 fixture 定义位置
  rg "@pytest.fixture" tests/v6/ -A 10

  # 分析主要 fixture 结构
  # 注意: 主要 fixture 位于 tests/v6/test_state_machine_intelligence.py 第 92-162 行
  rg "def (sample_context|mock_stack|mock_action|sample_container_node)" tests/v6/test_state_machine_intelligence.py -A 15
  ```
- [ ] 记录 TraversalRuntimeContext 完整属性列表
  ```bash
  # 分析 Context 属性（预期20+字段）
  rg "class TraversalRuntimeContext" src/trace/context.py -A 50

  # 统计字段数量
  rg -c "^\s+\w+:" src/trace/context.py | head -20
  ```
- [ ] 对比 fixture 完整性
  ```bash
  # 生成缺失字段清单
  # TraversalRuntimeContext 预期字段 vs 实际 fixture 设置字段
  # 当前 fixture 仅设置约 7 个字段，Context 有 20+ 字段
  ```
- [ ] 验证 fixture 位置和完整性
  ```bash
  # 验证 fixture 在 test_state_machine_intelligence.py 中
  rg -n "@pytest.fixture" tests/v6/test_state_machine_intelligence.py

  # 检查 conftest.py 是否存在（预期不存在或为空）
  cat tests/v6/conftest.py 2>/dev/null || echo "conftest.py 不存在（符合预期）"
  ```
- [ ] 定义"完整 context mock"标准
  - 必需字段：current_path, context_tree, node_stack, failed_nodes
  - 测试特定字段：根据测试需求添加（如 current_page_analysis, visited_children）
  - 可选字段：metrics, trace_recorder 等
- [ ] 记录 fixture 结构和缺失字段清单到附录 9.1

#### 0.3 测试运行数据收集
- [ ] 运行完整测试套件，收集所有失败日志和堆栈跟踪
  ```bash
  pytest tests/v6/test_state_machine_intelligence.py -v --tb=long > temp/test_failures.log
  ```
- [ ] 记录到附录 9.1

#### 0.4 覆盖面基线收集
- [ ] 收集覆盖面基线
  ```bash
  pytest tests/v6/test_state_machine_intelligence.py --cov=src/state_machine --cov-report=term-missing --cov-report=html:temp/coverage_baseline > temp/coverage_baseline.txt
  ```

#### 0.5 运行时间基线
- [ ] 记录测试套件运行时间基线
  ```bash
  pytest tests/v6/test_state_machine_intelligence.py --durations=0 > temp/duration_baseline.txt
  ```

#### 0.6 历史基线获取
- [ ] 从 git 历史获取 V6.11.0 之前的覆盖面数据
  ```bash
  git log --oneline --before="$(git show -s --format=%ci v6.11.0)" | head -1
  git show <commit>:tests/coverage/coverage.xml 2>/dev/null || echo "No historical coverage found"
  ```

#### 0.7 专项测试数据收集 (3.5/3.6/3.7)
- [ ] 对 pending 测试使用专项分析
  ```bash
  # test_backtrack_pops_stack
  pytest tests/v6/test_state_machine_intelligence.py::test_backtrack_pops_stack -vv --tb=long > temp/test_3_5.log
  
  # test_abort_sets_terminated
  pytest tests/v6/test_state_machine_intelligence.py::test_abort_sets_terminated -vv --tb=long > temp/test_3_6.log
  
  # test_catches_handler_exception
  pytest tests/v6/test_state_machine_intelligence.py::test_catches_handler_exception_and_routes_to_error_handling -vv --tb=long > temp/test_3_7.log
  ```

**验收标准**:
- 每个失败测试有完整的日志和堆栈跟踪
- 覆盖面基线数据已记录（用于阶段5验证）
- 运行时间基线已记录（用于成功标准6.1验证）
- 代码静态分析完成，关键逻辑已验证
- 架构职责边界已确认

### 阶段 1: 简单修复 (低风险)
- [ ] 修复 `test_deeper_executes_back` - 将 `action` 改为 `mock_action`
- [ ] 验证测试通过

**验收标准**: 测试通过

### 阶段 2: FrameCompleteHandler 修复 (中风险)
- [ ] 修复 `test_auto_escape_clicks_unvisited_menu` - 添加完整 context mock
- [ ] 修复 `test_auto_escape_fallback_to_back_when_no_unvisited` - 添加完整 context mock
- [ ] 验证测试通过

**验收标准**: 两个测试通过

### 阶段 3: ErrorHandler 修复 (中风险)
- [ ] 修复 `test_retry_with_remaining_retries` - 设置 retry_count
- [ ] 分析并修复 `test_backtrack_pops_stack`
- [ ] 分析并修复 `test_abort_sets_terminated`
- [ ] 验证测试通过或正确标记

**验收标准**: 所有 ErrorHandler 测试通过或正确标记

### 阶段 4: StepExceptionHandling 修复 (中风险)
- [ ] 分析并修复 `test_catches_handler_exception_and_routes_to_error_handling`
- [ ] 修复 `test_preserves_error_type_in_metadata` - 调整验证时机
- [ ] 验证测试通过

**验收标准**: 所有异常处理测试通过

### 阶段 5: 覆盖面验证 (必须)
- [ ] 运行 `pytest --cov=src/state_machine --cov-report=term-missing`
- [ ] 验证覆盖面不低于迁移前
- [ ] 识别覆盖面缺口
- [ ] **状态转换路径覆盖面验证** (附录 9.2 状态转换图必须完成)
  ```bash
  # 分析 TraversalStateMachine 的状态转换
  rg "class State" src/state_machine/traversal_fsm.py -A 20
  rg "def _handle_.*_state" src/state_machine/traversal_fsm.py | wc -l

  # 验证每个合法状态转换是否被测试覆盖
  # 状态类型: EXECUTE, NODE_SELECT, FRAME_COMPLETE, ERROR_HANDLING, BACKTRACK, TERMINATED
  # 转换路径: 每个 State 的 _handle_xxx_state 方法可能的返回值
  ```
- [ ] 生成覆盖面报告到附录 9.3

**验收标准**:
- 覆盖面报告完成，无降低
- 状态转换路径覆盖面已验证，每个合法转换至少有一个测试
- 附录 9.2 状态转换图已完成

### 阶段 6: 测试辅助层 (未来增强)
- [ ] 参考 PRD_V6_14_0 创建状态机测试 helper
- [ ] **明确与 V6.14.0 helper 层关系**: 本阶段将扩展 `tests/v6/helpers/api_migration_helper.py` 或创建新的 `state_machine_helper.py`
- [ ] 封装状态转换验证逻辑
- [ ] 降低未来 API 变更影响
- [ ] **Helper 层决策树**:
  - **通用 API 差异** → 使用 `api_migration_helper.py`
    - PopupInfo 结构变化
    - DynamicChildManager API 变更
    - 通用 context 属性差异
  - **状态机专用测试辅助** → 创建 `state_machine_helper.py`
    - 状态转换验证辅助函数
    - Transition 历史追踪工具
    - Test Context 构建器
  - **避免重复**: 先检查 `api_migration_helper.py` 是否已提供所需功能

**与 V6.14.0 的关系说明**:
- V6.14.0 已创建 `tests/v6/helpers/api_migration_helper.py` 提供通用 API 迁移辅助函数
- 阶段 6 应参考该 helper 的设计模式，而非重新创建
- 如状态机测试需求特殊，可在同一 `helpers/` 目录下创建 `state_machine_helper.py`
- 避免重复 V6.14.0 已提供的通用功能

**验收标准**: Helper 函数文档化并使用

---

## 6. 成功标准

### 6.1 量化指标

| 指标 | 当前状态 | 目标状态 | 说明 |
|------|----------|----------|------|
| 失败测试数 | 8 | 0 | 不包括标记为 xfail 的测试 |
| 测试通过率 | <95% | 100% | 不包括 skip/xfail 的测试 |
| 覆盖面降低 | 阶段0测量 | 0% | 与阶段0基线对比 |
| 测试套件运行时间 | 阶段0测量 | ±10% | 与阶段0基线对比 |

**基线数据来源**: 阶段0执行后记录在 `temp/coverage_baseline.txt` 和 `temp/duration_baseline.txt`

### 6.2 质量标准

- [ ] 所有测试通过或正确标记（xfail/skip）
- [ ] 无测试测试已移除的功能
- [ ] 覆盖面报告完成（附录 9.3）

---

## 7. 影响分析

### 7.1 正面影响

| 影响 | 描述 |
|------|------|
| 测试准确性 | 修复测试设置问题，提高测试套件可信度 |
| 开发效率 | 减少误报警，加快问题定位 |
| 架构理解 | 状态转换文档化，帮助理解新架构 |

### 7.2 负面影响

| 影响 | 缓解措施 |
|------|----------|
| 覆盖面降低风险 | 每次修改前运行覆盖面分析 |
| 产品 bug 遗漏 | 确认根本原因后再修改/删除 |

---

## 8. 风险评估

### 8.1 风险矩阵

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|----------|
| 误删除有效测试 | 低 | 高 | 删除前覆盖面验证 |
| 遗漏产品 bug | 低 | 高 | 根本原因分析优先 |
| 覆盖面降低 | 低 | 中 | 覆盖面报告验证 |
| API 假设错误 | 中 | 中 | 参考 V6.13/V6.14 设计文档 |

### 8.2 依赖验证

| 依赖项 | 状态 | 验证 |
|--------|------|------|
| V6.13.0 状态迁移 | ✅ 完成 | Commit `11bcf06`: 46 导入更新，src/state/ 目录删除 |
| V6.14.0 Test API | ✅ 完成 | Commit `34aba4c`: 37 测试删除，helper 层创建 |

---

## 9. 附录

### 9.1 失败测试日志和标准化数据收集模板

**标准化数据收集模板** (JSON 格式):

```json
{
  "baseline_info": {
    "pytest_version": "string",
    "test_count": 20,
    "collection_timestamp": "ISO_8601"
  },
  "fixture_analysis": {
    "context_total_fields": 20,
    "fixture_set_fields": 7,
    "missing_fields": ["field1", "field2", "..."],
    "completeness_ratio": 0.35
  },
  "test_failures": [
    {
      "test_name": "string",
      "failure_mode": "mock_setup|test_design|product_logic|coverage_gap|timing|state_management",
      "error_type": "string",
      "stack_trace": "string"
    }
  ],
  "coverage_baseline": {
    "line_coverage": "percentage",
    "branch_coverage": "percentage",
    "missing_lines": ["file:line", "..."]
  },
  "duration_baseline": {
    "total_seconds": "number",
    "per_test_average": "number"
  },
  "platform_info": {
    "os": "win32",
    "shell": "bash",
    "compatibility_notes": "string"
  }
}
```

*(待阶段 0 完成 - 使用上述模板记录数据)*

### 9.2 状态转换图

*(待阶段 2 完成 - 使用 Mermaid 格式)*

### 9.3 覆盖面报告

*(待阶段 5 完成)*

---

## 10. 修订记录

| 日期 | 版本 | 内容 |
|------|------|------|
| 2026-06-10 | 1.0 | 初始版本 |
| 2026-06-10 | 1.1 | 基于对抗审阅反馈修正 - 添加代码级根本原因分析，转换为 AI 可执行格式 |
| 2026-06-10 | 1.2 | Loop 修正 - 纠正 2.3 过度结论，添加覆盖面基线收集，验证 3.8 metadata 分析 |
| 2026-06-10 | 1.3 | Loop 修正 - 添加代码静态分析和架构验证步骤，细化阶段0数据收集 |
| 2026-06-10 | 1.4 | Loop 修正 - 添加 fixture 结构分析，修正测试路径为 tests/v6/ |
| 2026-06-10 | 1.5 | Loop 修正 - 修正 V6.13.0 夸大陈述，添加 pytest 环境验证，明确 fixture 位置，细化 3.8 metadata 分析，明确阶段 6 与 V6.14.0 helper 层关系 |
| 2026-06-10 | 1.6 | Loop 修正 - 添加失败模式分类学，TraversalRuntimeContext完整属性分析，修正3.8 metadata fix_strategy，添加状态转换路径覆盖面验证，添加阶段6决策树，添加Windows兼容性说明，添加标准化数据收集模板 |

---

**文档所有者**: Uni-Claw 开发团队
**相关文档**:
- [PRD_V6_11_0_engine_refactor.md](./PRD_V6_11_0_engine_refactor.md)
- [PRD_V6_13_0_state_migration.md](./PRD_V6_13_0_state_migration.md)
- [PRD_V6_14_0_Test_API_Migration.md](./PRD_V6_14_0_Test_API_Migration.md)
