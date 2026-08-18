# run-lifecycle Specification

## Purpose
TBD - created by archiving change phase1-deterministic-runtime. Update Purpose after archive.

## Requirements

### Requirement: Run Lifecycle, Startup & World Belief

**SHALL**

Run 从初始化到完成（或失败）必须遵循明确的生命周期；Startup 必须先于正式执行并建立 RecoveryAnchor；
World Belief 必须由 Observation 生成且与 Runtime State 严格分离；完成必须由 Goal Evidence 证明。

**Motivation**

宪章 §18（Global Lifecycle 不承担世界判断与页面恢复）、§19（Startup 是明确的一次生命周期阶段）、
§20（Recovery Anchor 是 Startup 的重要产物）、§10（Observation ≠ Semantic Truth，belief 允许未知）、
§43 / I-10（Completion 必须由 Goal Evidence 证明）。旧系统教训：lifecycle 与 intelligence 混杂、
Context 混装 runtime state 与 belief（I-11）。

**SHALL**

- **SHALL** 定义 RunState 枚举：`Idle / Initializing / Running / Completed / Failed`（`Terminated` 预留，本阶段不实现语义）。
- **SHALL** 在 `Initializing` 阶段执行 Startup；Startup 以 `StartupResult` 报告
  `Ready(RecoveryAnchor)` 或 `NotReady(原因)`；Ready 之前不得进入 `Running`（SC-P1-001 / SC-P1-002）。
- **SHALL** Startup 按 §19 顺序执行：Attach → Launch → Observe → Verify ForegroundApplication
  （确认 Observation 已呈现目标应用，作为解析语义入口的依据 — 裁决 7 的消费者）→
  Resolve Initial Semantic World → Establish Initial Container → Establish Recovery Anchor → Ready。
- **SHALL** Verify ForegroundApplication 失败时 Startup 报告 `NotReady(显式原因)`（SC-P1-002）：
  Run 不得进入 `Running`、RecoveryAnchor 不得建立、RunState 进入 `Failed`（记录显式原因）、
  不执行任何恢复动作（Phase 1 无 recovery 机制；Environment action history 只含 Launch + Observe，
  无 PressBack / 重新 Launch / 重试）。
- **SHALL** Startup 建立 RecoveryAnchor，记录建立可信恢复入口当前必须的数据：
  ApplicationIdentity / ExpectedSemanticEntry / VerificationCriteria（裁决 8）。
  EntryStrategy / RestoreRecipe 属恢复规划数据，恢复执行机制消费时（Phase 2）再引入；
  本阶段不创建 recovery planning / execution / FSM。
- **SHALL** 从 Observation 生成 WorldBelief，携带 SemanticPage / Confidence / Evidence /
  SourceObservationSequence（对支撑观测序列的引用）。WorldBelief 不复制场景特定语义字段
  （如 WiFi Switch 状态），Goal 完成判定直接基于 Observation evidence（裁决 2）。
- **SHALL** WorldBelief 允许 `Unknown / Uncertain / Conflicting` 状态；证据不足时不得假装确定。
- **SHALL** WorldBelief 与 Runtime State 严格分离：belief 记录"认为现实是什么"，runtime state 记录"程序内部执行状态"；禁止混入同一可变对象。
- **SHALL** Run 进入 `Completed` 仅当 Goal evidence evaluator（Goal 携带或接收的最小判定器，裁决 3）
  对 Observation evidence 判定成立并记录原因；动作 dispatch 结果（Dispatched / TimedOut / Rejected）
  与其它无证据启发均不构成完成判定（I-10）。否则进入 `Failed` 并记录原因。
- **SHALL** evaluator 的判定以 `GoalEvidence` 值表达：`Satisfied / Reason / SourceObservationSequence`
  （证据必须引用其依据的 Observation 序号；SC-P1-003）。GoalEvidence 是值类型，
  不是 GoalEvidenceSpec 层级（该层级仍 DEFER — 裁决 3）。
- **SHALL** 每次 post-action Observation 后由 Goal evidence evaluator 评估（SC-P1-003）：
  Plan 步数耗尽本身不构成完成判定（§43），动作 dispatch 结果不构成完成判定 —
  只有 evaluator 从 post-action Observation 产生 Satisfied 的 GoalEvidence 才能进入 `Completed`；
  Plan 耗尽 / 证据不满足 → `Failed` 并记录原因（负向变体）。
- **SHALL** 完成判定逻辑不得硬编码场景字符串（如 "WiFi" / "Network & Internet"）；场景 target / action
  数据由调用侧 / Scenario 输入注入（裁决 3 / 11）。本阶段不创建 GoalGraph / GoalEngine /
  GoalEvidenceSpec 层级。
- **SHALL** 生命周期状态不承担任何世界判断、页面恢复或 Agent Intelligence（§18）。

#### Scenario: Run Lifecycle, Startup & World Belief

Given 一个可确定性观察的 Fake Environment；
When Run 开始执行 Goal "Enable WiFi"；
Then 生命周期按 `Idle → Initializing → Running → Completed` 推进，
且进入 `Completed` 之前 Startup 已建立 RecoveryAnchor、最终判定携带 Goal Evidence。
