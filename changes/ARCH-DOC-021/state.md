# ARCH-DOC-021 — 基线 §3.1 UniAgent 定位措辞修订（Narrow Amendment v0.1.2）
lifecycle_state: closed · disposition: none · depth: standard · base: 85853109

## Intent（WHAT/WHY）

AGT-018 落地时记录的已知偏差收口：基线 §3.1 Definition 首句「面向用户的
完整智能主体」与所有者裁决（2026-10-07）不符——**UniAgent 是整个系统的
大脑（cognition core）**，不以「面向用户」为定位属性。所有者指令
「直接干」= 授权本修订。

## Scope / Out of Scope

In：§3.1 Definition 首句措辞修正 + §25 Narrow Amendment v0.1.2 留痕 +
`src/UniClaw.Agent/README.md` 已知偏差节改为已收口。
Out：Owner / Authority / Boundary / Lifecycle 零变化；不重开 §23 已关闭
的 L0-L3 横向结构；不改其他章节。

## Decisions

- D1：走 §24（RFS-001 v0.1.1）先例的 Narrow Amendment 形态：正文修正 +
  独立节留痕修订依据与性质。
- D2：措辞只改定位句，职责列表（理解 Goal/策略/Contract/评价）原文
  保留——它们与「大脑」定位一致且无争议。

## Acceptance

| # | 判据 |
|---|---|
| A1 | §3.1 Definition 首句 = 系统的大脑表述；职责句原文不变 |
| A2 | §25 留痕：依据（所有者裁决 + AGT-018）+ 性质（非结构性，零 Owner 变化） |
| A3 | UniAgent README 已知偏差节指向 §25，不再列为待办 |
| A4 | diff 仅三处；DocsMetadataTests 绿 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "git diff 范围核对 + dotnet test Kernel.Tests --filter DocsMetadata"
  expected: "diff 仅 §3.1 首句/新增 §25/README 偏差节；测试绿"
  actual: "diff 恰三处核对通过；DocsMetadata 4/4 绿；check-invariant-matrix PASS（47/47）"
  evidence: "evidence/arch-doc-021/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者「直接干」授权，
  按 §24 先例开立 ARCH-DOC-021。
- 2026-10-07 · PERSIST → IMPLEMENT → VERIFY → CLOSED · 三处修改落地；
  diff 范围核对、DocsMetadata 4/4、不变量矩阵 47/47 PASS；INDEX 已再生。
