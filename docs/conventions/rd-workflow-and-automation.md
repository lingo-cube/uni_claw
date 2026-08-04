# UniClaw.Core 项目研发流程与自动化体系

> 最后更新: 2026-08-03

---

## 一、核心研发流程：OpenSpec Spec-Driven 变更生命周期

整个研发流程围绕 **OpenSpec** 构建，以规格 (spec) 为驱动源头，走 **propose → apply → verify → archive** 四阶段闭环。

```
用户需求 → /opsx:propose → artifacts 生成 → /opsx:apply → 逐任务实施 → /opsx:archive → 归档沉淀
```

### 1.1 变更提出（Propose）

- **入口**: `/opsx:propose <change-name>`，Codex 侧自然语言 `openspec propose <topic>`
- **产出**: `openspec/changes/<name>/` 下的 `proposal.md` (WHAT)、`design.md` (HOW)、`tasks.md` (STEPS)、`specs/` (SHALL/MUST 规格)
- **关联 Skill**: `openspec-propose`

### 1.2 变更执行（Apply）

- **入口**: `/opsx:apply [change-name]`
- **流程**: 读取 artifacts → 展示进度 → 按 `tasks.md` 逐项实施 → 勾选 `- [x]` → 完成后建议 archive
- **关联 Skill**: `openspec-apply-change`

### 1.3 需求探索（Explore）

- **入口**: `/opsx:explore <topic>`
- **角色**: 纯思考姿态，可选派 `openspec-researcher` (haiku/只读) 做检索归纳，不进 Fable/Opus 统筹链路，不改代码
- **关联 Skill**: `openspec-explore`

### 1.4 归档沉淀（Archive）

- **入口**: `/opsx:archive <change-name>`
- **产出**: 提取 decisions，同步主规格和四层文档体系，change 移入 `openspec/changes/archive/`
- **关联 Skill**: `openspec-archive-change`

### 1.5 Fable 编排模式（Propose / Apply 强制启用）

`/opsx:propose` 和 `/opsx:apply` 执行时立即启用 Fable 编排模式。顶层统筹 = 主会话本身（不再额外开 orchestrator 子代理），按需拆分子任务并指派对应档位 SubAgent。

| 角色 | 模型档位 | 背后模型 | 职责 |
|------|---------|---------|------|
| **顶层统筹** | Fable | glm-5.2[1M] | 需求边界校验、架构决策、复杂逻辑推演、多子任务成果合并、风险识别、代码一致性审查 |
| `openspec-researcher` | haiku | deepseek-v4-flash | 文件检索、日志解析、正则校验、信息探查（轻量只读） |
| `openspec-coder` | sonnet | deepseek-v4-flash | 常规功能编码、Bug 修复、单元测试、接口实现 |
| `openspec-refactorer` | opus | deepseek-v4-pro | 跨模块重构、复杂流程梳理、深度故障定位（仅决策密集时派发） |

**降级链路**: `Fable → Opus`（止步，禁止继续落到 Sonnet / Haiku 承担顶层架构规划）。Opus 也异常时向用户告警，不继续自动降级，等待人工介入。

**派发触发条件**（满足任一即多 SubAgent 并行）：
- 改动 ≥ 3 个代码文件
- 涉及架构调整、新增模块、状态机/并发逻辑
- 需求存在歧义、需要多方案对比
- 包含「方案设计 + 编码落地 + 验证自测」完整链路

**简单单行修改、常量调整、日志打印、语法修复**：禁止派生 SubAgent，顶层统筹直接完成。

**硬性约束**：
1. 只有顶层统筹允许使用 Fable。SubAgent 上限为 Opus（`openspec-refactorer`）。
2. 顶层统筹只负责规划、评审、难点攻坚；大量机械编码委派下级 Agent。
3. 所有子任务完成后由顶层统筹统一校验：代码一致性、逻辑冲突、边界缺陷。
4. 火山级 / 宪章级决策上抛用户：新增 enum 值、改约束、改 layer 拓扑等，子代理不得自行实施。
5. C# 代码查询 MCP 优先：子代理与顶层统筹均需遵守。

### 1.6 Cross-Assistant 兼容（Codex）

Codex 不原生执行 Claude slash command，通过自然语言触发对应行为：

| Codex 触发语 | 行为 | 必读 playbook |
|-------------|------|---------------|
| `openspec propose <change-or-topic>` | 创建或补全 change 的 proposal/design/specs/tasks | `.claude/skills/openspec-propose/SKILL.md` |
| `openspec apply <change>` | 读取 change artifacts，按 tasks.md 实施 | `.claude/skills/openspec-apply-change/SKILL.md` |
| `openspec explore <topic>` | 只做需求探索、方案澄清和上下文整理 | `.claude/skills/openspec-explore/SKILL.md` |
| `openspec archive <change>` | 归档、提取 decisions、同步主规格和四层文档 | `.claude/skills/openspec-archive-change/SKILL.md` |

