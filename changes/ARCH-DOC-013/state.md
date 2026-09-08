# ARCH-DOC-013 — Product Architecture Authority Relocation
lifecycle_state: closed · disposition: none · depth: standard · base: a2bf82e9

## Intent（WHAT/WHY）
docs/ 新放置规则（`docs/README.md`，由 UWM-009 冻结迁移事件补立）判定：
L0–L3 已 CLOSED 的产品架构基线驻留 `docs/analysis/` 是已知 authority
inconsistency（`AGENTS.md` canonical 指针亦指向旧路径）。本 change 只做
physical authority relocation，不是 architecture revision。

## Scope / Out of Scope
Scope：
- `git mv docs/analysis/product-architecture-baseline-l0-l3.md →
  docs/architecture/product-architecture-baseline-l0-l3.md`（内容零改动）
- `AGENTS.md` L0–L3 产品架构 canonical 指针改到新路径
- 全仓旧路径引用清零：协议基线头注、analysis 三文档相对链接、
  2 个 plans 头部 references、3 个 closed change 的 ADR Refs 路径
- `docs/README.md` 删除 product-architecture-baseline「已知未完成迁移」注记
- 建立本 state 记录

Out of scope：基线文档任何领域语义；ADR 物理迁移（维持协议基线头注的
deferred decision，另立 change 再做）；UWM-009 / P1–P22 / 产品代码。

## Decisions
- 人工指令（2026-09-09）：立即收口，独立小 change，7 条执行边界。
- 历史 durable 文档（plans/、closed change references）中的旧路径按
  「指针修正」更新并注明 ARCH-DOC-013 provenance；不重写任何决策内容。
- 基线文件本体零改动（git rename 100% 相似度保持）。

## Acceptance
- docs/analysis/ 不再存在 CLOSED/FROZEN product architecture baseline
- docs/architecture/product-architecture-baseline-l0-l3.md 存在且内容不变
- AGENTS.md 唯一 canonical pointer 指向新位置
- 全仓旧路径引用 = 0
- docs/README.md 与实际目录状态一致
- git diff 仅表现为 relocation + pointer/documentation correction

## Constraints
不修改产品代码；不重开 L0–L3 语义；不顺手迁移 ADR。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >
    grep -rn "docs/analysis/product-architecture-baseline-l0-l3" 全仓（排除 .tmp-hf-intake）
    + grep -rn "](product-architecture-baseline-l0-l3.md)" docs/analysis/
    + git diff -M --stat/--summary + ls docs/analysis docs/architecture
  expected: 旧路径文档引用 0；analysis 内无断裂相对链接；rename 100% 相似；diff 仅 relocation + 指针修正
  actual: >
    旧路径唯一命中 = .git/worktrees 内部索引（git plumbing，非文档引用）；
    analysis 相对链接 0 残留；docs/analysis/ 无该文件，新路径存在（40407B）；
    git 判定 rename docs/{analysis => architecture}/... (100%)，0 insertions/deletions；
    变更面 = 1 rename + AGENTS.md/协议基线头注/3 个 analysis 文档/2 个 plans/
    3 个 closed change refs/docs/README.md 的指针与注记修正
  evidence: 上述命令输出（2026-09-09 执行）
```

## Status log
2026-09-09 · understanding→closed · 单会话执行完毕（人工指令边界 7 条全遵守；验收 6 项全过）
