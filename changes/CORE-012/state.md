# CORE-012 — 产品侧可靠执行源接入（规划：实现前 Human Gate 复审）

lifecycle_state: planned · disposition: none · depth: decision-heavy · base: working-tree
triage_label: awaiting-human-review
parent_change: CORE-010
prior_slice: CORE-011（closed：测试侧 fixture 仿真证明）

## Intent

把 CORE-009 契约 + CORE-010 计划 + CORE-011 仿真证明推进到产品侧：在
`EffectBoundary.Dispatch` 的 gate 通过、driver 调用之前接入可靠执行源。
本 Change 只承载实现前 Human Gate 复审材料与裁决记录；实现授权仍为
NONE，未获显式授权前不修改 `src/`。

## Scope

- 产出四问复审 docket（Owner / 复用判断 / 持久性范围 / 最小接口），
  逐问给出仓库事实、选项与建议裁决。
- 记录用户对 `DECIDE-Q1..Q4` 的裁决；裁决齐备后形成实现计划供下一次
  单独实现授权。

## Out of Scope（授权前禁止）

- 修改 `src/`（含 `EffectBoundary` 构造函数——产品轮必须显式点名放开）。
- 新增生产存储、Core 顶层类型；把 fixture/接口名冻结为公共 API。
- Trace 权威化、真实外部操作、数据库、跨节点。

## Decisions

pending——见 docket 的 `DECIDE-Q1..Q4` 槽位与建议摘要
（Q1=A EffectBoundary 注入 · Q2=不复用现有 Log · Q3=B 文件 journal ·
Q4=最小面+五约束）。

## Verification

```yaml
level: CONTRACT
method: docket 复审（仓库事实核对 + CORE-011 S1–S12 映射）
expected: 四问各有显式裁决；授权边界清单点名必改文件
actual: pending human review
evidence: evidence/2026-09-19-core-012-human-review-docket.md
```

## Status log

2026-09-19 · created · commit 检查点后起草四问复审 docket；CORE-012
保持 planned + implementation_forbidden，等待用户逐问裁决。
