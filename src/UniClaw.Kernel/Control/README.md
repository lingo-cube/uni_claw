# Control Loop 代码索引（Product T2 · 基线 §14）

Sole **Control Intent Authority**：Control State、Tactical Hypothesis、
Observation Control、Traversal Control、Recovery/Reorientation。不拥有
WorldBelief、Run progress truth、Assurance Judgment 或 effect delivery
（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `ObservationRecoveryControl.cs` | 当前执行位置的有界补证等待与run子配额，恢复不续预算 |
| `TraversalControl.cs` | Control所有的有界探索/返回/深度和义务游标；经Run保存进度，不投递Effect |
| `ControlLoop.cs` | control intent 权威本体（不变量 21） |
| `ControlIntent.cs` | intent 的 typed 形状（跨 Product T2 只传 immutable command） |
| `ControlPolicy.cs` | 控制策略词汇 |
| `TacticalHypothesis.cs` | 战术假设（reorientation/recovery 的输入） |
| `DescriptorTargetPolicy.cs` | 目标规格（CTL-001 D1）：authoring 侧「对什么 affordance 施加什么」的表达 |

## 变更规则

产生/记录 control intent 的语义进本目录；intent 的**执行**归 Effect
Boundary，intent 的**安全裁决**归 Assurance，进度真值归 Run Model——
三权分立是基线 §7 的硬约束，不得在 本目录合并。

## 指向

- 基线 §14；组件间协议：`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`；
- 决策缝消费（Agent 咨询）：`../Runtime/`（AgentDecision/PolicyRuntime）。
