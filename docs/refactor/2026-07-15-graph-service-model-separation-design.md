# Design: Graph 层服务/模型分离 (D-28)

> **创建时间**: 2026-07-15
> **状态**: 设计阶段
> **路线图**: P3, 无前置依赖
> **原则**: 高内聚低耦合 — 服务与模型 namespace 分离, 接口提取, 零行为变更

## 1. 现状

9 个文件全部平铺在 `Graph/Models/`，namespace 统一为 `UniClaw.Core.Graph.Models`。三类代码混杂：

```
src/UniClaw.Core/Graph/Models/   ← 9 文件, 单一 namespace
├── 纯模型 (5): EntryConfig, NodeData, TraversalNode, TraversalPlan, Template
├── 服务类 (3): DynamicMatcher, PlanCompiler, TemplateInstantiator
├── 静态工具 (2): PlaceholderResolver, TemplateValidator (均嵌在 Template.cs)
└── 接口 (2):   ITraversalNode + IStackFrame, ITemplateRegistry
```

### 问题

1. **服务与模型同 namespace**: `DynamicMatcher` 和 `TraversalNode` 都在 `.Graph.Models`，外部消费者无法区分"数据"和"逻辑"
2. **服务类无接口**: `TraversalEngine` 直接 `new DynamicMatcher()` / `new TemplateInstantiator()`，硬编码具体类型
3. **Template.cs 4 类型共栖**: `Template` record + `ITemplateRegistry` interface + `PlaceholderResolver` + `TemplateValidator` 挤在一个文件
4. **缺少 Services/ Abstractions/ 目录**: 虽然 `graph.md` 已经规划了这两个目录，但未创建

### 外部引用

所有 15 个跨层消费者只 `using UniClaw.Core.Graph.Models`，没有引用任何 service class：

| 层 | 消费者数 | 使用的类型 |
|----|---------|-----------|
| Traversal/ | 4 | TraversalPlan, TraversalNode, ChildrenStrategy, MatchCondition 等模型 |
| StateMachine/ | 11 | ITraversalNode, IStackFrame, ChildrenStrategyType 等模型 |

唯一直接实例化服务类的地方：`TraversalEngine.cs:439-440`:
```csharp
private readonly DynamicMatcher _matcher = new();
private readonly TemplateInstantiator _instantiator = new();
```

## 2. 设计目标

### Goals

- 创建 `Graph/Abstractions/` + `Graph/Services/` 目录，完成三目录结构
- 为 3 个服务类提取接口 → `Abstractions/`
- 拆分 `Template.cs` → 4 独立文件（按类型分离）
- `TraversalEngine` 构造器改用接口注入
- 零行为变更 — 纯机械操作

### Non-Goals

- 不改服务类内部逻辑
- 不改模型类型定义
- 不改外部消费者（15 个文件无需变更）
- 不新增 Graph.Tests/

### 内聚原则

| 目录 | 职责 | 依赖方向 |
|------|------|---------|
| `Models/` | 纯数据模型: record, enum, 纯接口 (`ITraversalNode`). | → Domain only |
| `Abstractions/` | 服务接口: 定义契约，参数类型引用 Models/ 和 Domain. | → Models, Domain |
| `Services/` | 服务实现: 实现 Abstractions/ 接口，消费 Models/ 数据. | → Abstractions, Models, Domain |

**耦合约束**: Models MUST NOT 引用 Abstractions 或 Services（DAG 底部）。

### 已验证的关键事实

- `TraversalPlan.TemplateRegistry` 是 `string?`（文件路径），**不是** `ITemplateRegistry` → `ITemplateRegistry` 搬入 Abstractions/ 无 Models→Abstractions 风险
- `PlanCompiler` 不在任何生产代码中实例化（仅 `GraphTests.cs` 引用）
- 所有外部消费者只使用 Models/ 中的类型 → 本次变更对它们透明

## 3. 目标结构

