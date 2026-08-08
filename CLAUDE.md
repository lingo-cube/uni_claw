# CLAUDE.md — Claude Code 入口

> 本文件是 Claude Code 的轻量适配层。
> 共享项目协议、架构约束、上下文路由和开发流程请先阅读 `AGENTS.md`。
> 最后更新: 2026-08-08

## 共享规则

Claude Code 进入本仓库后，必须把 `AGENTS.md` 作为项目规则单点入口。
不要在本文件复制 `AGENTS.md` 的规则内容；规则变更应优先更新 `AGENTS.md`。

## Claude 专属扩展

- MCP 查询规则单点来源：`.claude/MCP-QUERY.md`
- OpenSpec 命令编排规则：`.claude/commands/opsx/AGENT.md`
- 跨 Codex / Claude 的 agent 角色路由：`.ai/agent-routing.md`
- 跨 Codex / Claude 的模型档位配置：`.ai/model-routing.yaml`
- Claude skills：`.claude/skills/`
- Claude workflows：`.claude/workflows/`
- 编辑前上下文提醒 hook：`.claude/hooks/context-routing.sh`

## Skills / Workflows 说明

Claude Code 可继续使用 `.claude/skills/` 与 `.claude/workflows/` 中的现有入口。
Codex 侧的共享协议在 `AGENTS.md`；如需复用 Claude skill，优先读取对应 `SKILL.md`/`skill.md` 作为项目 playbook，而不是维护第二份副本。
Claude custom agent 的 frontmatter `model` 只表达平台档位（`opus` / `sonnet` / `haiku`）；背后 provider 与 fallback 链以 `.ai/model-routing.yaml` 为准。
