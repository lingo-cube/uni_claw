# V6.9.1 测试体系重构设计文档

**日期**: 2026-06-07
**版本**: 1.0
**状态**: 设计阶段
**依赖**: V6.3/V6.6/V6.7/V6.8/V6.9 PRD

---

## 1. 概述

### 1.1 背景

当前仿真测试体系存在三类问题：
- **不可运行**：死引用导致测试无法执行
- **冗余重复**：测试代码重复，资产散落
- **功能缺失**：新增功能无测试覆盖

### 1.2 目标

本设计的目标不是"让测试通过"，而是**让测试发现真实缺陷**。

| 传统目标 | 本设计目标 |
|---------|-----------|
| 验证代码正确性 | 发现代码缺陷 |
| 简化场景 | 复杂场景暴露问题 |
| 只验证输出 | 深度验证内部状态 |
| 静态用例 | 随机化、故障注入 |
| 覆盖 PRD 场景 | 覆盖真实运行情况 |

### 1.3 核心原则

1. **准确性优先**：测试资产必须准确，复用现有需验证
2. **覆盖率驱动**：L1-L4 四层次全覆盖
3. **缺陷发现**：通过边界、故障、压力测试发现真实问题
4. **不随意修改**：只有用例真实有问题才修正，不得为通过测试而改代码

---

## 2. 架构设计

### 2.1 目录结构

```
tests/
├── helpers/                          # 共享辅助模块
│   ├── __init__.py
│   ├── chaos_engine.py              # 随机化与变化注入
│   ├── boundary_tester.py           # 边界测试
│   ├── fault_injector.py            # 故障注入
│   ├── realism_simulator.py         # 真实场景模拟
│   ├── state_inspector.py           # 状态深度验证
│   ├── stress_tester.py             # 压力测试
│   ├── factories.py                 # 工厂函数
│   └── trace_analyzer.py            # Trace 分析工具
│
├── assets/
│   └── fixtures/                    # 虚拟页面 JSON
│       ├── pages_all.json           # 现有 - 全遍历
│       ├── pages_find.json          # 现有 - 目标搜索
│       ├── pages_dynamic.json       # 新建 - 动态匹配
│       ├── pages_correction.json    # 新建 - 智能纠正
│       ├── pages_entry.json         # 新建 - 入口策略
│       ├── pages_boundary.json      # 新建 - 边界测试
│       └── pages_chaos.json         # 新建 - 故障注入
│
├── v6/
│   ├── test_simulation_base.py      # Mock 基础 + M 系列
│   ├── test_v6_9_dynamic_matching.py  # D 系列
│   ├── test_v6_9_plan_compilation.py  # C 系列（已存在）
│   ├── test_simulation_sm.py        # P 系列
│   ├── test_engine_initialization.py  # V6.8 测试
│   ├── test_trace_simulation.py    # Trace 集成
│   ├── test_state_machine.py       # 状态机单元
│   ├── test_trace_models.py        # Trace 模型
│   ├── test_compiler.py            # 编译器测试
│   └── unit/
│       └── test_compiler.py        # 编译器详细
│
└── integration/
    └── test_simulation_e2e.py      # E2E 测试
```

### 2.2 共享辅助模块设计

#### chaos_engine.py - 随机化与变化注入

```python
"""引入随机性和变化，测试鲁棒性。"""

class ChaosEngine:
    """随机化测试工具，确保逻辑不依赖固定顺序。"""

    def randomize_page_order(self, pages: List) -> List:
        """随机打乱页面元素顺序。"""

    def inject_delay(self, delay_ms: int, variance: float = 0.5):
        """注入随机延迟，测试超时处理。"""

    def corrupt_page_data(self, page: Dict, corruption_type: str) -> Dict:
        """制造部分缺失数据。corruption_type: missing_field/null_value/wrong_type"""

    def duplicate_elements(self, page: Dict) -> Dict:
        """制造重复元素，测试去重逻辑。"""
```

#### boundary_tester.py - 边界测试

