# Tasks — phase2-trap-recovery

> 实施前必读: proposal.md + design.md + specs/** + scenarios/**
> 完成一项立即勾选 `- [x]`。
> 实施遵守: 宪章 §54（Responsibility → Authority → State Owner → Interfaces → Implementation → Verification）、§48（核心类九个问题）、§49（接口证明价值）；3 Scenario 共享同一 Runtime slice（延续 Phase 1 裁决 7 模式）。
> ⚠️ Human Gate HG-1..5 必须在 Phase 2A 启动前全部获批（proposal.md §Human Gate 决策点）。

## Phase 2A — Model Changes（Trap + Recovery types + field additions）

- [x] A1. **Trap model**: `Model/Trap.cs` — Trap(TrapKind, TrapScope, Source, ExpectedSeq?, ObservedSeq?, Recoverability, Evidence, LastAction?)
      + `Model/TrapKind.cs` enum（UnexpectedPage / WorldLost / StateMismatch / TargetLost / PlanInvalid / ContainerMismatch）
      + `Model/TrapScope.cs` enum（Step / Container / Agent）
      — 全部不可变 sealed record / enum；Expected/Observed 为观测序号引用（long?），非 Observation 快照（I-13）
- [x] A2. **RecoveryResult**: `Model/RecoveryResult.cs` — Verified | Failed(Reason)
- [x] A3. **RecoveryAnchor 字段**: +RestoreRecipe（string? — 恢复动作描述，注入数据），+EntryStrategy（string? — 入口策略描述）
      — 裁决 8 解除，Charter §20 字段落地
- [x] A4. **TraceEvent 字段**: +TrapKind?, +TrapScope?, +RecoveryId?（§28 因果链扩展）
- [x] A5. **TraversalJournalEntry**: +retry entries（re-observe SequenceNumber / re-resolve result markers）
- [x] A6. **Guard 5 修订（与 A1 原子完成，防 guard 空窗）**: Model 层 Trap 类型允许（`Trap|TrapKind|TrapScope`），`RecoveryRequest` 保持全目录禁止——正则缩小范围；详见 Phase 2D
- [x] A7. **Phase 1 回归**: Phase 1 路径不触发 Trap/Recovery 代码路径（A1-A5 + 修订后 Guard 5 全部通过；104 测试 + 5/6 Guards 保持——修订后 Guard 5 只缩小范围不减弱）

## Phase 2B — Recovery Mechanism（Behavioral implementation）

- [ ] B1. **Drift detection**（Agent）: Agent 在 post-action Observation 后检测 Agent-scope 漂移
      — ForegroundApplication 变化 + IsStillMine 失败 + 语义页面不可解析 → Agent-scope mismatch
      — HG-3 判定：依赖现有表面（ForegroundApplication + IsStillMine + SemanticPage），若 Scenario 证明不足再新增 DriftStatus 字段
- [ ] B2. **Trap emission**（Agent）: 结构化 Trap 作为 Agent-scope escalate 载体
      — Agent scope: Agent 发射（漂移检测 → Trap(Scope=Agent)）
      — Container scope: 枚举预留，Phase 3 消费（Popup）
      — Step scope: **不产生 Trap**——retry 耗尽走 Phase 1 `TraversalStepResult.Failed` 路径（I-12: Step-scope Trap 无 Scenario 购买）
- [ ] B3. **Agent Recovery path**: Trap(Scope=Agent) → Suspend Plan → Consume RecoveryAnchor.RestoreRecipe
      → Execute Recovery Actions（Relaunch / Navigate）→ Observe → Verify（VerificationCriteria）
      → VERIFIED: Reconcile → Rebind Container → Resume Plan → Continue
      — HG-4 判定：Recovery 逻辑形态（Agent 内方法 vs 独立 Recovery 组件）
      — HG-5 判定：最小化实现（不引入 RecoveryRequest/Planner/Runtime 除非 Scenario 断言需要）
- [ ] B4. **Step-scope retry**（Traversal）: Select 失败 → Re-observe → Re-resolve → 成功 → Continue
      — 有界（max retry count）、确定性（同输入同重试序列）、仅 re-observe（不派发动作）
      — 重试耗尽 → Failed(Reason) → escalate to Agent（走 Phase 1 失败路径）
- [ ] B5. **Recovery verification**: Verify 对照 VerificationCriteria
      — PASS → RecoveryResult.Verified → Reconcile → Resume
      — FAIL → RecoveryResult.Failed(Reason) → Agent.Fail(reason) → Run Failed
      — 不得假设"恢复动作成功 = 恢复完成"（I-9：Recovery 不是单个动作）
- [ ] B6. **Phase 1 回归**: Phase 1 全部 5 Scenario 不受影响
      — 恢复路径仅在 launcher-drift / flicker-target / unrecoverable 新 data variant 下触发
      — happy / startup-fg-fail / switch-stuck / missing-target / same-text 行为不变
      — 104 测试全绿 + 确定性重放保持

## Phase 2C — Verification（Scenario tests + Fakes + Regression）

- [ ] C1. **ScriptedEnvironment 新 data variant**: `launcher-drift`（SC-P2-001）
      — SettingsMain → Tap → NetworkSettings → 下次 Observe → ForegroundApplication="Launcher" + 无 Settings 元素
      — Relaunch(Settings) → ForegroundApplication="Settings" + SettingsMain 元素可见
- [ ] C2. **ScriptedEnvironment 新 data variant**: `flicker-target`（SC-P2-002）
      — Network Settings 首次 Observe 只含 "Bluetooth" → 第二次 Observe 才含 "WiFi"
- [ ] C3. **ScriptedEnvironment 新 data variant**: `unrecoverable`（SC-P2-003）
      — Relaunch(Settings) 后 ForegroundApplication 仍为 "Launcher"（恢复动作无效）
- [ ] C4. **SC-P2-001 场景测试**（Scenario/AgentRecoveryLauncherDriftTests.cs）:
      launcher-drift 变体 → Trap(Scope=Agent) → RecoveryAnchor 消费 → 恢复动作 → Verify → Resume → Complete
      + 断言：Trap 事件在 Trace、恢复动作与计划动作可区分、无从头重跑、确定性重放
- [ ] C5. **SC-P2-002 场景测试**（Scenario/StepRetryTests.cs）:
      flicker-target 变体 → Step retry → re-observe → re-resolve → Complete
      + 断言：journal 含重试条目、无 Trap 事件、Run 未 Failed、重试有界
- [ ] C6. **SC-P2-003 场景测试**（Scenario/RecoveryVerificationFailureTests.cs）:
      unrecoverable 变体 → 恢复动作 → 验证失败 → Run Failed（Reason 显式）
      + 断言：Trace 含验证失败事件、无 Resume 标记、无恢复后的 Plan 步骤
- [ ] C7. **Phase 1 全量回归**: 全部 104 测试 + 确定性重放 + 所有 Scenario
- [ ] C8. **Full build + test**: `dotnet build` 0 警告 0 错误；`dotnet test` 全绿；`check-consistency.sh` ALL PASS

## Phase 2D — Guard Updates（Architecture Guards 新增——Guard 5 修订已随 Phase 2A 原子完成）

- [ ] D1. **Guard 7 新增**: Recovery 组件不得引用 Container/Traversal 内部实现（I-1 依赖方向）
      — 扫描 `src/UniClaw.Runtime/Recovery/` 下 .cs 文件，禁止 `using UniClaw.Runtime.Container` 与 `using UniClaw.Runtime.Traversal`
- [ ] D2. Guard 1-4 + Guard 6 保持不削弱
- [ ] D3. 全部 Guards 通过（6 + Guard 7 new）+ Phase 1 回归

## Phase 2E — Final Verification（Phase Gate）

- [ ] E1. Full build 0 警告 0 错误
- [ ] E2. All tests: Phase 1 104 + Phase 2 new → 全部通过
- [ ] E3. Phase 2 Scenario assertions（SC-P2-001 / SC-P2-002 / SC-P2-003）全部满足
- [ ] E4. Architecture Guards（1-4 + 5 revised + 6 + 7 new）全部通过
- [ ] E5. `scripts/check-consistency.sh` ALL PASS
- [ ] E6. Deterministic replay（SC-P1-001 断言 7 + SC-P2-001 恢复路径重放）
- [ ] E7. Phase Boundary audit（Phase 3 deferred 保持缺席：Popup / Uncertain / Scroll / Fingerprint / coordinate）
- [ ] E8. Phase 2 independent acceptance（runtime-validator）
