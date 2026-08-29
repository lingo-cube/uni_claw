## Why

Human Gate `PROJECT_LEADER_RUNTIME_EVIDENCE_BASED_QUIESCENCE_ADMISSION_OPENSPEC_PROPOSAL`
(2026-08-28), grounded in the IR-G0/STOP-3 evidence chain: the Runtime ALREADY has
post-scroll visual quiescence confirmation (`ConfirmScrollStabilityAsync` /
`IsViewportStable` / `NavigationRowCenters`, Agent.OpenWorld.cs:2200/2274/2290) — but
its comparison evidence **loses occurrence multiplicity** (`Dictionary.TryAdd` collapses
same-frame duplicate signatures to one entry) and does not treat in-frame ambiguity as
instability. Consequence (run-6): a duplicate-artifact frame pair compares "stable",
is admitted as the stable decision basis, and the normalizer then correctly fails on
the real two occurrences — a stable gate confirming an ambiguous world.

This change defines the reusable internal capability principle **Evidence-Based
Quiescence Admission** and applies it by repairing the EXISTING post-scroll gate only:

> 人操作 GUI 时不会在动作刚发生后立即相信第一眼看到的内容：动作 → 观察 →
> 若内容仍在移动/闪烁/重复/矛盾则继续观察 → 连续新鲜观察稳定且无歧义后才基于
> 最新画面继续 → 始终无法稳定则停止，不猜测。

## What Changes

- Freeze the capability semantics: Fresh Observation; Multiplicity Preservation;
  Ambiguity-Aware Admission; Evidence-Based Convergence (count + multiplicity + ordered
  identity contract + deterministic occurrence correspondence + bounded drift + no
  normalization-blocking in-frame ambiguity); Latest-Frame Admission; Bounded
  Fail-Closed; Traceability.
- Repair the existing post-scroll buyer to satisfy those semantics: stability evidence
  keeps EVERY occurrence (ordered, multiplicity-preserving); same-signature count
  changes and ordering changes are instability signals; frames with duplicate-signature
  ambiguity are NOT confirmable as stable decision frames (they remain "not yet
  stable" evidence; persistent ambiguity → budget exhaustion → fail-closed).
- NO second parallel settle/quiescence loop; NO changes to normalizer, identity,
  perception, agent/FSM/traversal authority, wire/API; NO new owners.

## Capabilities

### New Capabilities

- `EVIDENCE_BASED_QUIESCENCE_ADMISSION`: the Runtime's observation-acceptance seam can
  prove, from consecutive fresh observations' verifiable consistency (multiplicity- and
  order-preserving, drift-bounded, ambiguity-aware), that a frame may become a decision
  basis — or fail closed without admitting any unstable frame.

### Modified Capabilities

- None beyond the existing post-scroll stability gate internals (same owner, same call
  sites, same contract surfaces).

## Impact

- Production scope: `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs`
  (`NavigationRowCenters`/`IsViewportStable` comparison evidence + confirmation logic),
  additive trace fields. Runtime-internal; zero wire/API/authority change.
- Implementation NOT authorized by this proposal (separate Human Gate required).
- Lifecycle context: supersedes WITHDRAWN `runtime-viewport-exhaustion-confirmation`;
  `unique-corroboration-admission` remains ABANDONED_AS_PRIMARY_FIX. Phase 2.6 remains
  STOPPED; no automatic reentry.