```
src/UniClaw.Core/Graph/
├── Abstractions/                    ← NEW
│   ├── IDynamicMatcher.cs           ← 提接口: Match + MatchAll
│   ├── IPlanCompiler.cs             ← 提接口: Compile
│   ├── ITemplateInstantiator.cs     ← 提接口: Instantiate
│   └── ITemplateRegistry.cs         ← 从 Template.cs 搬出
├── Models/                          ← 保留纯模型
│   ├── EntryConfig.cs               (不变)
│   ├── ITraversalNode.cs            (不变, 含 IStackFrame)
│   ├── NodeData.cs                  (不变)
│   ├── Template.cs                  ← 只保留 Template record
│   ├── TraversalNode.cs             (不变)
│   └── TraversalPlan.cs             (不变)
└── Services/                        ← NEW
    ├── DynamicMatcher.cs            ← 从 Models/ 搬入, namespace → .Graph.Services
    ├── PlanCompiler.cs              ← 从 Models/ 搬入
    ├── TemplateInstantiator.cs      ← 从 Models/ 搬入
    ├── PlaceholderResolver.cs       ← 从 Template.cs 拆出
    └── TemplateValidator.cs         ← 从 Template.cs 拆出
```

### 接口定义

```csharp
// Abstractions/IDynamicMatcher.cs
namespace UniClaw.Core.Graph.Abstractions;
public interface IDynamicMatcher
{
    MatchResult Match(MatchCondition condition, MatchableItem item);
    List<MatchResult> MatchAll(List<DynamicRule> rules, List<MatchableItem> items);
}

// Abstractions/IPlanCompiler.cs
namespace UniClaw.Core.Graph.Abstractions;
public interface IPlanCompiler
{
    TraversalPlan Compile(IntentSlots slots);
}

// Abstractions/ITemplateInstantiator.cs
namespace UniClaw.Core.Graph.Abstractions;
public interface ITemplateInstantiator
{
    TraversalNode Instantiate(Template template, Dictionary<string, object> context, List<string> parentPath);
}

// Abstractions/ITemplateRegistry.cs (从 Template.cs 搬出, namespace 改为 .Graph.Abstractions)
namespace UniClaw.Core.Graph.Abstractions;
public interface ITemplateRegistry
{
    Template? GetTemplate(string templateId);
    bool HasTemplate(string templateId);
    List<string> GetTemplateIds();
    Task LoadFromFileAsync(string path);
    TraversalNode Instantiate(Template template, Dictionary<string, object> context, List<string> parentPath);
}
```

### MatchableItem / MatchResult 处理

这两个 record 是 `DynamicMatcher` 的 I/O 类型，被接口 `IDynamicMatcher` 引用。
当前与 `DynamicMatcher` class 同文件。**拆分到 Models/ 独立文件**：

- `Models/MatchableItem.cs` — `MatchableItem` record（引用 `Domain.Models.Content`）
- `Models/MatchResult.cs` — `MatchResult` record（引用 `MatchAction` enum，已在 `TraversalNode.cs` 中）
- `Services/DynamicMatcher.cs` — `DynamicMatcher` class + private helpers

**理由**: `IDynamicMatcher` 接口在 Abstractions/ 中引用 `MatchableItem` 和 `MatchResult` 类型，两者必须放在 Abstractions 可达的位置（Models/）。这是数据模型，不是服务逻辑。

## 4. 改动清单

