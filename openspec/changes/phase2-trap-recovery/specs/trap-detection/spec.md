# Trap Detection Boundary — Capability Spec

> Phase 2 Trap & Recovery | 契约层级: Capability SHALL
> 消费者: SC-P2-001 (Agent-scope Trap)
> 宪章依据: §21 Trap 一等模型 / §22 Trap Scope / I-8 I-9

## ADDED Requirements

### Requirement: Trap Detection Boundary — Capability Spec

**SHALL**

**Trap 类型定义**

- **SHALL** `Trap` 为不可变 sealed record，字段包含 Kind / Scope / Source / Expected / Observed / Evidence / LastAction?（恰好 7 字段；HG-2 冻结: 无 Recoverability / Confidence / Severity / Timestamp / HistoricalMemoryFields）。
- **SHALL** `TrapKind` enum 包含: UnexpectedPage / WorldLost / StateMismatch / TargetLost / PlanInvalid / ContainerMismatch（Charter §21 分类词汇）。
- **SHALL** `TrapScope` enum 包含: Step / Container / Agent（Step/Container 为词汇预留，Phase 2 仅 Agent scope 实际发射）。
- **SHALL** `Expected` 与 `Observed` 为 `long?` 类型——观测序号引用，不嵌入 Observation 快照（I-13: 不重新聚合成 God Context）。
- **SHALL** Trap 是 Model 层纯不可变值类型，不包含行为逻辑（I-7: Trap 是 evidence，不是 intelligence）。

**Trap 发射**

- **SHALL** Agent-scope Trap 由 Agent 发射（漂移检测：ForegroundApplication 变化 + IsStillMine 失败 + 语义页面不可解析）。
- **SHALL** Container-scope Trap 枚举预留，Phase 2 不消费（Phase 3 Popup 场景购买）。
- **SHALL NOT** Step-scope retry 耗尽产生 Trap——retry 耗尽走 Phase 1 失败路径（`TraversalStepResult.Failed(Reason)` → escalate → Agent.Fail）（I-12: Step-scope Trap 无 Scenario 购买；SC-P2-002 只买 retry 成功路径）。
- **SHALL** Trap 作为不可变值在组件间流动（Traversal → Container → Agent），跨 owner 边界只传不可变快照（I-2）。

**Trap 记录**

- **SHALL** Trap 事件记录于 Trace（TrapKind / TrapScope 字段），纳入因果链（§28）。
- **SHALL** Trap 事件与触发步骤的 StepId 关联（如适用）。

**禁止**

- **SHALL NOT** Trap 包含 Observation 快照（Expected/Observed 仅引用序号——I-13）。
- **SHALL NOT** Trap 包含恢复决策逻辑（I-7: FSM 不做 intelligence；Trap 也不做）。
- **SHALL NOT** 低层组件基于 Trap 自行发起恢复动作（I-8: 恢复 authority 唯一在 Agent）。

**与 Phase 1 的关系**

- `TraversalStepResult.Failed(Reason)` 保留——Trap 作为 escalate 载体与其**并列**，不回退 Phase 1 契约（SC-P1-004 断言 1 语义不变）。
- Phase 1 失败路径（Step 失败 → Failed → Agent.Fail → Run Failed）仍是有效路径。Trap 提供**额外**的恢复路径，不取代。

#### Scenario: SC-P2-001 — Agent Recovery: Launcher Drift 恢复并继续

冻结消费者场景契约见 `../../scenarios/SC-P2-001-agent-recovery.md`。
