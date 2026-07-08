# Layers — Simulation Baseline

> **Tier 3 · Layers**: Simulation 基线端到端测试规格书。改基线场景/规则/数值时更新。
> 约束: → constitution C-11 (基线 E2E 回归门槛)
> 配套代码层规格: → layers/simulation.md
> Python 对齐参考: 本文档从 Python `expected_behavior.yaml` + `simulation-ci.yaml` 提取

---

## 0. 项目结构与索引

基线测试体系横跨**文档层**和**代码层**，完整索引如下：

### 文档层 (docs/system/)

| 文件 | 层级 | 内容 | 更新触发 |
|------|------|------|---------|
| `constitution/constraints.md` C-11 | Tier 1 | 基线 E2E 回归门槛原则: "必须通过，回归 = CI-blocking" | 新增基线场景或发现回归 |
| `layers/simulation-baseline.md` | Tier 3 | **本文件**: 2 核心场景定义 + 7 类规则体系 + 基线数值 + Python↔C# 对照 | 改基线场景/规则/数值 |
| `layers/simulation.md` | Tier 3 | Simulation 代码层规格 (类型清单、数据流、依赖) | 改 Simulation 代码 |

### 代码层 (tests/)

| 文件 | 目录 | 内容 | 性质 |
|------|------|------|------|
| `SimulationBaselineTests.cs` | `tests/.../Baseline/` | 场景1+2 C# 测试代码 (Assert 验证) | **功能回归 guard** — CI-blocking |
| `settings-app.json` | `tests/.../Baseline/Fixtures/` | 7页 Settings App fixture 数据 | 基线专用测试资产 |
| `ArchitectureGuardTests.cs` | `tests/.../Architecture/` | 架构约束 guard (C-1~C-8) | **架构约束 guard** — CI-blocking |
| `SimulationE2ETests.cs` | `tests/.../Simulation/` | 2-page/4-page 开发验证 E2E | 普通 E2E (非基线) |

### 三类测试的区分

