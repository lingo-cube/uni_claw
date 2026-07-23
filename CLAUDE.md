# CLAUDE.md — UniClaw.Core 项目指南

> 本文件为 Claude Code（及其他 AI 编码助手）提供项目上下文。
> 最后更新: 2026-07-21

## 项目概览

UniClaw.Core 是一个 C# Domain 层项目，从 Python `uni_claw` 代码库迁移而来。
目标是构建一个类型安全、不可变、fail-fast 校验的 Domain 层，
为上层 Graph/Traversal/AI 层提供纯数据模型和映射基础设施。

- **框架**: .NET 9, C# 12
- **测试**: xUnit 2.6, 840 测试全绿
- **风格**: sealed record class + ImmutableArray + DomainValidationException fail-fast
- **序列化**: System.Text.Json, camelCase + enum-as-string (DomainJsonOptions)

## 构建与测试

```bash
# 构建
dotnet build src/UniClaw.Core.sln

# 测试
dotnet test src/UniClaw.Core.sln

# 预期结果: 0 错误, 0 功能性警告, 840 测试通过
```

## 项目结构

> 代码结构随各层演进，权威结构描述在各 layer 文档 (`docs/system/layers/*.md`)。
> 概览: `src/UniClaw.Core/` (net9.0 生产) 含 Domain / Graph(AI) / StateMachine / Traversal / Observability / UniBrain 子目录；`src/UniClaw.Core.SourceGen/` (Roslyn 源生成器)；`tests/UniClaw.Core.Tests/` (xUnit)。详见各 layer 文档。

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

## 系统设计文档（AI Coding 宪章）

> 权威定义：`docs/system/charter-specification.md`（§5 四层纵切体系 + §6 Guard Tests）。
> 本段不重复内容，改文档改 charter。以下为指针：

- **四层纵切**：`docs/system/{constitution,patterns,layers,decisions}/` —— Tier 1 跨 Phase 不变 / Tier 2 缓慢追加 / Tier 3 改代码才改 / Tier 4 append-only。旧横切文档 (01-07) 保留在 `docs/system/` 作历史参考。
- **AI Context Routing**（改代码前读哪些文档）：charter §5.6 + 下表，按任务影响层级组装最小文档集。规则：先 constitution，再 patterns，再当前 layer，不读不相关 layer。

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

- **宪章 Guard Tests**：`ArchitectureGuardTests.cs` 中所有 CI-blocking 约束验证 —— EnumValueGuardTests (12) + DependencyDirectionGuardTests (4)。⚠️ 当前 Guard 只验 Domain 和 Graph→StateMachine，不验 StateMachine→Traversal/Observability 向上引用（实际依赖存在，→ D-17: Observability 是 cross-cutting utility，非设计缺陷）。新增约束必须在此文件加测试（charter §6）。
- **AI Context Routing Hook**：`.claude/hooks/context-routing.sh` 在每次 Edit/Write 前按编辑目录自动提醒必读文档（Domain/Graph/StateMachine/Traversal/AI/Observability/Simulation 各对应 constitution/* + 相关 layer/patterns）。

## 代码查询：MCP 工具优先 🔍

> 规则单点真源：`.claude/MCP-QUERY.md`（服务器对照、查询→定位→阅读工作流、速查表、跨机器策略）。
> 改规则改那里，本段不重复内容。`.claude/commands/opsx/AGENT.md` 也引用该文件，让 OpenSpec 子代理遵守同一规则。

**核心规则**：查询 C# 代码（定义、引用、继承、诊断）时，**始终先用 MCP 工具定位，再用 Read 按需读片段**。MCP 一次查询 ~100-500 tokens，grep + Read 同类探索 ~2000-5000 tokens，节省 80-90%。**禁止用 `grep` / `find` 定位 C# 符号**。详见 `.claude/MCP-QUERY.md`。

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
