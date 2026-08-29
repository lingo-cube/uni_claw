# AGENTS.md — UniClaw Agent Runtime（uni-agent 分支）

> **Universal Agent Map（map, not manual）** — 所有采用通用项目协议的 AI Coder（Codex / DSH / 其他 Host）
> 的唯一通用规则入口（Single Source of Truth）。
> 30 秒内知道：项目身份 → 权威来源 → 开发入口 → 核心禁止。
> 详细协议在 `.ai/`；系统真相在 `docs/`；实现证据在 `src/`。
> 最后更新: 2026-08-24

## 1. Project Identity

UniClaw 是运行在真实 GUI / Device Environment 上的智能执行 Runtime（.NET 10 / C# 12 / xUnit，Scenario-first）。

- 核心闭环: Observe → Reconcile → Decide → Execute → Verify → Update；异常: Trap → Determine Scope → Recovery → Resume。
- 分支角色: `main` = Python 历史基线；`feature/agent-runtime` = 基线分支（旧代码 + Greenfield 地基，只读参考）；`uni-agent`（本分支）= Greenfield Runtime 架构框架（宪章 + Contract + Runtime 骨架 + 机械 Guard）。
- 当前阶段: `POST_DETERMINISTIC_SEMANTIC_RUNTIME_PROGRESS`（已毕业能力见 `docs/snapshots/latest.md`）；S1/S2/S3 是 proposal / 授权 gate 候选。
- 旧代码迁移: 从基线迁入必须先走 OpenSpec 决策，不在本分支复制旧控制结构。

## 2. Authority Order

规则冲突时按此顺序裁决（低优先级不得覆盖高优先级）：

```
1. Runtime Architecture Contract（docs/system/constitution/runtime-architecture-contract.md, I-1..I-14）
2. Approved OpenSpec Specs（openspec/changes/<change>/specs/**/*.md）
3. Architecture Decision Records（docs/decisions/）
4. AGENTS.md（本文件 — 导航与核心禁止）
5. Development Protocol（.ai/development-protocol.md）
6. Skills（.ai/skills/）
7. Existing Code（src/ 实现）
8. Agent Assumption（永远最低）
```

核心原则：

- **Skill 不产生架构权威**；**Agent 不得通过解释覆盖 Contract**（不变量不因"更灵活"或"测试方便"而改变）。
- **Code 是 evidence，不是真相源**；Existing implementation 不是架构真相。
- **Assumption 必须显式**，且永远最低优先级。
- 详细定义: `.ai/development-protocol.md` §1（规则类型排序；本文按来源排序，顶部一致）。

## 3. Where Is Truth

| 需要什么 | 去哪里读 |
|---|---|
| 上下文加载顺序 | `docs/context-loading-guide.md` |
| 顶层架构基线（v1，sole baseline） | `docs/architecture/README.md` + `uniagent-architecture-v1-core-development-guide.md` |
| Protocol v1 基线 | `docs/architecture/uniagent-protocol-v1-consolidation-design.md` |
| Repository Governance 基线 | `docs/decisions/repository-governance-authority-baseline.md` |
| RuntimeAgent 行为（60 节宪章） | `docs/system/greenfield-runtime-charter.md` |
| RuntimeAgent 边界契约（I-1..I-14） | `docs/system/constitution/runtime-architecture-contract.md` |
| Runtime 构建区地图 | `src/UniClaw.Runtime/AGENTS.md` |
| 测试区域地图 | `tests/UniClaw.Runtime.Tests/AGENTS.md` |
| OpenSpec 区域地图 | `openspec/AGENTS.md` + `.ai/openspec-workflow.md` |
| 共享开发协议 | `.ai/development-protocol.md` |
| Agent / model 路由 | `.ai/agent-routing.md` · `.ai/model-routing.yaml` |
| Agent 并行工作区隔离 | `.ai/agent-branch-workflow.md`（worktree / feature branch） |
| 通用 Agent Profile / UniFlow 工作流 | `.ai/profiles/` · `.ai/workflows/uniflow-coding-workflow.md` |
| Skill 注册与发现 | `.ai/skills/README.md` |
| 跨助手通用行为基线（仅行为原则） | `.ai/universal-agent-guideline.md` |
| 手动同步到个人全局指令 | `scripts/sync-universal-agent-guideline.sh`（默认预览，需显式 `--apply`） |
| 变更分级 | `.ai/change-classification.md` |
| Agent 交接协议 | `.ai/agent-message-contract.md` |
| 变更评审清单 | `.ai/reviews/change-review.md` |
| 决策 / 历史 | `docs/decisions/`（按需检索，不默认加载） |
| OpenSpec 进度（system of record） | `openspec/changes/` |
| C# 代码查询（MCP 优先） | `.ai/tooling/csharp-mcp-query.md` · `.mcp.json` |

