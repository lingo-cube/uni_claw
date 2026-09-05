# Harness V2 — Skill Provenance Inventory

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `COMPLETE`（机器可读真相 = `skills-lock.json`，本文件是其人读索引）
> Authority: `NONE`
> 禁止边界: 上游 SKILL.md 正文不可改写；本地 delta 必须逐条登记。

## 安装实录（2026-09-05）

安装命令（Codex agent、复制模式、非交互）：

```bash
npx skills@latest add mattpocock/skills \
  -s setup-matt-pocock-skills -s grilling -s grill-with-docs \
  -s domain-modeling -s codebase-design -s tdd -s code-review \
  -s diagnosing-bugs -a codex -y --copy

npx skills@latest add humanlayer/skills -s show-me -a codex -y --copy
```

## Provenance 表

| Skill | Source | Upstream repo | Upstream path | Installed path | Lock hash（前 8 位） | Local delta | Consumer | Migration action |
|---|---|---|---|---|---|---|---|---|
| setup-matt-pocock-skills | UPSTREAM_MATT | mattpocock/skills | skills/engineering/setup-matt-pocock-skills/ | .agents/skills/setup-matt-pocock-skills/ | caa9a086（见 lock） | 无 | 用户显式调用（disable-model-invocation） | 新装 |
| grilling | UPSTREAM_MATT | mattpocock/skills | skills/productivity/grilling/ | .agents/skills/grilling/ | 见 lock | 无 | Codex + DSH | 替换本地自撰版（已删） |
| grill-with-docs | UPSTREAM_MATT | mattpocock/skills | skills/engineering/grill-with-docs/ | .agents/skills/grill-with-docs/ | 见 lock | 无 | 用户显式调用（disable-model-invocation，by design，与 setup-matt-pocock-skills 同类） | 新装 |
| domain-modeling | UPSTREAM_MATT | mattpocock/skills | skills/engineering/domain-modeling/ | .agents/skills/domain-modeling/ | a11713c0 | 无 | Codex + DSH | 替换本地自撰版（已删） |
| codebase-design | UPSTREAM_MATT | mattpocock/skills | skills/engineering/codebase-design/ | .agents/skills/codebase-design/ | 5a17552c | 无 | Codex + DSH | 替换本地自撰版（已删） |
| tdd | UPSTREAM_MATT | mattpocock/skills | skills/engineering/tdd/ | .agents/skills/tdd/ | 见 lock | 无 | Codex + DSH | 替换本地自撰版（已删） |
| code-review | UPSTREAM_MATT | mattpocock/skills | skills/engineering/code-review/ | .agents/skills/code-review/ | caa9a086 | 无 | Codex + DSH | 替换本地自撰版（已删） |
| diagnosing-bugs | UPSTREAM_MATT | mattpocock/skills | skills/engineering/diagnosing-bugs/ | .agents/skills/diagnosing-bugs/ | 37b5e9c6 | 无 | Codex + DSH | 替换本地自撰版（已删） |
| show-me | UPSTREAM_HUMANLAYER | humanlayer/skills | plugins/show-me/skills/show-me/ | .agents/skills/show-me/ | de32a72f | 无 | Codex + DSH | 替换本地自撰版（已删） |
| uniflow | LOCAL_UNIFLOW | —（本地原创） | —（原根 uniflow.md） | .agents/skills/uniflow/ | —（本地 skill 不入 lock；frontmatter `source: LOCAL_UNIFLOW`） | 原生 | Codex + DSH（catalog 实测即时发现） | 由根 uniflow.md 迁入标准 skill 位置（ADR-0005） |
| uniclaw-debug-evidence | LOCAL_UNICLAW | —（语义源：uni-agent 谱系 .ai/skills/{evidence-driven-debugging, runtime-behavior-debugging}） | —（见下方处置映射） | .agents/skills/uniclaw-debug-evidence/ | —（本地 skill 不入 lock；frontmatter `source: LOCAL_UNICLAW`） | 独有语义整体迁入；通用部分不复制（归上游 diagnosing-bugs） | Codex + DSH（catalog 实测即时发现） | Phase 2 新建（diagnostic extension） |

## 旧 Debug Skill 处置映射（Phase 2，证明 unique semantics 无未迁移即删）

uni-agent 谱系的两个旧 skill（`.ai/skills/evidence-driven-debugging` 302 行、
`.ai/skills/runtime-behavior-debugging` 116 行，原文经 `git show uni-agent:`
取证）逐节处置：

| 旧节 | 处置 | 去向 |
|---|---|---|
| E0-E4 风险表（edd §1）+ 可用性表（rbd §2）+ E0-E1/E2-E4 规则 | MIGRATE（逐字保留） | extension §1 |
| 失败分类 A-F + lifecycle/last-correct/invariant 记录（rbd §1） | MIGRATE（逐字保留） | extension §2 |
| Reality Analysis 模板 Expected/Observed/Gap/FDP（edd 模型段 + rbd §1 首段） | MIGRATE | extension §3 |
| Architecture Ownership Check（rbd §4）+ Runtime Change Check（edd §4） | MIGRATE（收敛为 Owner Localization + 不触碰声明） | extension §4 |
| Canonical Examples ×3（edd §6） | MIGRATE（原文） | extension references/canonical-cases.md |
| STOP 条件（edd STOP / rbd §4 尾） | MIGRATE（改写为 escalation 返回，裁决权上交） | extension §4 尾 |
| Debug Packet 返回格式（edd §8） | MIGRATE（简化为 evidence packet） | extension §5 |
| Core Principle / Worker Flow / Test Design / Review Checklist（edd） | REPLACE_WITH_SKILL | 上游 diagnosing-bugs / tdd / code-review（通用纪律不本地重复） |
| Scope/Authority 声明 | MIGRATE | frontmatter + composition contract（extension §0） |

结论：两个旧 skill 的全部 unique semantics 已迁移至
`.agents/skills/uniclaw-debug-evidence/`；其通用部分与上游重复，不复制。
uni-agent 分支上的原文件删除属该分支后续工作（本分支不持有它们）。

完整 hash 与来源以 `skills-lock.json` 为准（`source` / `skillPath` /
`computedHash`）；更新经 `npx skills@latest update`，hash 变化即上游升级。

## 已删除的本地自撰版本（违规修正记录）

此前安装在 `skills/`（repo 根）的 7 个自撰 SKILL.md（grilling /
domain-modeling / codebase-design / tdd / code-review / diagnosing-bugs /
show-me）属于 directive 明令禁止的「聊天重写版本」，已整体删除并由上表
上游版本取代。其正文中的 UniFlow 约束语（不控制生命周期、不绑模型）已
上移到 `AGENTS.md` §3 作为仓库级规则——**约束属于 AGENTS.md，不属于
改写上游 Skill**。

## 待办（Phase 5，本地扩展不落上游）

- UniClaw 调试特有语义（E0-E4 证据分级 / FDP / Owner / 失败分类学）：
  以 LOCAL_UNICLAW 扩展或 reference 文档组合使用，挂接点记录于
  `docs/analysis/harness-v2-phase2-skill-migration-matrix.md`。
- ~~`grill-with-docs` 的 DSH catalog 可见性核查（frontmatter description 存在
  性）~~ 已解决：SKILL.md frontmatter 含 `disable-model-invocation: true`，
  DSH 不可见为 by design（与 setup-matt-pocock-skills 同类，见 Provenance 表）。
