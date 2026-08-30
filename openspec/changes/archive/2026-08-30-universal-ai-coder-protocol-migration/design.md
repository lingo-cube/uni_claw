## Context

当前仓库已经有可移植的 `.ai` Profile Core、WorkItem / WorkResult schema、UniFlow
workflow 和通用行为基线，但 `.claude` 同时承载了三类不同内容：Claude Host
配置、可移植 Skill 正文、通用 MCP 指南。DSH 又通过链接消费其中的 Skill，形成
不必要的协议歧义。部分 Claude Agent、Hook 和权限配置还引用旧路径、旧测试数量
与旧 Runtime 结构，不应迁移为新真相源。

## Goals / Non-Goals

**Goals:**

- 一个 portable core：`.ai`。
- 一个跨 Host 的项目 Skill 发现层：`.agents/skills`。
- Codex、DSH 和其他 AI Coder 只增加必要 adapter，不复制语义。
- 所有当前入口、Validator 和 Guard 都能机械证明不再依赖 `.claude`。
- 删除前有可读备份，删除后可由 Git 或备份恢复。

**Non-Goals:**

- 不建立新的 Runtime、Agent、模型路由或生命周期权威。
- 不把 Claude Agent 的旧代码路径、测试数字、权限白名单或 Hook 逻辑迁入 `.ai`。
- 不改写历史 Decision、Archive 或历史证据中的 Claude 引用。
- 不宣称任何 Host 已实际执行；Host execution 仍需独立 receipt。

## Decisions

### D1 — `.ai` 是唯一 portable core

项目协议、Profile、Workflow、Schema、Skill 正文与平台无关工具指南只在 `.ai`
维护。根 `AGENTS.md` 是唯一项目规则入口；Skill 仍为 `Authority: NONE`。任何 Host
目录不得重新定义 scope、ownership、permissions、contract 或 lifecycle。

### D2 — `.agents/skills` 是发现层，不是第二份正文

`.agents/skills/<name>` 只能是指向 `../../.ai/skills/<name>` 的项目内相对符号
链接。支持该约定的 Codex 和其他 AI Coder 直接发现这些链接。DSH 若因 Host 扫描
规则需要 `.dsh/skills`，仅保留同样指向 `.ai/skills` 的相对链接；DSH 不拥有第二
套 Skill 语义。

### D3 — Skill 解析与 Host 发现分离

UniFlow Validator 只从 `.ai/skills/<name>/SKILL.md` 解析 canonical body。零匹配、
非法名称、frontmatter 不一致、不可读或重复 required Skill 都 fail-closed。
`.agents`、`.dsh` 和任何 Host 目录不能成为 Validator truth source。

### D4 — 只迁移仍有效的通用内容

5 个 Skill 保留原有目标、方法、测试和安全边界，但将 Claude slash command、
`AskUserQuestion`、`TodoWrite` 等专属名称改成平台无关表达。C# MCP 指南迁入
`.ai/tooling`。旧 Claude Agent、Hook、权限 allowlist、model routing 和 command
wrapper 不迁移，因为它们包含过时或重复语义。

### D5 — 根 `CLAUDE.md` 仅为无状态兼容入口

删除 `.claude/` 后保留最小 `CLAUDE.md`，只要求读取并遵循 `AGENTS.md`，且明确
不在该文件维护协议、Skill、路由、权限或工作流真相。这允许不完全支持通用发现
协议的 Claude Host 仍能进入统一入口，但不会形成第二份配置。

### D6 — 当前来源与历史证据分开治理

当前执行入口、活动 OpenSpec、Validator、脚本、指南和投影不得依赖 `.claude/`。
历史 Decision / Archive 保留原文；机械检查使用明确排除清单，防止为了“零文本
命中”篡改历史证据。

## Migration Plan

1. 建立并校验时间戳备份。
2. 迁移 Skill 与 MCP 指南，先更新 canonical 解析和两个发现 adapter。
3. 更新当前协议、活动 OpenSpec、文档和机械检查。
4. 将 `CLAUDE.md` 缩减为兼容入口并删除 `.claude/`。
5. 运行 Skill、Validator、AgentWorkflow、OpenSpec、一致性和引用扫描。

## Rollback

若任一验证失败，可用 Git 恢复已跟踪文件，或从本次记录的 tar.gz 备份按精确路径
恢复。回滚不得覆盖迁移开始前工作区中其他未提交修改。
