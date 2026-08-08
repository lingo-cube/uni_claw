# Phase 2 Architecture Receipt

> 状态: Frozen | 日期: 2026-08-08
> 关联: phase2-human-gate-decision.md (HG-1..5 Approved) + phase2-trap-recovery OpenSpec change
> 依据: Phase 2C DONE (158/158 tests, 8/8 guards, 3/3 formal scenarios proven)

## Frozen Ownership (I-2: 一个 mutable state 一个 owner)

| 可变状态 | Owner | 宪章依据 |
|---------|-------|---------|
| RunState（全局生命周期） | Agent | §5 / §18 / Phase 1 |
| WorldBelief | Agent | §5 / §10 / Phase 1 |
| Active Container Stack | Agent | §5 / §6 / Phase 1 |
| Container 局部状态（当前 Observation / candidates / visited / progress / 完成判断） | Container | §6 / Phase 1 |
| Traversal 单步状态（journal + retry count） | Traversal | §7 / §22 (SC-P2-002 购买 retry count) |
| 模拟世界状态（当前 Screen / transition 配置） | ScriptedEnvironment (Fake, tests/) | §8 / §33 / Phase 1 |
| TraceEvent 列表 | Agent | §28 / 裁决 5 / Phase 1 |
| Trap 实例（escalation 期间） | 流动：Traversal → Container → Agent 只读转交（不可变值，I-2 跨 owner 快照规则） | §21 / §22 / Phase 2A |
| **Recovery 进度状态**（恢复阶段 / 配方动作列表 / 分发游标 / 恢复后观测 / 验证结果） | **Recovery 组件** | **HG-4 Option B** (component owns mechanism state; Agent owns decision authority) |

## Frozen Authority (I-3: 一个 decision 一个 authority)

### Agent (Run-level 控制者)
| 决策 | 宪章依据 |
|------|---------|
| 发射 Agent-scope Trap | §22 (SC-P2-001) |
| 发起 Agent Recovery | §5 / I-8 (SC-P2-001) |
| 选择恢复策略（消费 RecoveryAnchor.RestoreRecipe） | §5 / §20 (裁决 8 解除) |
| 执行恢复动作 | I-8（通过 IEnvironment — 低层不得私自恢复） |
| 判定恢复验证通过 / 失败 | I-9（使用注入 VerificationCriteria） |
| Resume（继续 Plan） | §5 / I-3 |
| 终止 Run（恢复失败） | Phase 1 authority 保留 |
| 完成判定 | I-10（基于 Goal evaluator — Phase 1 保留） |

### Traversal (确定性执行 Kernel)
| 决策 | 宪章依据 |
|------|---------|
| Step retry（re-observe / re-resolve） | §30 (SC-P2-002) |
| 重试耗尽 → escalate | I-8 (走 Phase 1 `Failed` 路径，不产生 Trap — I-12) |

### Container (语义页面局部域)
| 决策 | 宪章依据 |
|------|---------|
| Container-scope Trap 发射 | §22 (**Phase 3 Popup 消费 — 枚举预留，Phase 2 不发射**) |

### 禁止 (I-8: 低层不得偷取高层 authority)

| 禁止事项 | 保护不变式 |
|---------|-----------|
| Traversal 自行 PressBack / LaunchApp | I-8 |
| Container 自行恢复（Popup 归 Phase 3） | I-8 / Phase Boundary |
| Recovery 组件自行判定 Run 终止 | I-3 / HG-4 |
| 低层（Traversal / Container）私自恢复 | I-8 (SC-P2-001 Evidence 6: action history 无低层恢复动作) |

## Scenario → Semantic → Architecture Decision

### SC-P2-001 — Agent Recovery (Launcher Drift)

**Semantic**: Agent-scope 世界漂移可以恢复——I-9 act→observe→verify→reconcile→resume 闭环首次完整实现。§35: 不得从头重跑；§23: Recovery 是完整协议不是单个动作。

