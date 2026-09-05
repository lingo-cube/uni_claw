> DecisionType: PROCESS
> Status: FROZEN
> Date: 2026-09-05

# 采用上游 Skill 生态与 `.agents/skills/` 标准位置

## Context

UniFlow V2 Alignment Directive 禁止自发明 Skill 格式/路径/协议；此前安装在
`skills/`（repo 根）的 7 个自撰 SKILL.md 属于「聊天重写版本」，违反上游优先
原则。

## Decision

1. Skill 物理位置采用 vercel-labs `skills` installer 的项目级标准输出
   **`.agents/skills/<name>/`**；不再使用自发明的 `skills/` 根目录。
2. 第一批 allowlist 从官方上游安装且保持原文不改：
   - `mattpocock/skills`：setup-matt-pocock-skills、grilling、grill-with-docs、
     domain-modeling、codebase-design、tdd、code-review、diagnosing-bugs；
   - `humanlayer/skills`：show-me。
3. Provenance 由 installer 的 `skills-lock.json` 承载；不发明新格式。
4. 本地需求一律经 UniFlow 组合 / 本地扩展 / adapter / reference 解决；
   fork 须记录 upstream base、reason、delta。
5. 仓库级约束（Skill 不控制生命周期、不建第二 task system、不绑模型）写在
   `AGENTS.md`，不写进上游 SKILL.md。

## Consequences

- 删除自撰 7 skill；`AGENTS.md`/schema 的路径与 allowlist 同步更新。
- 更新流程变为 `npx skills@latest update`（hash 变化即上游升级）。
- Codex 与 DSH 已实测从同一 `.agents/skills/` 消费——单一 canonical 源成立。
- UniClaw 特有调试语义（E0-E4/FDP/Owner）延后到 Phase 5 以本地扩展承载。
