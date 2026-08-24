# OpenSpec Workflow — 入口与触发

> 定位: OpenSpec spec-driven 生命周期入口 + Codex 触发规则（跨助手共用）。
> 上级: AGENTS.md「Where Is Truth」；详细生命周期见 `.ai/development-protocol.md` §4；
> 变更分级见 `.ai/change-classification.md`。

## 生命周期

`propose → apply → verify → archive` — 定义见 `.ai/development-protocol.md` §4（本文不重复）。

要点：规格定义 WHAT (SHALL/MUST)，design 定义 HOW，tasks 定义 STEPS；工作单位是
change（specs + design + tasks），不是孤立的任务；`openspec/changes/` 是变更进度
权威来源（活跃 change 的 `tasks.md` 记录实施清单和完成状态）；不在 OpenSpec 中的
工作需特别说明。

## 入口

- **提出变更**: `/opsx:propose` 或 `/openspec-propose`
- **执行变更**: `/opsx:apply` 或 `/openspec-apply-change`
- **探索需求**: `/opsx:explore` 或 `/openspec-explore`
- **归档完成**: `/opsx:archive` 或 `/openspec-archive-change`

## Codex 触发规则

Codex 不原生执行 Claude slash command。用户在 Codex 中提到以下自然语言触发语时，
按 OpenSpec 生命周期处理，并优先读取对应 `.claude/skills/openspec-*` playbook：

| Codex 触发语 | 行为 | 必读 playbook |
|-------------|------|---------------|
| `openspec propose <change-or-topic>` / `按 OpenSpec propose ...` | 创建或补全 `openspec/changes/<change>/` 下的 proposal/design/specs/tasks | `.claude/skills/openspec-propose/SKILL.md` |
| `openspec apply <change>` / `按 OpenSpec apply ...` | 读取 change artifacts，按 `tasks.md` 实施，完成一项立即勾选 `- [x]` | `.claude/skills/openspec-apply-change/SKILL.md` |
| `openspec explore <topic>` / `按 OpenSpec explore ...` | 只做需求探索、方案澄清和上下文整理；除非用户明确要求，不改代码 | `.claude/skills/openspec-explore/SKILL.md` |
| `openspec archive <change>` / `按 OpenSpec archive ...` | 完成归档、提取 decisions、同步主规格 | `.claude/skills/openspec-archive-change/SKILL.md` |

## 执行约定

- OpenSpec artifacts 是跨助手共享真相源；活跃变更看 `openspec/changes/<change>/`，
  已归档变更看 `openspec/changes/archive/`。
- Claude 的 `/opsx:*`、`/openspec-*` 是 Claude Code 专属命令；Codex 遇到这些写法时，
  将其解释为对应自然语言 OpenSpec 请求。
- apply 前必须读取该 change 的 `proposal.md`、`design.md`、`tasks.md`、`specs/**/*.md`；
  实现后同步更新 `tasks.md`。
