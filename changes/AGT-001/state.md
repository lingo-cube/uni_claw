# AGT-001 — UniAgent Runtime Architecture（DSH Product Realization）

lifecycle_state: designing · disposition: none · depth: decision-heavy · base: 83fa8c0e

## Status

**ready-for-grill**（Step 1 设计稿完成；不 CLOSED）。

设计稿：`plans/2026-09-24-agt-001-uniagent-runtime-architecture.md`

## 状态速览

- Explore 完成：FROZEN 上游全量对齐（baseline §24.2/§24.5、realization
  baseline v0.1、consultation-protocol v0.1、granularity 文档、RUN-004、
  GEV-004、现行缝类型）+ DSH checkout 机制核查（agent loop / concludesTurn
  终结工具 / event-sourced session / tool registry / headless-SDK 驱动面）。
- 核心裁决：sidecar 边界（B）、1:1 session 映射、1:1 consultation-turn、
  四元 AgentDecision（Policy 正式成员）、单工具 profile、策略状态白名单、
  12 项失败模型（DSH 失败折叠为咨询失败，永不升级 Kernel authority）。
- 4 个 Open Questions 登记待裁决（context 预算 / realization 标注 /
  失败咨询计轮语义 / transport 信封）。
- Amendment candidate 登记（RUN-004 Acceptance #8 的 live 语义补注）——
  本步未改任何 baseline。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 设计稿交叉核验：每条裁决 → FROZEN 上游条文引用或 DSH 机制证据（file:line）
  expected: 与冻结面零冲突；十问预答齐备；无软约束（提示词级）执法点
  actual: READY_FOR_GRILL（设计稿 §14；独立 grill 待执行）
  evidence: plans/2026-09-24-agt-001-uniagent-runtime-architecture.md（全稿）
```

## Status log

- 2026-09-24 · understand→designing · Step 1 Explore/Architecture Draft
  完成：设计稿落 plans/；spec/plan/state 落 changes/AGT-001/；下一步 =
  独立 Grill（见 plan.md 步骤 1）。
