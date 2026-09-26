# PER-011 Visual + Hierarchy Fusion（设计计划）

版本：v0.1.1 narrow amendment；保持 design-only/FROZEN。

## 前置

`changes/PER-010/state.md` 的 `design_status: FROZEN` 已成立。PER-011 不重新定义 hierarchy semantics。

## 设计交付

- `changes/PER-011/spec.md`：Source Authority Matrix、Temporal Alignment Diagram、Conflict Matrix、Failure Matrix、Fusion Scenarios、association/provenance/escalation 边界。
- `changes/PER-011/plan.md`：垂直切片、acceptance、后续 implementation slices。
- `changes/PER-011/state.md`：WHAT/WHY、冻结决策、Grill disposition、验证声明。

本次只修三点：checked capability inheritance、derived evidence lineage/no-self-corroboration、以及 `Aligned = eligible for joint fusion` 的时间语义。

## 关键验证问题

1. hierarchy 是否被误造为第二 WorldModel；
2. confidence 是否替代 field authority；
3. stale/fresh source 是否被跨 mutation 强行融合；
4. omission 是否被变成 false/absence；
5. Compose/virtual list/overlay 是否显式允许多对一、一对多和歧义；
6. fused result 是否绕过 P2、WorldModel、Grounding 直接成为 target；
7. provenance 是否能回溯每个 source evidence、rule/version、coverage、conflict；
8. escalation 是否由 buyer 和 bounded budget 约束。

## 后续实现切片（本轮不执行）

source fixtures → association fixtures → derived proposal ingress → bounded escalation telemetry → WorldModel scenario verification。