```python
"""测试边界条件，发现极限情况下的缺陷。"""

class BoundaryTester:
    """边界测试工具。"""

    def test_empty_elements(self, vision, action):
        """空页面元素，确保不崩溃。"""

    def test_excessive_depth(self, depth: int = 100):
        """超深路径，测试栈溢出保护。"""

    def test_massive_elements(self, count: int = 1000):
        """超多元素，测试性能。"""

    def test_unicode_edge_cases(self):
        """测试特殊 Unicode 字符。"""

    def test_extreme_coordinates(self):
        """测试坐标边界值。"""
```

#### fault_injector.py - 故障注入

```python
"""注入故障，测试恢复能力。"""

class FaultInjector:
    """故障注入工具。"""

    def inject_vision_failure(self, failure_type: str):
        """注入 vision 失败。failure_type: timeout/null_result/exception"""

    def inject_action_failure(self, failure_type: str):
        """注入 action 失败。"""

    def inject_state_corruption(self):
        """注入状态不一致。"""

    def inject_mismatched_page(self, expected: str, actual: str):
        """注入页面不匹配，测试智能纠正。"""
```

#### realism_simulator.py - 真实场景模拟

```python
"""模拟真实应用的复杂情况。"""

class RealismSimulator:
    """真实场景模拟工具。"""

    def simulate_ui_animation(self, duration_ms: int = 300):
        """模拟 UI 动画期间的不稳定状态。"""

    def simulate_slow_response(self, latency_ms: int):
        """模拟慢速响应。"""

    def simulate_popup_chain(self, depth: int):
        """模拟连续弹窗。"""

    def simulate_page_transition_delay(self):
        """模拟页面跳转延迟。"""

    def simulate_scrollable_list(self, items: int, page_size: int):
        """模拟可滚动列表。"""
```

#### state_inspector.py - 状态深度验证

```python
"""深度验证内部状态，不只看输出。"""

class StateInspector:
    """状态验证工具。"""

    def verify_stack_consistency(self, stack, context) -> bool:
        """验证栈与 current_path 一致性。"""

    def verify_cache_coherency(self, engine) -> bool:
        """验证缓存与实际页面一致。"""

    def verify_no_orphan_spans(self, trace) -> bool:
        """验证所有 Span 都有正确父子关系。"""

    def verify_metrics_completeness(self, trace) -> bool:
        """验证所有操作都有对应 metrics。"""

    def verify_state_machine_invariants(self, fsm) -> bool:
        """验证状态机不变量。"""
```

#### stress_tester.py - 压力测试

```python
"""压力测试，发现内存泄漏、性能退化。"""

class StressTester:
    """压力测试工具。"""

    def test_long_traversal(self, steps: int = 1000):
        """测试长时间遍历不崩溃。"""

    def test_memory_leak(self, iterations: int = 100):
        """测试内存不持续增长。"""

    def test_rapid_state_transitions(self, transitions: int = 10000):
        """测试快速状态转换不丢状态。"""
```

#### factories.py - 工厂函数

```python
"""简化版工厂函数。"""

def create_minimal_plan(**kwargs) -> TraversalPlan:
    """创建最小可用计划。"""

def create_test_node(**kwargs) -> TraversalNode:
    """创建测试节点。"""

def create_mock_vision(**kwargs) -> MockVisionService:
    """创建 Mock 视觉服务。"""
```

---

## 3. 死代码清理

### 3.1 清理清单

| 动作 | 文件 | 变更 |
|------|------|------|
| 删除 fixture | `conftest.py` | 删除 7 个 AI fixture（第 13-93 行） |
| 删除文件 | `tests/v6/test_v6_4_simulation_alignment.py` | 整个文件 |
| 重命名 | `tests/v6/test_simulation.py` → `test_simulation_base.py` | 文件重命名 |
| 修复导入 | `tests/integration/test_simulation_e2e.py` | 移除死引用，替换枚举值 |

### 3.2 详细变更

#### conftest.py 清理

删除以下 fixture（全部引用不存在的 `tests.ai.fixtures`）：
- `mock_provider`
- `mock_deepseek_provider`
- `mock_claude_provider`
- `mock_mimo_provider`
- `all_mock_providers`
- `response_recorder`
- `response_replayer`

**保留**：pytest markers、路径设置、asyncio 配置。

#### test_simulation_e2e.py 修复