| # | 操作 | 文件 | 类型 |
|---|------|------|------|
| 1 | 新建目录 | `Graph/Abstractions/`, `Graph/Services/` | 基础设施 |
| 2 | 拆 MatchableItem + MatchResult | 从 `DynamicMatcher.cs` → `Models/MatchableItem.cs` + `Models/MatchResult.cs` | 模型分离 |
| 3 | 提 3 个新接口 | `IDynamicMatcher.cs`, `IPlanCompiler.cs`, `ITemplateInstantiator.cs` → `Abstractions/` | 接口提取 |
| 4 | 搬 ITemplateRegistry | 从 `Template.cs` → `Abstractions/ITemplateRegistry.cs`，namespace `.Graph.Models` → `.Graph.Abstractions` | 接口分离 |
| 5 | 拆 Template.cs | `PlaceholderResolver` → `Services/PlaceholderResolver.cs`; `TemplateValidator` → `Services/TemplateValidator.cs`; `Template` record 留在 `Models/Template.cs` | 类型分离 |
| 6 | 搬 3 个服务类 | `DynamicMatcher`, `PlanCompiler`, `TemplateInstantiator` → `Services/`，namespace `.Graph.Models` → `.Graph.Services` | 服务分离 |
| 7 | 服务类 implement 接口 | `DynamicMatcher : IDynamicMatcher`, `PlanCompiler : IPlanCompiler`, `TemplateInstantiator : ITemplateInstantiator` | 接口实现 |
| 8 | 改 TraversalEngine | `_matcher: IDynamicMatcher`, `_instantiator: ITemplateInstantiator`（构造器注入，默认 `new DynamicMatcher()` / `new TemplateInstantiator()`） | DI 兼容 |
| 9 | 加 using | 所有搬入 Services/ 的文件加 `using UniClaw.Core.Graph.Models` + `using UniClaw.Core.Graph.Abstractions` | 引用修复 |
| 10 | Guard test | `GraphAbstractions_Has4Interfaces` (CI-blocking) | 架构约束 |

### 不改的文件（透明变更）

- `Models/`: `EntryConfig.cs`, `ITraversalNode.cs`, `NodeData.cs`, `TraversalNode.cs`, `TraversalPlan.cs` — 内容不变
- `Traversal/` (4 文件): `IGraphTraversalEngine.cs`, `StepOrchestrator.cs`, `DictionaryNodeRegistry.cs`, `TraversalEngine.cs` (仅构造器部分改)
- `StateMachine/` (11 文件): 零改动
- `tests/Graph/GraphTests.cs`: 仅加 `using UniClaw.Core.Graph.Services` / `Abstractions`

## 5. TraversalEngine 改动

```csharp
// BEFORE (当前)
private readonly DynamicMatcher _matcher = new();
private readonly TemplateInstantiator _instantiator = new();

// AFTER (D-28)
private readonly IDynamicMatcher _matcher;
private readonly ITemplateInstantiator _instantiator;

// 构造器: 默认实现 = 向后兼容
public TraversalEngine(...)
{
    _matcher = new DynamicMatcher();       // or accept IDynamicMatcher? parameter
    _instantiator = new TemplateInstantiator();
    ...
}
```

不需要对外暴露 DI 参数（TraversalEngine 构造器已有很多参数）。保持 `new()` 作为默认实现，但字段类型改为接口。当需要 mock 测试时可通过可选参数注入。

## 6. 风险

| 风险 | 缓解 |
|------|------|
| namespace 变更导致全量编译错误 | 按文件逐一搬移 + build 验证, 每步编译通过 |
| `ITemplateRegistry` namespace 变更影响外部 | 已确认: 无外部引用（仅定义在 Template.cs） |
| `MatchableItem`/`MatchResult` 搬移影响 DynamicMatcher 使用者 | 已确认: 仅在 GraphTests 中使用 |
| `PlaceholderResolver`/`TemplateValidator` 搬移影响外部 | 已确认: 仅 PlanCompiler 使用, 同在 Services/ 内 |
| Guard test 数量增长 | 加 1 个 guard（GraphAbstractions_Has4Interfaces） |

## 7. 验证方式

- `dotnet build` 每步 0 错误
- `dotnet test` 665 全绿（回归护栏）
- 新 guard: `GraphAbstractions_Has4Interfaces` — 锁定 Abstractions/ 4 接口数
- `openspec validate` (if available)
