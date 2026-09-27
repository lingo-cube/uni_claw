# PER-015 — Legacy Reader Retirement + Rollback Observation

lifecycle_state: verified · disposition: hold · depth: standard · base: 8ddc32c7

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
  certification 29/29；coverage truth chain PASS；git diff --check PASS。
  HostLiveFull 与 TypedLiveChain 在 DSH_TEST_PERCEPTION_LIVE=1 下均受环境阻塞：
  adb 无 emulator-5554（real dump 失败 / device not found）。未观察到
  typed→legacy rollback，但无真机代表性证据，因此 rollback observation window = HOLD。

evidence: evidence/2026-09-27-per-015-legacy-reader-rollback-observation.md

status: verified · Gate 4 HOLD · NOT_READY_FOR_LEGACY_REMOVAL
