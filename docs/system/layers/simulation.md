# Layers — Simulation

> **Tier 3 · Layers**: Simulation 层规格书。新增仿真能力时更新。
> 状态: Phase 2.3-sim 完成 (核心 3 组件 + 488 tests pass)
> 源码: `src/UniClaw.Core/Simulation/`
> 约束: → constitution C-4
> 对齐: Python `src/simulation/` (main 分支)

---

## 1. Type Inventory

### Record Classes (4)

| Record | 用途 | JSON 字段 |
|--------|------|----------|
| `StateFixture` | 页面/跳转规则顶层容器，含运行时 `_transitionIndex` | `initialPage`, `pages`, `transitions` |
| `PageState` | 单个页面定义 | `pageName`, `elements`, `isComplete` |
| `PageElement` | 可交互元素 | `id`, `type`, `text`, `x`, `y`, `actionTarget?` |
| `PageTransition` | 页面跳转规则 | `id`, `trigger`, `fromPage`, `toPage`, `action` |

### Service Classes (3)

| Class | 实现接口 | 用途 |
|-------|---------|------|
| `StatefulMockVisionService` | `IVisionProvider` | 状态感知页面模拟: 维护 `_currentPageId` 状态机, `FindElementAt` 坐标匹配 |
| `StatefulMockActionExecutor` | `IActionExecutor` | 联动 vision 的操作模拟: `TapAsync` → `FindElementAt` → `SimulateAction` |
| `SimpleNodeRegistry` | `INodeRegistry` | 测试用节点注册表 (Dictionary-backed) |

### Builders (4)

| Builder | 产出 | 用途 |
|---------|------|------|
| `StateFixtureBuilder` | `StateFixture` | 顶层 Fluent API: `.Page(...).Transition(...).Build()` |
| `PageStateBuilder` | `PageState` | 页面构建: `.Name(...).Button(...).Switch(...).Tab(...)` |
| `PageElementBuilder` | `PageElement` | 元素构建: `.Type(...).Text(...).At(...).Targets(...)` |
| `PageTransitionBuilder` | `PageTransition` | 跳转构建: `.Id(...).Click(...).From(...).To(...)` |

### DTO Classes (3, internal)

| DTO | 用途 |
|-----|------|
| `StateFixtureDto` | JSON 反序列化中间层 (`ImmutableDictionary` 不支持直接反序列化) |
| `PageStateDto` / `PageElementDto` / `PageTransitionDto` | 嵌套 DTO |

---

## 2. 数据流

### 2.1 Fixture 创建 (两种路径)

```
JSON 文件
  → StateFixture.FromJson(json)
    → StateFixtureDto (System.Text.Json 反序列化)
      → StateFixture (含 _transitionIndex 索引)

Fluent Builder
  → StateFixtureBuilder
    .Page(...).Transition(...).Build()
      → StateFixture
```

### 2.2 仿真遍历循环

```
StepOrchestrator.ExecuteStep(ctx)
  ctx.Vision → StatefulMockVisionService
    AnalyzeCurrentPageAsync()
      → _fixture.GetPage(_currentPageId)
        → BuildPageAnalysis(page)
          → PageElement → MenuItem/MenuInfo 映射

  ctx.Action → StatefulMockActionExecutor
    TapAsync(x, y)
      → _vision.FindElementAt(x, y)  // 容差 ±0.05
        → _vision.SimulateAction(element.Id, "click")
          → _fixture.ResolveTarget(_currentPageId, elementId, action)
            → _navigationHistory.Push(_currentPageId)
            → _currentPageId = target
    PressBackAsync()
      → _vision.NavigateBack()
        → _navigationHistory.Pop()
```

---

## 3. Element Type → MenuItem 映射表

