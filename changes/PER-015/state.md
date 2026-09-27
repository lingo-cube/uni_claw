# PER-015 — Legacy Reader Retirement + Rollback Observation

lifecycle_state: closed · disposition: none · depth: standard · base: 8ddc32c7

## Intent

完成 PER-012 rollback observation 前的最后一个 production reader cutover：ConflictResolver
只消费 typed semantic.checked；保留 legacy writers、LegacyStateProjection、LegacyStateEgress
和 rollback support surface，不删除 legacy surface，不启动 PER-011 Fusion。

## Scope

- ConflictResolver typed Checked/Unchecked authority path。
- Partial/Unknown/Unsupported fail-closed。
- rendered appearance 与 semantic checked 分轴。
- production `*.state` reader inventory = 0。
- representative Kernel、Simulation、post-action、RuntimeAssurance、HostLiveFull、
  TypedLiveChain、coordinate-space/live effect path verification。
- rollback observation verdict：PASS 或 exact blocker 的 HOLD。

## Out of Scope

- 删除任何 legacy writer/projection/egress surface。
- PER-011 Fusion。
- 修改 PER-009 frozen conflict semantics 的 authority owner。
- 以环境失败冒充 semantic rollback。

## Acceptance

1. ConflictResolver typed migration tests A1–A8 pass。
2. production `*.state` readers = 0；migration fixture and cutover tests pass。
3. Kernel, Simulation, post-action, RuntimeAssurance, coordinate/live effect tests pass。
4. real-device representative paths are either PASS or recorded as environment blocker。
5. typed→legacy rollback observed = NO unless direct evidence shows otherwise.
6. Gate 4 = PASS only with real-device evidence; otherwise HOLD with exact blocker.
7. legacy surface remains present; PER-011 remains unstarted.

## Verification

level: ENVIRONMENT

method: focused ConflictResolver + PER-014 inventory + Kernel + Simulation +
HostLiveFull + TypedLiveChain + scenario certification + scenario coverage +
full solution + diff check.

expected: zero production readers, typed preservation, no semantic/rendered leakage,
no typed→legacy rollback, and an explicit Gate 4 PASS/HOLD verdict.

actual: >
  ConflictResolver A1–A8 PASS；production *.state readers = 0；Kernel 643/643；
  Simulation 184/184；Host deterministic 63/63；full solution 1051/1051；
  certification 29/29；coverage truth chain PASS；git diff --check PASS；
  Host deterministic 63/63；真实设备 selector/capability manifest 在 API 35 AVD
  上通过；TypedLiveChain 3/3（TypedLiveChain 1 + LiveCoordinateGate 2）通过；
  HostLiveFull 真实截图、视觉结果、ADB tap、post-action XML 与独立 settings
  复核 PASS 1/1，status=Completed，delivered=1，legacyEgress=false。typed→legacy
  rollback = 0，effect-critical legacy dependency = NONE，Unknown/Partial 未触发
  reader fallback；Gate 4 PASS。scenario certification 29/29、coverage truth
  chain PASS、full solution PASS。

evidence: evidence/2026-09-27-per-015-legacy-reader-rollback-observation.md

status: closed · Gate 4 PASS · READY_FOR_LEGACY_REMOVAL

## Status Log

- 2026-09-27 · verified→closed · Owner final closure PASS；PER-015 Gate 1–4 PASS；PER-016 removal PASS；production legacy `*.state` readers/writers = 0、LegacyStateProjection buyers = 0、legacy surface removed；Simulation 184/184、HostLiveFull PASS、TypedLiveChain PASS、full solution 1051/1051、certification/coverage 29/29；既有 verification、real-device、Simulation、certification/coverage 与 rollback-window evidence 保持不变；PER-011 未启动。