移除：
- `from tests.simulation.helpers import ...`
- `CompletionPolicyType.EXHAUSTIVE` → 替换为 `CompletionPolicyType.MAX_STEPS`
- 无效的 Mock(spec=TraversalPlan) 段

---

## 4. 测试场景矩阵

### 4.1 L1: 数据模型与契约

| 组件 | 验证点 | 测试文件 |
|------|--------|----------|
| Trace 模型 (V6.3) | Session/Step/Span 字段、ULID 唯一性、父子关系 | `test_trace_models.py` |
| Span 类型 (V6.6) | 8 种 span 类型、page_id/element_count 序列化 | `test_trace_models.py` |
| 计划模型 (V6.8) | EntryConfig 字段验证、序列化/反序列化 | `test_compiler.py` |
| 节点模型 (V6.9) | IntentSlots 字段、ChildrenStrategy/DynamicRule 转换 | `test_compiler.py` |

### 4.2 L2: 单个组件行为

#### D 系列 - 动态匹配 (10 场景)

| ID | 场景 | 复杂度 | 验证点 |
|----|------|--------|--------|
| D1 | 首次生成动态子节点 | 基础 | `_dynamic_children[root]` 长度 = 匹配数 |
| D2 | MenuItem → dict 字段映射 | 基础 | `match_all` 正确消费 `text`/`type`/`index`/`coordinate` |
| D3 | 逐个取子节点无重复 | 基础 | 多次调用返回不同 ID |
| D4 | 全部访问后 FRAME_COMPLETE | 基础 | 返回 None |
| D5 | FRAME_COMPLETE 拦截 | 中等 | 还剩子节点时推入栈，继续遍历 |
| D6 | 路径变化触发缓存失效 | 中等 | 失效后重新生成 |
| D7 | 路径拼接 | 基础 | `precondition.path = parent + [child]` |
| D8 | 跳过元素记录 Span | 基础 | `_record_skip_span` 被调用 |
| D9 | page_analysis 为 None | 边界 | 不崩溃，返回空列表 |
| D10 | DynamicRule → dict 转换 | 基础 | `load_rules` 正确消费 |

**扩展场景**：
- D11: 元素顺序随机化
- D12: 空/超多元素边界
- D13: vision 失败容错

#### C 系列 - 编译器 (12 场景)

| ID | 场景 | 验证点 |
|----|------|--------|
| C1 | `scope="full"` | `completion_policy.type == NONE` |
| C2 | `scope="partial"` | `type == MAX_STEPS` |
| C3 | `scope="target_only"` + target | `type == TARGET_FOUND` + target_name |
| C4 | `scope="target_only"` 缺 target | `CompilerError` |
| C5 | `scope="target_path"` | STATIC 节点链 + path 层层拼接 |
| C6 | `element_handling="full_interaction"` | 4 个 dynamic_rules |
| C7 | `element_handling="menu_only"` | 仅 menu_container |
| C8 | `element_handling="safe_mode"` | 4 个规则 + `meta["safe_mode"]=True` |
| C9 | `element_handling="read_only"` | 仅 leaf_info |
| C10 | `navigation="back"` vs 缺失 | BACK vs AUTO_ESCAPE |
| C11 | `completion="timeout"` 覆盖 | scope 推导被覆盖 |
| C12 | 缺 `target_app` | `CompilerError` |

#### P 系列 - 智能纠正 (10 场景)

| ID | 场景 | 复杂度 | 验证点 |
|----|------|--------|--------|
| P1 | precondition 满足 | 基础 | 直接进 EXECUTE |
| P2 | NAVIGABLE 1轮纠正 | 中等 | 点击同级菜单，vision 验证成功 |
| P3 | NAVIGABLE 3轮纠正 | 复杂 | 每轮失败，第3轮后进 ERROR |
| P4 | DEEPER 纠正成功 | 中等 | back 后路径匹配 |
| P5 | DEEPER 回退过头 | 复杂 | 过深返回 UNKNOWN，继续 back |
| P6 | UNKNOWN 迷失恢复 | 复杂 | 完全迷失，3次 back 后 ERROR |
| P7 | Vision 失败容错 | 复杂 | vision 调用失败，继续重试 |
| P8 | 并发 precondition | 复杂 | 多个节点 precondition 全部验证 |
| P9 | precondition 超时 | 边界 | 3轮重试耗尽，记录 timeout |
| P10 | 纠正后页面未变 | 边界 | action 执行但页面未变化，继续重试 |

