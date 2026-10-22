# SIM-017 — 弹窗窗口证据与未知状态前置设计
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089
## Intent / Owner decision
2026-10-23用户选择“先接完提取与注入；另建窗口证据和未知状态前置任务，暂不改原判定”。本任务交付前置设计，父SIM-016等待设计完成；不修改现有分类和产品协议。
## Scope / Acceptance
实际检查当前Host/Director/World输入链；明确仅XML包名不足以证明覆盖窗的最小反例；提出窗口/覆盖关系证据的来源、Owner、捕获/cycle/session关联、缺失/冲突/迟到语义和恢复条件；区分旧录制可验证、受控窗口事实及未来实采。具体公开字段保持candidate，不冻结新协议。解释未知时如何阻止自动清障并避免沿用旧popup present，以及如何恢复而不盲重发；列出正反例和后续Owner裁决项。落changes/SIM-017/design.md与证据，不实现、不设备/模型、不扩大授权。
level: CONTRACT
method: 源码/原录制核对与前置设计独立复核
expected: 可实施但不越权的证据与未知状态设计
actual: 前置设计完成并经独立Spec/Standards及Root源码复核。已说明跨包误判、缺观察时旧present延续、同周期窗口来源/关联及unknown有界补采与恢复；字段/阈值/来源仍为候选，无产品实现或协议变更。
evidence: evidence/sim-017/
## Status log
- 2026-10-23 PLAN：真实公开Host回放红例见evidence/sim-016/classification-red.trx；用户裁决保持旧判定，先完善独立证据设计。
- 2026-10-23 IMPLEMENT→REVIEW→VERIFY→CLOSED（design-only）：design.md与source-audit.md已交付。Spec来源措辞问题已修，Standards确认Owner/边界无阻断；父SIM-016已等待交付完成。关闭只指设计任务完成；窗口采集、current/stale/unknown实现与分类误判均未修复，未来按设计列出的Owner裁决项另开实施Change。
