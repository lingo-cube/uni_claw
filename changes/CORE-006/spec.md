# CORE-006 — Effect Attempt 与固定依据契约

Status: `READY FOR IMPLEMENTATION`
Triage: `ready-for-agent`
Publication: repository Change State（本仓库未配置外部 issue tracker）
Parent: CORE-004（步骤子规格）

## Problem Statement

CORE-003 已建立 `Effect → Attempt → TargetBinding` 的最小层次，CORE-005 已完成
World 侧 Evidence/Claim 对齐。但当前 Core 候选记录还不能完整表达：

- 当次请求的不可变快照；
- 实际执行端和当次授权依据；
- 发送进展、外部执行判断、协调状态三者同时存在；
- TargetBinding 使用的通用固定依据（Evidence、Claim、Slice 或资源版本）；
- 固定非 Slice 依据下的历史复核和有效性检查。

如果直接把 Kernel 的 `EffectReceipt`、生命周期枚举或 UI binding 字段提升为 Core，
会把投递、外部结果、Runtime 协调和视觉定位混成一个事实模型。

## Solution

先定义执行语义的最小契约，不预先冻结具体字段名：

```text
Effect
  ├─ logical operation / parameters / clause references / expectations
  └─ Attempt*
       ├─ immutable request snapshot
       ├─ executor and authorization references
       ├─ TargetBinding*
       └─ dispatch progress / external execution evidence / coordination state
```

`TargetBinding` 至少要有一个可解析的固定依据，但依据可以是 Evidence、Claim、
Slice 或固定资源版本；Slice 不是强制类型。有效性是按用途、时间和依赖计算的使用
判据，不简化成 freshness 时间差。

## User Stories

1. As an Effect planner, I want a logical operation separated from each Attempt, so that retries and compensation remain distinguishable.
2. As an Attempt owner, I want an immutable request snapshot, so that a later Effect revision cannot rewrite what was actually sent.
3. As an executor, I want the actual executor and authorization references recorded for the Attempt, so that planning-time assumptions are not reused as execution proof.
4. As a delivery observer, I want dispatch progress separated from external execution, so that transport acceptance is not business completion.
5. As a World observer, I want external execution evidence and World verification separate, so that a receipt does not become a result.
6. As a Runtime coordinator, I want unresolved obligations and recovery state separate from delivery outcome, so that coordination is not compressed into one lifecycle enum.
7. As a binding owner, I want each binding to retain a parseable fixed basis, so that historical target selection can be reconstructed.
8. As a file/API realization, I want a fixed resource-version basis without manufacturing a visual Slice, so that non-UI targets remain honest.
9. As a UI/robot realization, I want Slice, calibration, pose, native locator, and spatial locator to remain realization contracts, so that Core does not interpret their vocabulary.
10. As a recovery owner, I want unknown and late results to remain append-only evidence, so that recovery does not rewrite prior attempts or resurrect terminal state.
11. As a safety owner, I want stale, ambiguous, unauthorized, and unverified bindings to fail closed, so that missing validity is not guessed from elapsed time.
12. As a migration owner, I want this contract evaluated before mapping `EffectReceipt` or binding classes, so that current implementation shape cannot become the architecture.

## Implementation Decisions

- Treat `Effect`, `Attempt`, and `TargetBinding` as semantic responsibilities, not as a
  mandatory class hierarchy or transaction aggregate.
- Define a candidate BasisRef contract that can identify the referenced record, fixed
  snapshot/resource version, and validity conditions without hiding the reference in an
  opaque locator string. The exact value type remains open until a non-UI and UI case are
  both tested.
- Keep the logical Effect independent from a particular Attempt. A retry may be another
  Attempt under the same Effect; a changed operation, target, or parameters may require a
  new Effect with an explicit relation.
- Keep the Attempt request snapshot immutable. Runtime persistence and crash-window
  guarantees are separate responsibilities; an Attempt record alone cannot promise exactly
  once delivery.
- Keep at least three semantic dimensions distinct: local dispatch progress, external
  execution/result evidence, and coordination/obligation status. They may be Unknown or
  unresolved independently and must not be compressed into one enum.
- Keep TargetBinding target reference, locator material, fixed basis, and use-time validity
  conditions distinct. A binding may be historically explainable and currently unusable.
- Do not let a subject reference, Segment id, receipt, or current canonical lookup grant
  permission. Clause and authorization remain explicit and separately checked.
- Late receipts and post-action Evidence may be appended and trigger evaluation, but cannot
  silently mutate prior Attempt, Binding, World Claim, or terminal truth.
- A missing guarantee blocks only recovery, retry, binding reuse, or other uses that depend
  on it. It does not block unrelated Core object use or the whole repository.
- This Change does not choose final field names, storage, serialization, driver protocols,
  state-machine vocabulary, or inheritance.

## Testing Decisions

- Use the existing Core tests and Kernel Effect/Binding projection seam as the highest
  available seam. Add a new seam only if a current public seam cannot express a required
  distinction.
- Positive tests cover one Effect with multiple Attempts, immutable request snapshots,
  separate Attempt and Slice times, explicit authorization references, and both Slice and
  fixed-resource binding bases.
- Negative/history tests cover receipt-as-result, transport acceptance-as-completion,
  unknown timeout, late result, stale binding, ambiguous target, authorization revocation,
  and current canonical lookup changing a historical binding.
- Tests must prove that delivery progress, external execution evidence, and coordination
  status can coexist without one overwriting another.
- Test a non-UI fixed-resource case and a UI/spatial case with the same Core questions but
  different locator payloads. This validates the contract without freezing one locator type.
- Do not use current lifecycle enum names or `EffectReceipt` fields as architecture tests.

## Out of Scope

- Implementing a complete Runtime recovery protocol or exactly-once delivery.
- Replacing Kernel EffectBoundary, drivers, Assurance, RunModel, OutcomeState, or Trace.
- Adding an independent Event, Expectation, Verification, or OpenDuties Core object.
- Migrating or deleting existing binding/receipt classes.
- Choosing universal freshness, trust, confidence, correlation, or version fields.
- Freezing final public API, inheritance, persistence, serialization, or transport layout.

## Further Notes

The governing materials are CORE-001 through CORE-005, the vNext.1 Effect/Attempt/
TargetBinding sections, the scenario library’s timeout/retry/partial-success/late-result/
identity cases, and existing Effect/Outcome/Binding tests. A field or type is admitted only
when a concrete scenario needs it and the same semantic question appears in more than one
realization or cannot be preserved by a reliable reference.
