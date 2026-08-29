# OpenSpec Workflow — 入口与触发

> 定位: OpenSpec spec-driven 生命周期入口 + 通用 AI Coder 触发规则。
> 上级: AGENTS.md「Where Is Truth」；详细生命周期见 `.ai/development-protocol.md` §4；
> 变更分级见 `.ai/change-classification.md`。

## 生命周期

`propose → apply → verify → archive` — 定义见 `.ai/development-protocol.md` §4（本文不重复）。

要点：规格定义 WHAT (SHALL/MUST)，design 定义 HOW，tasks 定义 STEPS；工作单位是
change（specs + design + tasks），不是孤立的任务；`openspec/changes/` 是变更进度
权威来源（活跃 change 的 `tasks.md` 记录实施清单和完成状态）；不在 OpenSpec 中的
工作需特别说明。

### OpenSpec 与 WorkItem 边界

OpenSpec change 是变更与规格真相，不反向绑定执行 WorkItem。active
`openspec/changes/` 不得链接 `docs/work/active/workitems/`，也不得嵌入具体
`WI-*` 执行编号；WorkItem 可以在自己的 `anchors` / `read_hints` 中单向引用
OpenSpec。`openspec/changes/archive/` 中已有的 WorkItem 编号属于历史执行证据，
保留但不构成当前关联。

## 入口

- **提出变更**: `openspec propose <change-or-topic>`
- **执行变更**: `openspec apply <change>`
- **探索需求**: `openspec explore <topic>`
- **归档完成**: `openspec archive <change>`
- **收尾同步**: `python3 scripts/finalize-change.py <change> [--archive] [--workitem CS-XXX]`
  （tasks 完成度检查 → `regenerate-projections.py` 再生投影 → 归档 git mv →
  `archive-workitems.py` 联动。新增/归档 change 后必须跑一次，防止
  current-gates/projection 与 source 漂移——见 check-consistency C11）

## 通用触发规则

用户以自然语言触发以下操作时，所有 Host 都按同一 OpenSpec 生命周期处理，并读取
`.ai/skills/` 下对应 playbook：

| 通用触发语 | 行为 | 必读 playbook |
|-------------|------|---------------|
| `openspec propose <change-or-topic>` / `按 OpenSpec propose ...` | 创建或补全 `openspec/changes/<change>/` 下的 proposal/design/specs/tasks | `.ai/skills/openspec-propose/SKILL.md` |
| `openspec apply <change>` / `按 OpenSpec apply ...` | 读取 change artifacts，按 `tasks.md` 实施，完成一项立即勾选 `- [x]` | `.ai/skills/openspec-apply-change/SKILL.md` |
| `openspec explore <topic>` / `按 OpenSpec explore ...` | 只做需求探索、方案澄清和上下文整理；除非用户明确要求，不改代码 | `.ai/skills/openspec-explore/SKILL.md` |
| `openspec archive <change>` / `按 OpenSpec archive ...` | 完成归档、提取 decisions、同步主规格 | `.ai/skills/openspec-archive-change/SKILL.md` |

## 执行约定

- OpenSpec artifacts 是跨助手共享真相源；活跃变更看 `openspec/changes/<change>/`，
  已归档变更看 `openspec/changes/archive/`。
- 某个 Host 的快捷命令只能映射到以上自然语言语义，不得成为第二份生命周期协议。
- apply 前必须读取该 change 的 `proposal.md`、`design.md`、`tasks.md`、`specs/**/*.md`；
  实现后同步更新 `tasks.md`。
- 新增 change（active 成员变化）或 `openspec archive` 之后，运行
  `scripts/finalize-change.py <change> [--archive] [--workitem CS-XXX]`
  统一执行投影再生与 workitem 联动；不得手工编辑 current-gates/latest
  的成员计数（派生投影，见 `docs/work/active/current-gates.md` 文件头）。
