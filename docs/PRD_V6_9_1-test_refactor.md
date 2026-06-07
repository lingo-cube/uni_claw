# V6.9.1 测试体系重构 PRD

**版本**: V6.9.1
**日期**: 2026-06-07
**依赖**: V6.3/V6.6/V6.7/V6.8/V6.9 PRD
**状态**: 待实施

---

## 1. 概述

### 1.1 背景

当前仿真测试体系存在三类问题：
- **不可运行**：死引用导致测试无法执行
- **冗余重复**：测试代码重复，资产散落
- **功能缺失**：新增功能无测试覆盖

### 1.2 目标

本 PRD 的目标不是"让测试通过"，而是**让测试发现真实缺陷**。

| 传统目标 | 本 PRD 目标 |
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

## 2. 范围

### 2.1 包含

- 清理死代码（conftest.py、重复文件、无效引用）
- D 系列：动态匹配集成测试（10+3 场景）
- C 系列：PlanCompiler 编译测试（12 场景）
- P 系列：状态机智能纠正回归测试（10 场景）
- E2E 系列：端到端全链路测试（7 场景）
- M 系列：Mock 服务映射验证（5 场景）
- 新建 `tests/helpers/` 共享辅助模块
- 补全虚拟页面 JSON fixtures

### 2.2 不包含

- 真实 AI 服务集成测试（另案处理）
- 性能/压力测试基准建立

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

## 4. 架构设计

### 4.1 目录结构

```
tests/
├── helpers/                          # 共享辅助模块
│   ├── __init__.py
│   ├── factories.py                 # 工厂函数（第一批）
│   ├── state_inspector.py           # 状态深度验证（第一批）
│   ├── trace_analyzer.py            # Trace 分析工具（第一批）
│   ├── chaos_engine.py              # 随机化与变化注入（第二批）
│   ├── boundary_tester.py           # 边界测试（第二批）
│   ├── fault_injector.py            # 故障注入（第二批）
│   ├── realism_simulator.py         # 真实场景模拟（第三批）
│   └── stress_tester.py             # 压力测试（第三批）
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
│   ├── test_v6_9_dynamic_matching.py  # D 系列（已存在）
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

### 4.2 共享辅助模块设计

#### 第一批（必需）

**factories.py - 工厂函数**
```python
def create_minimal_plan(**kwargs) -> TraversalPlan:
def create_test_node(**kwargs) -> TraversalNode:
def create_mock_vision(**kwargs) -> MockVisionService:
```

**state_inspector.py - 状态深度验证**
```python
class StateInspector:
    def verify_stack_consistency(self, stack, context) -> bool
    def verify_cache_coherency(self, engine) -> bool
    def verify_no_orphan_spans(self, trace) -> bool
    def verify_metrics_completeness(self, trace) -> bool
    def verify_state_machine_invariants(self, fsm) -> bool
```

**trace_analyzer.py - Trace 分析工具**
```python
class TraceAnalyzer:
    def build_tree(self, spans: List) -> Dict
    def extract_operations(self, trace) -> List[str]
    def count_span_types(self, trace) -> Dict[str, int]
```

#### 第二批（增强）

**chaos_engine.py - 随机化与变化注入**
```python
class ChaosEngine:
    def randomize_page_order(self, pages: List) -> List
    def inject_delay(self, delay_ms: int, variance: float = 0.5)
    def corrupt_page_data(self, page: Dict, corruption_type: str) -> Dict
    def duplicate_elements(self, page: Dict) -> Dict
```

**boundary_tester.py - 边界测试**
```python
class BoundaryTester:
    def test_empty_elements(self, vision, action)
    def test_excessive_depth(self, depth: int = 100)
    def test_massive_elements(self, count: int = 1000)
    def test_unicode_edge_cases(self)
    def test_extreme_coordinates(self)
```

**fault_injector.py - 故障注入**
```python
class FaultInjector:
    def inject_vision_failure(self, failure_type: str)
    def inject_action_failure(self, failure_type: str)
    def inject_state_corruption(self)
    def inject_mismatched_page(self, expected: str, actual: str)
```

#### 第三批（可选）

**realism_simulator.py - 真实场景模拟**
```python
class RealismSimulator:
    def simulate_ui_animation(self, duration_ms: int = 300)
    def simulate_slow_response(self, latency_ms: int)
    def simulate_popup_chain(self, depth: int)
    def simulate_page_transition_delay(self)
    def simulate_scrollable_list(self, items: int, page_size: int)
