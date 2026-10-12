# ARCH-DOC-025 — 组件、协议与生命周期完备性审查
lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

围绕用户提出的题目，审查 UniClaw 当前组件、P1–P26/O 边协议、产品语义生命周期和 150 场景等价类是否足以表达需求，并用数学性质与哲学前提检查完备性。将“结构成立”“条件成立”“明确缺口”和“有意 deferred”分开，避免把工程门禁或测试绿灯当成题目结论。

## Scope / Out of Scope

In：Product T0–T3、语义生命周期、主协议与外围协议、生命周期分支、`scenario-reasoning-v0.1.md` 的 150 场景等价类、`protocol-completeness-formal-audit-v0.1.md` 的数学/哲学审计。

Out：实现 P24/P25、恢复、Memory、用户中途控制或 Observation Request；不修改产品代码、测试、场景库或协议状态机。

## Decisions

- 本 Change 按用户要求直接记录，不经过 grill。
- 场景层与协议图层结论互补：场景负责实例化和反例，形式审计负责可达性、终止性、闭合性和认识论循环。
- 150 场景结论为 `✅122 · ◐14 · D13 · NB1`；整体生命周期判定为“部分完备，尚不能称为完全完备”。
- `ARCH-DOC-024` 保持组件分级与工程化管理主题；本 Change 独立承载生命周期题目，避免两个问题共用一个 Change。

## Acceptance

1. 文档以组件/协议/生命周期/场景/数学/哲学为主线回答原题。
2. 明确正常、拒绝、等待、恢复、终止、迟到、重复和用户中途控制分支的状态。
3. 引用 150 场景等价类结果、47/47 不变量命中和已登记 D/◐ 缺口。
4. 明确 Safety、Consistency、Authority closure、Liveness、Reachability、Epistemic soundness、Terminal completeness 的分别判定。
5. 不将未实现建议写成已验证事实。

## Verification

```yaml
verification:
  level: CONTRACT
  method: "逐段对照 docs/design/scenario-reasoning-v0.1.md 与 docs/design/protocol-completeness-formal-audit-v0.1.md；核对产品架构基线与协议基线；git diff --check"
  expected: "文档结论可回指场景等价类、形式审计和架构/协议权威；无工程管理主题漂移；差异格式通过"
  actual: "已完成重写：正文以题目四问为主线，明确 150 场景、数学性质、哲学推导和部分完备结论；未改产品代码、测试、场景或协议状态机；git diff --check 通过"
  evidence: "docs/analysis/uniclaw-lifecycle-completeness-and-optimization-2026-10-12.md"
```

## Status log

- 2026-10-13 · UNDERSTAND → PERSIST · 根据用户反馈纠正文档主题；以场景推理和协议形式审计为主证据重写；新增独立 Change，避免与组件工程化审查 Change 混淆。
