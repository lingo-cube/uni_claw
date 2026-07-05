# Phase 2.3-sim — 仿真基础设施迁移设计

> 基于 main 分支 Python `src/simulation/` 与 feature/refactor 分支 C# 代码的对照设计。
> 目标: 将 Python 仿真体系的核心 3 组件迁移到 C#，使遍历引擎能在无真实 ADB/AI 的环境下端到端运行。
> 日期: 2026-07-05

---

## 1. 动机

### 1.1 当前状态

C# 遍历引擎有完整的 FSM（8 handler × 迁移矩阵）、Context（30 可变状态）、StepOrchestrator（14-step 编排），但无法端到端运行：

- `IVisionProvider` 只有 1 个 placeholder 方法，无真实或 mock 实现
- `IActionExecutor` 只有测试用 `MockActionExecutor`，返回固定 `NextResult`
- 2.3a 的 `HandleExecute` + `HandleBranch` 只能做单元测试，无法在真实循环中验证
- 2.3b（`HandleResultVerify` + `HandlePreconditionCheck`）依赖视觉验证，写完也无法端到端测
- 2.3c（`HandleErrorHandling` + `HandlePopupHandling`）子组件就绪，但没有集成验证平台

### 1.2 Python 对照

Python `src/simulation/` (~3,154 行) 提供了完整仿真能力：

```
SimulationRunner（编排层）
  ├── StateFixture（YAML 驱动的页面状态 + 跳转规则）
  ├── StatefulMockVisionService : VisionService ABC（状态感知页面模拟）
  ├── StatefulMockActionExecutor : OperationExecutor ABC（联动 vision 的操作模拟）
  ├── BehaviorValidator（实际 trace vs 期望行为）
  └── ProblemDetector（异常模式检测）
```

Python 的设计原则：**引擎是真实的，只有 Vision 和 Action 是 mock**。FSM、Context、TraceRecorder 全部使用生产代码。

### 1.3 第一阶段范围

迁移 Python 仿真体系的核心 3 组件：

| Python | C# | 行数估算 |
|--------|-----|---------|
| `state_fixture.py` (331 行) | `StateFixture.cs` + `StateFixtureBuilder.cs` | ~350 |
| `stateful_mock_vision.py` (292 行) | `StatefulMockVisionService.cs` | ~250 |
| `stateful_mock_action.py` (232 行) | `StatefulMockActionExecutor.cs` | ~200 |
| — | `IVisionProvider` 接口补全 + `AppEntryPoint` | ~30 |
| — | `SimpleNodeRegistry` (测试用) | ~15 |

**总估算**: ~850 行 C#，零新 NuGet 依赖。

后续阶段（不入本文）:
- `SimulationRunner` — 编排层，一键运行完整仿真
- `BehaviorValidator` + `ExpectedBehavior` — 声明式行为验证
- `ProblemDetector` — 自动异常检测
- `ScrollableMockVisionService` — 滚动列表仿真

---

## 2. 架构总览

### 2.1 文件布局

```
src/UniClaw.Core/
├── StateMachine/
│   ├── StepContext.cs              ← IVisionProvider 接口补全（§3）
│   └── TraversalFSM.cs             ← 不变
│
├── Simulation/                     ← NEW namespace: UniClaw.Core.Simulation
│   ├── StateFixture.cs             ← 数据模型 + JSON 反序列化（§4）
│   ├── StateFixtureBuilder.cs      ← Fluent Builder（§4.3）
│   ├── StatefulMockVisionService.cs ← : IVisionProvider（§5）
│   └── StatefulMockActionExecutor.cs ← : IActionExecutor（§6）
│
└── Traversal/
    └── StepOrchestrator.cs         ← 一行修改: Step() → Step(ctx)（§7）

tests/UniClaw.Core.Tests/
├── Simulation/                     ← NEW
│   ├── StateFixtureTests.cs
│   ├── StatefulMockVisionTests.cs
│   ├── StatefulMockActionTests.cs
│   └── SimulationE2ETests.cs       ← 端到端遍历测试
└── Fixtures/                       ← NEW
    └── two-page-app.json
```

### 2.2 依赖关系

