# 感知平台人读文档目录

> 面向人的阅读入口。这里不写 Gate / 决策流程，只回答「这是什么、在哪、怎么读」。
> 架构与决策记录在 [`../decisions/`](../decisions/)。

## 文档列表

| 文档 | 内容 |
|---|---|
| [perception-platform-overview.md](perception-platform-overview.md) | **感知平台全景阅读指南**：目录地图、训练结果怎么读、评估结果怎么读、身份工件怎么读、决策文档阅读顺序 |
| [skills-catalog.md](skills-catalog.md) | **技能目录（人读版）**：人会用到的 skill 清单、怎么触发、机器内部协议一句话带过 |

## 自动生成的人读报告

由 `perception-model-intelligence` Skill（[SKILL.md](../../.claude/skills/perception-model-intelligence/SKILL.md)）
从 canonical 机器真理只读生成，会随机器状态过期（每份报告头部有
`DerivedFrom` 校验行）：

- [`platforms/perception/reports/CURRENT.md`](../../platforms/perception/reports/CURRENT.md) —— 当前状态
- [`platforms/perception/reports/TRAINING_RUNS.md`](../../platforms/perception/reports/TRAINING_RUNS.md) —— 训练历史
- [`platforms/perception/reports/ARTIFACT_GUIDE.md`](../../platforms/perception/reports/ARTIFACT_GUIDE.md) —— 训练工件指南

> 机器清单是真理，人读报告只解释真理、不创造真理。训练指标零发布权威。

## 约定

- 文档一律中文；代码标识符（文件名、类名、ID）保持原文。
- 每个指南都给出「三分钟读完」和「逐个文件深读」两档路径。
