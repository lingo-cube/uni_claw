# CLAUDE.md — UniClaw.Core 项目指南

> 本文件为 Claude Code（及其他 AI 编码助手）提供项目上下文。
> 最后更新: 2026-07-02

## 项目概览

UniClaw.Core 是一个 C# Domain 层项目，从 Python `uni_claw` 代码库迁移而来。
目标是构建一个类型安全、不可变、fail-fast 校验的 Domain 层，
为上层 Graph/Traversal/AI 层提供纯数据模型和映射基础设施。

- **框架**: .NET 8, C# 12
- **测试**: xUnit 2.6, 229 测试全绿
- **风格**: sealed record class + ImmutableArray + DomainValidationException fail-fast
- **序列化**: System.Text.Json, camelCase + enum-as-string (DomainJsonOptions)

## 构建与测试

```bash
# .NET SDK 不在 PATH，需要前置：
export PATH="/c/Users/vs-dr-zhangfan/AppData/Local/Microsoft/dotnet:$PATH"
export DOTNET_ROOT="C:\\Users\\vs-dr-zhangfan\\AppData\\Local\\Microsoft\\dotnet"

# 构建
dotnet build src/UniClaw.Core.sln

# 测试
dotnet test src/UniClaw.Core.sln

# 预期结果: 0 错误, 0 功能性警告, 229 测试通过
```

## 项目结构

```
src/UniClaw.Core/               ← 生产代码 (net8.0 classlib)
  Domain/                        ← Domain 层 (核心模型, Phase 1 完成)
    DomainValidationException.cs  ← 跨切面: 校验异常 (FieldName + IllegalValue)
    DomainJsonOptions.cs          ← 跨切面: JSON 序列化策略
    Models/
      Vision/                     ← 8 类型: BoundingBox, Region, RegionRole, TypeHint+Extensions,
                                   │         SelectionState+Extensions, FlattenedElement, FlattenedScreen, ScreenHints
      Content/                    ← 10 类型: Coordinate, Direction+Ext, MenuItemType+Ext,
                                   │         ExpectedAction+Ext, MenuInfo, MenuItem, PopupInfo,
                                   │         PageAnalysis, VisitFingerprint, ContentNode
      Common/                     ← 5 类型: OperationType, Operation, TargetType, Target, RestoreAction
    Mappings/                     ← 2 类型: ElementTypeMapper (核心桥), AndroidWidgetClass (孤立enum)
  AI/                             ← AI 层骨架 (接口定义, Phase 2+)
  Graph/                          ← Graph 层骨架 (TraversalNode, Template 等, Phase 2+)
  StateMachine/                   ← 状态机 (GlobalState, ITraversalContext, Phase 2+)
  Traversal/                      ← 遍历引擎接口 (Phase 2+)
  Observability/                  ← 可观测性接口 (Phase 2+)

tests/UniClaw.Core.Tests/         ← 测试 (net8.0 xunit)
  Domain/
    Vision/                       ← TypeHintTests, SelectionStateTests, BoundingBoxTests, RegionTests 等
    Content/                       ← ContentModelsTests
    Common/                        ← OperationTests, TargetAndRestoreActionTests
    Mappings/                      ← ElementTypeMapperTests
    DomainSerializationTests.cs
```

## 关键架构决策

### 两级映射分离 (P0 fix 已完成)

`ElementTypeMapper.MapAndroidClass` 返回 **中间字符串** (如 `"toggle"`)，不返回 TypeHint enum。
两套系统独立：
- **视觉外观**: TypeHint (8 值) — "看起来像什么"
- **行为语义**: 中间字符串 (14 值) → MenuItemType (11 值) / ExpectedAction (4 值) — "能做什么"

两者不重叠。TypeHint 没有 `"toggle"`/`"menu_item"`/`"input"`。
`ToTypeHint(string)` 是可选便利方法，不是核心链路。

### 两种 AI 分析模式

| 模式 | 链路 | Domain.Vision 参与? | ElementTypeMapper 参与? |
|------|------|---------------------|------------------------|
| A (直接) | 截图 → 多模态AI → PageAnalysis | ❌ 不参与 | ❌ 不参与 |
| B (两步) | 截图 → AI → FlattenedScreen → 规则/文本模型 → PageAnalysis | ✅ 核心链路第一步 | ✅ 规则引擎路径 |

Phase 2 需先决定走哪种模式再设计上层架构。

### 校验策略

- **构造期**: DomainValidationException fail-fast (Coordinate, BoundingBox, Operation 等)
- **解析期**: FromString/MapAndroidClass graceful 回落 + IsValid 通知上层 (TypeHint, SelectionState)

### 序列化

- camelCase 键名 (PRD §6: 本期不背 Python snake_case)
- enum-as-string (JsonStringEnumConverter)
- null 跳过 (WhenWritingNull)
- TypeHint 缺 `[JsonPropertyName]` — 其他 3 个 Domain enum 都有 (P3 待修)

## Domain 层完成状态

**核心 24 类型全部完成**。剩余 P3 级补齐：

| # | 项目 | 优先级 |
|---|------|--------|
| 1 | ContentNode.ToMarkdown() | P3 |
| 2 | Region.Id 非空校验 | P3 |
| 3 | TypeHint 加 [JsonPropertyName] | P3 |
| 4 | TypeHint Values 改为 IReadOnlyList\<string\> | P3 |
| 5 | 补 IsCanonical(string) 区分精确值 vs 别名 | P3 |

PRD 明确 defer 到 Phase 2: SimulationState, ContentTree

## 系统设计文档

详细架构分析在 `docs/system/` 目录下 (7 个角度)：

- `01-dependency-topology.md` — 依赖 DAG 图
- `02-data-flow-paths.md` — 数据流路径 (两种模式)
- `03-semantic-contracts.md` — 每个类型的职责声明
- `04-cross-domain-bridges.md` — 跨域桥分析
- `05-change-stability.md` — 变更稳定性评级
- `06-validation-boundaries.md` — 校验矩阵
- `07-serialization-contracts.md` — 序列化行为表

## Python 对齐参考

Python 源码在 `main` 分支 (当前分支 `feature/refactor` 不含 Python 代码):
- `src/models/vision/` — Vision 7 模型
- `src/models/content_models.py` — Content 12 类型
- `src/models/element_type_mapper.py` — ElementTypeMapper
- `src/models/traversal_context.py` — GlobalState + TraversalContext (Phase 2)

Python↔C# 全量对比: `docs/refactor/04-phase1-python-csharp-comparison.md`
模型关系图 (P0 fix 前版本, 部分过时): `docs/refactor/05-model-relationship-map.md`

## 重要约定

- **不要新增 TypeHint enum 值** — 🔴火山级, 8 值锁定。如需行为分类用中间字符串/MenuItemType
- **不要新增 SelectionState enum 值** — 🔴火山级, 3 值锁定
- **不要加 ToDictionary/FromDictionary** — PRD §4.4 明确禁止, 用 JSON 序列化替代
- **不要把视觉外观和行为语义混在一个类型里** — TypeHint 只回答"看起来像什么"
- **Domain.Vision ↔ Domain.Content 零直接 import** — 唯一桥是 ElementTypeMapper (Mappings)
- **所有 record 用 sealed record class + ImmutableArray** — 不可变设计
- **所有校验用 DomainValidationException** — 不用 ValueError/InvalidOperationException

## Git 分支

- `main` — Python 代码库 (C# 项目不存在)
- `feature/refactor` — C# 迁移 (当前工作分支)
- 当前 Domain 层已合并到 feature/refactor
