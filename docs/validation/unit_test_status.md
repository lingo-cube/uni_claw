# Unit Test Status

**Project**: UniClaw.Core  
**Version**: Phase 2.3  
**Change**: globalfsm-activation  
**Task**: 6 - Validation (build + full test suite + openspec validate)  
**Generated**: 2026-07-15  
**Git Branch**: feature/refactor  
**Git Commit**: d685791 (base; change uncommitted)

---

## Executive Summary

GlobalFSM 激活完成（B2 — 激活已有代码，非新增功能）。`SessionContext` 持有 `GlobalFSM` 实例，废除 `GlobalState` public setter；所有正常状态变更走 `TransitionTo()`（8 状态矩阵校验 + 回调 + 历史）；新增 `internal ForceState()` 恢复路径（PopupHandler/StateRestorer，绕过矩阵、不触发回调、记录 `"force_restore"` 历史）；`TraversalEngine` 注册 trace callback，GlobalFSM 转换写入 `StateTransition(FsmType="GlobalFSM")`。
全量测试 **677/677 通过**（671 基线 + 6 新增 spec-scenario 测试），0 失败，0 跳过。
`openspec validate globalfsm-activation` — valid。

| Metric | Value |
|--------|-------|
| Total Tests | **677** |
| Passed | **677** |
| Failed | **0** |
| Error | **0** |
| Skipped | **0** |
| Build | 0 errors |
| Duration | ~1s |

**Overall Status**: ✅ PASSED

## Module-Scoped Results (this change)

Data source: `.claude/skills/module-test/contracts/state_machine_unit.json` (2026-07-15T15:30:00Z, FRESH)

| Scope | Total | Passed | Failed | Notes |
|-------|-------|--------|--------|-------|
| StateMachine | 127 | 127 | 0 | GlobalFSM/SessionContext/TraversalRuntimeContext/StateRestorer regression-clean |
| Traversal | 145 | 145 | 0 | TraversalEngine lifecycle + trace callback tests |
| Architecture Guards | 46 | 46 | 0 | enum locks + dependency direction unchanged |
| **Full suite** | **677** | **677** | **0** | |

## New Spec-Scenario Tests (6)

| Test | Spec Scenario | Status |
|------|---------------|--------|
| `ForceState_RecordsHistoryWithoutCallbacks` | ForceState records history without callbacks | ✅ PASS |
| `SetGlobalState_InvalidTransition_Throws` | Invalid transition throws DomainValidationException | ✅ PASS |
| `SetGlobalState_ValidTransition_RecordsHistoryAndInvokesCallback` | Valid transition succeeds and records history | ✅ PASS |
| `StopAsync_TwoStepTermination_RecordsHistory` | (design decision) Traversing→Paused→Terminated | ✅ PASS |
| `GlobalFsmTransitions_TracedWithGlobalFsmType` | GlobalFSM Completion transition is traced | ✅ PASS |
| `ForceState_DoesNotProduceTraceRecords` | ForceState does not produce trace records | ✅ PASS |

其余 2 个 spec scenario 为编译级保证：`GlobalState` 只读（setter 已删除）、`ForceState` 不在 `IGlobalStateMachine` 接口上。

## Design Coverage

Doc: `docs/system/layers/state-machine.md` ⚠️ **doc_outdated** — GlobalFSM 激活状态与 SessionContext 新属性（`GlobalStateMachine`/`InternalGlobalFSM`）需 archive 时同步。

| Component | Change | Direct tests |
|-----------|--------|--------------|
| `GlobalFSM` | +`internal ForceState()` | ✅ GlobalFSM suite（矩阵/回调/历史）+ ForceState 新测试 |
| `SessionContext` | raw field → GlobalFSM 实例; setter 删除; +2 属性 | ✅ SetGlobalState 路由测试 + 编译级只读 |
| `TraversalRuntimeContext` | `SetGlobalState(value, reason?)` → TransitionTo; +`internal ForceGlobalState` | ✅ 2 新测试（invalid throws / valid records） |
| `StateRestorer` (PopupHandler.cs) | RestoreState → `ForceGlobalState` | ✅ PreserveAndRestore_All5FieldsMatch（setup 改合法路径） |
| `TraversalEngine` | trace callback 注册 + reason 参数 + 两步终止 | ✅ 3 新测试 + 既有 lifecycle/RunAsync suites |

**Gaps**: 无 — 5/5 changed classes 有测试覆盖，8/8 spec scenarios 已验证。

## Design Deviations Found & Resolved During Apply