```

**stress_tester.py - 压力测试**
```python
class StressTester:
    def test_long_traversal(self, steps: int = 1000)
    def test_memory_leak(self, iterations: int = 100)
    def test_rapid_state_transitions(self, transitions: int = 10000)
```

---

## 5. 测试场景矩阵

### 5.1 L1: 数据模型与契约

| 组件 | 验证点 | 测试文件 |
|------|--------|----------|
| Trace 模型 (V6.3) | Session/Step/Span 字段、ULID 唯一性、父子关系 | `test_trace_models.py` |
| Span 类型 (V6.6) | 8 种 span 类型、page_id/element_count 序列化 | `test_trace_models.py` |
| 计划模型 (V6.8) | EntryConfig 字段验证、序列化/反序列化 | `test_compiler.py` |
| 节点模型 (V6.9) | IntentSlots 字段、ChildrenStrategy/DynamicRule 转换 | `test_compiler.py` |

### 5.2 L2: 单个组件行为

#### D 系列 - 动态匹配 (10 基础 + 3 扩展)

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
| D11 | 元素顺序随机化 | 复杂 | 随机顺序下仍能正确匹配 |
| D12 | 空/超多元素边界 | 边界 | 空 list 不崩溃，1000+ 元素性能可接受 |
| D13 | vision 失败容错 | 边界 | vision 异常时不崩溃 |

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

### 5.3 L3: 组件协作

| 协作场景 | 验证点 | 测试文件 |
|----------|--------|----------|
| 引擎+状态机+Vision (V6.7) | precondition 检查时 vision 调用、纠正后刷新 | `test_simulation_sm.py` |
| 引擎+Action+Vision (V6.8) | 入口策略执行、等待条件验证 | `test_engine_initialization.py` |
| Metrics→Span (V6.6) | _record_metrics_as_spans 转换 | `test_trace_simulation.py` |
| Trace 记录 (V6.3) | Session/Step/Span 树结构 | `test_trace_integration.py` |

### 5.4 L4: 端到端需求满足

| 场景 | 复杂度 | 验证点 |
|------|--------|--------|
| E2E1 全菜单遍历 | 中等 | visited_tree 完整 |
| E2E2 目标搜索 | 中等 | completion_reason = TARGET_FOUND |
| E2E3 静态路径 | 中等 | 沿预定义路径到达 |
| E2E4 嵌套弹窗处理 | 复杂 | 多层弹窗全部关闭 |
| E2E5 动态+智能纠正协同 | 复杂 | 两个机制协同工作 |
| E2E6 深度遍历+回退 | 复杂 | 深度限制+back 策略 |
| E2E7 错误恢复+重试 | 复杂 | error_policy + retry |

### 5.5 M 系列 - Mock 映射验证 (5 场景)

| ID | 场景 | 验证点 |
|----|------|--------|
| M1 | elements[].text → MenuItem.name | 断言 name 正确 |
| M2 | coordinate → Coordinate 转换 | 断言 coordinate.x/y 正确映射 |
| M3 | 路径切换 | `set_path_context` 返回对应页面 |
| M4 | 路径不存在 | 返回空 PageAnalysis |
| M5 | 操作记录 | click/back/swipe 出现在历史 |

---

## 6. 虚拟页面 Fixture 设计

### 6.1 MockVisionService 解析规则

根据 `src/simulation/mock_vision.py` 的实际实现：

```python
# line 106-118
items_raw = data.get("elements", [])
items.append(MenuItem(
    name=item.get("text", item.get("name", "")),
    type=item.get("type", "item"),
    coordinate=Coordinate(
        x=coord.get("x", 0.5),
        y=coord.get("y", 0.5),
    ),
))
```

**关键字段映射**：
- `elements[].text` 或 `elements[].name` → `MenuItem.name`
- `elements[].type` → `MenuItem.type`
- `elements[].coordinate.x` → `Coordinate.x`
- `elements[].coordinate.y` → `Coordinate.y`

### 6.2 pages_dynamic.json

**用途**：动态匹配场景（D1-D10）

```json
{
  "/Settings/Main": {
    "elements": [
      {"type": "menu_item", "text": "Display", "coordinate": {"x": 0.25, "y": 0.2}},
      {"type": "switch", "text": "WiFi", "coordinate": {"x": 0.25, "y": 0.4}},
      {"type": "slider", "text": "Brightness", "coordinate": {"x": 0.25, "y": 0.6}},
      {"type": "button", "text": "Save", "coordinate": {"x": 0.5, "y": 0.8}}
    ]
  }
}
```

### 6.3 pages_correction.json

**用途**：智能纠正场景（P1-P10）

```json
{
  "/Settings": {
    "elements": [
      {"text": "Display", "type": "menu_item", "coordinate": {"x": 0.5, "y": 0.3}}
    ]
  },
  "/Settings/Display": {
    "elements": [
      {"text": "Brightness", "type": "menu_item", "coordinate": {"x": 0.5, "y": 0.3}}
    ]
  },
  "/Settings/Display/Brightness": {
    "elements": [
      {"text": "Auto", "type": "switch", "coordinate": {"x": 0.5, "y": 0.3}}
    ]
  }
}
```

### 6.4 pages_entry.json

**用途**：入口策略场景

```json
{
  "/Home": {
    "elements": [
      {"type": "app_icon", "text": "Settings", "coordinate": {"x": 0.5, "y": 0.5}}
    ]
  },
  "/Settings": {
    "elements": [
      {"text": "Main", "type": "menu_item", "coordinate": {"x": 0.5, "y": 0.3}}
    ]
  }
}
```

### 6.5 pages_boundary.json

**用途**：边界测试

```json
{
  "/EmptyPage": {
    "elements": []
  },
  "/MassiveElements": {
    "elements": "GENERATE_1000_ITEMS"
  },
  "/DeepPath": {
    "elements": [
      {"text": "Level1", "coordinate": {"x": 0.5, "y": 0.1}}
    ]
  }
}
```

### 6.6 pages_chaos.json

**用途**：故障注入测试

```json
{
  "/MissingFields": {
    "elements": [
      {"text": "NoType"}
    ]
  },
  "/DuplicateElements": {
    "elements": [
      {"text": "Same", "type": "button", "coordinate": {"x": 0.5, "y": 0.3}},
      {"text": "Same", "type": "button", "coordinate": {"x": 0.5, "y": 0.3}}
    ]
  },
  "/WrongTypes": {
    "elements": [
      {"text": 123, "coordinate": "invalid"}
    ]
  }
}
```

---

## 7. 实施步骤

### 7.1 阶段划分

#### 阶段 1：清理与基础（第 1 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 1.1 | 清理 conftest.py | 删除 7 个 fixture | `pytest --collect-only` |
| 1.2 | 删除重复文件 | 删除 test_v6_4 | 文件不存在 |
| 1.3 | 重命名 test_simulation.py | test_simulation_base.py | 文件存在 |
| 1.4 | 修复 test_simulation_e2e.py | 移除死引用 | 可导入 |

#### 阶段 2：共享辅助模块 - 第一批（第 1 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 2.1 | factories.py | 3 个工厂函数 | import 通过 |
| 2.2 | state_inspector.py | 5 个验证方法 | 单元测试通过 |
| 2.3 | trace_analyzer.py | 3 个分析方法 | 单元测试通过 |

#### 阶段 3：虚拟页面 Fixture（第 2 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 3.1 | pages_dynamic.json | 动态匹配场景 | MockVisionService 正确解析 |
| 3.2 | pages_correction.json | 智能纠正场景 | 多级菜单正确 |
| 3.3 | pages_entry.json | 入口策略场景 | 桌面+应用正确 |
| 3.4 | pages_boundary.json | 边界测试场景 | 边界场景覆盖 |
| 3.5 | pages_chaos.json | 故障注入场景 | 故障场景覆盖 |

#### 阶段 4：D 系列审查修正（第 2 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 4.1 | 审查 test_v6_9_dynamic_matching.py | 对齐 D1-D10 | 基础场景通过 |
| 4.2 | 添加边界测试 | D11-D13 | 边界场景通过 |
| 4.3 | 验证 JSON 格式 | coordinate 字段正确 | Mock 解析正确 |

#### 阶段 5：C 系列补充（第 3 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 5.1 | 新建 test_compiler.py | C1-C12 | 全部通过 |

#### 阶段 6：P 系列扩展（第 3 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 6.1 | 扩展 test_simulation_sm.py | P1-P10 | 全部通过 |

#### 阶段 7：E2E 与 M 系列（第 4 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 7.1 | 重写 test_simulation_e2e.py | E2E1-E2E7 | 全部通过 |
| 7.2 | 扩展 test_simulation_base.py | M1-M5 | 全部通过 |

#### 阶段 8：共享辅助模块 - 第二批（第 4 周）

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 8.1 | chaos_engine.py | 随机化工具 | 单元测试通过 |
| 8.2 | boundary_tester.py | 边界测试工具 | 单元测试通过 |
| 8.3 | fault_injector.py | 故障注入工具 | 单元测试通过 |

#### 阶段 9：全量回归（第 5 周）

| 步骤 | 内容 | 验证 |
|------|------|------|
| 9.1 | 全量测试 | `pytest tests/v6/ tests/integration/` |
| 9.2 | 覆盖率检查 | 核心模块 > 80% |
| 9.3 | 性能基准 | 无明显退化 |
| 9.4 | 文档更新 | REFERENCE.md 同步 |

### 7.2 验收标准

- [ ] `conftest.py` 不依赖不存在模块，无 AI fixture
- [ ] `test_v6_4_simulation_alignment.py` 已删除
- [ ] `tests/helpers/` 第一批模块存在且可被测试导入
- [ ] D 系列 13 个用例全部通过
- [ ] C 系列 12 个用例全部通过
- [ ] P 系列 10 个用例全部通过
- [ ] E2E 系列 7 个用例全部通过
- [ ] M 系列 5 个用例全部通过
- [ ] `pytest tests/v6/ tests/integration/` 全量通过

---

## 8. 测试执行速查

### 单文件快速执行

```bash
# 动态匹配集成测试
pytest tests/v6/test_v6_9_dynamic_matching.py -v

