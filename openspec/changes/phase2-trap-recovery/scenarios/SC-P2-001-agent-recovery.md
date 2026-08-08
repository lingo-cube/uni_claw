# SC-P2-001 — Agent Recovery: Launcher Drift 恢复并继续

> Phase 2 Trap & Recovery | Scenario-first
> 宪章依据: §35 Recovery WiFi Scenario / §23 Recovery 完整协议 / §20 RecoveryAnchor 消费
> 消费者: specs/agent-recovery.md / specs/trap-detection.md / specs/recovery-verification.md

## Goal

验证 Agent-scope Trap 检测 + RecoveryAnchor 消费 + I-9 全闭环：世界漂移到 Launcher → Agent-scope Trap → 恢复（relaunch + verify）→ Resume → 继续 Plan → Complete。不得从头重跑（§35），不得由 Traversal 私自恢复（I-8）。

## Given（Initial World — ScriptedEnvironment 数据变体 launcher-drift）

```
Screen 1:  Settings Main — "Network & Internet"（SwitchState=null），Tap → Screen 2
Screen 2:  Network Settings — "WiFi"（SwitchState=null），Tap → Screen 3
Screen 3:  WiFi Settings — 开关 "WiFi"（SwitchState=false），SetSwitch(ON) → SwitchState=true

漂移注入点: 进入 Screen 2 后，下一次 Observe 时 ForegroundApplication 变为 "Launcher"，
且当前 Observation 不含任何 Settings 元素（模拟真实 Launcher 漂移）。

Recovery 注入: Relaunch(Settings) → ForegroundApplication="Settings" + SettingsMain 元素可见，
对照 RecoveryAnchor.VerificationCriteria 验证通过。
```

RecoveryAnchor（Startup 建立）:
- ApplicationIdentity = "Settings"
- ExpectedSemanticEntry = "SettingsMain"
- VerificationCriteria = "ForegroundApplication == Settings && SettingsMain 元素可见"
- RestoreRecipe = "Relaunch(Settings)"（裁决 8 解除——Phase 2 字段）
- EntryStrategy = "Resolve(SettingsMain)"（裁决 8 解除——Phase 2 字段）

## When（执行到漂移点）

```
Run 已建立 RecoveryAnchor（Startup Ready）
  → Bind Settings Main Container
  → Step-1: Tap("Network & Internet") → Observe → Screen 2（Network Settings）
  → （漂移注入触发: 下一次 Observe 返回 Launcher）

Container.IsStillMine(新观测) → false（Network Settings 元素不存在于 Launcher 观测）
  → Agent navigate: 语义页面不可解析（ForegroundApplication="Launcher" ≠ "Settings"）
  → Agent 检测到 Agent-scope 假设失效
  → 结构化 Trap（Kind=UnexpectedPage, Scope=Agent, Expected=Step-1 post-action seq, Observed=当前 seq）
```

## Then（恢复 → 验证 → Resume → Complete）

```
Agent 挂起 Plan（当前 Step-2 "WiFi" 待执行）
  → Agent 消费 RecoveryAnchor.RestoreRecipe("Relaunch(Settings)")
  → ExecuteAsync(LaunchApp(Settings)) → Re-observe
  → Verify: ForegroundApplication == "Settings" && SettingsMain 元素可见（对照 VerificationCriteria）
  → VERIFIED → Reconcile
  → 重建 Container（从 EntryStrategy: resolve "SettingsMain"）
  → 位置恢复导航：Tap("Network & Internet")（进入 Network Settings——恢复后的 position-restore，非 Plan 步骤，关联 RecoveryId）
  → 重绑 Network Container（从挂起位置：原 Step-1 已完成）
  → Resume Plan: Step-2 Tap("WiFi") → Step-3 SetSwitch(ON)
  → GoalEvidence Satisfied → Completed
```

## Evidence Required

1. **Trap 事件在 Trace**：TrapKind = UnexpectedPage, TrapScope = Agent, Expected 与 Observed 为观测序号引用（非快照）
2. **Recovery 事件在 Trace**：RecoveryId 关联的恢复动作序列（LaunchApp → Observe → Verify → Reconcile → Resume），与正常 Step 事件可区分
3. **ActionHistory 完整性**：LaunchApp(启动) → Tap(Network) → **LaunchApp(恢复)** → **Tap(Network, 位置恢复导航)** → Tap(WiFi) → SetSwitch(ON) ——恢复动作（LaunchApp）与位置恢复导航动作（Tap(Network)，恢复后重新进入 Network Container）可识别，均关联 RecoveryId；原计划动作保留
4. **无从头重跑**：Trace 中 Step-1 仍为 "Step-1"（Container/Step 历史保留），Step-2 从挂起位置继续（非重新 Steps-1/2/3）——位置恢复导航不计为新的 Plan 步骤
5. **终态 Completed**：GoalEvidence.Satisfied 基于恢复后的 post-action Observation
6. **低层无越权恢复**：action history 中无来自 Traversal/Container 的恢复动作
7. **确定性重放**：同 runId 同输入 → 完全相同的 Trace（含恢复路径）

## Expected Authority

| 决策 | Authority | 宪章依据 |
|------|-----------|---------|
| 检测漂移、发射 Agent-scope Trap | Agent | §22 |
| 挂起 Plan | Agent | §5 |
| 消费 RecoveryAnchor.RestoreRecipe | Agent | §20 / 裁决 8 |
| 执行恢复动作 | Agent（通过 IEnvironment） | I-8 |
| 验证恢复成功 | Agent（使用注入 VerificationCriteria） | I-9 |
| Resume（重绑 Container、继续 Plan） | Agent | §5 / I-3 |
| 完成判定 | Agent（基于 Goal evaluator） | I-10 |
| Step 执行（恢复后的 Plan 步骤） | Traversal | Phase 1 保留 |

## Architecture Pressure

- **I-9 闭环**：act（LaunchApp）→ observe → verify → reconcile → resume 首次完整实现
- **I-8 recovery 半句**：低层（Traversal / Container）不得私自恢复——本场景断言"action history 无低层恢复动作"
- **RecoveryAnchor 消费**：RestoreRecipe + EntryStrategy 首次有消费者（裁决 8 解除）
- **Trap 一等模型**：Kind / Scope / Expected / Observed 首次有端到端断言消费
- **不回头**：SC-P1-001..005 全部 Scenario 在本场景的变体数据下不触发（恢复路径仅 launcher-drift 触发）
