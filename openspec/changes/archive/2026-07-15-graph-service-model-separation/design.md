## Context

Graph 层 9 个文件全在 `Models/` namespace，三类代码混杂：纯模型（`TraversalNode`, `TraversalPlan`, `EntryConfig`, `NodeData`, `Template`）、服务类（`DynamicMatcher`, `PlanCompiler`, `TemplateInstantiator`）、静态工具（`PlaceholderResolver`, `TemplateValidator`）。`TraversalEngine` 直接 `new DynamicMatcher()` / `new TemplateInstantiator()`。`Template.cs` 单文件含 4 类型。

D-V 已在 Traversal 层完成接口提取（6+ 接口），D-28 是 Graph 层的对应操作。完整设计见 `docs/refactor/2026-07-15-graph-service-model-separation-design.md`。

## Goals / Non-Goals

**Goals:**
- 创建 `Abstractions/` + `Services/` 目录，完成三目录结构
- 提取 3 个服务接口：`IDynamicMatcher`, `IPlanCompiler`, `ITemplateInstantiator`
- `ITemplateRegistry` 从 `Template.cs` → `Abstractions/`
- 拆分 `Template.cs` 4 类型 → 4 独立文件
- `TraversalEngine` 字段类型改为接口（默认实现仍为 `new()`）
- 零行为变更

**Non-Goals:**
- 不改服务类内部逻辑
- 不改模型类型定义
- 外部 15 个消费者零改动
- 不新增测试

## Decisions

### 1. 三目录内聚原则

| 目录 | 职责 | 依赖方向 |
|------|------|---------|
| `Models/` | 纯数据: record, enum, 纯接口 (`ITraversalNode`) | → Domain |
| `Abstractions/` | 服务接口: 参数类型引用 Models | → Models, Domain |
| `Services/` | 实现 Abstractions/ 接口, 消费 Models | → Abstractions, Models, Domain |

**耦合约束**: Models MUST NOT → Abstractions 或 Services。已验证 `TraversalPlan.TemplateRegistry` 为 `string?`（非 `ITemplateRegistry`），无违规。

### 2. MatchableItem / MatchResult → Models/

`IDynamicMatcher` 接口的签名引用 `MatchableItem` 和 `MatchResult` 类型。若留在 `Services/DynamicMatcher.cs`，则 Abstractions 引用 Services 私有类型（违规）。**拆出到 `Models/MatchableItem.cs` + `Models/MatchResult.cs`** 作为纯数据模型。

### 3. ITemplateRegistry → Abstractions/

原在 `Template.cs`（namespace `.Graph.Models`），无外部引用。因无 Models 类型依赖它，可安全搬入 Abstractions/ 不破坏内聚约束。

### 4. PlanCompiler 提接口（YAGNI 豁免）

`PlanCompiler` 当前仅在 `GraphTests.cs` 中使用。提取 `IPlanCompiler` 是为了与 D-V 保持一致：每个 Services/ 对应一个 Abstractions/ 接口。将来 TraversalEngine 接入时接口就绪。

### 5. TraversalEngine 默认实现策略

保持构造器不变，字段类型改为接口，默认 `new DynamicMatcher()` / `new TemplateInstantiator()`：
```csharp
private readonly IDynamicMatcher _matcher;
private readonly ITemplateInstantiator _instantiator;
```
不对外暴露可选 DI 参数（TraversalEngine 构造器已有很多参数）。Mock 测试可通过内部 setter 或派生类注入。

## Target Structure

```
src/UniClaw.Core/Graph/
├── Abstractions/                    ← NEW
│   ├── IDynamicMatcher.cs
│   ├── IPlanCompiler.cs
│   ├── ITemplateInstantiator.cs
│   └── ITemplateRegistry.cs
├── Models/
│   ├── EntryConfig.cs
│   ├── ITraversalNode.cs            (+ IStackFrame)
│   ├── MatchableItem.cs             ← 从 DynamicMatcher.cs 拆出
│   ├── MatchResult.cs               ← 从 DynamicMatcher.cs 拆出
│   ├── NodeData.cs
│   ├── Template.cs                  ← 只保留 Template record
│   ├── TraversalNode.cs
│   └── TraversalPlan.cs
└── Services/
    ├── DynamicMatcher.cs
    ├── PlaceholderResolver.cs
    ├── PlanCompiler.cs
    ├── TemplateInstantiator.cs
    └── TemplateValidator.cs
```

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| namespace 变更导致编译错误 | 逐文件搬移 + 每步 `dotnet build` |
| `ITemplateRegistry` namespace 变更影响外部 | 已确认零外部引用 |
| `MatchableItem`/`MatchResult` 拆出遗漏引用 | grep + build 验证 |
| Guard test 数量增长 | 加 1 个 guard: `GraphAbstractions_Has4Interfaces` |

## Migration Plan

单分支提交，每步 build 验证:
1. 创建目录 + 拆出 MatchableItem / MatchResult → Models/
2. 提取 4 接口 → Abstractions/，服务类 implement 接口
3. 拆分 Template.cs（ITemplateRegistry → Abstractions/，PlaceholderResolver + TemplateValidator → Services/）
4. 搬 3 服务类 → Services/（namespace 改为 `.Graph.Services`）
5. TraversalEngine 字段类型改接口
6. Guard test + `dotnet test` 全量回归
