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
