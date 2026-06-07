# V6.9-Test 仿真测试体系重构与补全 PRD

**版本**: V6.9-Test
**日期**: 2026-06-07
**依赖**: V6.7 / V6.8 / V6.9 设计 PRD
**参考**: `tests/REFERENCE.md`（数据模型速查）
**状态**: 待实施

---

## 1. 概述

当前仿真测试代码存在三个问题：

1. **不可运行**：`test_simulation_e2e.py` 引用不存在的 `tests.simulation.helpers` 模块和不存在的 `CompletionPolicyType.EXHAUSTIVE` 枚举值，`conftest.py` 定义 6 个依赖不存在 `tests.ai.fixtures` 的 fixture。
2. **冗余重复**：`test_simulation.py` 与 `test_v6_4_simulation_alignment.py` 测试同样的 Mock 服务接口，虚拟页面字典散落各文件。
3. **功能缺失**：V6.9 动态匹配集成、缓存失效、计划编译器等功能无测试覆盖。

本 PRD 对仿真测试体系进行清理、补全和验证，确保全部可运行，为引擎提供可靠回归基线。

---

## 2. 目标

1. **消除死代码**：移除错误的模块引用、重复测试文件、不存在的 fixture。
2. **补全测试覆盖**：以 V6.7 / V6.8 / V6.9 三个 PRD 为场景来源，为动态匹配、编译器、智能纠正、入口策略编写测试。
3. **抽取共享辅助**：统一工厂函数、断言工具、虚拟页面 fixtures，消除内联样板。
4. **验证 Mock 准确性**：MockVisionService / MockActionExecutor 数据映射、路径切换、操作记录的正确性。
5. **全量可运行**：`pytest tests/v6/ tests/integration/` 全部通过。

---

## 3. 范围

**包含**：
- 清理死代码（conftest.py、重复文件、无效引用）
- D 系列：动态匹配集成测试
- C 系列：PlanCompiler 编译测试
- P 系列：状态机智能纠正回归测试
- E2E 系列：端到端全链路测试
- M 系列：Mock 服务映射验证
- 新建 `tests/helpers/` 共享辅助模块
- 补全虚拟页面 JSON fixtures

**不包含**：
- 真实 AI 服务集成测试（另案处理）
- 性能/压力测试

---

## 4. 设计决策

| 决策 | 结论 | 理由 |
|------|------|------|
| 测试参考文档 | `tests/REFERENCE.md`，不做 skill 文件 | 纯数据速查，无流程指令，不漂移 |
| conftest.py 清理 | 仅删除 6 个 AI fixture | markers、路径、asyncio 无害且被使用 |
| `test_v6_9_dynamic_matching.py` | 已存在（318 行），审查修正而非新建 | 避免重复工作 |
| 共享辅助目录 | `tests/helpers/` | 与 `tests/assets/` 职责分离 |

---

## 5. 第一步：消除死代码

### 5.1 conftest.py — 删除 AI fixture

`tests/conftest.py` 中 `mock_provider` / `mock_deepseek_provider` / `mock_claude_provider` / `mock_mimo_provider` / `all_mock_providers` / `response_recorder` / `response_replayer` 共 7 个 fixture 均依赖不存在的 `tests.ai.fixtures`。全项目无任何测试引用它们。

**动作**：删除这 7 个 fixture 定义及 `from tests.ai.fixtures import ...` 导入。

**保留**：路径设置、pytest markers 注册、`pytest_asyncio` 配置。

### 5.2 删除重复文件

| 动作 | 文件 | 说明 |
|------|------|------|
| 删除 | `tests/v6/test_v6_4_simulation_alignment.py` | 与 `test_simulation.py` 高度重复，有效用例合并到 `test_simulation_base.py` 后删除 |

### 5.3 重命名

| 原文件名 | 新文件名 | 理由 |
|---------|---------|------|
| `tests/v6/test_simulation.py` | `tests/v6/test_simulation_base.py` | 明确语义：基础 Mock 服务测试，区别于 D/C/P/E2E 系列 |

### 5.4 移除死引用

| 文件 | 移除 |
|------|------|
| `tests/integration/test_simulation_e2e.py` | `from tests.simulation.helpers import ...`、`CompletionPolicyType.EXHAUSTIVE`、`Mock(spec=TraversalPlan)` 整段无效代码 |

---

## 6. 第二步：测试场景矩阵

### D 系列 — 动态匹配（对应 V6.9 PRD 4.1-4.4）

文件：`tests/v6/test_v6_9_dynamic_match.py`（已存在 318 行，审查修正）