**Architecture Decisions**:
- Trap 一等模型 7 字段落地 (HG-2: Kind/Scope/Expected/Observed/Source/Evidence/LastAction。Expected/Observed = long? 序号引用，非 Observation 快照 I-13。排除 Confidence/Severity/Timestamp/HistoricalMemory)
- RecoveryAnchor +RestoreRecipe / +EntryStrategy (裁决 8 解除，Charter §20 字段落地。注入数据载体，裁决 11: 不硬编码恢复策略)
- Recovery 组件 Option B (HG-4: 组件持机制状态，Agent 持全部决策 authority。Agent 组合 Recovery，Recovery 不接触 Container/Traversal/Agent——Guard 7)
- 不加 DriftStatus (HG-3: 现有 ForegroundApplication + IsStillMine + SemanticPage 三表面覆盖 SC-P2-001)
- TraceEvent +TrapKind? / +TrapScope? / +RecoveryId? (A4, §28 因果链扩展)
- Guard 5 修订 + Guard 7 新增 (HG-1, 与 A1 原子完成防 guard 空窗)

### SC-P2-002 — Step Retry (Flicker Target)

**Semantic**: Step-scope 临时缺失可以本地有界重试——I-8 对偶：能本地处理不升级；不 steal 上层 recovery authority。

**Architecture Decisions**:
- Traversal maxRetries (B4: 仅 Select re-observe + re-resolve。零动作派发。耗尽走 Phase 1 `Failed` 路径，不产生 Trap——I-12: Step-scope Trap 无 Scenario 购买)
- TraversalJournalEntry +RetryCount (A5: 0 = 正常首次执行 Phase 1 兼容；>0 = 第 N 次重试条目。SC-P2-002 Evidence 1: journal 含重试条目可区分)
- **不**产生 Step-scope Trap (I-12: 无 Scenario 购买。Step-retry 耗尽 → Phase 1 Failed 路径)
- **不**产生 Recovery 事件 (SC-P2-002 Evidence 3/4: TrapKind/TrapScope/RecoveryId null)

### SC-P2-003 — Recovery Verification Failure (Unrecoverable)

**Semantic**: 恢复动作成功 ≠ 恢复完成。I-9 负向面: 未验证不得 Resume。§42: RecoveryFailure 场景。裁决 10 在恢复语境延续——dispatch outcome ≠ world success。

**Architecture Decisions**:
- RecoveryResult Verified | Failed(Reason) (HG-5: 恰好 2 变体，无 Incomplete/Retryable)
- VerificationCriteria 语义消费 (B5: 驱动 pass/fail + 失败原因期望侧，非原样透传)
- TraceEvent.RecoveryId 关联验证失败事件
- Run Failed 显式原因 (Reason 语义源自恢复验证失败——"恢复验证失败：期望 X，实际 Y (seq=N)"，区别于 Plan 耗尽 / 步骤失败)
- **不**引入 RecoveryRequest / Planner / Runtime (HG-5: 无购买者)

## Guard Preservation (D2)

Guards 1-4 + Guard 6 规则与 Phase 1 零差异（design.md §7 表格"不变"列）:
- Guard 1: csproj 零 ProjectReference
- Guard 2: 零旧 namespace 引用 (UniClaw.Core.Traversal / UniClaw.Core.StateMachine)
- Guard 3: Contract doc + I-1..I-14 全部 header 存在 + AGENTS.md 导航有效
- Guard 4: AGENTS.md 导航指向 Contract
- Guard 6: Model 层无 coordinate / hierarchy 类型声明或成员声明（X/Y/Left/Top/CenterX/CenterY — 裁决 3）

8/8 Guard [Fact]s 全部通过 (158/158 tests)。Phase 1 场景语义不回退 (HG ADR Approved Constraints 2)。

## Phase Boundary

design.md §9 Deferred 全表保持缺席 (HG-3 / HG-5 + Phase 3 postponed):
- 无 RecoveryRequest / RecoveryPlanner / RecoveryRuntime (HG-5)
- 无 WorldBelief.DriftStatus (HG-3)
- 无 Popup recovery / Container-scope recovery (Phase 3 §38)
- 无 Uncertain action (Phase 3 §37)
- 无 Scroll identity / Fingerprint (Phase 3 §36 / 裁决 2)
- 无 coordinate / hierarchy grounding (裁决 3 / Guard 6)
- 无 FSM (I-7: 恢复协议普通方法表达)
- 无 DI 容器 / 真实设备 / Vision Adapter (Phase 4)
- 无 LLM / VLM / Memory (Phase 5)

## State

```
PHASE_2_ARCHITECTURE_FROZEN
```
