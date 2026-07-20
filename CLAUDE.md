# CLAUDE.md — UniClaw.Core 项目指南

> 本文件为 Claude Code（及其他 AI 编码助手）提供项目上下文。
> 最后更新: 2026-07-19

## 项目概览

UniClaw.Core 是一个 C# Domain 层项目，从 Python `uni_claw` 代码库迁移而来。
目标是构建一个类型安全、不可变、fail-fast 校验的 Domain 层，
为上层 Graph/Traversal/AI 层提供纯数据模型和映射基础设施。

- **框架**: .NET 10, C# 12
- **测试**: xUnit 2.6, 703 测试全绿
- **风格**: sealed record class + ImmutableArray + DomainValidationException fail-fast
- **序列化**: System.Text.Json, camelCase + enum-as-string (DomainJsonOptions)

## 构建与测试

```bash
# 构建
dotnet build src/UniClaw.Core.sln

# 测试
dotnet test src/UniClaw.Core.sln

# 预期结果: 0 错误, 0 功能性警告, 703 测试通过
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
  StateMachine/                   ← 状态机 (双 FSM, Handler 子组件, 30 mutable Context)
  Traversal/                      ← 遍历引擎 (StepOrchestrator + 6 子组件)
  Observability/                  ← 可观测性 (cross-cutting utility, 被 SM+Traversal 共同消费)

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
| A (直接) | 截图 → 多模态AI → PageAnalysis (AI 返回 type, code 经 ElementTypeMapper 派生 action) | ❌ 不参与 | ✅ 部分 (type→action, 经 ToMenuItemType/ToExpectedAction) |
| B (两步) | 截图 → AI → FlattenedScreen → 规则/文本模型 → PageAnalysis | ✅ 核心链路第一步 | ✅ 规则引擎路径 (MapAndroidClass 全链路) |

**上层架构已 mode-agnostic** (消费 `IVisionProvider`, 输出 `PageAnalysis`) —— **接口即接缝**, Mode A/B 为可插拔实现, 不需二选一。先建 Mode A 通真机, Mode B 为可替换后备。`ElementTypeMapper` 是类型→动作映射的**唯一真相源** (两 mode 共用, Mode A 即激活)。详见 `docs/refactor/2026-07-15-vision-mode-strategy-design.md`。

### 校验策略

- **构造期**: DomainValidationException fail-fast (Coordinate, BoundingBox, Operation 等)
- **解析期**: FromString/MapAndroidClass graceful 回落 + IsValid 通知上层 (TypeHint, SelectionState)

### 序列化

- camelCase 键名 (PRD §6: 本期不背 Python snake_case)
- enum-as-string (JsonStringEnumConverter)
- null 跳过 (WhenWritingNull)
- TypeHint 缺 `[JsonPropertyName]` — 其他 3 个 Domain enum 都有 (P3 待修)

## Domain 层完成状态

**核心 24 类型全部完成**。P3 补齐状态 (2026-07-20 更新):

| # | 项目 | 状态 |
|---|------|------|
| 1 | ContentNode.ToMarkdown() | ✅ 已实现 (单节点; 树级随 ContentTree Phase 2) |
| 2 | Region.Id 非空校验 | ✅ 已实现 |
| 3 | TypeHint 加 [JsonPropertyName] | ✅ 已标注 (8 成员) |
| 4 | TypeHint Values 改为 IReadOnlyList\<string\> | ❌ 待改 (并入 C-6) |
| 5 | 补 IsCanonical(string) 区分精确值 vs 别名 | ✅ 已实现 |

PRD 明确 defer 到 Phase 2: SimulationState, ContentTree

## 系统设计文档

从横切文档升级为四层纵切「AI Coding 宪章」体系 (→ `docs/system/charter-specification.md`):

```
docs/system/
  constitution/               ← Tier 1: 跨 Phase 不变, CI 强制
    constraints.md            全项目 hard constraint 清单
    locked-enums.md           10+2 enum 值锁定 + cascade 影响图
    prohibited-patterns.md    禁止模式 + 原因 + 替代方案
  patterns/                   ← Tier 2: 缓慢追加
    fsm-design.md             双 FSM 架构 + 迁移矩阵 + 独立性原则
    handler-pipeline.md       通用管道 (PopupHandler 6-step 遵循; Container/Error 为 3 独立子组件, → D-16)
    readonly-isolation.md     三级集合安全 + ReadOnlySetWrapper
    dispatch-table.md         Hook dispatch + fallback chain
  layers/                     ← Tier 3: 改代码才改文档
    domain.md                 24+2 类型 + 三岛拓扑 + 桥 + 稳定性 + 校验 + 序列化
    graph.md                  TraversalPlan + PlanCompiler + DynamicMatcher
    state-machine.md          双 FSM + PopupHandler(6-step) + Container/Error(3子组件) + NodeStack + Context(30 mutable)
    traversal.md              StepOrchestrator + 6 子组件
  decisions/                  ← Tier 4: append-only
    log.md                    Source: openspec / finding / direct-commit
