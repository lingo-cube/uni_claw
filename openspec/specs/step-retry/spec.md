# step-retry Specification

## Purpose
TBD - created by archiving change phase2-trap-recovery. Update Purpose after archive.

## Requirements

### Requirement: Step-scope Retry — Capability Spec

**SHALL**

**Retry 协议**

- **SHALL** Traversal.Select 失败（无匹配候选）时，Traversal 可在 Step-scope 内重试：Re-observe → Re-resolve → 成功 → 继续 Execute。
- **SHALL** 重试仅包含 re-observe（调用 `IEnvironment.ObserveAsync`）与 re-resolve（重新 grounding），**不派发动作**——派发后 timeout 重试归 Phase 3（§37 Uncertain Action，裁决 10 防盲重试）。
- **SHALL** 重试有界：max retry count 由 Traversal 持有（硬编码常量或注入，Phase 2 最小实现），耗尽 → `TraversalStepResult.Failed(Reason)` → escalate。
- **SHALL** 重试确定性：同一输入 + 同一 ScriptedEnvironment 配置 → 同一次数的重试 → 同一结果（SC-P1-001 断言 7 重放不回归）。

**Journal 记录**

- **SHALL** 每次重试尝试记录于 Traversal journal（首次 Select 失败 + re-observe SequenceNumber + 最终结果）。
- **SHALL** journal 重试条目与正常单步条目可区分（RetryAttempt 标记或独立条目类型）。

**不升级**

- **SHALL** Step-scope retry **不**产生 Trap 事件、**不**升级到 Container/Agent scope、**不**触发恢复路径。
- **SHALL** retry 成功后 Run 正常继续（Completed 路径不受影响）。

**禁止**

- **SHALL** retry 耗尽 → `TraversalStepResult.Failed(Reason)` → escalate 到 Agent（走 Phase 1 失败路径，**不产生 Trap**——与 specs/trap-detection.md 一致：Step-scope Trap 无 Scenario 购买）。
- **SHALL NOT** Step retry 派发任何 DeviceAction（仅 re-observe——I-12: Phase 2 只买 re-observe/re-resolve）。
- **SHALL NOT** Step retry 无界循环（max retry count 硬上限）。
- **SHALL NOT** Step retry 修改 Container 状态或 WorldBelief（仅 Traversal 内部——I-2）。

**与 Phase 1 的关系**

- Phase 1 `Traversal.ExecuteStep` 的 Select→Check→Execute→Observe→Verify→Branch 协议保留。Retry 发生在 Select 失败后的 **Check 之前**（re-observe → 重新 Select）。
- Phase 1 失败路径（Select 失败 → 直接 `Failed`）保留为 retry 耗尽后的 fallback。
- 重试对 Container / Agent 透明（Container 只看到最终的 Succeeded 或 Failed——只读转交面不变）。

#### Scenario: SC-P2-002 — Step-scope Retry: Re-observe / Re-resolve 后继续

冻结消费者场景契约见 `../../scenarios/SC-P2-002-step-retry.md`。
