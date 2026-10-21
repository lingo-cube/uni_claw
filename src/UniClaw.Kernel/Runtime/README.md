# Runtime 代码索引 — Uni Kernel 驱动面（Product T1 本体 · 基线 §7/§24）

`UniKernel` 的**驱动面**：六个 Product T2 责任域的组装与推进循环。它不是第七
个 Product T2——不拥有任何 Product T2 authority，只保证它们的组合纪律（§7：互不重叠、
intent/judgment/delivery 分离、跨 Product T2 只传 typed immutable、capability
不旁路、terminal 后无旁路 effect path、outcome 只从 canonical Outcome
State 发出）。

**公开面 = 冻结面**：本目录类型在 Kernel 公开面白名单中
（RUN-003/RUN-005 提升，`KernelRuntimeSurfaceWhitelistTests` 执法）——
任何公开类型增删 = 白名单修订 = 显式 change（HOST-001 D8）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `KernelRunDriver.cs` | primary run 的推进循环（legal activation 结果、幂等语义 §24.1 不变量 44；ADR-0019 自驱） |
| `RunDriverInputs.cs` | 驱动输入（observation/cancel/unexpected 的 typed 形状） |
| `AgentDecision.cs` | Agent 决策缝词汇（proposal/disposition） |
| `AgentPlanPolicy.cs` | 决策计划策略（FROZEN v0.3.1 咨询缝词汇） |
| `TraversalExecution.cs` | 持续遍历World引用投影、操作级typed语言handler、调用/状态/报告及Recovery事实交接词汇 |
| `PlanLanguageHandlerCall.cs` | 单个typed语言handler的机械异步生命周期、协作取消与迟到异常观察；无Owner写权限 |
| `PlanExecution.cs` | 计划候选/守卫投影、明确续行位置、逐动作尝试及阻碍证据视图 |
| `PolicyProtocol.cs` / `PolicyRuntime.cs` | 策略求值协议与运行时（Slice A/B；求值机制面保持 internal） |
| `ConsultationTypes.cs` | 咨询缝 typed 词汇 |
| `ConsultationJournal.cs` | P25 四元组 journal seam、semantic count 与 fail-closed replay |

## 变更规则

驱动循环、Product T2 组装顺序、决策/咨询缝词汇进本目录；新增公开类型必须
同 change 修订白名单测试；决策语义的所有权仍在 UniAgent（L1），本目录
只持有缝的 Kernel 侧形状。

## 指向

- 基线 §7/§24；ADR-0019（自驱）、ADR-0025（driving surface 公开+白名单）、
  ADR-0026（buyer-driven 泛化）；
- 被组装的六域：`../Evidence/` `../World/` `../Run/` `../Control/`
  `../Assurance/` `../Effects/` 各 README。

## RUNTIME-PLAN-002 持续遍历

`PlanItem.TraverseItem`复用原Plan/ContinuePlan。ControlLoop持有Control/TraversalControl；KernelRunDriver协调当前World投影、typed语言handler和原StepAct→StepVerify链。RunModel记录TraversalExecutionView和历史，状态查询读Owner记录，暂停不产生最终报告。ReadOnly仅记录/检查，Navigate始终经原许可/验证；发现不足/深度/排除/未知在报告中保留，不推出Goal完成。字段、输入关联及协议生成闭包见[正式执行契约](../../../changes/RUNTIME-PLAN-002/execution-contract.md)。

RUNTIME-PLAN-003增加typed异步语言适配。`Drive`在等待调用时可恢复地返回`WaitingForHandler`；`DriveAsync`等待完成/截止/取消后接回原位置，真实外部输入等待仍原样返回。Run按callId记录Running及单一终态，状态查询不推进。业务Finding及原逐动作验证语义不变，见[异步执行契约](../../../changes/RUNTIME-PLAN-003/execution-contract.md)。