### 4.3 L3: 组件协作

| 协作场景 | 验证点 | 测试文件 |
|----------|--------|----------|
| 引擎+状态机+Vision (V6.7) | precondition 检查时 vision 调用、纠正后刷新 | `test_simulation_sm.py` |
| 引擎+Action+Vision (V6.8) | 入口策略执行、等待条件验证 | `test_engine_initialization.py` |
| Metrics→Span (V6.6) | _record_metrics_as_spans 转换 | `test_trace_simulation.py` |
| Trace 记录 (V6.3) | Session/Step/Span 树结构 | `test_trace_integration.py` |

### 4.4 L4: 端到端需求满足

| 场景 | 复杂度 | 验证点 |
|------|--------|--------|
| E2E1 全菜单遍历 | 中等 | visited_tree 完整 |
| E2E2 目标搜索 | 中等 | completion_reason = TARGET_FOUND |
| E2E3 静态路径 | 中等 | 沿预定义路径到达 |
| E2E4 嵌套弹窗处理 | 复杂 | 多层弹窗全部关闭 |
| E2E5 动态+智能纠正协同 | 复杂 | 两个机制协同工作 |
| E2E6 深度遍历+回退 | 复杂 | 深度限制+back 策略 |
| E2E7 错误恢复+重试 | 复杂 | error_policy + retry |

### 4.5 M 系列 - Mock 映射验证 (5 场景)

| ID | 场景 | 验证点 |
|----|------|--------|
| M1 | elements[].text → MenuItem.name | 断言 name 正确 |
| M2 | bounds → Coordinate 转换 | 断言坐标为中点 |
| M3 | 路径切换 | `set_path_context` 返回对应页面 |
| M4 | 路径不存在 | 返回空 PageAnalysis |
| M5 | 操作记录 | click/back/swipe 出现在历史 |

---

## 5. 虚拟页面 Fixture 设计

### 5.1 pages_dynamic.json

**用途**：动态匹配场景（D1-D10）

**结构**：
```json
{
  "/Settings/Main": {
    "elements": [
      {"type": "menu_item", "text": "Display", "bounds": [0, 100, 500, 180]},
      {"type": "switch", "text": "WiFi", "bounds": [0, 200, 500, 280]},
      {"type": "slider", "text": "Brightness", "bounds": [0, 300, 500, 380]},
      {"type": "button", "text": "Save", "bounds": [0, 400, 500, 480]}
    ]
  }
}
```

### 5.2 pages_correction.json

**用途**：智能纠正场景（P1-P10）

**结构**：多级菜单，支持 NAVIGABLE/DEEPER/UNKNOWN 关系测试
```json
{
  "/Settings": {"elements": [{"text": "Display"}]},
  "/Settings/Display": {"elements": [{"text": "Brightness"}]},
  "/Settings/Display/Brightness": {"elements": [{"text": "Auto"}]}
}
```

### 5.3 pages_entry.json

**用途**：入口策略场景

**结构**：桌面 + 目标应用入口
```json
{
  "/Home": {
    "elements": [
      {"type": "app_icon", "text": "Settings", "bounds": [100, 200, 300, 400]}
    ]
  },
  "/Settings": {"elements": [{"text": "Main"}]}
}
```

### 5.4 pages_boundary.json

**用途**：边界测试

**结构**：包含空页面、超多元素、超深路径

### 5.5 pages_chaos.json

**用途**：故障注入测试

**结构**：包含缺失字段、重复元素、错误类型

---

## 6. 实施计划

### 6.1 阶段划分

#### 阶段 1：清理与基础（第 1 周）

| 步骤 | 内容 | 验证 |
|------|------|------|
| 1.1 | 清理 conftest.py | `pytest --collect-only` |
| 1.2 | 删除重复文件 | 文件不存在 |
| 1.3 | 重命名 test_simulation.py | 新文件存在 |
| 1.4 | 修复 test_simulation_e2e.py | 可导入 |

#### 阶段 2：共享辅助模块（第 1 周）

