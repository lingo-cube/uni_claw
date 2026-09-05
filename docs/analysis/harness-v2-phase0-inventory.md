# Harness V2 — Phase 0 Inventory（Development Harness 现状盘点）

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `COMPLETE / EVIDENCE-BACKED`（只读调查产物，未修改任何 Harness 文件）
> Authority: `NONE`（本文件是 inventory，不是契约、决策或授权）
> Scope: AI Coding / Development Harness 全部组成（Instruction / Protocol / Workflow / Skill /
> Profile / Routing / Adapter / OpenSpec / Verification / Evidence / Session）。
> 调查基线: `uni-agent` 谱系（`0771d7d` + `47aab75`）；调查日期 2026-09-05。
> 本分支（uni-harness）已重建为仅含基线文档与流程文件的精简分支。
> 禁止边界: 本文件不授权任何删除/重写/迁移动作；Product Architecture（src/tests/platforms）
> 仅作为边界标注，不入 Harness 盘点。

调查方法：4 个并行只读 subagent（Skills / 契约 / OpenSpec / 适配层）+ 第一手阅读核心协议
（development-protocol.md 731 行、uniflow-coding-workflow.md 264 行、context-loading-guide.md、
change-classification.md、agent-branch-workflow.md、universal-agent-guideline.md、嵌套
AGENTS.md、tools/ 结构）。所有结论以 repo 当前符号与文件为准，不采信旧完成报告。

## 0. 分支与基线状态

- `uni-harness` = `uni-agent@0771d7d` + 1 commit（`47aab75`，4 个产品架构基线文档，
  docs/analysis/，1962 行）。**分支上没有任何 Harness V2 内容**——本目录三份文档是第一批。
- `uni-agent` 在途产品工作已本地提交为 `ab70f82`（90 文件，container-runtime-v2 至
  task 3.4 + 4.1 interim），不在 uni-harness 上。产品线与 harness 线物理隔离。

## 1. Instruction 层

| 工件 | 角色 | 事实 |
|---|---|---|
| 根 `AGENTS.md` | SSOT 入口（map, not manual） | Authority Order 7 级；UniFlow 触发词（§4）；变更分级入口；验证入口；`.ai/` 为可移植协议唯一源 |
| `CLAUDE.md` | Claude host 适配器 | 纯指针（322B），无任何协议真相；`check-consistency.sh` C9 机械强制 |
| 嵌套 AGENTS.md ×6 | 模块局部 map | `src/UniClaw.Runtime/`（Runtime Agent Map）、`tests/UniClaw.Runtime.Tests/`（Test Area Map）、`openspec/`、`docs/decisions/`、`docs/decisions/runtime-debugging-casebook/`、`docs/analysis/`（KNOWLEDGE_ROUTING_RULE，本目录） |
| `.ai/universal-agent-guideline.md` | 跨平台行为基线 | 仅 8 行 4 原则；`scripts/sync-universal-agent-guideline.sh` 手动同步个人全局指令（无 hook） |
| validator 内解析 | 目标路径生效 AGENTS.md | `tools/agent_profile_validator.py` L189 解析 `AGENTS.override.md` / `AGENTS.md` 进入 Worker context manifest |

## 2. Protocol 层（`.ai/`，19 项）

