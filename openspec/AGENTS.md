# OpenSpec Area Map — openspec/

> 本文件是 OpenSpec 区域的 **map, 不是 manual**。
> `openspec/` 是 **change authority workflow**（system of record），不是架构文档。
> 本文件不复制 OpenSpec 流程文档；生命周期见 `.ai/openspec-workflow.md` + `.ai/development-protocol.md` §4。
> 上级入口: 根 [AGENTS.md](../AGENTS.md)（Single Source of Truth）。

## 1. 是什么

- 所有变更走 OpenSpec 生命周期: propose → apply → verify → archive
- 工作单位是 change（proposal / design / specs / tasks），不是孤立任务
- 规格定义 WHAT (SHALL/MUST)，design 定义 HOW，tasks 定义 STEPS
- `openspec/changes/` 是变更进度权威来源；已归档看 `openspec/changes/archive/`

## 2. 修改 OpenSpec 时

**必须：**

- 遵循 proposal → design → spec → tasks 生命周期（**先 spec 后代码**）
- apply 前读取该 change 的 proposal / design / tasks / specs；实现后同步更新 `tasks.md`
- Large change（新 abstraction / boundary / lifecycle / architecture）先过 Human Gate

**禁止：**

- 代码完成后倒改 spec（spec 是驱动源，不是事后记录）
- 绕过 Human Gate
- 用 spec 替代 Contract（冲突时 Contract 赢，见根 AGENTS.md §2 Authority Order）
- 把 OpenSpec 文档内容复制到 AGENTS.md / 本文件

## 3. 入口

- 根: `../AGENTS.md`（SSOT）
- 生命周期与 Codex 触发: `.ai/openspec-workflow.md`
- 变更分级: `.ai/change-classification.md`
- Playbook: `.claude/skills/openspec-propose` / `openspec-apply-change` / `openspec-explore` / `openspec-archive-change`
