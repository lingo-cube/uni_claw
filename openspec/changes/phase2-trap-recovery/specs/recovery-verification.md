# Recovery Verification — Capability Spec

> Phase 2 Trap & Recovery | 契约层级: Capability SHALL
> 消费者: SC-P2-003 (unrecoverable → verify fail → Run Failed)
> 宪章依据: §23 Recovery / §42 RecoveryFailure Scenario / I-9 / I-10

## SHALL

### Verification 协议

- **SHALL** 恢复动作后，Agent 必须执行 Verify 步骤：使用注入的 `VerificationCriteria`（字符串或 `Func<Observation, bool>`——Phase 2 最小实现）对照 post-recovery Observation。
- **SHALL** VerificationCriteria 语义由调用侧注入（如 "ForegroundApplication == Settings && SettingsMain 元素可见"），Runtime 不硬编码验证逻辑。
- **SHALL** 验证结果记录于 Trace（`RecoveryResult.Verified` 或 `RecoveryResult.Failed(Reason)`）。

### 验证成功（I-9 正向）

- **SHALL** 验证 PASS → `RecoveryResult.Verified` → Agent 执行 Reconcile → 重建/重绑 Container → Resume Plan。
- **SHALL** Verified 事件携带 RecoveryId + 验证依据（对照 VerificationCriteria 的观测证据）。

### 验证失败（I-9 负向，SC-P2-003）

- **SHALL** 验证 FAIL → `RecoveryResult.Failed(Reason)` → Agent 判定 Run Failed。
- **SHALL** Failed Reason 显式提及验证期望与实际观测（如 "恢复验证失败：期望 Foreground==Settings，实际==Launcher（seq=N）"）。
- **SHALL** 验证失败后不得 Resume（无后续 Plan 步骤执行、无 Container 重建）。
- **SHALL** 验证失败事件记录于 Trace（RecoveryId + Reason + 期望 vs 实际）。

### 禁止

- **SHALL NOT** 跳过验证直接 Resume（I-9 机械执行：恢复动作返回成功 ≠ 恢复完成）。
- **SHALL NOT** 在验证失败后继续执行 Plan（§23："如果无法验证：Recovery 仍然未完成"）。
- **SHALL NOT** 把恢复动作的 ActionResult.Dispatched 等同于恢复成功（裁决 10 延续：dispatch ≠ world success）。

## 与 Phase 1 的关系

- Phase 1 `Agent.Fail()` 是恢复验证失败后的最终路径（与 Phase 1 失败路径收敛到同一终止 authority）。
- SC-P1-003 的 dispatch ≠ completed（裁决 10）语义延续到恢复语境：恢复动作 dispatch 结果不证明恢复成功。
- Phase 1 RecoveryAnchor.VerificationCriteria 的"非空"断言（SC-P1-001 断言 2）保留——Phase 2 增加语义消费（用于验证判定）。