| 文件 | 行数 | 职责（实证） |
|---|---|---|
| `development-protocol.md` | 731 | 权威排序（§1: invariant→OpenSpec SHALL→scenario→charter→原则）；Two-Lane（Semantic Discovery / Fast Lane + 6 类 Hard Gate）；provider 中立路由（Sol/Luna、Opus/Haiku）；**§4 OpenSpec 生命周期强制**；Phase Boundary；Human Gate 七类 material boundary；失败分类；Scenario Receipt；验证节奏；**§17 Evidence-Driven Workflow（L0-L4 任务分级 / E0-E4 证据分级 / Worker 7 步 / 输出格式 / Review 四象限）** |
| `task-contract.md` | 219 | WorkItem 之上的 Scenario 生命周期层（DevelopmentLane / HardGatePolicy / AcceptedSemanticEnvelope / AutoContinue / EvidenceAssetPlan） |
| `result-contract.md` | 325 | WorkResult 详细语义 + Fast Lane 状态机（IMPLEMENTED/TEST_FAILED/REPAIR_REQUIRED…）+ AUTOPILOT/HUMAN_DECISION/VALIDATION/PHASE_CONTROLLER 包；**DONE ≠ 完成（L28-30，canonical 完成归 Leader）** |
| `agent-message-contract.md` | — | Agent 交接 8 字段（Goal/Context/Facts/Unknowns/Decision/Constraints/Expected Result/Verification） |
| `leader-decision-contract.md` | — | H4-1：Result→Leader Decision（10 种 decision_type）→Next Action 机器可读接口 |
| `auto-continue-contract.md` | — | H4-3：Scenario 内自动继续循环 + 16 条强制停机条件 |
| `scenario-trigger-contract.md` | — | H4-2：Scenario 触发→唯一 Leader Decision（9 步解析；含 RECONCILE_SPEC/GENERATE_TASKS OpenSpec 状态机，16 处引用） |
| `change-classification.md` | 37 | Small/Medium/Large；**Large 必须 OpenSpec + Human Gate**；不确定取高一级 |
| `agent-branch-workflow.md` | 65 | ≥2 agent 并行 → git worktree 隔离（`scripts/agent-worktree.sh`） |
| `openspec-workflow.md` | 57 | propose→apply→verify→archive + finalize-change.py 收尾强制 |
| `agent-routing.md` | — | model-routing.yaml 的人读解释 + Lane 路由 + dispatch 规则 |
| `model-routing.yaml` | 341 | v5：canonical_roles（PROJECT_LEADER/EXECUTION_WORKER，16 may/14 must-not-commit）+ tiers（leader/expert/standard/fast）+ providers（openai/codex=Sol/Luna；anthropic=Opus/Haiku）+ roles→tier + `codex_profile_adapters` ×4 + policy（禁 silent downgrade；fallback 耗尽=ROUTING_UNAVAILABLE） |
| `profiles/execution.json` | — | 5 个 ExecutionProfile（development/test-authoring/verification/semantic-analysis/tool-only），permissions 矩阵细至 read/write/spawn_agent |
| `profiles/roles.json` | — | 2 个 RoleProfile（coding-leader v1.1.0 / module-worker v1.0.0） |
| `profiles/modules.json` | — | **5 个** ModuleProfile（runtime-core/runtime-integration/semantic-capability/engineering-governance/perception-platform），含 owned_paths/test_gates/context_sources |
| `schemas/work-item.schema.json` | — | WorkItem 17 字段（详见 Phase 1 §3） |
| `schemas/work-result.schema.json` | — | WorkResult 契约（详见 Phase 1 §5） |
| `workflows/uniflow-coding-workflow.md` | 264 | UniFlow v1.0.0（详见 §3） |
| `workflows/codex-coding-workflow.md` | 7 | Compatibility Pointer 空壳 |
| `reviews/change-review.md` + `runtime-change-review.md`、`tooling/csharp-mcp-query.md`、`skills/README.md` | — | 评审清单 ×2；C# MCP 查询指南；Skill 注册声明 |

## 3. Workflow 层：UniFlow v1.0.0

- **触发**（§0）：`执行 UniFlow：<任务>`；未触发不预载。
- **Profile 模型**（§1）：`AgentProfile = RoleProfile + ExecutionProfile + Optional ModuleProfile`；
  ModelBinding 独立于 model-routing.yaml。
- **Leader 12 步**（§4）：Tool Only → Reality Preflight → ModuleProfile → ExecutionProfile →
  required_skills → 冻结 principles/contract/acceptance → 生成 WorkItem → 校验 →
  **单播（禁 fanout）** → WorkResult 独立核对 → 接受后应用 ModuleContext Delta → 最终验证。
- **上下文**（§5）：validator `context` 子命令生成 manifest（AGENTS + Skill 正文 +
  selectors + gates + ProfileContextKey）；禁止扫描全部 OpenSpec/Decisions。
- **WorkItem/WorkResult**（§6）：semantic_brief 优先级链 Contract→change_principles→
  forbidden→acceptance→scope→semantic_brief；冲突返回 `BLOCKED_FOR_SPEC/RULE_CONFLICT`。
- **Worker 上报**（§6.1）：5 类必上报 + Leader 三裁决（A 自理/B 新 WorkItem/C 冻结重派）。
- **路由表**（§7）：确定性→Tool Only；单模块→Luna worker；权威决策→Sol Leader。
- **缓存**（§8）：`ProfileContextKey`（键一致可复用 Worker thread）。
- **DSH 边界**（§10）：DSH 消费同一 Profile/WorkItem；无独立委派能力时主执行者内联并记录限制。