| # | Deviation | Resolution |
|---|-----------|------------|
| 1 | proposal 称"已验证 2 个调用点" → TraversalEngine 实际有 7 处 `SetGlobalState`，3 处矩阵非法 | 全部调用点逐一审计并修正（见 2-4） |
| 2 | RunAsync catch 块 `SetGlobalState(Error)` + `Done(Error)` 双重设置 → `Error→Error` 会在 catch 内抛异常，破坏 Log-and-Continue | 删除 catch 块冗余设置，`Done()` 统一设置 |
| 3 | `StopAsync` / `Done(Cancelled/Timeout)` 需 `Traversing→Terminated`，矩阵（C#/Python 一致）无此直边 | 用户决策: 两步终止 `Traversing→Paused("stopping")→Terminated`（矩阵保持锁定；拒绝 ForceState 旁路与矩阵扩展） |
| 4 | `StateRestorerTests` setup 走 `Idle→Traversing`（矩阵非法） | setup 改为合法路径 `Idle→Initializing→Traversing`（断言未动） |
| 5 | spec 称 `RegisterStateCallback` 经 `IGlobalStateMachine` 可达 → 接口实际未声明该方法（在具体类上） | 保持接口不变（避免 method-count guard 扰动）；注册经 `InternalGlobalFSM`；archive 时修正 spec 措辞 |

## Cross-Module Contract Aggregation

⚠️ **Data Freshness Warning**: 部分 contract 文件为历史快照，早于本次全量运行（当前全量 677/677 绿是权威状态）。

| Module | Tests (P/F) | Timestamp | Freshness |
|--------|------------|-----------|-----------|
| state_machine (this change) | 677/0 | 2026-07-15 15:30 | ✅ FRESH |
| traversal (steporchestrator-decomposition) | 671/0 | 2026-07-15 13:38 | ✅ FRESH |
| graph (graph-service-model-separation) | 670/0 | 2026-07-15 13:27 | ✅ FRESH |
| simulation-expected-behavior | 665/0 | 2026-07-15 | ✅ FRESH |
| v6_9_plan_compilation | 192/0 | 2026-06-07 | 🔴 VERY STALE (>7d) |
| e2e_test | 2/1 | 2026-06-06 | 🔴 VERY STALE (>7d) — 历史快照 |
| simulation | 28/0 | 2026-06-06 | 🔴 VERY STALE (>7d) |
| trace | 123/0 | 2026-06-06 | 🔴 VERY STALE (>7d) |

历史 contract 中的 failed 计数不代表当前状态；当前全量 suite 0 failures。

## Code Changes

| File | Change |
|------|--------|
| `StateMachine/GlobalFSM.cs` | +`internal ForceState(targetState)` — 绕过矩阵、记录 `"force_restore"` 历史、不触发回调 |
| `StateMachine/Session/SessionContext.cs` | `_globalState` raw field → `_globalFsm` 实例; `GlobalState` 只读; +`GlobalStateMachine` (public IGlobalStateMachine) + `InternalGlobalFSM` (internal) |
| `StateMachine/TraversalRuntimeContext.cs` | `SetGlobalState(value, reason?)` → `TransitionTo()`; +`internal ForceGlobalState(value)` → `ForceState()` |
| `StateMachine/PopupHandler.cs` | `StateRestorer.RestoreState`: `SetGlobalState` → `ForceGlobalState`（恢复语义） |
| `Traversal/TraversalEngine.cs` | +`RegisterGlobalFsmTraceCallbacks()`（Completed/Error/Traversing/Idle → `StateTransition(FsmType="GlobalFSM")`）; 全部调用点带 reason; catch 块冗余 Error 设置删除; `Done()`/`StopAsync` 两步终止 + 幂等守卫 |
| `tests/.../StateMachineTests.cs` | StateRestorer setup 合法路径修正; +3 spec-scenario 测试 |
| `tests/.../TraversalEngineTests.cs` | +3 spec-scenario 测试（两步终止历史 / GlobalFSM trace / ForceState 无 trace） |

## Conclusions

- ✅ 19/19 tasks 完成；`GlobalFSM` 从零实例化变为被 `SessionContext` 持有并激活
- ✅ 转换历史激活: `GetTransitionHistory()` 遍历结束非空（两步终止测试直接断言）
- ✅ Trace 激活: `StateTransition.FsmType = "GlobalFSM"` 记录出现（含 Reason 透传）
- ✅ 5 处设计偏差发现即修正（1 项经用户决策，非静默偏离）
- ⚠️ Archive 待办: 同步 `layers/state-machine.md`（GlobalFSM 激活 + SessionContext 新属性 + 两步终止语义）、修正 spec `RegisterStateCallback` 接口措辞、decisions/log 记录两步终止决策
- Ready for archive

---

*Report generated per validation-documentation skill standards. Data source: `.claude/skills/module-test/contracts/` (standardized JSON contracts).*