```

旧横切文档 (01-07) 保留在 `docs/system/` 原位置作为历史参考。

## 代码查询：MCP 工具优先 🔍

查询 C# 代码（定义、引用、继承、诊断）时，**始终先用 MCP 工具定位，再用 Read 按需读片段**。
MCP 一次查询 ~100-500 tokens，grep + Read 同类探索 ~2000-5000 tokens，节省 80-90%。

### 可用 MCP 服务器

两个服务器能力有重叠，各有独到之处 —— 不是读写分工，是**导航 / 重构**两个场景各有侧重：

| 服务器 | 命令 | 定位 |
|--------|------|------|
| `cwm-roslyn-navigator` | `cwm-roslyn-navigator`（自动发现 .sln） | **日常导航首选**：`find_symbol`, `find_references`, `get_type_hierarchy`, 死代码检测, 反模式检测 |
| `csharper-mcp` | `csharper-mcp --solution <sln>` | **重构 + DLL 探索**：`get_code_actions` / `apply_code_action`（安全重命名等）, `get_decompiled_source`（看 BCL/NuGet 源码）, `get_symbol_info` |

两者都支持：符号定义查找、引用查找、编译器诊断。

### 工作流：查询 → 定位 → 阅读

```
MCP 查询（获取 file:line + 签名）
    → 需要看实现？Read(file, offset, limit) 只读相关行
        → 修改或决策
