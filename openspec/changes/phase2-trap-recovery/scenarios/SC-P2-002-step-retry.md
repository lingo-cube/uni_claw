# SC-P2-002 — Step-scope Retry: Re-observe / Re-resolve 后继续

> Phase 2 Trap & Recovery | Scenario-first
> 宪章依据: §22 Step Scope / §30 Traversal retry authority / I-8 对偶（能本地处理不升级）
> 消费者: specs/step-retry.md

## Goal

验证 Step-scope retry：Select 失败（临时 target missing）→ Traversal 在 Step-scope 内 re-observe → re-resolve → 成功 → 继续执行，不升级 Agent Scope，不产生 Trap 事件。

## Given（Initial World — ScriptedEnvironment 数据变体 flicker-target）

```
Screen 1:  Settings Main — "Network & Internet"（SwitchState=null），Tap → Screen 2
Screen 2:  Network Settings — 首次 Observe 只含 "Bluetooth"（无 "WiFi"）
                             第二次 Observe 含 "Bluetooth" + "WiFi"（元素出现——世界抖动恢复）
Screen 3:  WiFi Settings — 开关 "WiFi"（SwitchState=false），SetSwitch(ON) → SwitchState=true
```

## When（Plan Step-2 执行中发生 flicker）

```
Step-1: Tap("Network & Internet") → Observe → Screen 2（Network Settings）→ Succeeded
Step-2: PlanStep("WiFi", "Tap")
  → Traversal.Select("WiFi"): 当前 candidates（首次 Observe）只含 "Bluetooth"
  → 无匹配候选 → Step-scope 临时失败
  → （不立即 Failed 上报 Agent）→ Re-observe（ObserveAsync）
  → 第二次 Observe: candidates 含 "Bluetooth" + "WiFi"
  → Re-resolve: Select("WiFi") → 匹配（Index=1）
  → 继续 Execute: Tap(TargetElementIndex=1) → Observe → Verify → Succeeded
```

## Then（正常继续，无升级）

```
Step-2 Succeeded → Step-3: SetSwitch(ON) → GoalEvidence Satisfied → Completed
全程无 Agent 介入、无 Trap 事件、无恢复动作。
```

## Evidence Required

1. **Journal 含重试条目**：Step-2 的 journal 显示首次 Select 失败 + re-observe SequenceNumber（第二观测序号）+ 最终 Succeeded
2. **Run 未中断**：RunState = Completed（非 Failed）
3. **Trace 无 Trap 事件**：TrapKind / TrapScope 字段均为 null
4. **Trace 无 Recovery 事件**：RecoveryId 为 null
5. **重试有界**：journal 重试次数 ≤ max retry count（确定性上限）
6. **确定性**：同输入 + 同 ScriptedEnvironment → 同重试次数 + 同结果
7. **ActionHistory**：LaunchApp → Tap(Network) → Tap(WiFi,Index=1) → SetSwitch(ON)（WiFi 的 Index 来自 re-resolve 后的正确极——重试对动作透明）

## Expected Authority

| 决策 | Authority | 宪章依据 |
|------|-----------|---------|
| Step retry（re-observe / re-resolve） | Traversal | §30 |
| 重试耗尽后 escalate | Traversal → Container → Agent | I-8 |
| Run 终止（如重试耗尽） | Agent | Phase 1 保留 |

## Architecture Pressure

- **I-8 对偶**：能本地处理不升级——Traversal 拥有 Step-scope retry authority，但不得 steal Agent recovery authority
- **重试有界 + 确定性**：SC-P1-001 断言 7 重放必须不回归（同输入→同重试次数→同结果）
- **无动作派发**：retry 只 re-observe，不派发 DeviceAction——Phase 3 的 Uncertain Action（派发后 timeout 重试）归另外 Scenario
- **Journal 扩展**：journal 从"每步一条"扩展为"每步可能有多条重试条目"——Container/Agent 的 journal 消费面需兼容
