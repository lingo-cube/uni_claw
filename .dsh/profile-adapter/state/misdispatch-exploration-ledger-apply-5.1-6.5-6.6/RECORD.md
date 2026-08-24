# 隔离记录：错误派发 `exploration-ledger-apply-5.1-6.5-6.6`

状态：**QUARANTINED — 未验收，不接受其结果或 ModuleContext Delta**

- 建立时间：2026-08-25（DSH UniFlow 机械强制闭环修复期间）
- 关联 WorkItem id：`exploration-ledger-apply-5.1-6.5-6.6`
- 来源：历史错误派发（非法的非 WorkItem 任务说明直接派发 / 未经验证的模型绑定），
  其产生的工作树 diff 在此隔离。

## 隔离内容

本次隔离记录的是该错误派发在工作树中留下的实际 diff（相对于 HEAD eac69ee）：

| 文件 | 快照 |
|---|---|
| `src/UniClaw.Runtime/Agent/Agent.cs` + `Agent.OpenWorld.cs` | `tracked-diff.patch` |
| `src/UniClaw.Runtime/Model/ExplorationLedger.cs`（新增） | `ExplorationLedger.cs.snapshot` |
| `src/UniClaw.Runtime/Model/ExplorationLedgerCompiler.cs`（新增） | `ExplorationLedgerCompiler.cs.snapshot` |
| `tests/UniClaw.Runtime.Tests/Unit/ExplorationLedgerTests.cs`（新增） | `ExplorationLedgerTests.cs.snapshot` |
| `tests/UniClaw.Runtime.Tests/Unit/ExplorationDepthBoundaryTests.cs`（新增） | `ExplorationDepthBoundaryTests.cs.snapshot` |
| `tests/UniClaw.Runtime.Tests/Architecture/ExplorationLedgerAuthorityGuardTests.cs`（新增） | `ExplorationLedgerAuthorityGuardTests.cs.snapshot` |

注：`Agent.cs`/`Agent.OpenWorld.cs` 的 `tracked-diff.patch` 也包含同一工作树内其他
既有未提交改动（PreTerminalCycle 等）。隔离记录的是**文件级现状快照**，不试图从
不可分割的工作树中进一步拆分单个符号级的归因。

## 重新验收状态（2026-08-25 更新）

已通过新 Gateway 为 `exploration-ledger-apply-5.1-6.5-6.6` 生成合法 JSON WorkItem
（execution_profile=`development`、module_profile=`runtime-core`、唯一
worker_owner=`module-worker-ledger-apply`、Leader 冻结 Agent 暴露形式为只读
`CompileExplorationLedgerView`、无活跃 Run fail-closed），并完成独立验收：

1. `validate_work_item` 通过（schema/profile/owner/scope/冻结/无未决架构）。
2. `DshWorkflowRuntime.dispatch_work_item()` 经 DispatchGate 校验；默认 Host 无
   能力时在写入前 `ROUTING_CAPABILITY_LIMIT` fail-closed（正确）。
3. Envelope 携带完整 requested binding：
   `implementation_efficient → opencode-go/deepseek-v4-flash/medium` +
   profile_version + binding_revision/digest + work_item id + worker_owner；
   解包后通用 WorkItem 保持上游校验通过（无污染）。
4. 隔离快照与工作树 diff 一致（ExplorationLedger.cs / Compiler.cs IDENTICAL）。
5. 独立验收证据：`dotnet build` 0 错误 0 警告；
   `--filter ExplorationLedgerTests|ExplorationDepthBoundaryTests|
   ExplorationLedgerAuthorityGuardTests` → **28/28 通过**
   （覆盖 5.1 只读投影、6.5 台账满足不替代 GoalEvidence/FSM 完成authority、
   6.6 活跃 Run 深度不可变反射断言）。
6. ModuleContext Delta：本次 WorkItem 属于工程治理模块的重新验收，非新 Delta；
   既有 ledger diff 不重复实现已合格部分。

## 约束

1. 在 DSH Host 模型绑定闭环修复并重新验收前，不将以上 diff 视为已验收产物，
   不接受其结果，也不接受其 ModuleContext Delta（不写入 `state/module-context.json`）。
2. 不得提交、重置、清理、整文件回退或覆盖这些 diff 中的无关修改。
3. 重新验收路径：由修复后的 `DshWorkflowRuntime.dispatch_work_item()` 生成合法
   JSON WorkItem（execution_profile=`development`，module_profile=`runtime-core`，
   唯一 worker_owner，Leader 冻结 Agent 暴露形式与无活跃 Run 行为），经新的
   强制 Gateway 派发，核对 Host 实际模型回执，再独立验收本隔离区 diff；
   不重复实现已合格部分。