## 4. Skill 层

- Canonical：`.ai/skills/` 18 个（README 声明唯一正文源）。详见 Phase 2 矩阵。
- 适配：`.agents/skills/`、`.dsh/skills/` 各 18 个**相对符号链接**，diff 零发散。
- 同步：`scripts/setup-dsh-skills.sh`（幂等、fail-closed、不自动触发）+
  `check-consistency.sh` C13（pre-commit 守护链接精确性）。
- 消费：Codex = validator 直读 `.ai/skills`（`SKILL_SOURCE_DIRS`，L17）；DSH = 扫描
  `.dsh/skills` + payload 携带完整正文与 fail-closed 指令。
- 结构：仅 evidence-driven-debugging 有 references/（10 文件+fixtures）；openspec-×4
  外部生成（generatedBy 1.3.1）；uniagent-evolution-loop 缺 metadata 块。

## 5. Adapter 层

### Codex（`.codex/`）
- `config.toml`（58 行）：5 agent 注册；`max_concurrent_threads_per_session=8`；
  `default_subagent_model=gpt-5.6-luna`；`default_subagent_reasoning_effort="high"`（L5）；
  2 个 MCP server（csharper-mcp、cwm-roslyn-navigator，approval auto）；含绝对路径 cwd。
- 5 个 agent TOML：全部 `model="gpt-5.6-luna"`；instructions **自含**完整 UniFlow 语义
  （WorkItem 校验/Skill 解析/优先级链/WorkResult 格式，×4 近重复）；sandbox 分级
 （module-worker/test-author=workspace-write，verifier=仅测试产物，semantic-analyzer/
  openspec-researcher=read-only）。

### DSH（`.dsh/` + `tools/dsh_profile_adapter.py`）
- `profile-adapter/profile-source.yaml`：DSH 作为 Profile Core 消费者——sha256
  `source_revision` 钉扎、`validation_command`、4 组 `model_bindings`（decision_frontier:
  zai glm-5.2 / fallback opencode-go；implementation_efficient 与 semantic_read:
  deepseek-v4-flash；tool_only: none）；声明不复制 Profile 语义。
- `tools/dsh_profile_adapter.py`（79KB）：ProfileSource→Adapter→ModelBinding→Router/
  Scheduler/ResultGate/Checkpoint/Events；`dispatch` 唯一派发入口（原子 record +
  `PENDING_SESSION_SPAWN` 回执）；receipt 从 Host session 日志重建核对；v2 run 级状态
  （`state/sessions/<sid>/runs/<rid>/`）。
- `dsh-plugin-uniclaw/`：独立 npm 包，DSH→DriverHost **产品控制面**适配器（5 只读命令 +
  Shadow Cognition）。与 profile-adapter 无直接关系——DSH 侧两条独立适配路径。

## 6. OpenSpec 层

- 生命周期：propose→apply→verify→archive（development-protocol §4 强制；Large 必经）。
- 脚本链：`finalize-change.py`（收尾：勾选检查→归档→投影再生→可选 workitem 归档）→
  `regenerate-projections.py`（再生 current-gates.md / snapshots/latest.md）→
  `archive-workitems.py`。
- 规模：**13 active**（2 个未开工：runtime-debug-post-graduation-conformance-repair 0/11、
  uniagent-local-exploration-memory 0/31；1 个已完成待归档：perception-ocr-en-v4-
  normalization 23/0）+ **81 archived**（日期前缀）+ **`openspec/specs/` 78 个能力主干
  spec 目录**（V2 提取 Decisions/Contracts/Invariants/Acceptance 的主要来源）。
- 顶层无 project.md；`config.yaml`（schema: spec-driven）。
- **最硬锁点**：`.git/hooks/pre-commit`（唯一非 sample hook）→ check-consistency.sh，
  其中 C11（投影↔openspec 一致）/C15（active 不含 WI-*）间接强制 OpenSpec 结构；
  不改 C11/C15 直接删 openspec/ 会阻断所有提交。

## 7. Verification 层