约定：不在 OpenSpec change 中的工作，需要在回复中明确说明"本次未走 OpenSpec 流程"。

---

## 二、AI Coding 宪章：四层纵切文档体系

定义于 `docs/system/charter-specification.md`（§5 四层纵切 + §6 Guard Tests）。改代码前按任务影响层级组装最小文档集：先 constitution，再 patterns，再当前 layer，不读不相关 layer。

| 层级 | 目录 | 性质 | 内容 |
|------|------|------|------|
| **Tier 1: Constitution** | `docs/system/constitution/` | 跨 Phase 不变 | `constraints.md`, `locked-enums.md`, `prohibited-patterns.md` |
| **Tier 2: Patterns** | `docs/system/patterns/` | 缓慢追加 | `fsm-design.md`, `dispatch-table.md`, `handler-pipeline.md`, `readonly-isolation.md`, `system-orchestration.md` |
| **Tier 3: Layers** | `docs/system/layers/` | 改代码才改 | `domain.md`, `graph.md`, `state-machine.md`, `traversal.md`, `simulation.md`, `simulation-baseline.md`, `observability.md`, `host.md`, `device.md` |
| **Tier 4: Decisions** | `docs/system/decisions/` | append-only | `log.md`（决策日志） |

### 上下文路由速查表