```
Simulation → StateMachine (IVisionProvider, StepContext)
Simulation → Domain.Models.Content (PageAnalysis, MenuItem, Coordinate, ...)
Simulation → Domain.Models.Common (Operation, Target, ...)
Simulation → Graph.Models (TraversalNode, ChildrenStrategy, ...)
Simulation → Traversal (IActionExecutor, ActionRecord)
```

**Simulation 不依赖外部 NuGet 包**。JSON 反序列化使用已有的 `System.Text.Json`。

### 2.3 核心原则

- **FSM 是真实的** — `TraversalFSM` 不改一行代码（除 2.3a 已完成的 handler）
- **Context 是真实的** — `TraversalRuntimeContext` 不改一行代码
- **只有 Vision 和 Action 是 mock** — 通过 `StepContext` 注入
- **StepOrchestrator 是真实的** — 仅一行修改，传递 `StepContext` 给 FSM

---

## 3. IVisionProvider 接口补全

### 3.1 当前接口

```csharp
// StepContext.cs — 当前 1 方法
public interface IVisionProvider
{
    Task<PageAnalysis?> GetCurrentPageAnalysisAsync(CancellationToken ct = default);
}
```

### 3.2 补全后接口

```csharp
// StepContext.cs — 补全为 2 方法
public interface IVisionProvider
{
    /// <summary>分析当前页面截图 → PageAnalysis</summary>
    Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default);

    /// <summary>在启动器中查找目标 app 入口坐标</summary>
    Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default);
}

/// <summary>App 入口坐标（归一化 0-1）</summary>
public sealed record class AppEntryPoint(double X, double Y);
```

### 3.3 方法说明

| 方法 | 消费方 | 说明 |
|------|--------|------|
| `AnalyzeCurrentPageAsync` | HandleResultVerify, HandlePreconditionCheck, HandlePopupHandling | 获取当前页面的结构化分析结果（元素列表、菜单、弹窗等） |
| `FindAppEntryAsync` | HandlePreconditionCheck | 在桌面/启动器中定位目标 app 图标坐标，用于首次进入应用 |

### 3.4 对应 Python

```python
# Python VisionService ABC (src/ai/vision_service.py)
class VisionService(ABC):
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis: ...
    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]: ...
    def get_current_page(self) -> Optional[dict]: ...
```

C# 将 `get_current_page` 的能力合并到 `AnalyzeCurrentPageAsync` 中 — 返回 `PageAnalysis` 包含 `CurrentPath` 字段，无需独立方法。

### 3.5 迁移影响

- `StepContext.cs`: 接口签名变更 + 新增 `AppEntryPoint`
- `MockVisionProvider`（tests/）: 方法重命名，保持返回 null（单元测试 stub）
- `StatefulMockVisionService`（新）: 实现完整 2 方法
- 现有 438 测试: 需更新方法名引用（如果有），预计 0-2 个测试受影响

---

## 4. StateFixture 数据模型

### 4.1 类型定义

```csharp
namespace UniClaw.Core.Simulation;

/// <summary>仿真页面/跳转规则的顶层容器</summary>
public sealed record class StateFixture(
    string InitialPage,
    ImmutableDictionary<string, PageState> Pages,
    ImmutableArray<PageTransition> Transitions)
{
    // 运行时索引: (fromPage, trigger, action) → toPage
    private readonly Dictionary<(string, string, string), string> _transitionIndex;

    public StateFixture(...)
    {
        _transitionIndex = Transitions.ToDictionary(
            t => (t.FromPage, t.Trigger, t.Action),
            t => t.ToPage);
    }

    /// <summary>解析跳转目标。未匹配返回 null。</summary>
    public string? ResolveTarget(string fromPage, string elementId, string action)
        => _transitionIndex.TryGetValue((fromPage, elementId, action), out var to)
            ? to : null;

    public PageState? GetPage(string pageId)
        => Pages.TryGetValue(pageId, out var page) ? page : null;
}

/// <summary>单个页面的完整定义</summary>
public sealed record class PageState(
    string PageName,
    ImmutableArray<PageElement> Elements,
    bool IsComplete = false);

/// <summary>页面上的一个可交互元素</summary>
public sealed record class PageElement(
    string Id,
    string Type,           // "button" | "switch" | "tab" | "back_button" | "icon" | "text" | "input" | "readonly"
    string Text,
    double X, double Y,    // 归一化坐标 (0-1)
    string? ActionTarget = null);  // 可选: 点击后触发哪个 transition（仅供文档，运行时不依赖）

/// <summary>一条页面跳转规则</summary>
public sealed record class PageTransition(
    string Id,
    string Trigger,        // element id
    string FromPage,       // source page id
    string ToPage,         // target page id
    string Action);        // "click" | "back" | "swipe"
```

