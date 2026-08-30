## Why

仓库当前把一部分通用 Skill、OpenSpec playbook、C# MCP 指南和一致性检查放在
`.claude/` 下，同时让 DSH 的 Skill adapter 指回该目录。这使 Claude Code 的 Host
约定与项目通用协议混在一起，也让 DSH 看起来像在消费 Claude 协议，而不是与
Codex 一样消费统一的 WorkItem / Profile / Skill 契约。

用户已明确授权清理 Claude 项目配置，并要求迁移过程稳定、可回滚，最终支持
Codex、DSH 以及采用通用文件协议的其他 AI Coder。

## What Changes

- 将 `.ai/` 固定为项目可移植协议、Profile、Workflow 与 Skill 正文的唯一真相源。
- 将 `.agents/skills/` 固定为通用 Skill 发现层；Codex 与支持该约定的 AI Coder
  直接消费它，DSH 仅在自身扫描约束要求时保留 `.dsh/skills/` 相对链接 adapter。
- 把 5 个仍在 `.claude/skills/` 的项目 Skill 迁入 `.ai/skills/`，移除 Claude
  专属命令和工具措辞，不改变其方法边界或 Authority。
- 把 C# MCP 查询指南迁入 `.ai/tooling/`，更新当前协议、文档、Validator 和机械
  Guard 的引用。
- 删除仓库 `.claude/` 配置、旧 Agent、Hook、命令与重复路由；根 `CLAUDE.md`
  仅保留指向 `AGENTS.md` 的无状态 Host adapter。
- 保留历史 Decision / Archive 的原始 Claude 引用作为历史证据；它们不得被当前
  解析器或运行入口当作真相源。

## Capabilities

### New Capabilities

- `universal-ai-coder-protocol`: 定义唯一通用协议源、通用 Skill 发现层、Host
  adapter 边界、DSH 消费方式和 Claude 项目配置退役条件。

### Modified Capabilities

- `uniflow-required-skill-propagation`: required Skill 只从 `.ai/skills/` 唯一解析，
  `.agents/skills/` 与 `.dsh/skills/` 仅用于 Host 发现。

## Impact

- `AGENTS.md`、`CLAUDE.md`、`.ai/`、`.agents/skills/`
- `.codex/` 与 `.dsh/` Host adapter 文档/链接
- `tools/agent_profile_validator.py`、`scripts/setup-dsh-skills.sh`、
  `scripts/check-consistency.sh`
- `openspec/AGENTS.md`、当前 OpenSpec 与当前知识投影
- 删除 `.claude/`

不修改 Runtime、Perception、Architecture Contract、产品协议、生命周期语义或
模型 Provider 可用性。迁移前备份已经生成并校验。
