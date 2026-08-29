# 技能目录（人读版）

> 项目 Skill 的唯一正文位于 `.ai/skills/<name>/SKILL.md`。本目录只做导航，
> 不复制规则，也不收录某个 Host 的内置或用户全局 Skill。

## 人直接触发

| Skill | 人怎么说 | 作用 |
|---|---|---|
| `perception-model-intelligence` | 「当前感知模型是什么状态？」「这些训练文件哪些有用？」 | 只读解释感知平台机器真理，零发布/激活权威 |
| `openspec-propose` | 「按 OpenSpec propose <变更>」 | 生成 proposal / design / specs / tasks |
| `openspec-apply-change` | 「按 OpenSpec apply <change>」 | 按已批准 artifacts 实施并验证 |
| `openspec-explore` | 「按 OpenSpec explore <主题>」 | 只探索、调查和澄清，不实现代码 |
| `openspec-archive-change` | 「按 OpenSpec archive <change>」 | 在完成门满足后归档并同步规范 |
| `uniagent-evolution-loop` | 「按 UniAgent 演进闭环分析」 | 受控编排模拟、证据、First Divergence 与 Owner 路由 |

## 按任务自动匹配的方法 Skill

`.ai/skills/` 还包含架构上下文、证据驱动调试、Runtime 行为调试、知识维护、
文档迁移、决策检索、停止条件和任务分级等方法 Skill。只有当用户点名或任务与
frontmatter `description` 清晰匹配时才加载；所有 Skill 均为 `Authority: NONE`，
不得扩大任务 scope、权限、ownership、contract 或 lifecycle。

## Host 发现

- 支持通用约定的 AI Coder 从 `.agents/skills/` 发现 Skill。
- DSH 可同时从 `.agents/skills/` 或兼容 adapter `.dsh/skills/` 发现。
- 两处都只能是指向 `.ai/skills/` 的项目内相对符号链接；不得复制正文。
- 新增或改名后运行 `scripts/setup-dsh-skills.sh` 幂等同步，再运行
  `scripts/check-consistency.sh` 验证。

## 感知机器真理边界

`perception-model-intelligence` 及其 helper 只读 canonical 机器真理
（manifests / EvaluationRun / identity 工件），唯一允许写入的是明确授权的三份
人读报告。训练指标零发布权威；生产、训练、评估和发布代码不得反向依赖该 helper。