### 4.2 JSON 格式

```json
{
  "initialPage": "home",
  "pages": {
    "home": {
      "pageName": "HomeScreen",
      "isComplete": false,
      "elements": [
        {
          "id": "btn_settings",
          "type": "button",
          "text": "Settings",
          "x": 0.5,
          "y": 0.9,
          "actionTarget": "settings"
        },
        {
          "id": "btn_profile",
          "type": "button",
          "text": "Profile",
          "x": 0.5,
          "y": 0.8,
          "actionTarget": "profile"
        },
        {
          "id": "tab_home",
          "type": "tab",
          "text": "Home",
          "x": 0.2,
          "y": 0.95
        }
      ]
    },
    "settings": {
      "pageName": "SettingsScreen",
      "isComplete": false,
      "elements": [
        {
          "id": "sw_wifi",
          "type": "switch",
          "text": "Wi-Fi",
          "x": 0.8,
          "y": 0.3
        },
        {
          "id": "sw_bt",
          "type": "switch",
          "text": "Bluetooth",
          "x": 0.8,
          "y": 0.4
        },
        {
          "id": "btn_back",
          "type": "back_button",
          "text": "Back",
          "x": 0.05,
          "y": 0.05
        }
      ]
    }
  },
  "transitions": [
    {
      "id": "home_to_settings",
      "trigger": "btn_settings",
      "fromPage": "home",
      "toPage": "settings",
      "action": "click"
    },
    {
      "id": "settings_back",
      "trigger": "btn_back",
      "fromPage": "settings",
      "toPage": "home",
      "action": "click"
    },
    {
      "id": "home_to_profile",
      "trigger": "btn_profile",
      "fromPage": "home",
      "toPage": "profile",
      "action": "click"
    }
  ]
}
```

### 4.3 Fluent Builder（代码驱动快捷构建）

```csharp
var fixture = new StateFixtureBuilder()
    .Page("home", p => p
        .Name("HomeScreen")
        .Element("btn_settings", e => e.Button("Settings").At(0.5, 0.9).Targets("settings"))
        .Element("btn_profile",  e => e.Button("Profile").At(0.5, 0.8).Targets("profile"))
        .Tab("tab_home", "Home", 0.2, 0.95))
    .Page("settings", p => p
        .Name("SettingsScreen")
        .Element("sw_wifi",  e => e.Switch("Wi-Fi").At(0.8, 0.3))
        .Element("sw_bt",    e => e.Switch("Bluetooth").At(0.8, 0.4))
        .BackButton("btn_back", 0.05, 0.05))
    .Transition(t => t.Id("go").Click("btn_settings").From("home").To("settings"))
    .Transition(t => t.Id("back").Click("btn_back").From("settings").To("home"))
    .Build();
```

### 4.4 JSON 反序列化策略

`System.Text.Json` 对 `ImmutableDictionary` 和 `ImmutableArray` 的支持有限。采用两步转换：

```csharp
// 中间 DTO（仅用于反序列化，不对外暴露）
internal sealed record class StateFixtureDto(
    string InitialPage,
    Dictionary<string, PageStateDto> Pages,
    List<PageTransition> Transitions);

// StateFixture.FromJson(string json)
public static StateFixture FromJson(string json)
{
    var dto = JsonSerializer.Deserialize<StateFixtureDto>(json, DomainJsonOptions.Default);
    return new StateFixture(
        InitialPage: dto.InitialPage,
        Pages: dto.Pages.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => new PageState(kvp.Value.PageName, kvp.Value.Elements, kvp.Value.IsComplete)),
        Transitions: dto.Transitions.ToImmutableArray());
}
```

