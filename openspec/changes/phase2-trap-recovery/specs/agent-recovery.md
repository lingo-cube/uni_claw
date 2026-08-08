# Agent-scope Recovery — Capability Spec

> Phase 2 Trap & Recovery | 契约层级: Capability SHALL
> 消费者: SC-P2-001 (Launcher drift → recover → resume → complete)
> 宪章依据: §35 Recovery Scenario / §23 Recovery 完整协议 / §20 RecoveryAnchor 消费 / I-8 I-9

## SHALL

### Recovery 触发

- **SHALL** Agent-scope Trap（Kind=UnexpectedPage / WorldLost）触发 Agent Recovery 路径。
- **SHALL** Agent 在启动恢复前挂起当前 Plan 位置（saved step index / saved Container reference——Phase 2 最小实现）。
- **SHALL** Agent 不得委托 Container / Traversal 执行恢复动作（I-8：恢复 authority 唯一在 Agent）。

### RecoveryAnchor 消费

- **SHALL** Agent 消费 `RecoveryAnchor.RestoreRecipe`（注入的恢复动作描述）生成恢复动作序列。
- **SHALL** `RestoreRecipe` 为注入数据（如 "Relaunch(Settings) → Observe → Verify Foreground==Settings"），不硬编码恢复策略。
- **SHALL** Agent 消费 `RecoveryAnchor.EntryStrategy`（注入的入口策略）作为恢复后的首个容器判定依据。
- **SHALL** RecoveryAnchor 的 `ApplicationIdentity` / `ExpectedSemanticEntry` / `VerificationCriteria` 保留 Phase 1 语义。

### Recovery 执行

- **SHALL** 恢复动作通过 `IEnvironment` 执行（Phase 1 端口不变）。
- **SHALL** 恢复动作序列记录于 Trace（RecoveryId 关联），与正常计划动作可区分。
- **SHALL** 恢复动作后必须重新 Observe（§3：Internal Runtime State ≠ External World State）。

### Recovery 验证（I-9 full loop）

- **SHALL** 恢复动作后，Agent 必须使用注入的 `VerificationCriteria` 对照 post-recovery Observation 进行验证。
- **SHALL** 验证 PASS → `RecoveryResult.Verified` → Reconcile → Agent 重建/重绑 Container（从 ExpectedSemanticEntry 判定初始容器）→ Resume Plan（从挂起位置继续）。
- **SHALL** 验证 FAIL → `RecoveryResult.Failed(Reason)` → Run Failed（显式原因记录于 Trace；Reason 含验证期望与实际）。
- **SHALL NOT** 假设恢复动作成功 = 恢复完成（I-9：Recovery 不是单个动作，是完整协议）。

### Resume

- **SHALL** Resume 后必须重新走 Goal evidence evaluator 评估（I-10：恢复 ≠ 完成——SC-P1-003 语义保留）。
- **SHALL** Resume 后的 Plan 步骤从挂起位置继续（不从头重跑全部 Plan）。
- **SHALL** Container / Step 历史保留（Phase 1 journal 不受恢复影响——追加式）。

### 禁止

- **SHALL NOT** Traversal / Container 自行发起恢复动作（PressBack / LaunchApp / 重试——I-8: 低层不得私自恢复）。
- **SHALL NOT** 从头重跑整个 Run（§35："不得直接从任务头重新执行一切"）。
- **SHALL NOT** 恢复成功后跳过 Goal evidence（I-10: 完成仍由证据判定）。

## 与 Phase 1 的关系

- Phase 1 `Agent.Fail()` 路径保留——Agent-scope Trap 后如果恢复验证失败，最终走同一 Fail 终止路径。
- Phase 1 失败路径（SC-P1-004 missing-target）不受影响——missing-target 是 Step-scope 失败，不是 Agent-scope 漂移。
- RecoveryAnchor 的 Phase 1 字段语义不回退（SC-P1-001 断言 2 / SC-P1-002 断言 3 仍然成立）。