| ID | 场景 | PRD 引用 | 验证点 |
|----|------|---------|--------|
| D1 | 首次生成动态子节点 | §4.1 | `_dynamic_children[root]` 长度 = 匹配数 |
| D2 | MenuItem → dict 字段映射 | §4.1 | `match_all` 正确消费 `text`/`type`/`index`/`coordinate_x/y` |
| D3 | 逐个取子节点，无重复 | §4.1 | 多次调用 `_get_next_unvisited_child` 返回不同 ID |
| D4 | 全部访问后 FRAME_COMPLETE | §4.1 | `_get_next_unvisited_child` 返回 None |
| D5 | FRAME_COMPLETE 拦截过早退出 | §4.2 | 还剩未访问子节点时推入子节点，继续 NODE_SELECT |
| D6 | 路径变化触发缓存失效 | §4.3 | `invalidate_children_cache` 后下次从新页面生成 |
| D7 | 路径拼接 | §4.4 | `precondition.path = parent_path + [child.name]` |
| D8 | 跳过元素记录 Span | §4.1 | 不匹配元素 `_record_skip_span` 被调用 |
| D9 | page_analysis 为 None | §4.1 | 不崩溃，空列表 |
| D10 | DynamicRule → dict 转换 | §4.1 | `load_rules` 正确消费 |

### C 系列 — 编译器（对应 V6.9 PRD §5）

文件：`tests/v6/unit/test_compiler.py`（新建）

| ID | 场景 | 验证点 |
|----|------|--------|
| C1 | `scope="full"` | `completion_policy.type == NONE` |
| C2 | `scope="partial"` | `type == MAX_STEPS` |
| C3 | `scope="target_only"` + target | `type == TARGET_FOUND, target_name` 正确 |
| C4 | `scope="target_only"` 缺 target | `CompilerError` |
| C5 | `scope="target_path"` | STATIC 节点链 + precondition.path 层层拼接 |
| C6 | `element_handling="full_interaction"` | 4 个 dynamic_rules |
| C7 | `element_handling="menu_only"` | 仅 menu_container |
| C8 | `element_handling="safe_mode"` | 4 个规则 + `meta["safe_mode"]=True` |
| C9 | `element_handling="read_only"` | 仅 leaf_info |
| C10 | `navigation="back"` vs 缺失 | BACK vs AUTO_ESCAPE |
| C11 | `completion="timeout"` 覆盖 | scope 推导被覆盖 |
| C12 | 缺 `target_app` | `CompilerError` |

### P 系列 — 智能纠正（对应 V6.7 PRD）

文件：`tests/v6/test_simulation_sm.py`（补充）

| ID | 场景 | 验证点 |
|----|------|--------|
| P1 | precondition 满足 | 直接进入 EXECUTE |
| P2 | NAVIGABLE 纠正成功 | 点击同级菜单后路径匹配 |
| P3 | DEEPER 纠正成功 | back 后路径匹配 |
| P4 | UNKNOWN 纠正 | back 重试 |
| P5 | 3 轮重试耗尽 | 进入 ERROR_HANDLING |

### E2E 系列 — 端到端（对应 V6.9 §5.5）

文件：`tests/integration/test_simulation_e2e.py`（重写）

| ID | 场景 | 验证点 |
|----|------|--------|
| E2E1 | 全菜单遍历 | 根容器 → 所有子菜单 → 完整 visited_tree |
| E2E2 | 目标搜索 | `completion_reason` 为目标触发 |
| E2E3 | 静态路径 | 沿预定义路径遍历到目标叶节点 |

### M 系列 — Mock 映射验证

文件：`tests/v6/test_simulation_base.py`（新增用例）

| ID | 场景 | 验证点 |
|----|------|--------|
| M1 | elements[].text → MenuItem.name | 已知 JSON → 断言 name 正确 |
| M2 | bounds → Coordinate 转换 | 断言坐标为中心点 `(x1+x2)/2, (y1+y2)/2` |
| M3 | 路径切换 | `set_path_context` 后返回对应页面 |
| M4 | 路径不存在 | 返回空 PageAnalysis |
| M5 | 操作记录 | click/back/swipe 出现在 `executed_actions` |

---

## 7. 测试资产补全

### 7.1 共享辅助 `tests/helpers/`

| 文件 | 内容 |
|------|------|
| `tests/helpers/__init__.py` | 模块初始化 |
| `tests/helpers/mock_factories.py` | `FailingMockVisionService`、`FailingMockActionExecutor`（从 engine_initialization.py 抽取） |
| `tests/helpers/engine_factories.py` | `create_minimal_plan()`、`quick_simulation_runner()` |
| `tests/helpers/trace_asserter.py` | `assert_completed(spans)`、`assert_operation_sequence(spans, expected)`、`assert_restore_count(spans, count)`、`assert_span_types_present(spans, types)` |

### 7.2 虚拟页面补全 `tests/assets/fixtures/`

