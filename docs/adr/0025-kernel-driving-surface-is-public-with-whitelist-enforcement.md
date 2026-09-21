---
status: accepted
date: 2026-09-20
---

# 0025 — Kernel 驱动面公开化由白名单执法，Product Host 是第一产品买方

KernelRunDriver、RunDriverInputs、RunDriverInput（含 Observation /
Cancel / Unexpected 三 case）、AgentDecision 契约族（AgentDecisionPhase /
AgentDecisionContext / AgentObligationView / AgentActionProposal /
AgentActionStep / AgentNoActionProposal / AgentDecision）、AgentPlanPolicy、
ActivationResult、RunDriveStatus / RunDriveResult 自 internal 提升为
public（RUN-003；HOST-001 D8 裁决，2026-09-20）。零行为变化——仅
可见性修饰符与注释；签名、命名空间、逻辑不动。理由：RFS-001 已写明
这组缝「Simulation Host 与 Product Host 各自提供 adapter」，但
InternalsVisibleTo 只含测试程序集，Product Host 作为组件消费者编译
不可达——没有公开面的组件不是完成组件化的组件。

公开 ≠ 冻结 API：UniClaw.Kernel 程序集公开类型集合（196 项，2026-09-20
冻结）由 `KernelRuntimeSurfaceWhitelistTests` 字面白名单执法
（EXP-008 public-shape-allowlist 先例）；任何增删公开类型 = RED =
须经 change 显式修订名单——名单修订即评审点。

## Consequences

- Product Host（HOST-001）可编译组合自驱缝（kernel, planPolicy,
  RunDriverInputs{NextInput, ConsultAgent}）；IVT 维持不变，测试程序集
  内部访问不收回。
- 「internal 最小 concrete seam（非公共契约）」时期对这组类型结束：
  其演进改由白名单 + 既有测试族（KernelRunDriverTests 等）约束。
- 后续任何 Kernel 公开面扩张（含 fresh 类型公开化）须经 change。
