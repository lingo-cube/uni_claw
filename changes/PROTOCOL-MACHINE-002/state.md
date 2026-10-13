# PROTOCOL-MACHINE-002 — 状态机可视化出口（gen-protocol-machine 第四出口）
lifecycle_state: closed · disposition: none · depth: minimal · base: 9518b53

## Intent（WHAT/WHY）

PROTOCOL-MACHINE-001 三出口缺图视：状态/转移/吸收态无可视面。新增第四出口
`tools/gen-state-machine-viz.py`：消费单源 state-machine.yaml，渲染自包含 HTML。

## Scope / Out of Scope

In：生成器 + 产物 `docs/design/state-machine-viz-v0.1.html`（生成物勿手改）。
Out：不改单源与既有三出口；不引交互（<details> 折叠已够评审用）。

## Acceptance / Verification

- A1 生成幂等（两次字节一致）✅（纯派生无时间戳）
- A2 覆盖全机器：5 台（run + 对象机四台）、28 转移、吸收态 9 ✅（正则清点）
- A3 徽章语义：◉initial / ▣terminal / ⛔absorbing / measure·await·edge 标注 ✅

```yaml
verification: {level: DETERMINISTIC, method: "python tools/gen-state-machine-viz.py + 正则清点",
  expected: "5 机/28 转移/9 吸收", actual: "5/28/9 全中", evidence: "生成输出 + 本档"}
```

## Status log

- 2026-10-12 · OPENED → CLOSED · Owner 指示建立状态机 HTML 可视化；单源生成
  （非手写，遵守 PM-001 纪律）；生成与清点一次通过。
