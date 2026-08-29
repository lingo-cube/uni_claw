# Development Semantic IR — implementation phase (Gate auto-activation, 2026-08-28)

- DesiredReality: post-scroll 稳定门以有序、保留 multiplicity 的连续新鲜观测证据
  判定静止；同帧歧义帧永不可确认为 decision frame；预算耗尽 fail-closed 并经
  现有 RunFailed Surface B 终态上报（Principle 8）。
- ClaimUnderTest: EVIDENCE_BASED_QUIESCENCE_ADMISSION（post-scroll buyer）。
- ObservedReality: NavigationRowCenters 的 TryAdd 折叠 + IsViewportStable 无序比较
  使重复帧/计数变化/顺序变化可被错误确认为稳定（run-6 obs13/14 实证）。
- FDP: 稳定性比较证据丢失 occurrence multiplicity（Agent.OpenWorld.cs:2290）。
- Owner: RuntimeAgent observation-acceptance seam（现有 gate，repair-in-place）。
- AllowedChange: ConfirmScrollStabilityAsync/IsViewportStable/NavigationRowCenters
  最小修复 + additive trace + 现有 RunFailed reason 增强；测试。
- ForbiddenChange: 新 wire/DTO/EventKind/callback/mid-Run transport；normalizer/
  identity/perception 改动；dedupe/topmost-wins；其他 Buyer 接线；sleep 正确性。
- AcceptanceEvidence: S1/2/5/6/7 RED→GREEN；S3/4/8 controls 绿；S9-12 通过；
  ScrollStability/normalization/traversal/open-world + Phase2/2.5 全回归绿；
  guards+consistency+diff-check；零 EventKind/wire 变化。
- StopCondition: RED 未按预期失败；或需任何 ForbiddenChange 才能实现。
- SemanticResolution: RESOLVED。

Gate record: `PROJECT_LEADER_..._IMPLEMENTATION_GATE_WITH_TERMINAL_UNIAGENT_HANDOFF` —
amendments applied (tasks D.7), exhaustion-confirmation ARCHIVED (2026-08-28-),
projections 22, strict/consistency/diff-check PASS → implementation authorization
ACTIVATED. Lifecycle: unique-corroboration-admission remains ABANDONED_AS_PRIMARY_FIX.
