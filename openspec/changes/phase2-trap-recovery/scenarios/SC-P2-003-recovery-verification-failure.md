# SC-P2-003 — Recovery Verification Failure: 未验证不得 Resume

> Phase 2 Trap & Recovery | Scenario-first
> 宪章依据: §42 RecoveryFailure Scenario / §23 I-9 负向面
> 消费者: specs/recovery-verification.md

## Goal

验证 I-9 的负向面：恢复动作已执行但世界不配合 → 验证失败 → Run Failed（显式原因），不得 Resume。锁定"恢复动作成功 ≠ 恢复完成"这一 I-9 核心语义。

## Given（Initial World — ScriptedEnvironment 数据变体 unrecoverable）

```
同 SC-P2-001 的漂移触发条件:
  Screen 1: Settings Main → Tap → Screen 2: Network Settings
  → 漂移注入: 下一次 Observe 时 ForegroundApplication = "Launcher"

Recovery 注入（不可恢复）:
  Relaunch(Settings) 后 ForegroundApplication 仍为 "Launcher"（恢复动作无效）
  → VerificationCriteria 对照失败
```

RecoveryAnchor（同 SC-P2-001）:
- ApplicationIdentity = "Settings"
- VerificationCriteria = "ForegroundApplication == Settings && SettingsMain 元素可见"
- RestoreRecipe = "Relaunch(Settings)"

## When（恢复动作执行但验证失败）

```
同 SC-P2-001 的检测路径:
  → Agent-scope Trap（Kind=UnexpectedPage, Scope=Agent）
  → Agent 消费 RecoveryAnchor.RestoreRecipe
  → ExecuteAsync(LaunchApp(Settings))
  → Observe → ForegroundApplication 仍为 "Launcher"（恢复无效）
  → Verify: ForegroundApplication ≠ "Settings" → 验证失败
```

## Then（验证失败 → Run Failed，不得 Resume）

```
RecoveryResult.Failed(Reason: "恢复验证失败：期望 Foreground==Settings，实际==Launcher（seq=N）")
  → Agent 判定 Run Failed（Reason 记录于 Trace）
  → 无 Resume、无后续 Plan 步骤执行
  → ActionHistory 不含恢复后的计划动作（仅原计划动作 + 恢复动作）
  → RunState = Failed（终态）
```

## Evidence Required

1. **Trace 含验证失败事件**：RecoveryId 关联的 Verify 步骤 → Failed（Reason 显式提及验证期望与实际）
2. **RunState = Failed**（非 Completed）
3. **Trace 无 Resume 事件**：无 Container 重建事件、无 Plan 恢复后的 Step 事件
4. **ActionHistory**：LaunchApp(启动) → Tap(Network) → **LaunchApp(恢复)** → 结束（无 Tap(WiFi) / SetSwitch(ON) 等恢复后动作）
5. **Reason 显式**：Failed Reason 非空，语义源自恢复验证失败（而非 Plan 耗尽 / 步骤失败等原因）
6. **无盲 Resume**：不存在"恢复动作 Dispatched → 直接继续 Plan"的代码路径（I-9 机械保证）
7. **确定性**：同 runId 同输入 → 同验证失败 Trace

## Expected Authority

| 决策 | Authority | 宪章依据 |
|------|-----------|---------|
| 执行恢复动作 | Agent | I-8 |
| 验证恢复结果 | Agent（使用注入 VerificationCriteria） | I-9 |
| 判定恢复失败、终止 Run | Agent | Phase 1 authority 保留 |
| 判定是否 Resume | Agent（验证失败 → 不得 Resume） | I-9 |

## Architecture Pressure

- **I-9 负向**：Recovery 必须经过 Observation + Verification——恢复动作返回成功不证明恢复完成
- **RecoveryFailure 独立终止路径**：区别于 Phase 1 的步骤失败（SC-P1-004）与证据不满足（SC-P1-003 负向）——原因是"恢复验证失败"
- **裁决 10 延续**：dispatch ≠ world success 在恢复语境中同样成立——LaunchApp Dispatched 不证明已回到 Settings
- **VerificationCriteria 首次语义消费**（Phase 1 只有非空断言）——Phase 2 的 Verify 步骤将其用于判定恢复成败