`DomainJsonOptions.Default` 已配置 `camelCase + enum-as-string`，无需额外配置。

---

## 5. StatefulMockVisionService

### 5.1 职责

实现 `IVisionProvider`，内部维护页面状态机。对齐 Python `StatefulMockVisionService`。

### 5.2 接口实现 + 仿真专用方法

```csharp
namespace UniClaw.Core.Simulation;

public sealed class StatefulMockVisionService : IVisionProvider
{
    private readonly StateFixture _fixture;
    private string _currentPageId;
    private readonly Stack<string> _navigationHistory = new();

    public StatefulMockVisionService(StateFixture fixture)
    {
        _fixture = fixture;
        _currentPageId = fixture.InitialPage;
    }

    // ── IVisionProvider 实现 ──────────────────────────

    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page == null)
            return Task.FromResult<PageAnalysis?>(null);
        return Task.FromResult<PageAnalysis?>(BuildPageAnalysis(page));
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
    {
        // 仿真总是返回屏幕中心
        return Task.FromResult<AppEntryPoint?>(new AppEntryPoint(0.5, 0.5));
    }

    // ── 仿真专用方法（StatefulMockActionExecutor 调用）──

    /// <summary>模拟用户操作 → 查找匹配 Transition → 切换页面</summary>
    public bool SimulateAction(string elementId, string action)
    {
        var target = _fixture.ResolveTarget(_currentPageId, elementId, action);
        if (target == null) return false;
        _navigationHistory.Push(_currentPageId);
        _currentPageId = target;
        return true;
    }

    /// <summary>模拟返回键 → 弹出导航历史</summary>
    public bool NavigateBack()
    {
        if (_navigationHistory.Count == 0) return false;
        _currentPageId = _navigationHistory.Pop();
        return true;
    }

    /// <summary>在当前页面上查找坐标 (x,y) 最近的元素（容差 0.05）</summary>
    public PageElement? FindElementAt(double x, double y)
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page == null) return null;
        return page.Elements.FirstOrDefault(e =>
            Math.Abs(e.X - x) < 0.05 && Math.Abs(e.Y - y) < 0.05);
    }

    /// <summary>重置到初始页面</summary>
    public void Reset()
    {
        _currentPageId = _fixture.InitialPage;
        _navigationHistory.Clear();
    }

    /// <summary>当前页面 ID（测试断言用）</summary>
    public string CurrentPageId => _currentPageId;

    /// <summary>导航历史深度（测试断言用）</summary>
    public int NavigationDepth => _navigationHistory.Count;
}
```

### 5.3 BuildPageAnalysis 映射

```csharp
private static PageAnalysis BuildPageAnalysis(PageState page)
{
    var tabs = page.Elements.Where(e => e.Type == "tab").ToImmutableArray();
    var items = page.Elements.Where(e => e.Type != "tab").ToImmutableArray();
    var backButton = items.FirstOrDefault(e => e.Type == "back_button");
    var contentItems = items.Where(e => e.Type != "back_button");

    return new PageAnalysis(
        Level1Dir: Direction.Top,
        Level2Dir: Direction.Left,
        Level1Menus: tabs.Select(MapToMenuInfo).ToImmutableArray(),
        Level2Menus: ImmutableArray<MenuInfo>.Empty,
        CurrentPath: ImmutableArray.Create(page.PageName),
        Items: contentItems.Select(MapToMenuItem).ToImmutableArray(),
        IsPopup: false,
        BackButton: backButton != null
            ? new Coordinate(backButton.X, backButton.Y) : null,
        IsEndOfList: page.IsComplete
    );
}
```

### 5.4 Element Type → MenuItem 映射表

| element.type | MenuItemType | ExpectedAction | ExpectsPageChange | ExpectsStateChange |
|-------------|-------------|----------------|-------------------|-------------------|
| `button` | Button | Navigate | true | false |
| `switch` | Switch | Toggle | false | true |
| `toggle` | Toggle | Toggle | false | true |
| `back_button` | Back | Navigate | true | false |
| `icon` | Icon | Action | true | false |
| `input` | Input | Action | false | false |
| `readonly` / `text` | Readonly | None | false | false |
| `tab` | → `MenuInfo`（非 MenuItem） | — | — | — |