# 编译器单元测试
pytest tests/v6/unit/test_compiler.py -v

# Mock 基础 + 映射验证
pytest tests/v6/test_simulation_base.py -v

# 状态机智能纠正
pytest tests/v6/test_simulation_sm.py -v

# 端到端
pytest tests/integration/test_simulation_e2e.py -v
```

### 按系列过滤

```bash
# D 系列（动态匹配）
pytest tests/v6/test_v6_9_dynamic_matching.py -v -k "test_dynamic"

# C 系列（编译器）
pytest tests/v6/unit/test_compiler.py -v

# P 系列（智能纠正）
pytest tests/v6/test_simulation_sm.py -v -k "test_precondition"

# M 系列（Mock 映射）
pytest tests/v6/test_simulation_base.py -v -k "test_mock"

# E2E 系列
pytest tests/integration/test_simulation_e2e.py -v -k "test_e2e"
```

### 全量回归

```bash
# 全量回归
pytest tests/v6/ tests/integration/ -v

# CI 模式（失败即停）
pytest tests/v6/ tests/integration/ -x --tb=short -q
```

---

## 9. 测试编写原则

### 9.1 好的测试 vs 坏的测试

| 坏的测试 | 好的测试 |
|---------|---------|
| 只验证"成功" | 验证内部逻辑和状态 |
| 简化场景 | 复杂边界场景 |
| 静态数据 | 随机化/变化注入 |
| 只看输出 | 深度状态验证 |
| 为通过测试改代码 | 为发现缺陷写测试 |

### 9.2 示例对比

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

## 10. 参考

- [PRD_V6.3_trace_integration.md](PRD_V6_3_trace_integration.md)
- [PRD_V6.6-trace-handler-metrics-enhancement.md](PRD_V6_6-trace-handler-metrics-enhancement.md)
- [PRD_V6.7-state-machine-intelligence.md](PRD_V6_7-state-machine-intelligence.md)
- [PRD_V6.8_engine_initialization.md](PRD_V6_8_engine_initialization.md)
- [PRD_V6.9_plan_compilation_and_matching.md](PRD_V6_9_plan_compilation_and_matching.md)
- [tests/REFERENCE.md](../tests/REFERENCE.md)

---

**修订记录**：

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-07 | V6.9.1 | 初始版本，升级自设计文档 |
| 2026-06-07 | V6.9.1 | 修正 JSON 格式（bounds → coordinate） |
| 2026-06-07 | V6.9.1 | 调整辅助模块实施顺序（渐进式） |
