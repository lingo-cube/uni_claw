---
status: accepted
date: 2026-09-27
supersedes: PER-004 terminology only
---

# Slow Perception 是一等能力，并以有界异步方式消费

Slow Perception 被定义为 Perception Capability Plane 中的一等能力，与 Fast
Perception 并列；DeepVLM、ScreenVLM 或其他模型只是 Slow 的 realization。Perception
pipeline 可以编排 Fast、Focused、Slow 和可选的 XML/Hierarchy 能力，但任何能力都
不因此获得 Evidence、WorldModel、identity、Grounding 或 Effect authority。

XML 是可选的语义证据源。只有当 required semantic claim 已被 XML/Hierarchy 或其他
较低成本、具备有效 authority 的能力充分建立时，才不需要 Slow；否则 Slow 是该
required claim 的异步补证路径。Effect-critical claim 由 Control 在 declared bounded
budget 内等待 Slow；超时或失败保持 `Unknown` / `Conflicted`，Effect Gate fail closed。
Effect Gate 本身永不调用或等待 Slow。非 effect-critical 流程可以继续；迟到的 Slow
结果仍可按原 `CaptureId`、`CaptureTime`、`ObservationCycleId` 进入 P2/Evidence
Ledger，并在 freshness/currentness 允许时参与后续 WorldModel reconciliation，但不能
满足已过期的 admission attempt，也不能追溯性授权已完成的 effect。

单次模型 confidence 不拥有 authority，也不是唯一 escalation 条件；source 选择可
结合 claim-domain authority、capability completeness、freshness/coverage 与经实证
校准的 source reliability。