| 门 | 内容 | 触发 |
|---|---|---|
| pre-commit（快门） | `git diff --cached --check`（空白/冲突标记）+ check-consistency.sh C1-C15 | git commit 自动 |
| `verify-before-commit.sh`（慢门） | + AgentWorkflow pytest + profile pin 核对；`--dotnet` 附加 build | 人显式运行；worktree 合入前必跑 |
| `check-consistency.sh` | 宪章 60 节/14 invariant 计数、skill 链接 C13、投影一致 C11/C12、`.claude` 退役 C9 等 | 两侧门调用 |
| 产品验证 | `dotnet build/test src/UniClaw.Runtime.sln`（0 warning） | 变更分级对应 |
| Harness 自身测试 | `tests/AgentWorkflow/`（pytest，validator 语义） | 慢门 |

## 8. Evidence / State 工件层

- `openspec/changes/<change>/evidence/`（任务级 RESULT/REVIEW 记录 + artifacts/）。
- `docs/work/active/`：current-gates.md、workitems/*.json（WI-CRV2-* 等）、gate 记录。
- `docs/snapshots/latest.md`（GeneratedProjection，C11/C12 机械核对计数）。
- `docs/decisions/`（+ runtime-debugging-casebook）、`docs/failures/index.md`。
- L1-L4 测试资产分级 + Scenario Receipt 机制（development-protocol §17/§10）。

## 9. 已核验的不一致 / 风险事实清单（Phase 1、Phase 3 引用）

| # | 事实 | 证据 |
|---|---|---|
| F1 | modules.json 有 5 个 ModuleProfile，workflow §2 表只列 4（perception-platform 无文档） | `.ai/profiles/modules.json` vs workflow L36-47 |
| F2 | workflow §3 称默认 effort `medium`，config 实为 `high` | workflow L59 vs `.codex/config.toml` L5 |
| F3 | UniFlow 语义三处平行维护：Codex TOML instructions ×4 + validator + dsh adapter | `.codex/agents/*.toml`、`tools/` |
| F4 | 共享路由层内嵌 host 专有块：model-routing.yaml `codex:` 块（L123-214）；agent-routing L90 列 Codex TOML 路径 | `.ai/model-routing.yaml`、`.ai/agent-routing.md` |
| F5 | 双 host 模型绑定源不对称：Codex TOML 硬编码 gpt-5.6-luna vs DSH profile-source.yaml model_bindings（zai/deepseek） | 两适配层 |
| F6 | 两条 Skill 消费路径（validator 直读 vs DSH 扫描+payload）无统一一致性校验 | §4 |
| F7 | WorkItem schema 无 `dependencies` 字段；依赖靠 H4-1 派发条件 + 文字串行规则 | work-item.schema.json |
| F8 | 完成判定分散：WorkResult DONE ≠ 任务完成；canonical 完成归 Leader | result-contract L28-30 |
| F9 | ProfileContextKey 允许 thread 复用，与 Fresh Context 方向相反 | workflow §8 |
| F10 | OpenSpec 流程性依赖约 20 份协议文件（最深：scenario-trigger 16 处、auto-continue 10 处、leader-decision 8 处） | Phase 1 附录 |
| F11 | `.codex/config.toml` 含绝对路径 cwd，不可移植 | config.toml |
| F12 | 符号链接依赖 git core.symlinks；同步为手动分批（mtime 三批） | setup-dsh-skills.sh |
| F13 | 双轨生命周期并存：UniFlow 派发协议 vs H4-1/2/3 Scenario 循环协议 | §2 |
| F14 | codex-coding-workflow.md 为空指针残壳 | §2 |
| F15 | openspec-×4 外部生成格式发散；uniagent-evolution-loop 缺 metadata | §4 |

## 10. Harness / Product 边界

- **Harness（本迁移对象）**：`.ai/`、`.agents/`、`.codex/`、`.dsh/`、`openspec/`、
  `scripts/`、`tools/`（validator + 两个 adapter + runtime-debug*）、`CLAUDE.md`、
  `AGENTS.md`（根+嵌套）、`docs/` 治理子集（architecture/decisions/snapshots/work/
  context-loading-guide/knowledge-map）、`tests/AgentWorkflow/`。
- **Product（不动）**：`src/`、`tests/UniClaw.Runtime.Tests/`、`platforms/`、
  `semantic-assets/`、`validation/`、`init/`、产品侧 docs。Uni-harness 分支上的 4 份
  产品架构基线文档属产品线背景资料，不是 Harness 组成。

—— 证据来源：4 份 subagent 只读报告 + 本会话第一手阅读与逐项核验（含 F2 矛裁决断）。