**Runtime 入口（`Agent Runtime（新）— Greenfield`）**: 改 `src/UniClaw.Runtime/` 前读
`src/UniClaw.Runtime/AGENTS.md`（区域地图）+ 宪章 + Contract。机械约束:
`tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs`（零 ProjectReference /
禁旧 namespace / 导航存在）+ `scripts/check-consistency.sh`（宪章 60 节 / Contract 14 条）。

## 4. Universal Agent Baseline

通用行为原则唯一源文件：`.ai/universal-agent-guideline.md`；本入口不复制其内容，也不赋予其项目架构权威。

需要同步到个人全局指令时，由人显式运行 `bash scripts/sync-universal-agent-guideline.sh ... --apply`；脚本不挂接 hook、启动流程或定时任务。

### UniFlow 按需触发

`UniFlow` 是 Codex 与 DSH 共用的 Profile-based Coding Workflow 触发词。用户输入
`执行 UniFlow：<任务内容>`（或明确要求“按 UniFlow 执行”）时，Agent 才按需读取
`.ai/workflows/uniflow-coding-workflow.md`，并依任务选择 `.ai/profiles/`、WorkItem Schema
与相关模块上下文；未触发时不得仅因本入口存在而预加载这些正文。

触发后的固定语义是：识别 ModuleProfile / ExecutionProfile → 生成并校验一个包含
`semantic_brief` 的 WorkItem → 单播给一个匹配执行者（确定性操作使用 Tool Only）→
按 `acceptance` 验证。`UniFlow` 只是执行路由约定，不是架构、协议或 Runtime 权威。

## 5. Context Loading

非简单任务开始前，读 `docs/context-loading-guide.md` 并生成一次工作级 `PROJECT_CONTEXT_RESOLUTION`（只加载最小上下文）：

```text
Task Type / Current State / Relevant Architecture / Relevant Contract / Active Work /
Required Decision / Required Skill / Excluded Context / Known Facts / Unknowns /
Assumptions / Allowed Actions / Forbidden Actions / Verification Plan
```

- 只输出工作状态摘要，不要求输出 reasoning chain。
- 历史（Decision / Archive）默认不加载，按需检索。
- PCR 不建立或改变 Architecture Authority；不是 Contract / Decision / Gate。

## 6. Ownership Boundary

- 只修改负责该问题的层（domain 地图见 `src/UniClaw.Runtime/AGENTS.md`）。
- 不绕过 invariant（I-1..I-14，见 Contract）。
- 不隐藏失败 — 不改测试断言隐藏真实问题、不放宽 fail-closed（Debugging Gate 见 `.ai/skills/evidence-driven-debugging`）。
- 无明确 Owner 时停止修改代码，先输出分析。

## 7. Change Entry

变更分级见 `.ai/change-classification.md`（Small / Medium / Large）。
**Large（新 abstraction / 新 boundary / lifecycle change / architecture change）必须 OpenSpec + Human Gate**；
分类不确定时取更高一级；禁止把 Large 拆成 Medium 绕过 gate。

## 8. Verification

完成标准（Definition of Done）：

| 维度 | 标准 |
|------|------|
| Code | 修改存在 |
| Architecture | 未违反 invariant |
| Evidence | 有验证依据 |
| Test | 有对应 scenario/test（见 `tests/UniClaw.Runtime.Tests/AGENTS.md`） |
| Documentation | authority docs 同步 |

验证入口: `dotnet build src/UniClaw.Runtime.sln` · `dotnet test src/UniClaw.Runtime.sln` · `scripts/check-consistency.sh`。

## References

- Context loading: `docs/context-loading-guide.md` · Architecture index: `docs/architecture/README.md`
- Development protocol: `.ai/development-protocol.md` · Routing: `.ai/agent-routing.md` · `.ai/model-routing.yaml`
- OpenSpec: `.ai/openspec-workflow.md` · Change: `.ai/change-classification.md` · Handoff: `.ai/agent-message-contract.md`
- Skills: `.ai/skills/README.md` · `.ai/skills/evidence-driven-debugging` · `.ai/skills/runtime-behavior-debugging`
- Review: `.ai/reviews/change-review.md` · Runtime: `src/UniClaw.Runtime/AGENTS.md` · Tests: `tests/UniClaw.Runtime.Tests/AGENTS.md` · OpenSpec: `openspec/AGENTS.md`
- `.ai/` 是唯一可移植协议与 Skill 真相源；`.agents/skills/`、`.codex/`、`.dsh/` 与根 `CLAUDE.md` 只做 Host 发现/适配，不维护项目规则。