映射逻辑对齐 Python `StatefulMockVisionService._build_page_analysis()` 和 `MenuItemTypeMapper`。

---

## 6. StatefulMockActionExecutor

### 6.1 职责

实现 `IActionExecutor`（8 方法），联动 `StatefulMockVisionService` 模拟页面跳转。对齐 Python `StatefulMockActionExecutor`。

### 6.2 实现

```csharp
namespace UniClaw.Core.Simulation;

public sealed class StatefulMockActionExecutor : IActionExecutor
{
    private readonly StatefulMockVisionService _vision;
    private readonly List<ActionRecord> _history = new();

    public StatefulMockActionExecutor(StatefulMockVisionService vision)
    {
        _vision = vision;
    }

    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
    {
        var element = _vision.FindElementAt(x, y);
        if (element != null)
            _vision.SimulateAction(element.Id, "click");

        _history.Add(new ActionRecord("tap", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["element_id"] = element?.Id ?? "none" },
            element != null));
        return Task.FromResult(element != null);
    }

    public Task<bool> PressBackAsync(CancellationToken ct = default)
    {
        var ok = _vision.NavigateBack();
        _history.Add(new ActionRecord("back", DateTimeOffset.UtcNow, new(), ok));
        return Task.FromResult(ok);
    }

    public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey,
        int durationMs, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("swipe", DateTimeOffset.UtcNow,
            new() { ["sx"] = sx, ["sy"] = sy, ["ex"] = ex, ["ey"] = ey, ["duration_ms"] = durationMs },
            true));
        return Task.FromResult(true);
    }

    public Task<bool> InputTextAsync(string text, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("input_text", DateTimeOffset.UtcNow,
            new() { ["text"] = text }, true));
        return Task.FromResult(true);
    }

    public Task<bool> LongPressAsync(double x, double y, int durationMs,
        CancellationToken ct = default)
    {
        var element = _vision.FindElementAt(x, y);
        _history.Add(new ActionRecord("long_press", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["duration_ms"] = durationMs, ["element_id"] = element?.Id ?? "none" },
            element != null));
        return Task.FromResult(element != null);
    }

    public Task WaitAsync(int milliseconds, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("wait", DateTimeOffset.UtcNow,
            new() { ["duration_ms"] = milliseconds }, true));
        return Task.CompletedTask;
    }

    public List<ActionRecord> GetHistory() => _history;
}
```

### 6.3 各方法行为

| 方法 | Vision 联动 | 返回值逻辑 |
|------|------------|-----------|
| `TapAsync(x,y)` | `FindElementAt` → `SimulateAction` | 找到元素 → true；未找到 → false |
| `PressBackAsync()` | `NavigateBack` | 栈非空 → true；空栈 → false |
| `SwipeAsync(...)` | 无（Scroll 留给 Phase 后续） | 始终 true |
| `InputTextAsync(...)` | 无 | 始终 true |
| `LongPressAsync(x,y)` | `FindElementAt`（仅记录，不触发跳转） | 找到元素 → true；未找到 → false |
| `WaitAsync(...)` | 无 | — |

### 6.4 与 MockActionExecutor（2.3a）的区别

| | MockActionExecutor | StatefulMockActionExecutor |
|---|---|---|
| 命名空间 | `UniClaw.Core.Tests.StateMachine` | `UniClaw.Core.Simulation` |
| 行为 | 返回预设 `NextResult` | 根据 fixture 数据实际判断 |
| 坐标匹配 | 无 | `FindElementAt(x, y)` 容差 0.05 |
| 页面跳转 | 无 | `vision.SimulateAction` → `_currentPageId` 切换 |
| 返回键 | 无 | `vision.NavigateBack` → 导航栈弹栈 |
| 用途 | 单元测试 HandleExecute | 端到端仿真 |

两者共存，不冲突。2.3a 的 `MockActionExecutor` 保留在 test 项目中作为轻量 stub。

---

## 7. StepOrchestrator 集成

### 7.1 需要的一行修改

