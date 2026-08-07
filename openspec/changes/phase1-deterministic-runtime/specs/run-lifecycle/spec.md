# Run Lifecycle, Startup & World Belief

## Requirement

Run 从初始化到完成（或失败）必须遵循明确的生命周期；Startup 必须先于正式执行并建立 RecoveryAnchor；
World Belief 必须由 Observation 生成且与 Runtime State 严格分离；完成必须由 Goal Evidence 证明。

## Motivation

宪章 §18（Global Lifecycle 不承担世界判断与页面恢复）、§19（Startup 是明确的一次生命周期阶段）、
§20（Recovery Anchor 是 Startup 的重要产物）、§10（Observation ≠ Semantic Truth，belief 允许未知）、
§43 / I-10（Completion 必须由 Goal Evidence 证明）。旧系统教训：lifecycle 与 intelligence 混杂、
Context 混装 runtime state 与 belief（I-11）。

## Scenario

Given 一个可确定性观察的 Fake Environment；
When Run 开始执行 Goal "Enable WiFi"；
Then 生命周期按 `Idle → Initializing → Running → Completed` 推进，
且进入 `Completed` 之前 Startup 已建立 RecoveryAnchor、最终判定携带 Goal Evidence。

## SHALL

- SHALL 定义 RunState 枚举：`Idle / Initializing / Running / Completed / Failed`（`Terminated` 预留，本阶段不实现语义）。
- SHALL 在 `Initializing` 阶段执行 Startup；Startup 报告 Ready 之前不得进入 `Running`。
- SHALL Startup 按 §19 顺序执行：Attach → Launch → Observe → Resolve Initial Semantic World → Establish Initial Container → Establish Recovery Anchor → Ready。
- SHALL Startup 建立 RecoveryAnchor（含 ApplicationIdentity / EntryStrategy / ExpectedSemanticEntry / RestoreRecipe / VerificationCriteria），其内容可支持未来完全迷失时恢复到可信入口。
- SHALL 从 Observation 生成 WorldBelief，携带 Confidence / Evidence / Source / Timestamp。
- SHALL WorldBelief 允许 `Unknown / Uncertain / Conflicting` 状态；证据不足时不得假装确定。
- SHALL WorldBelief 与 Runtime State 严格分离：belief 记录"认为现实是什么"，runtime state 记录"程序内部执行状态"；禁止混入同一可变对象。
- SHALL Run 进入 `Completed` 仅当 Goal Evidence 满足；否则进入 `Failed` 并记录原因（I-10，禁止无证据启发式完成）。
- SHALL 生命周期状态不承担任何世界判断、页面恢复或 Agent Intelligence（§18）。
