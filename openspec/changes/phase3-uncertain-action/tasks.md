# Tasks — phase3-uncertain-action

> 实施前必读: `proposal.md` + `design.md` + `specs/**` + `scenarios/SC-P3-001-uncertain-action-verification.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；每项完成后先验证，再由 phase-evolution-controller 选择下一项。
> SC-P3-001 已冻结为 `BEHAVIOR_PURCHASE_ONLY`；Production Model Delta Budget = 0。
> 本 change 不购买新 production model type / field / enum / interface / component / mutable state，也不购买通用 uncertainty 或 retry framework。

## 1. Deterministic Scenario Proof Infrastructure

- [x] 1.1 **ScriptedEnvironment dispatch-timeout proof capability**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Scenario Receipt:** SC-P3-001
  - **Goal:** 扩展现有测试 Fake，使同一个确定性动作可以先应用既有世界转场，再返回 `ActionResultOutcome.TimedOut`；同时支持不应用世界效果但仍返回 `TimedOut` 的负向配置。
  - **Required Semantic:** dispatch outcome 与 world result 分离；`TimedOut` 只表示动作是否生效未知，测试必须能分别证明“effect applied”和“effect absent”。
  - **Approved Production Purchase:** 0 model delta；本任务仅购买测试侧确定性证明能力，不修改生产代码。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironment.cs`、`tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironmentVariants.cs`、`tests/UniClaw.Runtime.Tests/Scenario/ScriptedEnvironmentTests.cs` 中与该能力直接相关的最小改动。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`；生产 model/type/field/enum/interface/component；新测试 framework；Harness 重写；任何 Runtime ownership 或 authority 变化。
  - **Assertions:** 动作只记录一次；正向配置先应用既有转场再返回 `TimedOut`；负向配置保持世界不变并返回 `TimedOut`；后续 `ObserveAsync` 返回单调递增的新 Observation；相同配置与动作序列产生相同结果。
  - **Verification:** 运行 `ScriptedEnvironmentTests` 的定向测试；确认现有 Fake 变体测试保持通过；检查生产目录零改动。
  - **Deferred Boundary:** 不实现 Runtime 行为；不引入 production state；不引入通用 uncertainty/retry framework、Confidence、Popup、Scroll、Fingerprint、multi-container 或 FSM。

## 2. Traversal TimedOut Continuation

- [x] 2.1 **Route TimedOut through existing Observe and Verify flow**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Scenario Receipt:** SC-P3-001
  - **Goal:** 最小修改现有 Traversal 执行分支，使 `TimedOut` 不再立即返回 `Failed`，而是继续取得 fresh post-action Observation 并复用既有 Verify/Branch 流程；`Rejected` 行为保持不变。
  - **Required Semantic:** `TimedOut` 不证明 world success 或 confirmed world failure；任何进一步判断必须先观察，并且不得立即重复派发同一动作。
  - **Approved Production Purchase:** 0 model delta；只购买现有 Traversal/execution behavior 的局部调整。
  - **Allowed Scope:** `src/UniClaw.Runtime/Traversal/Traversal.cs` 与直接验证该分支的现有 Traversal 单元测试文件。
  - **Forbidden Scope:** `ActionResult`、`Observation`、`TraversalStepResult`、journal、Trace、WorldBelief、GoalEvidence 的形状；任何新 production type/field/enum/interface/component/mutable state；Agent/Container/Recovery 行为；现有 pre-dispatch retry 语义。
  - **Assertions:** `TimedOut` 后调用一次 `ObserveAsync`；同一动作不被重复派发；journal 保留原动作和 fresh post-action Observation；freshness 验证仍生效；`Rejected` 仍立即失败；`RetryCount` 不因 post-dispatch timeout 增加；`TraversalStepResult.Succeeded` 仍只表达本地协议获得所需后续证据，不表达 Goal 或世界语义成功。
  - **Verification:** 运行 Traversal 定向单元测试；运行 StepRetry 定向测试确认 SC-P2-002 仍为 dispatch 前 re-observe/re-resolve 且零动作派发；确认 production model delta 为 0。
  - **Deferred Boundary:** 不定义 timeout 后重试或升级政策；不路由到 `_maxRetries`；不实现 generic uncertainty/retry framework、Popup、Scroll、Fingerprint、Confidence、multi-container 或 FSM。

## 3. SC-P3-001 Formal Scenario Verification

- [x] 3.1 **Prove positive, negative, and deterministic replay branches**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Scenario Receipt:** SC-P3-001
  - **Goal:** 使用 Task 1.1 的确定性 Fake 能力和 Task 2.1 的 Traversal 行为，增加 SC-P3-001 正向、负向与重放场景测试。
  - **Required Semantic:** 正向分支由 fresh Observation 中的 world evidence 允许继续；负向分支不得从 `TimedOut` 编造 action/Goal success、不得盲目 redispatch；Run 完成仍仅由 satisfied `GoalEvidence` 决定。
  - **Approved Production Purchase:** 0 model delta；本任务只购买正式 Scenario 证据与必要的测试侧组合。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironmentVariants.cs`、`tests/UniClaw.Runtime.Tests/Scenario/ScenarioHarness.cs`、SC-P3-001 专用 Scenario 测试文件，以及直接复用的现有测试 helper。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`；Scenario/Spec 意义修改；新生产模型或组件；为负向分支发明 retry/escalation policy；Popup/Scroll/Fingerprint 相关测试或实现。
  - **Assertions:** 正向动作恰好派发一次且结果配置为 `TimedOut`；世界效果已应用；取得 fresh Observation；journal 携带原动作和该 Observation；运行沿既有 world/Goal evidence 流继续；负向世界不变、无 fabricated `GoalEvidence.Satisfied`、无由 `TimedOut` 导致的 `Completed`、无 duplicate dispatch；相同输入重放得到相同 ActionHistory、Observation 序列、journal、Trace、GoalEvidence 和 final state。
  - **Verification:** 运行 SC-P3-001 专用 Scenario 测试；运行 GoalEvidence completion 与 deterministic replay 相关测试；核对 Evidence Required 1–7 全部由现有 evidence surfaces 证明。
  - **Deferred Boundary:** 不购买 unverified timeout 的 retry policy；不新增 observation fingerprint、confidence 或 dispatch-outcome Trace field；不引入 generic uncertainty framework、Popup、Scroll、multi-container 或 FSM。

## 4. Regression and Boundary Validation

- [x] 4.1 **Independent Phase 3 slice regression validation**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Scenario Receipt:** SC-P3-001
  - **Goal:** 独立验证 SC-P3-001 的完整证据、Phase 1/2 回归、零模型增量和 deferred boundary。
  - **Required Semantic:** 证明 `TimedOut → Observe → Verify` 没有把 dispatch outcome 当 world result、没有改变 Goal completion authority，也没有泄漏到 SC-P2-002 pre-dispatch retry。
  - **Approved Production Purchase:** 0 model delta；本任务不购买或实施任何生产变化。
  - **Allowed Scope:** read-only 检查 repository diff、OpenSpec/Scenario 对照、build/tests/guards/consistency 执行；默认不修改文件。
  - **Forbidden Scope:** 修补 production 或 tests；修改 Scenario/Spec；接受未经验证的 coder 结论；新增任何 production artifact 或 deferred capability。
  - **Assertions:** SC-P3-001 Evidence Required 1–7 全部满足；production delta 仅为既有 Traversal 行为调整；新 model/type/field/enum/interface/component/mutable state 数量为 0；SC-P2-002 retry boundary 不变；Rejected 路径不回归；Phase 1/2 场景保持通过；无 Popup、Scroll、Fingerprint、Confidence、multi-container、generic uncertainty/retry framework 或 FSM 泄漏。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；独立检查 Scenario Receipt、production diff 与 deterministic replay 证据。
  - **Deferred Boundary:** 所有 SC-P3-001 之外的 Phase 3 candidate 与未来 Phase capability 保持缺席；发现语义、ownership、authority、invariant 或 model-delta 压力时停止并按 Result Contract 上报。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Traversal/` | `docs/system/layers/traversal-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `docs/system/scenarios/04-uncertain-action.md` |
| `openspec/changes/phase3-uncertain-action/` | `openspec/changes/phase3-uncertain-action/design.md` |