```csharp
// StepOrchestrator.cs 第 41 行 — 当前
nextState = ctx.StateMachine.Step();

// 修改为
nextState = ctx.StateMachine.Step(ctx);
```

**原因**: `Step()` 无参 → `Step(null)` → `_currentStepContext` 为 null → handlers 进入 stub 模式。`Step(ctx)` 传递 StepContext → handlers 可访问 `_currentStepContext.Vision/Action`。

### 7.2 仿真所需的 StepContext 组装

```csharp
// 构建可用于仿真的 StepContext
var ctx = new TraversalRuntimeContext("sim-001");
var fsm = new TraversalFSM(ctx);
var vision = new StatefulMockVisionService(fixture);
var action = new StatefulMockActionExecutor(vision);
var nodeRegistry = new SimpleNodeRegistry();
// 注册所有参与遍历的节点
nodeRegistry.Register(rootNode);
nodeRegistry.Register(childNode1);
// ...

var stepCtx = new StepContext(
    Context: ctx,
    StateMachine: fsm,
    Vision: vision,
    Action: action,
    ChildMgr: new DynamicChildManager(),
    NodeRegistry: nodeRegistry,
    Trace: new TraceCoordinator(active: false),    // no-op
    SnapshotMgr: new PageSnapshotManager(),
    Stack: new NodeStackAdapter(ctx.NodeStack, nodeRegistry)
);
```

### 7.3 SimpleNodeRegistry

```csharp
/// <summary>测试用 INodeRegistry — 字典存储 TraversalNode。</summary>
public sealed class SimpleNodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, TraversalNode> _nodes = new();

    public TraversalNode? GetNode(string nodeId)
        => _nodes.TryGetValue(nodeId, out var n) ? n : null;

    public void Register(TraversalNode node)
        => _nodes[node.NodeId] = node;
}
```

### 7.4 依赖可用性

| StepContext 字段 | 仿真用 | 说明 |
|-----------------|--------|------|
| Context | `TraversalRuntimeContext`（真实） | 30 可变状态，生产代码 |
| StateMachine | `TraversalFSM`（真实） | 8 handler，生产代码 |
| Vision | `StatefulMockVisionService` | mock |
| Action | `StatefulMockActionExecutor` | mock |
| ChildMgr | `DynamicChildManager`（真实） | 生产代码 |
| NodeRegistry | `SimpleNodeRegistry` | 测试用 |
| Trace | `TraceCoordinator(active:false)` | no-op |
| SnapshotMgr | `PageSnapshotManager`（真实） | 无快照操作时为空操作 |
| Stack | `NodeStackAdapter`（真实） | 生产代码 |

---

## 8. 端到端测试策略

### 8.1 三层测试金字塔

```
        ┌─────────────┐
        │  E2E 遍历   │  ← StepOrchestrator + StatefulMock* + JSON fixture
        │  2-4 场景   │
        ├─────────────┤
        │  联动测试   │  ← StatefulMockVision + StatefulMockAction 交互
        │  3-5 场景   │
        ├─────────────┤
        │  单元测试   │  ← StateFixture 反序列化 / Builder / 页面跳转逻辑
        │  8-10 场景  │
        └─────────────┘
```

### 8.2 核心 E2E 场景

**场景 A: 2 页面线性遍历**
```
home → click Settings → settings → click Back → home
验证: 2 次 action 记录、2 个页面被访问、最终在 home
```

**场景 B: 静态子节点遍历**
```
root(home) → children[btn_settings, btn_profile]
验证: HandleBranch STATIC → 逐个选择 → FrameComplete
```

**场景 C: 操作失败 → ErrorHandling**
```
home → click 空白区域 → FindElementAt 返回 null → TapAsync 返回 false
验证: HandleExecute → ResultVerify（失败不抛异常）
```

**场景 D: 返回键 → 页面回退**
```
home → settings → PressBack → home
验证: NavigateBack 弹栈、vision.CurrentPageId = "home"
```

### 8.3 测试不会覆盖的

- **BehaviorValidator / ProblemDetector**: Python 各 ~550 行，留给后续 Phase
- **Scroll 仿真**: `ScrollableMockVisionService` / `ScrollDataStore`，留给后续 Phase
- **多级嵌套菜单**: 需要更复杂的 fixture（3+ 页面 + 多级 transition），可以作为后续测试场景补充
- **DYNAMIC_MATCH 子节点**: 需要 `DynamicChildManager` 的实际动态发现逻辑，超出 fixture 范围