| 任务类型 | 必读 | 按需读 |
|---------|------|-------|
| Domain 类型修改 | constitution/* + layers/domain.md | patterns/readonly-isolation（改集合暴露时） |
| Graph 层修改 | constitution/* + layers/graph.md | patterns/fsm-design（改节点策略时） |
| StateMachine 层修改 | constitution/* + patterns/fsm-design + layers/state-machine.md | patterns/handler-pipeline（改 handler 时） |
| Traversal 层修改 | constitution/* + patterns/dispatch-table + layers/traversal.md | patterns/fsm-design（改 step 流程时） |
| Simulation 层修改 | constitution/* + layers/simulation.md + layers/simulation-baseline.md | layers/state-machine.md（改 IVisionProvider 时） |
| 基线测试修改/新增 | constitution/* + layers/simulation-baseline.md | layers/simulation.md |
| 新增 enum | constitution/locked-enums.md + layers/<affected-layer>.md | decisions/log.md（查同类决策） |
| 修 bug | decisions/log.md + layers/<affected-layer>.md | constitution/constraints.md（检查是否违反约束） |
| 新增 Handler | constitution/* + patterns/handler-pipeline + patterns/dispatch-table | layers/state-machine.md |
| Phase 规划 | constitution/* + all patterns + decisions/log.md | all layers |

### Context Routing Hook

`.claude/hooks/context-routing.sh` 在每次 Edit/Write 前按编辑文件所在目录自动提醒必读文档（Domain/Graph/StateMachine/Traversal/AI/Observability/Simulation 各对应 constitution/* + 相关 layer/patterns）。

### Guard Tests

`ArchitectureGuardTests.cs` 中的 CI-blocking 约束验证：EnumValueGuardTests (12) + DependencyDirectionGuardTests (4)。Observability 被承认是 cross-cutting utility，StateMachine/Traversal 引用它不视为向上违规 (D-17)。新增约束必须在此文件加测试（charter §6）。

---

## 三、代码查询工作流：MCP 优先

**规则单点真源**: `.claude/MCP-QUERY.md`（服务器对照、查询→定位→阅读工作流、速查表、跨机器策略）。

### 核心规则

> 查询 C# 代码（定义、引用、继承、诊断）时，**始终先用 MCP 工具定位，再用 Read 按需读片段**。MCP 一次查询 ~100-500 tokens，grep + Read 同类探索 ~2000-5000 tokens，节省 80-90%。**禁止用 `grep` / `find` 定位 C# 符号**。

### 查询流程

```
MCP 查询（获取 file:line + 签名）
    → 需要看实现？Read(file, offset, limit) 只读相关行
        → 修改或决策
```

### 两套 MCP 服务器

两个服务器能力有重叠，各有独到之处——不是读写分工，是**导航 / 重构**两个场景各有侧重：

| 服务器 | 定位 | 独有优势 |
|--------|------|---------|
| `cwm-roslyn-navigator` | Claude 日常导航首选 | `find_symbol`, `find_references`, `get_type_hierarchy`, 死代码检测, 反模式检测 |
| `csharper-mcp` | 重构 + DLL 探索 | `get_code_actions` / `apply_code_action`（安全重命名等）, `get_decompiled_source`（看 BCL/NuGet 源码）, `get_symbol_info` |

两者都支持：符号定义查找、引用查找、编译器诊断。

### 常用查询速查

| 需求 | 工具 | 服务器 |
|------|------|--------|
| 查找类/方法定义 | `find_symbol` / `get_definition_location` | roslyn-navigator / csharper-mcp |
| 完整签名 + XML 文档 | `get_symbol_detail` | roslyn-navigator |
| 查找所有引用 | `find_references` | roslyn-navigator |
| 查找调用方 | `find_callers` | roslyn-navigator |
| 类型继承树 | `get_type_hierarchy` | roslyn-navigator |
| 接口实现 / 虚方法重写 | `find_implementations` / `find_overrides` | roslyn-navigator |
| 调用依赖图 | `get_dependency_graph` | roslyn-navigator |
| 项目依赖树 | `get_project_graph` | roslyn-navigator |
| 死代码 / 反模式检测 | `find_dead_code` / `detect_antipatterns` | roslyn-navigator |
| 编译器诊断 | `get_diagnostics` | roslyn-navigator / csharper-mcp |
| 代码重构（安全重命名） | `get_code_actions` → `apply_code_action` | csharper-mcp |
| 查看 BCL/NuGet DLL 源码 | `get_decompiled_source` | csharper-mcp |
| 符号类型 + 命名空间 | `get_symbol_info` | csharper-mcp |

### 跨机器策略

| 工具类型 | 方式 | 示例 |
|---------|------|------|
| MCP 服务器（常驻进程） | `.mcp.json` + 文档说明 | `csharper-mcp`, `cwm-roslyn-navigator` |
| 构建/测试依赖 | NuGet `PackageReference` | xUnit, System.Text.Json |
| 开发时偶尔用的 CLI | `npx` 免安装 | `npx token-ninja` |

---

## 四、Skills 体系（`.claude/skills/`）

Claude 项目内 skill，Codex 可按需阅读对应 `SKILL.md`/`skill.md` 作为项目 playbook。

### 4.1 OpenSpec 核心技能

| Skill | 触发方式 | 职责 |
|-------|---------|------|
| `openspec-propose` | `/opsx:propose` | 创建 change + 生成 proposal/design/tasks/specs |
| `openspec-apply-change` | `/opsx:apply` | 按 tasks.md 实施，完成立即勾选 |
| `openspec-explore` | `/opsx:explore` | 需求探索、方案澄清、上下文整理 |
| `openspec-archive-change` | `/opsx:archive` | 归档、提取 decisions、同步主规格和四层文档 |
| `wf-apply` | — | Workflow 驱动的任务执行，结合 openspec + 智能 Haiku/Opus 路由 |

### 4.2 工程实践技能

| Skill | 用途 |
|-------|------|
| `design-doc-sync` | 实施前从 PRD 更新模块设计文档，同步上下文到下游 agent |
| `module-test` | 执行模块单元测试，智能失败处理 + 决策追踪 |
| `test-extraction` | 从设计文档自动提取测试场景并生成测试代码 |
| `validation-documentation` | 生成标准化验证报告（一致命名和格式） |

### 4.3 可观测性技能

| Skill | 用途 |
|-------|------|
| `trace-collection` | 收集 Mock 测试资产 trace，零 API 成本生成 JSON trace 文件 |
| `trace-visualization` | 将 trace 数据可视化为 ASCII 树、Mermaid 流程图、状态转换日志 |
| `state-machine-integration` | 将 trace 数据与状态机集成——格式化为状态转换、错误事件、性能指标 |
| `workflow-trace-collection` | 将 trace 采集集成到 OpenSpec workflow——实施期间自动采集，trace-based 验证 |

---

## 五、Workflows 体系（`.claude/workflows/`）

通过 `/Workflow <name>` 调用的多代理编排脚本。

### 5.1 测试工程

**`integrated-test-gen`** — 可靠的集成测试生成

- 流程: Check → Extract → Generate → Verify → Report
- 闭环：检查设计文档是否有测试场景章节 → 如果没有则执行 test-extraction → 基于设计文档和测试规则进行 multi-agent 测试生成

**`test-scenario-generation-evaluation`** — 测试用例生成和评估

- 流程: ReadDesign → GenerateScenarios → ReadExisting → AgentVerify → Battle → Compare → Score → Report
- 核心机制：multi-agent 验证 + battle + 质量评分，输出测试用例列表文档

**`test-scenario-evaluation`** — 测试用例评估

- 单模块测试场景质量评分

### 5.2 质量保证

**`prd-review-judgment`** — PRD 多代理审阅与评判

- 流程:
  1. 审阅准备（读取所有 PRD 文档）
  2. Sonnet 代理 1: 全面审阅（完整性、逻辑一致性、可行性、依赖、成功标准、代码质量）
  3. Sonnet 代理 2: 对抗性审阅（挑战假设、发现遗漏、质疑优先级、风险评估）
  4. Opus 代理: 作为架构师做最终评判（综合评估、架构一致性、最终决策：批准/有条件批准/拒绝）
  5. 生成报告

**`rules-integration-closed-loop`** — 规则集成闭环

- 流程: Audit → Supplement → Create → Verify
- 审计现有设计文档 → 补充测试场景 → 创建规则集成 workflow → 验证整体方案

### 5.3 自驱动任务执行

三种版本逐步演进：

| Workflow | 特点 |
|----------|------|
| `self-driven-task-execution` | 原版：FetchTasks → AssignTask → Implement → SelfVerify → Battle → Judge → Complete |
| `self-driven-task-execution-optimized` | 优化版：渐进升级 + 智能路由 |
| `self-driven-task-execution-final` | 最终版：智能路由 + 顺序执行 + 持续学习 + 问题记录 |

**`self-driven-task-execution-final` 详细流程**:

```
FetchTasks（从 openspec 获取任务列表）
  → AssignTask（智能路由：按任务类型推断 Haiku 或 Opus）
    → Implement（Haiku 实现 或 Opus 实现）
      → SelfVerify（顺序执行：2×Haiku 需求验证+质量验证 → 1×Sonnet 边界验证）
        → Battle（顺序执行：1×Haiku 挑战需求验证 + 1×Sonnet 挑战质量验证）
          → Opus Judge（综合裁决：PASS/FAIL + 是否可完成）
            → Complete（勾选 tasks.md）
              → NextTask（循环直到全部完成）
```

核心机制：
- **智能路由**：按任务类型（测试/文档/配置/重构/架构/实现/修复）推断复杂度，历史成功率 ≥70% 则沿用该模型
- **路由记忆（持续学习）**：`routingMemory` 记录每种任务类型的成功/失败次数，自动调整后续路由
- **顺序执行**：验证和 battle 阶段从 parallel 改为顺序执行，避免 503 错误
- **问题自动记录**：失败或未通过的任务自动生成 ISSUES 文档到 `docs/issues/`

### 5.4 分析对比

**`single-vs-multi-agent-comparison`** — 单 Agent vs 多 Agent 对比

- 流程: Setup → Single（单 Agent 分析） → Multi（多 Agent 分析） → Compare（对比分析） → Report
- 对比维度：代码实现分析、设计文档分析、测试场景提取、边界条件识别、错误场景识别、覆盖度评估

---

## 六、基础设施一览

### Hooks

| Hook | 触发时机 | 作用 |
|------|---------|------|
| `.claude/hooks/context-routing.sh` | Pre-edit (每次 Edit/Write 前) | 按编辑文件所在目录自动提醒必读 charter 文档 |

### Custom Commands

| 命令 | 文件 | 作用 |
|------|------|------|
| `/opsx:propose` | `.claude/commands/opsx/propose.md` | 创建 change + 生成所有 artifacts |
| `/opsx:apply` | `.claude/commands/opsx/apply.md` | 按 tasks.md 实施变更 |
| `/opsx:explore` | `.claude/commands/opsx/explore.md` | 需求探索与方案澄清 |
| `/opsx:archive` | `.claude/commands/opsx/archive.md` | 归档完成 change |
| — | `.claude/commands/opsx/AGENT.md` | 编排规范单点真源（Fable 模式 + 子代理派发 + 降级策略） |

### 关键配置

| 组件 | 位置 | 作用 |
|------|------|------|
| OpenSpec 配置 | `openspec/config.yaml` | `schema: spec-driven` |
| MCP 查询规则 | `.claude/MCP-QUERY.md` | C# 代码查询 MCP 优先规则 + 速查表 |
| Guard Tests | `tests/.../ArchitectureGuardTests.cs` | EnumValueGuard(12) + DependencyDirectionGuard(4)，CI-blocking |

### Git Worktree 隔离

通过 `using-git-worktrees` skill 或 `EnterWorktree` 工具，在 `.claude/worktrees/` 下创建隔离工作区进行 feature 开发，避免影响当前工作空间。

### 项目约定文档

| 约定 | 文档 |
|------|------|
| 文档存放位置 | `docs/conventions/design-doc-location.md` |
| 命名空间隔离 | `docs/conventions/namespace-isolation.md` |
| LiteLLMBar 维护 | `docs/conventions/litellmbar-maintenance.md` |
| 可观测性与集成 | `docs/conventions/observation-conventions.md` |
| AI Coding 宪章 | `docs/system/charter-specification.md` |

---

## 七、当前活跃 OpenSpec Changes

```
core-observation-pipeline
deliver-safe-android-settings-test-loop
harden-deterministic-verification-async-evidence
host-target-architecture
local-vision-provider
optimize-locate-traversal-latency
runner-through-engine
trace-parent-linkage
trace-span-helpers
```

---

## 八、流程全景图

```
┌─────────────────────────────────────────────────────────────────┐
│                      UniClaw.Core R&D 流程                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  用户需求                                                        │
│     │                                                            │
│     ▼                                                            │
│  ┌──────────────────┐    Fable 统筹                              │
│  │ /opsx:propose    │──── 规划 + 派发子代理 ────┐                │
│  │ (openspec-propose)│                          │                │
│  └──────┬───────────┘                    ┌──────┴──────┐        │
│         │ 生成 artifacts                 │ researcher  │ (haiku) │
│         ▼                               │ coder       │ (sonnet)│
│  ┌──────────────────┐                    │ refactorer  │ (opus)  │
│  │ proposal.md      │                    └──────┬──────┘        │
│  │ design.md        │                           │               │
│  │ tasks.md         │◄──────────────────────────┘               │
│  │ specs/*.md       │                                           │
│  └──────┬───────────┘                                           │
│         │                                                        │
│         ▼                                                        │
│  ┌──────────────────┐    Fable 统筹                              │
│  │ /opsx:apply      │──── 逐 task 派发 ────────┐                │
│  │ (openspec-apply)  │                         │                │
│  └──────┬───────────┘                    ┌──────┴──────┐        │
│         │ 逐 task 实施 + 勾选             │ researcher  │        │
│         │                                │ coder       │        │
│         │  ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐     │ refactorer  │        │
│         │   wf-apply / self-driven       └──────┬──────┘        │
│         │  │ (可选自动化执行)     │              │               │
│         │   ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─            │               │
│         │                                      │               │
│         ▼                                      │               │
│  ┌──────────────────┐                          │               │
│  │ module-test      │◄── 验证 ─────────────────┘               │
│  │ validation-doc   │                                           │
│  └──────┬───────────┘                                           │
│         │                                                        │
│         ▼                                                        │
│  ┌──────────────────┐                                           │
│  │ /opsx:archive    │── 提取 decisions → 同步四层文档            │
│  │ (openspec-archive)│                                          │
│  └──────────────────┘                                           │
│                                                                  │
├─────────────────────────────────────────────────────────────────┤
│  基础设施层                                                       │
│  ├── MCP-First 代码查询 (cwm-roslyn-navigator + csharper-mcp)    │
│  ├── Context-Routing Hook (自动文档提醒)                         │
│  ├── Git Worktree 隔离                                          │
│  ├── 四层 Charter 文档体系 (Constitution → Patterns → Layers →   │
│  │   Decisions)                                                  │
│  └── Guard Tests (ArchitectureGuardTests.cs)                     │
├─────────────────────────────────────────────────────────────────┤
│  辅助 Workflows                                                  │
│  ├── prd-review-judgment (PRD 多代理审阅)                        │
│  ├── integrated-test-gen (集成测试生成)                          │
│  ├── test-scenario-generation-evaluation (测试场景生成+评估)     │
│  ├── rules-integration-closed-loop (规则集成闭环)                │
│  ├── self-driven-task-execution (自驱动任务执行，三种版本)        │
│  └── single-vs-multi-agent-comparison (单/多 Agent 对比)         │
├─────────────────────────────────────────────────────────────────┤
│  可观测性 Skills                                                 │
│  ├── trace-collection → trace-visualization                      │
│  ├── state-machine-integration                                  │
│  └── workflow-trace-collection                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 核心理念总结

- **Spec-driven**: 规格是唯一真相源，所有变更以 spec 为驱动
- **Fable 编排**: 顶层统筹用最强模型做架构规划，按任务复杂度分级派发子代理
- **MCP 优先**: 所有 C# 代码查询走语义 MCP，不走文本 grep，节省 80-90% token
- **四层文档**: 宪章级文档体系确保 AI 编码上下文一致性
- **多代理验证**: Workflow 层大量使用对抗审阅、battle、multi-agent judge 等模式保证质量
- **闭环自动化**: 从 trace 采集到测试生成到问题记录，均配置了自动化闭环
