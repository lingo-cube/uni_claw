# CLAUDE.md — Claude Code Adapter

> This repository uses **AGENTS.md as the single source of truth**.
> Claude Code must read and follow `AGENTS.md`.
>
> 本文件不维护项目规则 — 架构、编码、Runtime、工作流规则一律在
> `AGENTS.md` / `.ai/` / `docs/`。发现本文件出现重复项目指令时，迁移到 `AGENTS.md` 或 `.ai/`。
> 最后更新: 2026-08-23

## Claude-Specific Extensions

仅保留 Claude Code 平台专属内容：

- MCP 查询规则单点来源：`.claude/MCP-QUERY.md`
- OpenSpec 命令编排规则：`.claude/commands/opsx/AGENT.md`
- Claude skills：`.claude/skills/`；Claude workflows：`.claude/workflows/`
- 编辑前上下文提醒 hook：`.claude/hooks/context-routing.sh`
- Claude custom agent frontmatter `model` 只表达平台档位（`opus` / `sonnet` / `haiku`）；
  背后 provider 与 fallback 链以 `.ai/model-routing.yaml` 为准
- 跨 Codex / Claude 的角色路由：`.ai/agent-routing.md`（共享，不在此复制）