---

## 9. 迁移文件清单

### 9.1 新增文件（生产代码）

| # | 文件 | 说明 | 估算行数 |
|---|------|------|---------|
| 1 | `src/UniClaw.Core/Simulation/StateFixture.cs` | 4 record class + JSON 反序列化 + 运行时索引 | ~200 |
| 2 | `src/UniClaw.Core/Simulation/StateFixtureBuilder.cs` | Fluent Builder（Page/Element/Transition 构建器） | ~150 |
| 3 | `src/UniClaw.Core/Simulation/StatefulMockVisionService.cs` | `: IVisionProvider` + `BuildPageAnalysis` 映射 | ~250 |
| 4 | `src/UniClaw.Core/Simulation/StatefulMockActionExecutor.cs` | `: IActionExecutor` + 联动 vision | ~200 |

### 9.2 修改文件（生产代码）

| # | 文件 | 变更 | 影响 |
|---|------|------|------|
| 5 | `src/UniClaw.Core/StateMachine/StepContext.cs` | IVisionProvider 补全 + AppEntryPoint | 接口签名变更 |
| 6 | `src/UniClaw.Core/Traversal/StepOrchestrator.cs` | 第 41 行: `Step()` → `Step(ctx)` | 一行修改 |

### 9.3 新增文件（测试代码）

| # | 文件 | 说明 | 估算行数 |
|---|------|------|---------|
| 7 | `tests/.../Simulation/StateFixtureTests.cs` | JSON 反序列化、Builder、索引查询 | ~120 |
| 8 | `tests/.../Simulation/StatefulMockVisionTests.cs` | 页面跳转、坐标查找、导航回退 | ~130 |
| 9 | `tests/.../Simulation/StatefulMockActionTests.cs` | Tap 联动、Back 联动、记录验证 | ~100 |
| 10 | `tests/.../Simulation/SimulationE2ETests.cs` | E2E 遍历 via StepOrchestrator | ~150 |
| 11 | `tests/.../Fixtures/two-page-app.json` | 2 页面测试 fixture | ~40 |

### 9.4 新增文件（辅助）

| # | 文件 | 说明 |
|---|------|------|
| 12 | `src/UniClaw.Core/Simulation/SimpleNodeRegistry.cs` | 测试用 INodeRegistry（~15 行） |

**总计**: ~1,360 行（800 生产 + 560 测试），零新 NuGet 依赖。

---

## 10. 与后续 Phase 的关系

```
Phase 2.3a ✅     HandleExecute + HandleBranch（已完成）
Phase 2.3-sim     仿真基础设施（本文）
     ↓
Phase 2.3c        HandleErrorHandling + HandlePopupHandling
                   └── 子组件就绪（ErrorHandler + PopupHandler），仿真平台可验证
     ↓
Phase 2.3b        HandleResultVerify + HandlePreconditionCheck
                   └── 仿真提供 IVisionProvider，可端到端验证视觉相关逻辑
```

仿真先行让 2.3b/2.3c 完成后立即可以端到端验证，不需要等待真实 AI/ADB 实现。

---

## 11. 风险与约束

| 风险 | 缓解 |
|------|------|
| `BuildPageAnalysis` 映射可能与 Python 有偏差 | 参考 Python `_build_page_analysis` 源码逐字段对齐，测试覆盖 |
| `ImmutableDictionary` JSON 反序列化需要 DTO 中间层 | 内部 DTO 不对外暴露，维护成本低 |
| `StepOrchestrator.Step()` → `Step(ctx)` 可能影响现有测试 | 无参 `Step()` 等同于 `Step(null)`，handler 有 null-check stub fallback |
| `DynamicChildManager` 可能依赖未初始化的内部状态 | 仿真只测 STATIC 子节点路径，DYNAMIC_MATCH 留给后续 |
| fixture JSON 手写容易出错 | 提供 `StateFixtureBuilder` 代码驱动模式；JSON 用于文件驱动的集成测试 |
