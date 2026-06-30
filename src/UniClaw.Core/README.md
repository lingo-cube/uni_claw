# UniClaw.Core - C# Core Definitions

> **Phase 1**: 核心业务定义从 Python 迁移到 C#
> **Version**: 0.1.0
> **.NET**: 8.0
> **Branch**: `refactor/csharp-core-models`

---

## 项目概述

UniClaw.Core 是 Uni-Claw 移动UI自动化框架的核心定义层，采用渐进式迁移策略从 Python 移植到 C#。

### 设计原则

| 原则 | 说明 |
|------|------|
| **接口优先** | 所有核心能力定义为接口，无具体实现 |
| **类型安全** | 使用 C# 强类型系统和 record 类型 |
| **不可变性** | 使用 `readonly record` 和 `sealed record` 确保不可变 |
| **依赖注入** | 通过构造函数注入依赖 |

---

## 项目结构

```
src/csharp/UniClaw.Core/
├── Domain/
│   └── Models/
│       ├── Vision/           # 视觉模型
│       │   ├── BoundingBox.cs
│       │   ├── TypeHint.cs
│       │   ├── SelectionState.cs
│       │   ├── Region.cs
│       │   ├── FlattenedElement.cs
│       │   ├── ScreenHints.cs
│       │   └── FlattenedScreen.cs
│       └── Common/           # 通用模型
│           ├── Operation.cs
│           ├── Target.cs
│           └── RestoreAction.cs
├── StateMachine/             # 状态机
│   ├── GlobalState.cs
│   ├── TraversalState.cs
│   └── NodeStack.cs
├── Graph/
│   └── Models/               # 图模型
│       ├── TraversalNode.cs
│       ├── TraversalPlan.cs
│       └── Template.cs
├── Traversal/                # 遍历接口
│   └── IGraphTraversalEngine.cs
├── AI/                       # AI接口
│   └── IAIStrategyAdvisor.cs
└── Observability/            # 可观测性接口
    └── ITraceRecorder.cs
```

---

## 核心定义

### Vision Models (视觉模型)

```csharp
// 归一化边界框
public readonly record struct BoundingBox(double X, double Y, double Width, double Height);

// 视觉元素类型
public enum TypeHint { ClickableText, Switch, Slider, Button, Icon, InputField, Text, Image, Unknown }

// 扁平化元素
public sealed record class FlattenedElement(int Id, string Text, TypeHint TypeHint, BoundingBox BoundingBox, ...);

// 完整屏幕
public sealed record class FlattenedScreen(List<FlattenedElement> Elements, ScreenHints? ScreenHints = null);
```

### State Machine (状态机)

```csharp
// 全局状态
public enum GlobalState { Idle, Initializing, Traversing, Paused, Error, Recovering, Completed, Terminated }

// 遍历状态
public enum TraversalState { NodeSelect, PreconditionCheck, Execute, ResultVerify, Branch, FrameComplete, ErrorHandling, PopupHandling, DynamicMatch }

// 节点栈接口
public interface INodeStack { int Depth { get; } bool Push(ITraversalNode node, List<string>? children = null); IStackFrame? Pop(); ... }
```

### Graph Models (图模型)

```csharp
// 节点类型
public enum NodeType { Container, LeafSwitch, LeafSlider, LeafAction, LeafInfo, Screen, Action, Target }

// 遍历节点
public sealed record class TraversalNode(string NodeId, string Name, NodeType NodeType, Operation Operation, ChildrenStrategy ChildrenStrategy, ...);

// 遍历计划
public sealed record class TraversalPlan(string EntryApp, EntryPolicy EntryPolicy, TraversalNode? RootNode = null, ...);

// 模板系统
public sealed record class Template(string TemplateId, NodeType NodeType, Dictionary<string, object> Operation, ...);
```

### AI Interface (AI接口)

```csharp
public interface IAIStrategyAdvisor
{
    Task<ContainerInference> InferContainerTypeAsync(PageAnalysis pageAnalysis, ITraversalContext context, ...);
    Task<(DecisionResult Result, NodeData? NodeData)> DecideNextActionAsync(string goal, PageAnalysis pageAnalysis, ...);
    Task<(DecisionResult Result, NodeData? NodeData)> HandleExceptionAsync(Exception exception, ...);
}
```

---

## Python → C# 映射

| Python | C# | 说明 |
|--------|-----|------|
| `@dataclass` | `sealed record class` | 引用类型 |
| `@dataclass(frozen=True)` | `readonly record struct` | 值类型 |
| `str, Enum` | `enum` | 枚举类型 |
| `Protocol` | `interface` | 接口定义 |
| `List[T]` | `List<T>` | 泛型列表 |
| `Dict[K, V]` | `Dictionary<K, V>` | 泛型字典 |
| `Optional[T]` | `T?` | 可空类型 |
| `Tuple[T, U]` | `(T, U)` | 元组语法 |

---

## 使用示例

### 创建简单的遍历计划

```csharp
using UniClaw.Core.Graph.Models;

var plan = new TraversalPlan(
    EntryApp: "com.example.app",
    EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch),
    RootNode: new TraversalNode(
        NodeId: "root",
        Name: "Root Menu",
        NodeType: NodeType.Container,
        Operation: new Operation(OperationType.NoAction),
        ChildrenStrategy: new ChildrenStrategy(
            Type: ChildrenStrategyType.Static,
            StaticChildren: new List<string> { "settings", "profile" }
        )
    )
);
```

### 使用视觉模型

```csharp
using UniClaw.Core.Domain.Models.Vision;

var bbox = new BoundingBox(X: 0.1, Y: 0.2, Width: 0.3, Height: 0.05);
var center = bbox.Center(); // (0.25, 0.225)

var element = new FlattenedElement(
    Id: 1,
    Text: "Settings",
    TypeHint: TypeHint.ClickableText,
    BoundingBox: bbox
);

var isInteractive = element.IsInteractive; // true
```

### 使用状态机

```csharp
using UniClaw.Core.StateMachine;

var stack = new NodeStack();
var node = new TraversalNode(...);

stack.Push(node);
var depth = stack.Depth; // 1
var peeked = stack.Peek(); // StackFrame

var popped = stack.Pop();
```

---

## 后续阶段

### Phase 2: 核心实现
- [ ] GlobalStateMachine 实现
- [ ] TraversalStateMachine 实现
- [ ] TemplateRegistry 实现
- [ ] 基础 GraphTraversalEngine 实现

### Phase 3: 集成
- [ ] Python AI 模块进程桥接
- [ ] Vision Service 集成
- [ ] ADB 集成

### Phase 4: UI
- [ ] 控制台应用
- [ ] WPF 可视化

---

## 构建和测试

```bash
# 构建项目
dotnet build src/csharp/UniClaw.Core/UniClaw.Core.csproj

# 运行测试（待添加）
dotnet test tests/csharp/UniClaw.Core.Tests/

# 生成文档
dotnet build /t:GenerateDocumentation
```

---

## 许可证

与 Uni-Claw 主项目相同

---

**创建日期**: 2025-06-25
**维护者**: UniClaw Development Team
