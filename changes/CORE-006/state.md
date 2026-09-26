# CORE-006 — Effect Attempt 与固定依据契约
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent
parent_change: CORE-004

## Intent

先定义 Effect、Attempt、TargetBinding 的最小执行语义和固定依据契约，再决定是否
需要调整 Core 字段；不把当前 receipt、生命周期或 UI binding 结构直接升级为最终模型。

## Acceptance

1. Effect 与 Attempt 分离，能够表达同一逻辑操作的多次尝试。
2. Attempt 能保留当次请求快照、执行端和授权依据的语义位置。
3. 发送进展、外部执行判断、协调状态可同时存在且不互相覆盖。
4. TargetBinding 支持 Slice 和固定非 Slice 依据，并保留可解析的历史 basis。
5. receipt、transport accepted、late result、current canonical lookup 不自动生成 World result 或改写历史。
6. stale、ambiguous、unauthorized、unverified 继续 fail-closed。
7. Core 不依赖 Kernel driver、设备 SDK、浏览器、机器人或 Harness 类型。
8. 本 Change 不完成旧模型迁移，不冻结最终字段、继承、存储或协议。

## Verification

```yaml
level: DETERMINISTIC + SCENARIO_BACKED
primary_seam: Core semantic tests + existing Kernel Effect/Binding seam
expected: execution dimensions and fixed-basis guarantees are explicit and testable
actual: Core candidate contract is implemented and scenario-backed: Attempt keeps request
  snapshot, executor, authorization basis, and three independent execution dimensions;
  TargetBinding accepts Slice or fixed non-Slice BasisReference; canonical bindings without
  fixed basis fail closed. Existing Kernel projection remains read-only and Slice-based, so
  its non-Slice projection gate stays explicit for a later change.
evidence: evidence/2026-09-19-core-006-effect-attempt-contract.md
```

## Status log

- 2026-09-27 · verified→closed · Owner final closure PASS（联合 OWNER_GATE
  审阅：acceptance 8/8 机械证据，契约类型实存 CoreRecords.cs 且有具名测试；
  两条已登记 defer 的 transfer owner 在案——Attempt 生产投影 =
  CORE-015 Step 2/3（数据源已定位），非 Slice BasisReference 投影 gate =
  CORE-015/未来非 UI realization（FileSystemRealization.Tests 已是买方）；
  CORE-015/state.md 已确认字段全备、无需新增 Core 对象）。
2026-09-19 · planned · 根据 CORE-003 grill-with-doc 的 Effect/Attempt 与 BasisRef gates 建立规格；尚未实现。
2026-09-19 · planned→verified · 完成 Core 最小契约和确定性测试；未迁移旧模型、未冻结最终字段/API/存储；Kernel 非 Slice 投影保留为后续 gate。