| 步骤 | 内容 | 验证 |
|------|------|------|
| 2.1 | chaos_engine.py | 随机化可用 |
| 2.2 | boundary_tester.py | 边界测试可用 |
| 2.3 | fault_injector.py | 故障注入可用 |
| 2.4 | realism_simulator.py | 真实模拟可用 |
| 2.5 | state_inspector.py | 状态验证可用 |
| 2.6 | stress_tester.py | 压力测试可用 |

#### 阶段 3：虚拟页面 Fixture（第 2 周）

| 步骤 | 内容 | 验证 |
|------|------|------|
| 3.1 | pages_dynamic.json | MockVisionService 正确解析 |
| 3.2 | pages_correction.json | 多级菜单正确 |
| 3.3 | pages_entry.json | 桌面+应用正确 |
| 3.4 | pages_boundary.json | 边界场景覆盖 |
| 3.5 | pages_chaos.json | 故障场景覆盖 |

#### 阶段 4-7：测试系列实施（第 2-4 周）

按 D → C → P → E2E 顺序实施，每个系列包含：
- 基础场景（PRD 规定）
- 边界场景
- 故障注入
- 随机化测试

#### 阶段 8：回归与验收（第 5 周）

| 步骤 | 内容 | 验证 |
|------|------|------|
| 8.1 | 全量测试 | `pytest tests/v6/ tests/integration/` |
| 8.2 | 覆盖率 | 核心模块 > 80% |
| 8.3 | 性能基准 | 无明显退化 |
| 8.4 | 文档更新 | REFERENCE.md 同步 |

### 6.2 验收标准

| 类别 | 标准 | 验证方法 |
|------|------|----------|
| 清理完成 | 无死代码 | `pytest --collect-only` |
| 测试通过率 | 100%（无 skip） | `pytest tests/v6/ tests/integration/ -v` |
| 覆盖率 | 核心模块 > 80% | `pytest --cov=src` |
| 场景覆盖 | 44 基础 + 20+ 复杂 | 用例计数 |
| 缺陷发现 | 至少 3 个真实缺陷 | Bug 记录 |
| 性能 | 无明显退化 | 基准对比 |

---

## 7. 测试编写原则

### 7.1 好的测试 vs 坏的测试

| 坏的测试 | 好的测试 |
|---------|---------|
| 只验证"成功" | 验证内部逻辑和状态 |
| 简化场景 | 复杂边界场景 |
| 静态数据 | 随机化/变化注入 |
| 只看输出 | 深度状态验证 |
| 为通过测试改代码 | 为发现缺陷写测试 |

### 7.2 示例对比

```python
# 坏的测试：只验证成功
def test_dynamic_matching_success():
    result = matcher.match_all(items)
    assert len(result) == 3

# 好的测试：验证内部逻辑
def test_dynamic_matching_internal_logic():
    # 测试缓存机制
    engine._generate_dynamic_children(node)
    assert "node_id" in engine._dynamic_children
    first_id = id(engine._dynamic_children["node_id"])

    # 测试缓存失效
    engine.invalidate_children_cache("node_id")
    assert "node_id" not in engine._dynamic_children

    # 测试重新生成
    engine._generate_dynamic_children(node)
    second_id = id(engine._dynamic_children["node_id"])
    assert first_id != second_id

    # 测试 path 拼接
    child = engine._dynamic_children["node_id"][0]
    assert child.precondition.path == ["Parent", "Child"]
```

---

## 8. 参考

- [PRD_V6.3_trace_integration.md](../../PRD_V6_3_trace_integration.md)
- [PRD_V6.6-trace-handler-metrics-enhancement.md](../../PRD_V6_6-trace-handler-metrics-enhancement.md)
- [PRD_V6.7-state-machine-intelligence.md](../../PRD_V6_7-state-machine-intelligence.md)
- [PRD_V6.8_engine_initialization.md](../../PRD_V6_8_engine_initialization.md)
- [PRD_V6.9_plan_compilation_and_matching.md](../../PRD_V6.9_plan_compilation_and_matching.md)
- [PRD_V6.9.1_Test_refactor.md](../../PRD_V6_9.1_Test_refactor.md)
- [tests/REFERENCE.md](../../../tests/REFERENCE.md)

---

**文档状态**: 设计完成，待用户审核