| 文件 | 用途 | 覆盖场景 |
|------|------|---------|
| `pages_dynamic.json` | 动态匹配 | menu_item / switch / slider / button 混合页面 |
| `pages_correction.json` | 智能纠正 | 多级菜单（Settings → Display → Brightness） |
| `pages_entry.json` | 入口策略 | Home 桌面 + Settings 目标页 |
| 复用现有 | `pages_all.json` / `pages_find.json` | 端到端 / 目标搜索 |

---

## 8. 实施步骤

| 步骤 | 内容 | 产出 | 验证 |
|------|------|------|------|
| 1 | 清理死代码 | conftest.py 清理 + 删除 test_v6_4_alignment + 重命名 | `pytest tests/v6/ --collect-only` 无导入错误 |
| 2 | 新建 `tests/helpers/` | 3 个共享模块 | import 通过 |
| 3 | 补全虚拟页面 JSON | 3 个新 fixture 文件 | MockVisionService 正确解析 |
| 4 | 审查 `test_v6_9_dynamic_match.py` | 对齐 D 系列场景的已有测试 | D1-D10 全部通过 |
| 5 | 新建 `test_compiler.py` | C 系列单元测试 | C1-C12 全部通过 |
| 6 | 补充 `test_simulation_sm.py` | P 系列智能纠正 | P1-P5 全部通过 |
| 7 | 合并/修正 `test_simulation_base.py` | Mock 基础 + M 系列 | M1-M5 全部通过 |
| 8 | 重写 `test_simulation_e2e.py` | E2E 端到端 | E2E1-E2E3 全部通过 |
| 9 | 全量回归 | `pytest tests/v6/ tests/integration/` | 100% 通过 |

---

## 9. 测试执行速查

### 单文件快速执行

```bash
# 动态匹配集成测试
pytest tests/v6/test_v6_9_dynamic_match.py -v

# 编译器单元测试
pytest tests/v6/unit/test_compiler.py -v

# Mock 基础 + 映射验证
pytest tests/v6/test_simulation_base.py -v

# 状态机智能纠正
pytest tests/v6/test_simulation_sm.py -v

# 入口策略
pytest tests/v6/test_engine_initialization.py -v

# 端到端
pytest tests/integration/test_simulation_e2e.py -v
```

### 按系列过滤

```bash
# D 系列（动态匹配）：名称含 dynamic 的用例
pytest tests/v6/test_v6_9_dynamic_match.py -v -k "test_dynamic"

# C 系列（编译器）
pytest tests/v6/unit/test_compiler.py -v

# P 系列（智能纠正）
pytest tests/v6/test_simulation_sm.py -v -k "test_precondition"

# M 系列（Mock 映射）
pytest tests/v6/test_simulation_base.py -v -k "test_mock"

# E2E 系列
pytest tests/integration/test_simulation_e2e.py -v -k "test_e2e"
```

### 批量子集

```bash
# V6.9 新功能全套（D + C + E2E）
pytest tests/v6/test_v6_9_dynamic_match.py tests/v6/unit/test_compiler.py tests/integration/test_simulation_e2e.py -v

# 仿真核心全套（Mock + SM + Trace）
pytest tests/v6/test_simulation_base.py tests/v6/test_simulation_sm.py tests/v6/test_trace_simulation.py -v

# 仅单元测试（快速反馈，< 1s）
pytest tests/v6/unit/ -v

# 仅集成测试
pytest tests/integration/ -v
```

### 全量回归

```bash
# 全量回归
pytest tests/v6/ tests/integration/ -v

# CI 模式（失败即停，显示摘要）
pytest tests/v6/ tests/integration/ -x --tb=short -q
```

### 按标记运行

```bash
# 慢速测试（端到端）
pytest tests/ -m slow -v

# 排除慢速测试（快速验证）
pytest tests/ -m "not slow" -v
```

---

## 10. 验收标准

- [ ] `conftest.py` 不依赖不存在模块，无 AI fixture。
- [ ] `test_v6_4_simulation_alignment.py` 已删除。
- [ ] `tests/helpers/` 三个模块存在且可被测试导入。
- [ ] D 系列 10 个用例全部通过，注释引用 V6.9 PRD 场景编号。
- [ ] C 系列 12 个用例全部通过。
- [ ] P 系列 5 个用例全部通过。
- [ ] M 系列 5 个 Mock 验证用例全部通过。
- [ ] E2E 系列 3 个场景全部通过。
- [ ] `pytest tests/v6/ tests/integration/` 全量通过。

---

## 10. 附录

- 《V6.7 状态机智能化 PRD》— `docs/PRD_V6_7_state_machine_intelligence.md`
- 《V6.8 引擎初始化 PRD》— `docs/PRD_V6_8_engine_initialization.md`
- 《V6.9 遍历执行与计划编译 PRD》— `docs/PRD_V6_9_plan_compilation_and_matching.md`
- 《测试数据模型速查》— `tests/REFERENCE.md`
