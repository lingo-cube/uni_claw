# AGENTS.md — UniClaw Agent Runtime（uni-agent 分支）

> 本文件是 **map, 不是 manual**（Harness Engineering: "AGENTS.md as a map, not a manual"）。
> 本分支 = Greenfield Agent Runtime 的**架构框架**：宪章 + Contract + Runtime 骨架 + 机械 Guard，**不含旧代码**。
> 旧 UniClaw.Core 代码库保留在基线分支 `feature/agent-runtime`，需要时按 OpenSpec 决策逐步迁移。
> 最后更新: 2026-08-07

## 分支角色

| 分支 | 角色 |
|------|------|
| `main` | Python 代码库（历史基线） |
| `feature/agent-runtime` | **基线分支** — 完整旧代码 + Greenfield 地基 |
| `uni-agent`（本分支） | **架构框架** — 无业务代码；Runtime 业务代码从零生长（Phase 1 起） |

## 跨助手入口

- `AGENTS.md` 是项目协议、架构约束、上下文路由和开发流程的共享入口。
- `CLAUDE.md` 只作为 Claude Code 适配层存在，必须引用本文件，不再复制项目规则。
- 规则变更优先改本文件；Claude 专属 slash command / hook 规则仍放在 `.claude/`。

## Agent Runtime（新）— Greenfield

> 新 Runtime 是独立工程（`src/UniClaw.Runtime/`），不是旧 TraversalEngine 的重构。
> 改 Runtime 代码前必读：

- **Greenfield 宪章**（完整行为指导，60 节按职责分类）: [docs/system/greenfield-runtime-charter.md](docs/system/greenfield-runtime-charter.md)
- **Architecture Contract**（12 invariants，宪章的硬约束子集）: [docs/system/constitution/runtime-architecture-contract.md](docs/system/constitution/runtime-architecture-contract.md)
- **构建区 map**: [src/UniClaw.Runtime/AGENTS.md](src/UniClaw.Runtime/AGENTS.md)（目录职责 + 状态 owner 对照表）
- **OpenSpec change**: `openspec/changes/greenfield-agent-runtime/`（Phase 0 地基 + Vertical Slice 根）
- **Phase 1 change**（Deterministic Runtime / Normal WiFi Scenario）: `openspec/changes/phase1-deterministic-runtime/`（Architecture Proposal + Minimum Contracts，待审批实施）
- **机械约束**: [tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs](tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs) — Guard 1: csproj 零 ProjectReference；Guard 2: 禁 `UniClaw.Core.Traversal` / `UniClaw.Core.StateMachine`；Guard 3: 契约文档 + 本导航必须存在
- **机械文档检查**: `scripts/check-consistency.sh` — 宪章 60 节 / Contract 12 条 / 导航完整（"Docs rot; lint rules don't"）
- ⚠️ **第一阶段 UniClaw.Runtime 不引用 UniClaw.Core** — Greenfield 隔离。复用成熟能力时走 OpenSpec 决策（Extract Foundation / Create Adapter / Reuse Contract），不提前预设。

## 项目概览

UniClaw 是一个运行在真实 GUI / Device Environment 上的智能执行 Runtime。

- 核心闭环: Observe → Reconcile → Decide → Execute → Observe → Verify → Update → Continue
- 架构 Spine: Agent → Container → Traversal → Environment；异常路径: Trap → Determine Scope → Recovery → Observe → Verify → Reconcile → Resume
- **框架**: .NET 10 LTS, C# 12, async/await
- **测试**: xUnit 2.6, Scenario-first（Fake Environment 确定性模拟，第一阶段不连真实手机）
- **当前阶段**: Phase 0 完成（工程边界 + 机械 Guard）→ Phase 1 Deterministic Runtime（Normal WiFi Scenario）

## 构建与测试

```bash
# 构建
dotnet build src/UniClaw.Runtime.sln

# 测试
dotnet test src/UniClaw.Runtime.sln

# 预期结果: 0 错误, 0 警告, Guard 测试通过
```