```

1. **MCP 定位**：拿到精确的文件路径、行号、签名、XML 文档
2. **按需 Read**：需要实现细节时，Read 目标符号所在的行范围（几十行），不读整个文件
3. **禁止 grep**：不要用 `grep` / `find` 定位 C# 符号 —— MCP 提供语义理解，文本搜索做不到（e.g. 同名不同重载、partial class 分散在多个文件）
4. **Partial 类先查全**：C# 的 `partial class` 可能分散在多个文件。修改前必须用 `find_symbol` 查看所有分部位置，避免改了 A 文件漏了 B 文件

### 常用查询速查

| 需求 | 工具 | 服务器 | 示例 |
|------|------|--------|------|
| 查找类/方法定义 | `find_symbol` | roslyn-navigator | `find_symbol(name="ContainerHandler")` |
| 完整签名 + XML 文档 | `get_symbol_detail` | roslyn-navigator | `get_symbol_detail(symbolName="HandleContainer")` |
| 查找所有引用 | `find_references` | roslyn-navigator | `find_references(symbolName="PlanCompiler")` |
| 查找调用方 | `find_callers` | roslyn-navigator | `find_callers(methodName="Compile")` |
| 类型继承树 | `get_type_hierarchy` | roslyn-navigator | `get_type_hierarchy(typeName="ITraversalNode")` |
| 接口实现 / 虚方法重写 | `find_implementations` / `find_overrides` | roslyn-navigator | — |
| 调用依赖图 | `get_dependency_graph` | roslyn-navigator | `get_dependency_graph(symbolName="HandleContainer", depth=3)` |
| 项目依赖树 | `get_project_graph` | roslyn-navigator | — |
| 死代码 / 反模式检测 | `find_dead_code` / `detect_antipatterns` | roslyn-navigator | — |
| 编译器诊断 | `get_diagnostics` | roslyn-navigator | `get_diagnostics(scope="solution")` |
| 代码重构 (安全重命名等) | `get_code_actions` → `apply_code_action` | csharper-mcp | — |
| 查看 BCL/NuGet DLL 源码 | `get_decompiled_source` | csharper-mcp | `get_decompiled_source(typeName="System.String")` ⚠️ 带 `includeImplementation` 可能 >2000 tokens，先不带看签名 |
| 符号类型 + 命名空间 | `get_symbol_info` | csharper-mcp | — |

### 工具跨机器策略

新增工具时按以下原则选择安装方式，保证 `git clone` 后即可工作：

| 工具类型 | 方式 | 示例 |
|---------|------|------|
| MCP 服务器（常驻进程） | `.mcp.json` + 文档说明 | `csharper-mcp`, `cwm-roslyn-navigator` |
| 构建/测试依赖 | NuGet `PackageReference` | xUnit, System.Text.Json |
| 开发时偶尔用的 CLI | `npx` 免安装 | `npx token-ninja` |

原则：**能不装就不装**。npx 首次慢 2 秒但零残留，换机器零成本。

## AI Context Routing

修改代码前，按任务影响层级组装最小文档集：

| 任务类型 | 必读 | 按需读 |
|---------|------|-------|
| Domain 类型修改 | constitution/* + layers/domain.md | patterns/readonly-isolation (改集合暴露) |
| Graph 层修改 | constitution/* + layers/graph.md | patterns/fsm-design (改节点策略) |
| StateMachine 层修改 | constitution/* + patterns/fsm-design + layers/state-machine.md | patterns/handler-pipeline (改 handler) |
| Traversal 层修改 | constitution/* + patterns/dispatch-table + layers/traversal.md | patterns/fsm-design (改 step 流程) |
| Simulation 层修改 | constitution/* + layers/simulation.md + layers/simulation-baseline.md | layers/state-machine.md (改 IVisionProvider) |
| 基线测试修改/新增 | constitution/* + layers/simulation-baseline.md | layers/simulation.md |
| 新增 enum | constitution/locked-enums.md + layers/<affected-layer>.md | decisions/log.md (查同类决策) |
| 修 bug | decisions/log.md + layers/<affected-layer>.md | constitution/constraints.md (检查是否违反约束) |
| 新增 Handler | constitution/* + patterns/handler-pipeline + patterns/dispatch-table | layers/state-machine.md |
| Phase 规划 | constitution/* + all patterns + decisions/log.md | all layers |

规则: 先读 constitution，再读 patterns，再读当前 layer。不读不相关的 layer。

## 宪章 Guard Tests

`ArchitectureGuardTests.cs` 中所有 CI-blocking 约束验证：

- **EnumValueGuardTests** (12 tests): 10 Phase2 enum + 2 Domain enum 值数锁定
- **DependencyDirectionGuardTests** (4 tests): C-4 Domain 零向上引用 + C-5 Graph→StateMachine 单向依赖
  ⚠️ 当前 Guard 只验证 Domain 和 Graph→StateMachine, 不验证 StateMachine→Traversal/Observability 向上引用
  实际依赖: StateMachine 引用 Traversal + Observability (向上), Traversal 引用 Observability (向上)
  → decisions/log D-17: Observability 是 cross-cutting utility, 非设计缺陷

新增约束时必须在此文件加对应测试 (→ `docs/system/charter-specification.md` §6)

## AI Context Routing Hook

`.claude/hooks/context-routing.sh` 在每次 Edit/Write 操作前自动提醒必读文档:

| 编辑目录 | 提醒内容 |
|---------|---------|
| Domain/ | constitution/* + layers/domain.md |
| Graph/ | constitution/* + layers/graph.md |
| StateMachine/ | constitution/* + patterns/fsm-design + layers/state-machine.md |
| Traversal/ | constitution/* + patterns/dispatch-table + layers/traversal.md |
| AI/ | constitution/* + layers/state-machine.md |
| Observability/ | cross-cutting utility 影响 SM+Traversal |
| Simulation/ | constitution/* + layers/simulation.md |

## Python 对齐参考

Python 源码在 `main` 分支 (当前分支 `feature/refactor` 不含 Python 代码):
- `src/models/vision/` — Vision 7 模型
- `src/models/content_models.py` — Content 12 类型
- `src/models/element_type_mapper.py` — ElementTypeMapper
- `src/models/traversal_context.py` — GlobalState + TraversalContext (Phase 2)

Python↔C# 全量对比: `docs/refactor/04-phase1-python-csharp-comparison.md`
模型关系图 (P0 fix 前版本, 部分过时): `docs/refactor/05-model-relationship-map.md`

## 开发流程：OpenSpec Spec-Driven 变更生命周期

项目依托 OpenSpec 管理 spec-driven 变更的完整生命周期。
每个 change 以规格 (spec) 为驱动源头，走 propose → apply → verify → archive 流程:
规格定义 WHAT (SHALL/MUST), design 定义 HOW, tasks 定义 STEPS。
工作单位是 change (含 specs + design + tasks), 不是孤立的任务。

- **提出变更**: `/opsx:propose` 或 `/openspec-propose` 创建 change
- **执行变更**: `/opsx:apply` 或 `/openspec-apply-change` 按 tasks.md 实施, 验证对照 specs
- **探索需求**: `/opsx:explore` 或 `/openspec-explore` 讨论和澄清规格
- **归档完成**: `/opsx:archive` 或 `/openspec-archive-change` 提取 decisions, 同步四层文档

`openspec/changes/` 是变更进度权威来源:
- 活跃 change 的 `tasks.md` 记录实施清单和完成状态
- 已归档 change 在 `openspec/changes/archive/`
- 不在 OpenSpec 中的工作 = 不在 spec-driven 流程中的工作，需要特别说明

## 重要约定

- **不要新增 TypeHint enum 值** — 🔴火山级, 8 值锁定。如需行为分类用中间字符串/MenuItemType
- **不要新增 SelectionState enum 值** — 🔴火山级, 3 值锁定
- **不要加 ToDictionary/FromDictionary** — PRD §4.4 明确禁止, 用 JSON 序列化替代
- **不要把视觉外观和行为语义混在一个类型里** — TypeHint 只回答"看起来像什么"
- **Domain.Vision ↔ Domain.Content 零直接 import** — 唯一桥是 ElementTypeMapper (Mappings)
- **Observability 是 cross-cutting, 不是传统顶层** — StateMachine/Traversal 可引用它, 不视为向上违规 (D-17)
- **IGraphTraversalEngine 双定义 stub 已清理** — D-14 resolved: 空 stub 已删除, StateMachine→Traversal 向上引用显式承认 (与 D-17 一致)
- **所有 record 用 sealed record class + ImmutableArray** — 不可变设计
- **所有校验用 DomainValidationException** — 不用 ValueError/InvalidOperationException
- **如有新增要寻得用户同意**
- **重要约定和违规教训必须写入 memory** — 如果本文件中的规则 (如 MCP 优先) 或实际发生的违规教训尚未写入 memory 文件, **必须立即写入**。memory 跨 session 被召回, 是确保规则持续生效的关键机制。写入格式见 memory README
- **C# 代码查询 MCP 工具优先** — 查询 C# 符号 (定义/引用/继承/诊断) 时, **始终先用 MCP** (`find_symbol`/`find_references`), 再用 Read 按需读片段。**禁止 grep/find 定位 C# 符号**

## Git 分支

- `main` — Python 代码库 (C# 项目不存在)
- `feature/refactor` — C# 迁移 (当前工作分支)
- 当前 Domain 层已合并到 feature/refactor
