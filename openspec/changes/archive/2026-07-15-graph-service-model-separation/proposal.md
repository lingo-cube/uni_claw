## Why

Graph 层当前 9 个文件全部平铺在 `Graph/Models/` 单一 namespace 下，服务类（`DynamicMatcher`、`PlanCompiler`、`TemplateInstantiator`）与数据模型（`TraversalNode`、`TraversalPlan` 等）混杂。`TraversalEngine` 通过 `new DynamicMatcher()` / `new TemplateInstantiator()` 硬编码具体类型，无法 mock 测试。`Template.cs` 更是一个文件塞了 4 个类型（record + interface + 2 个 static class）。路线图 P3 阶段，D-V 已完成 Traversal 层接口提取，D-28 是 Graph 层的对应操作——纯机械分离，零行为变更。

## What Changes

- **创建 `Graph/Abstractions/` 目录**：4 个接口 — `IDynamicMatcher`、`IPlanCompiler`、`ITemplateInstantiator`（3 个新建）+ `ITemplateRegistry`（从 `Template.cs` 搬出）
- **创建 `Graph/Services/` 目录**：5 个实现 — `DynamicMatcher`、`PlanCompiler`、`TemplateInstantiator`（从 `Models/` 搬入，namespace 改为 `.Graph.Services`）+ `PlaceholderResolver`、`TemplateValidator`（从 `Template.cs` 拆出）
- **模型类型分离**：`MatchableItem`、`MatchResult` 从 `DynamicMatcher.cs` 拆出到 `Models/` 独立文件（接口参数类型，应位于接口可达的 Models 层）
- **拆分 `Template.cs`**：仅保留 `Template` record，其余 3 类型分别搬入 `Abstractions/` 和 `Services/`
- **`TraversalEngine` 接口注入**：`_matcher` 类型从 `DynamicMatcher` → `IDynamicMatcher`，`_instantiator` 类型从 `TemplateInstantiator` → `ITemplateInstantiator`（默认实现仍为 `new()`，对外构造器不变）
- **BREAKING — 无**：外部消费者 15 个文件只使用 `Graph.Models` 中的模型类型，零改动；`ITemplateRegistry` namespace 变更为 `.Graph.Abstractions`，已确认零外部引用

## Capabilities

### New Capabilities
_(无 — 纯架构重构，不新增功能)_

### Modified Capabilities
- `graph-foundation`: Graph 层从单一 `Models/` 目录扩展为三目录架构（`Models/` + `Abstractions/` + `Services/`）；新增 3 个服务接口（`IDynamicMatcher`、`IPlanCompiler`、`ITemplateInstantiator`）；`ITemplateRegistry` namespace 从 `.Graph.Models` → `.Graph.Abstractions`；`TraversalEngine` 依赖服务接口而非具体类型

## Impact

- **新建文件**: `Abstractions/` (4 interface) + `Services/` (5 class) + `Models/` (2 record) = **11 文件**
- **修改文件**: `Models/Template.cs`（删 3 类型）、`TraversalEngine.cs`（2 字段类型改接口）、`GraphTests.cs`（加 using）= **3 文件**
- **依赖**: 无新增外部依赖；Abstractions → Models + Domain（单向），Services → Abstractions + Models + Domain（单向），Models → Domain（不变）
- **风险**: 纯机械操作，每步 `dotnet build` 验证；665 测试回归护栏；新增 1 个 guard test (`GraphAbstractions_Has4Interfaces`)
- **详细设计**: 见 `docs/refactor/2026-07-15-graph-service-model-separation-design.md`
