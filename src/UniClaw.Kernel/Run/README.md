# Run Model 代码索引（L2 · 基线 §13）

Sole **Canonical Run State Recording Authority**：Execution Contract
View、Objective State、Proof Obligation State、Progress State、Outcome
State。不拥有 WorldBelief、Control Intent 或 Outcome Proof judgment
（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `RunModel.cs` | run state 记录权威本体（Target §13） |
| `ExecutionContract.cs` / `ExecutionContractView.cs` | 已接受 contract 的 typed 检查与 immutable canonical view（§13.2） |
| `RunState.cs` | Objective State：primary run 对 contract objective 的执行侧状态 |
| `RunObligation.cs` | Run-level Proof Obligation 六类（不变量 36） |
| `OutcomeState.cs` | Terminal Outcome State（terminal classification 记录） |
| `../Outcome/RuntimeOutcome.cs` | **本维度出口投影**（物理目录独立，语义归 Run Model）：UniKernel 只从 canonical Outcome State 发出的 immutable RuntimeOutcome（基线 §7） |

## 变更规则

run/contract 执行状态与义务语义进本目录；`Outcome/` 保持物理独立但
归属本维度——新增出口投影字段时两处同步审。run state 的事件化与投影
边界遵循 ADR-0036。

## 指向

- 基线 §13；primary run 自驱：ADR-0019；事件与投影边界：ADR-0036；
- 消费方：`../Runtime/README.md`（驱动面）、Host 投影。