## 项目结构

> 权威结构: 宪章 §40 + `src/UniClaw.Runtime/AGENTS.md`（目录职责表）。

```
src/UniClaw.Runtime/              ← 生产代码（Agent/Container/Traversal/Recovery/World/
                                      Planning/Memory/Capabilities/Model/Observability）
tests/UniClaw.Runtime.Tests/      ← Unit / Architecture / Scenario / Integration
docs/system/                      ← 宪章（greenfield-runtime-charter.md）+ Contract（constitution/）
openspec/changes/                 ← OpenSpec 变更（repo 是 system of record）
scripts/                          ← 机械检查（check-consistency.sh）
```

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
- 不在 OpenSpec 中的工作 = 不在 spec-driven 流程中的工作，需要特别说明

### Codex OpenSpec 触发规则

Codex 不原生执行 Claude slash command。用户在 Codex 中提到以下自然语言触发语时，按 OpenSpec 生命周期处理，并优先读取对应 `.claude/skills/openspec-*` playbook：

| Codex 触发语 | 行为 | 必读 playbook |
|-------------|------|---------------|
| `openspec propose <change-or-topic>` / `按 OpenSpec propose ...` | 创建或补全 `openspec/changes/<change>/` 下的 proposal/design/specs/tasks | `.claude/skills/openspec-propose/SKILL.md` |
| `openspec apply <change>` / `按 OpenSpec apply ...` | 读取 change artifacts，按 `tasks.md` 实施，完成一项立即勾选 `- [x]` | `.claude/skills/openspec-apply-change/SKILL.md` |
| `openspec explore <topic>` / `按 OpenSpec explore ...` | 只做需求探索、方案澄清和上下文整理；除非用户明确要求，不改代码 | `.claude/skills/openspec-explore/SKILL.md` |
| `openspec archive <change>` / `按 OpenSpec archive ...` | 完成归档、提取 decisions、同步主规格 | `.claude/skills/openspec-archive-change/SKILL.md` |

执行约定：
- OpenSpec artifacts 是跨助手共享真相源；活跃变更看 `openspec/changes/<change>/`，已归档变更看 `openspec/changes/archive/`。
- Claude 的 `/opsx:*`、`/openspec-*` 是 Claude Code 专属命令；Codex 遇到这些写法时，将其解释为对应自然语言 OpenSpec 请求。
- apply 前必须读取该 change 的 `proposal.md`、`design.md`、`tasks.md`、`specs/**/*.md`；实现后同步更新 `tasks.md`。
- 不在 OpenSpec change 中的工作，需要在回复中明确说明"本次未走 OpenSpec 流程"。

## 代码查询：MCP 工具优先 🔍

> 规则单点真源：`.claude/MCP-QUERY.md`（服务器对照、查询→定位→阅读工作流、速查表、跨机器策略）。
> 改规则改那里，本段不重复内容。`.claude/commands/opsx/AGENT.md` 也引用该文件，让 OpenSpec 子代理遵守同一规则。

**核心规则**：查询 C# 代码（定义、引用、继承、诊断）时，**始终先用 MCP 工具定位，再用 Read 按需读片段**。**禁止用 `grep` / `find` 定位 C# 符号**。详见 `.claude/MCP-QUERY.md`。

## 迁移约定（从基线分支迁入时）

- 迁移任何内容前先走 OpenSpec 决策（Extract Foundation / Create Adapter / Reuse Contract）。
- 基线分支只读参考；不在本分支复制旧控制结构（Contract I-11）。
- 需要查看旧代码时切到 `feature/agent-runtime` 工作区，不在本分支恢复旧文件。

## Git 分支

- `main` — Python 代码库（历史基线）
- `feature/agent-runtime` — 基线分支（旧代码 + Greenfield 地基）
- `uni-agent` — 本分支（架构框架；业务代码从零生长）
