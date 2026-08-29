# UniAgent Evolution Skill — Codex Discovery Human Gate

DocumentType: `HUMAN_GATE_PACKET`
Date: 2026-08-28
Authority: `NONE`
WorkItem: `WI-UEL-CODEX-001`
Disposition: `AUTHORIZED_IMPLEMENTED_VERIFIED`

## Human Decision

2026-08-28，Human 授权采用推荐方案：保留 `.ai/skills/uniagent-evolution-loop`
作为唯一正文，在 `.agents/skills/uniagent-evolution-loop` 创建项目内相对符号链接，
并同步仓库治理规则与机械守护。该授权不扩展到 Runtime、Perception、OpenSpec、
Strategy Contract、GoalEvidence 或 SourceIdentity。

## Goal

让 Codex 在 UniClaw 项目中原生发现 `.ai/skills/uniagent-evolution-loop/SKILL.md`，同时保持 `.ai/skills` 为 Skill 单一来源，不复制正文。

## What changed or was discovered

- Skill 本体已创建并通过 `quick_validate.py`。
- 先后验证 `[[skills.config]] path = "../.ai/skills/uniagent-evolution-loop"` 与 `path = ".ai/skills/uniagent-evolution-loop"`。
- 两次 `codex debug prompt-input` 均成功生成 Host prompt，但 Available skills 中都不存在 `uniagent-evolution-loop`。
- 因此 `skills.config` 在当前 Codex Host 上只对已发现 Skill 提供启停覆盖，不能把 `.ai/skills` 注册为项目 Skill 根。
- OpenAI Codex 的仓库级原生发现根是 `.agents/skills`；项目 `.ai/skills/README.md` 当前明确禁止创建 `.agents/skills`。
- 无效的 `skills.config` 与仅验证文本存在的伪集成测试已撤回；没有留下误导性配置。

## First Divergence Point

Codex Host 构造 Available skills 列表时没有扫描或注册 `.ai/skills/uniagent-evolution-loop`，早于 Skill 激活与工作流执行。

## Validation limitation

- Skill `quick_validate.py`、WorkItem validator、profile validator、`scripts/check-consistency.sh` 与 `git diff --check` 均通过。
- 全量 `tests/AgentWorkflow` 已执行但未全绿：121 tests 中 9 failures / 20 errors，首要原因是沙箱拒绝写既有 `.dsh/profile-adapter/state/events.jsonl`，以及 DSH profile source 固定 revision `e2d8dd4...` 与当前 HEAD `e6c6f4b...` 漂移。该测试族失败没有通过本次 Skill 工作绕过或修复，且不属于 perception S1 checkpoint。

## Owner

`engineering-governance`：这是 Codex 项目 Skill 发现适配与仓库 Skill 单一来源规则之间的冲突，不属于 Runtime、Perception、Strategy Contract、GoalEvidence 或 SourceIdentity。

## Architecture impact

Runtime / Protocol / Agent authority：`NONE`。

Repository governance：需要明确是否允许 `.agents/skills/<name>` 作为指向 `.ai/skills/<name>` 的相对符号链接适配层，并同步 `.ai/skills/README.md` 与一致性测试。

## Material trade-off

- 允许相对符号链接：Codex 获得原生发现，Skill 正文仍只有 `.ai/skills` 一份；但需要修改当前“禁止创建 `.agents/skills`”规则。
- 保持当前禁止：Skill 可继续作为共享协议源和 DSH 候选，但 Codex 无法原生 `$uniagent-evolution-loop` 发现。
- 改做 Codex Plugin 或用户级安装：范围和维护成本更高，也弱化项目隔离，不建议作为第一选择。

## Exact decision required

是否授权：创建 `.agents/skills/uniagent-evolution-loop` 相对符号链接指向 `../../.ai/skills/uniagent-evolution-loop`，并将 `.ai/skills/README.md` 的禁止规则收敛为“禁止复制 Skill 本体；允许受一致性检查保护的 Codex 发现符号链接”。

Human 已明确授权上述相对符号链接方案；Plugin、MCP 与用户全局安装仍不在授权范围内。

## Implementation Evidence

- `.agents/skills/uniagent-evolution-loop` 是相对符号链接，精确指向
  `../../.ai/skills/uniagent-evolution-loop`；Skill 正文仍只有 `.ai/skills` 一份。
- `quick_validate.py`：PASS。
- focused `tests/AgentWorkflow/test_codex_skill_discovery.py`：4/4 PASS。
- `tools/agent_profile_validator.py validate`：`AGENT_WORKFLOW_VALIDATION_PASS`。
- `scripts/check-consistency.sh`：C1–C13 全部 PASS；C13 守护相对链接、目标可达与无正文副本。
- `codex debug prompt-input`：PASS；Available skills 已包含
  `uniagent-evolution-loop`，source locator 为项目 `.agents/skills` 路径。
- `git diff --check`：PASS。

该 Gate 到此关闭；未实施 Plugin、MCP、用户全局安装，也未进入 Runtime、Perception、
OpenSpec、Strategy Contract、GoalEvidence 或 SourceIdentity。