| `PageElement.Type` | `MenuItemType` | `ExpectedAction` | `ExpectsPageChange` | `ExpectsStateChange` | 放入 |
|--------------------|----------------|------------------|---------------------|---------------------|------|
| `button` | Button | Navigate | true | false | Items |
| `switch` | Switch | Toggle | false | true | Items |
| `toggle` | Toggle | Toggle | false | true | Items |
| `back_button` | BackButton | Navigate | true | false | Items + `BackButton` 坐标提取 |
| `icon` | Icon | Action | true | false | Items |
| `input` | Item | Action | false | false | Items |
| `readonly` | Readonly | None | false | false | Items |
| `text` | Text | None | false | false | Items |
| `tab` | — (→ `MenuInfo`) | — | — | — | Level1Menus |

对齐 Python `StatefulMockVisionService._build_page_analysis()` 和 `MenuItemTypeMapper`。

---

## 4. IActionExecutor 方法行为

| 方法 | Vision 联动 | 返回逻辑 |
|------|------------|---------|
| `TapAsync(x, y)` | `FindElementAt` → `SimulateAction("click")` | 找到元素 → true; 未找到 → false |
| `PressBackAsync()` | `NavigateBack()` | 栈非空 → true; 空栈 → false |
| `SwipeAsync(...)` | 无 (Scroll 留给 Phase 后续) | 始终 true |
| `InputTextAsync(text)` | 无 | 始终 true |
| `LongPressAsync(x, y)` | `FindElementAt` (仅记录) | 找到元素 → true; 未找到 → false |
| `WaitAsync(ms)` | 无 | — |

---

## 5. 与 Python 的差异

| 项目 | Python | C# | 原因 |
|------|--------|-----|------|
| 数据格式 | YAML | JSON + Fluent Builder | 零新 NuGet 依赖 (`System.Text.Json` 已内置) |
| VisionService ABC | 3 方法 | IVisionProvider 2 方法 | `get_current_page` 合并到 `AnalyzeCurrentPageAsync` 返回值 |
| `action_target` | Element 必有字段 | 可选字段，运行时不依赖 | Transition 表是单一数据源 |
| Scroll 仿真 | `ScrollableMockVisionService` | 未迁移 | 留给后续 Phase |
| `BehaviorValidator` / `ProblemDetector` | 各 ~550 行 | 未迁移 | 留给后续 Phase |

---

## 6. 测试覆盖

| 测试类 | 场景数 | 覆盖 |
|--------|--------|------|
| `StateFixtureTests` | 6 | JSON 反序列化, ResolveTarget hit/miss, GetPage, Builder |
| `StatefulMockVisionTests` | 11 | SimulateAction, NavigateBack, FindElementAt, Reset, AnalyzeCurrentPage, BuildPageAnalysis mapping, FindAppEntry |
| `StatefulMockActionTests` | 5 | TapAsync 联动, PressBackAsync 联动, GetHistory 顺序 |
| `SimulationE2ETests` | 2 | 2-page 线性遍历, 空区域 tap |

---

## 7. Dependency

```
Simulation → StateMachine (IVisionProvider, AppEntryPoint)
Simulation → Domain.Models.Content (PageAnalysis, MenuItem, MenuInfo, Coordinate, Direction, MenuItemType, ExpectedAction)
Simulation → Domain.Models.Common (Operation, Target — via StatefulMockActionExecutor)
Simulation → Graph.Models (TraversalNode, ChildrenStrategy — via SimpleNodeRegistry)
Simulation → Traversal (IActionExecutor, ActionRecord, INodeRegistry)
```

**零新 NuGet 依赖**。仅使用 `System.Text.Json` (已内置) 和 `System.Collections.Immutable` (已内置)。

---

## 8. 设计决策 (→ decisions/log.md)

| ID | 决策 | 状态 |
|----|------|------|
| D-21 | IVisionProvider 2 方法 (AnalyzeCurrentPageAsync + FindAppEntryAsync) | ✅ Implemented |
| D-22 | Simulation 在 Core 库 (非 test project) | ✅ Implemented |
| D-23 | JSON + Fluent Builder 双格式 | ✅ Implemented |
| D-24 | PageElement.ActionTarget: JSON 可选, 运行时忽略 | ✅ Implemented |
| D-25 | StepOrchestrator.Step(ctx) 传递 StepContext | ✅ Implemented |
| D-26 | FindElementAt 容差 ±0.05 | ✅ Implemented |