| 目录 | 性质 | CI-blocking | 失败语义 | 对应文档 |
|------|------|-------------|---------|---------|
| `Architecture/` | 架构约束 guard | ✅ 阻断 | 规则违反，修代码 | constitution/* |
| `Baseline/` | 功能回归 guard | ✅ 阻断 | 主功能退化，修代码 | C-11 + simulation-baseline.md |
| `Simulation/` | 普通 E2E / 单元 | ✅ 阻断 | 功能不工作，排查 | layers/simulation.md |

### 紧密配对关系

```
constitution/constraints.md (C-11 原则)
  ↕ 交叉验证
layers/simulation-baseline.md (场景定义 + 基线数值)
  ↕ 测试断言映射
tests/.../Baseline/SimulationBaselineTests.cs (代码验证)
tests/.../Baseline/Fixtures/settings-app.json (fixture 数据)
```

### Python↔C# 资产对照

| Python 资产 | C# 等价 | 位置 |
|------------|---------|------|
| `expected_behavior.yaml` (规则+数值) | `simulation-baseline.md` (文档) + `SimulationBaselineTests.cs` (代码断言) | docs/ + tests/ |
| `simulation-ci.yaml` (CI配置) | GitHub Actions workflow (dotnet test filter) | `.github/workflows/` |
| `run_simulation_ci.py` (调度脚本) | `dotnet test --filter "FullyQualifiedName~Baseline"` | 内置，无独立脚本 |
| `test_settings_simulation.py` (全量遍历) | `SimulationBaselineTests.cs` 全量遍历场景 | `tests/.../Baseline/` |
| `test_target_search.py` (目标搜索) | `SimulationBaselineTests.cs` 目标搜索场景 | `tests/.../Baseline/` |
| `settings_page.json` (fixture 数据) | `settings-app.json` | `tests/.../Baseline/Fixtures/` |
| `expected_behavior.py` (行为定义类) | `SimulationBaselineTests.cs` 内联验证逻辑 | 无独立 C# 类 (C# 用 Assert 内联) |

---

## 1. 核心场景

### 场景 1: Settings 全量遍历 (safe_full_traversal)

| 属性 | 值 |
|------|-----|
| 基线版本 | Python V6.11.0 |
| Python 测试入口 | `test_settings_simulation.py::test_settings_simulation_run` |
| Completion | `expected_state: completed`, `expected_reason: natural` |
| CompletionPolicy | NONE (自然完成) |

**虚拟 App 结构** — 7 页 + 2 子页的 Android Settings:

```
root (设置主页)
  ├── menu_container-Wi-Fi-0-root          ← level1
  │     ├── HomeNetwork                     ← level2
  │     ├── OfficeWiFi                      ← level2
  │     ├── GuestNetwork                    ← level2
  │     └── switch_leaf-ON (Wi-Fi 开关)     ← leaf operation
  ├── menu_container-Bluetooth-1-root       ← level1
  │     ├── Headphones Pro                  ← level2
  │     ├── Speaker Mini                    ← level2
  │     └── switch_leaf-ON (蓝牙开关)       ← leaf operation
  ├── menu_container-Display-2-root         ← level1
  │     ├── Brightness level (slider)       ← level2
  │     ├── Wallpaper                       ← level2
  │     └── switch_leaf-Dark mode           ← leaf operation
  ├── menu_container-Storage-3-root         ← level1
  │     ├── Internal Storage (只读)         ← level2
  │     ├── SD Card (只读)                  ← level2
  ├── menu_container-Battery-4-root         ← level1
  ├── menu_container-Apps-5-root            ← level1
```

**基线数值** (Python V6.11.0):

| 指标 | 值 |
|------|-----|
| 总步数 | **118** |
| 访问节点数 | **19** |
| 执行时间 | < 5s |
| Trace nodes | ~600 |

**visited_pages 基线明细**:

```
root:       设置主页
level1:     Wi-Fi, Bluetooth, Display, Storage, Battery, Apps (6 个)
level2:     HomeNetwork, OfficeWiFi, GuestNetwork,
            Headphones Pro, Speaker Mini,
            Brightness level, Wallpaper,
            Internal Storage, SD Card (8 个)
leaf_ops:   Wi-Fi 开关, 蓝牙开关, Dark mode 开关 (3 个)
```

---

### 场景 2: Settings 目标搜索 (TARGET_FOUND)

| 属性 | 值 |
|------|-----|
| 基线版本 | Python V6.11.1 |
| Python 测试入口 | `test_target_search.py::test_target_search_stops_at_dark_mode` |
| CompletionPolicy | TARGET_FOUND: Dark mode (EXACT, MARK_AND_STOP) |
| MatchMode | EXACT — 精确匹配文本 "Dark mode" |

**基线数值** (Python V6.11.1):

| 指标 | 值 |
|------|-----|
| 总步数 | **49** |
| 访问节点数 | **9** |
| 执行时间 | < 2s |

**DFS 遍历路径与提前终止**:

```
设置主页 (root)                → visited
  Wi-Fi 子树完整               → visited (3 子页 + 开关)
  Bluetooth 子树完整           → visited (2 子页 + 开关)
  Display 子树 → 命中 Dark mode → MARK_AND_STOP
  Storage                       → ❌ 未访问 (提前终止)
  Battery                       → ❌ 未访问
  Apps                          → ❌ 未访问
```

**visited_pages 顺序**:

```
1. 设置主页 (root)
2. Wi-Fi (menu_container-Wi-Fi-0-root)
3. Bluetooth (menu_container-Bluetooth-1-root)
4. Display (menu_container-Display-2-root)
5. Dark mode (switch_leaf-Dark mode-2-menu_container-Display-2-root) ← 目标命中
```

**not_visited** (证明提前终止有效):

```
Storage, Battery, Apps — 均排在 Display 之后，命中目标后不再访问
```

**行为特性**:
- 深度优先: Wi-Fi 子树完成 → Bluetooth 子树完成 → Display 子树中命中
- 提前终止: Storage/Battery/Apps 未被访问，证明 MARK_AND_STOP 生效
- 二级菜单无 fixture 页的项: HomeNetwork 等失败回退，不阻塞遍历

---

### 两个场景核心差异

| 对比项 | 全量遍历 | 目标搜索 |
|--------|---------|---------|
| 目的 | 验证 DFS 完整性 + 所有行为规则 | 验证 TARGET_FOUND 提前终止策略 |
| CompletionPolicy | NONE (自然完成) | TARGET_FOUND + MARK_AND_STOP |
| 步数 | 118 | 49 (少 59%) |
| 节点数 | 19 | 9 (少 53%) |
| 验证重点 | 7 类规则全覆盖 | DFS 顺序 + 提前终止 + 未访问项证明 |

---

## 2. 七类规则验证体系

Python `expected_behavior.yaml` 定义了 7 类验证维度。全量遍历场景必须通过全部 7 类，目标搜索场景聚焦 DFS 顺序和提前终止。

### 维度 1: completion (遍历完成状态)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| 最终状态 | `expected_state: completed` | 全量遍历 |
| 完成原因 | `expected_reason: natural` | 全量遍历 |
| 目标搜索完成 | TARGET_FOUND 原因正确 | 目标搜索 |

### 维度 2: page_rules (页面验证规则)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| leaf_pages_visited | 所有只读/空页面被访问 (Internal Storage, SD Card) | 全量遍历 |
| popup_absent | `is_popup == true` 不存在 | 全量遍历 |

### 维度 3: operation_rules (操作验证规则)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| depth_first_order | 操作顺序符合 DFS (设置→Wi-Fi→开关→子页→蓝牙→显示…) | 全量遍历 |
| restore_operations_count | switch/slider 后执行恢复, `count ≥ 2` | 全量遍历 |
| skip_dangerous_buttons | 恢复出厂设置/清除数据被跳过 | 全量遍历 |
| no_duplicate_actions | 同节点连续重复 ≤ 2 | 全量遍历 |

### 维度 4: error_recovery (智能纠错验证)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| precondition_correction | 导航偏差触发 precondition correction (retry_count > 0) | 全量遍历 |

### 维度 5: exit_strategy (退出策略验证)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| auto_escape_used | 同级菜单切换 ≥ 2 (Wi-Fi→蓝牙, 显示→存储) | 全量遍历 |

### 维度 6: node_coverage (节点访问覆盖率)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| dynamic_nodes_visited | `dyn_*` 动态节点覆盖率 ≥ 95% | 全量遍历 |

### 维度 7: trace_integrity (Trace 完整性)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| span_types_present | session_end, step_end, state_transition, execution, ai_call, page_transition 全存在 | 全量遍历 |
| page_transitions_recorded | page_transition ≥ 10 | 全量遍历 |

---

## 3. C# 当前状态与缺口

### 已有 (SimulationE2ETests.cs, 7 个场景)

| # | 场景 | 验证内容 | 是否基线级 |
|---|------|---------|-----------|
| 1 | 空节点树立即完成 | AllVisited, steps≤5 | ❌ 开发验证 |
| 2 | 2 页遍历 | AllVisited, ActionHistory 非空 | ❌ 开发验证 |
| 3 | MaxSteps 超限 | MaxSteps, TotalSteps=1 | ❌ 开发验证 |
| 4 | VisitedPages 按序 | root 首个被访问 | ❌ 开发验证 |
| 5 | 4 页 Settings App 全路径 | AllVisited, tap+back 混合 | ❌ 简化版 (4 页 vs 7 页) |
| 6 | Settings App WiFi 路径 | 4 步 (2 tap + 2 back) | ❌ 简化版 |
| 7 | 空区域 tap | ResultVerify, success=false | ❌ 开发验证 |

### 缺口 (待建)

| 缺口 | 说明 | 依赖 |
|------|------|------|
| **7 页 Settings App fixture** | 当前 fixture 只有 2-page 和 4-page, 没有 Python 等价的 7 页完整结构 | StateFixture Fluent Builder 已可用 |
| **全量遍历基线测试** | 7 类规则验证 + 数值断言 (118/19) | 7 页 fixture + DynamicMatch handler (Phase 2.3b) |
| **目标搜索基线测试** | DFS 顺序 + 提前终止 + 未访问项证明 (49/9) | CompletionPolicy TARGET_FOUND 实现 |
| **7 类规则验证框架** | C# 没有 Python `ExpectedBehavior` 的等价定义类 | 可内联 Assert 或建独立验证 helper |

### 建设时序

```
Phase 2.3b 完成 (HandlePreconditionCheck + HandleResultVerify)
  → 基线建设前置条件满足
  → 建设顺序:
    1. settings-app.json (7页 fixture, 照 Python settings_page.json)
    2. SimulationBaselineTests.cs (2 核心场景)
    3. C-11 加入 constitution/constraints.md
    4. simulation-baseline.md 基线数值更新为 C# 实际值
```

C# 基线数值**不会**与 Python 完全一致 (引擎行为差异、DFS 顺序差异、元素映射差异)。第一步用 Python 数值作为**参考锚点**，待 C# 测试实际运行后更新为 C# 实际基线值。

---

## 4. 基线数值更新规则

基线数值是 Tier 3 数据，不是 Tier 1 不可变约束。数值随代码演进自然变化。

| 变更类型 | 是否需要更新 | 更新方式 |
|---------|------------|---------|
| 加新页面到 fixture | ✅ steps/nodes 增加 | 更新本文件 §1 基线数值 |
| 修复 DFS 顺序 bug | ✅ steps 可能变化 | 更新本文件 §1 visited_pages 顺序 |
| CompletionPolicy 变更 | ✅ 完成条件变化 | 更新本文件 §1 Completion 字段 |
| 引擎内部优化 (不影响行为) | ❌ 数值不变 | 无需更新 |
| 新增规则验证维度 | ✅ 加新规则到 §2 | 更新本文件 §2 |

更新时同步更新 `SimulationBaselineTests.cs` 中的 Assert 断言值，确保文档数值 = 代码断言 = 实际运行结果 三路一致。
