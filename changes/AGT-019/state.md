# AGT-019 — Agent 异常升级与观察机制

lifecycle_state: designing · review_state: **draft-not-grilled** · disposition: none · depth: decision-heavy · base: 7b49038d

## Status

**设计收敛完成，未 grill**——spec/机制/修正面/编队全部为候选形态；
grill 是第一道闸（plan 步骤 1），通过前不进入任何实现。

设计讨论稿：`docs/design/agent-exception-escalation-and-observation-v0.1.md`
（同未 grill，与本 change 同命运）

## 来源证据

SETTINGS-COVERAGE-002/003/004 全遍历实测（2026-10-14/15）+ 2026-10-16
owner 系列讨论（Slow 参与分析、维度化编排重述、三层观察阶梯、profile
修正面订正）。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 实测证据归因（三个 change 的 evidence/manifest）+ 代码链路静态核证
  expected: 病根定位可复现（P1-P7 各有证据锚点）
  actual: 设计收敛完成；机制 M1-M6 + 修正面 + 修订通道成文；NOT GRILLED
  evidence: docs/design/agent-exception-escalation-and-observation-v0.1.md + changes/SETTINGS-COVERAGE-00{2,3,4}/
```

## Status log

- 2026-10-16 · understand→designing · 遍历问题归因 + 机制讨论收敛
  （P1-P7 / M1-M6 / harness 对照 / Q1-Q8 待裁决 / 修正面含 profile 订正）
  → spec/plan/state 落盘，**review_state: draft-not-grilled**。
  下一步 = plan 步骤 1（grill，建议 owner 主审 + 盲审双形态）。
