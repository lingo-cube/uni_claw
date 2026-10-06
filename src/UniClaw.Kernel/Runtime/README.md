# Runtime 代码索引 — Uni Kernel 驱动面（L1 本体 · 基线 §7/§24）

`UniKernel` 的**驱动面**：六个 L2 责任域的组装与推进循环。它不是第七
个 L2——不拥有任何 L2 authority，只保证它们的组合纪律（§7：互不重叠、
intent/judgment/delivery 分离、跨 L2 只传 typed immutable、capability
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
| `PolicyProtocol.cs` / `PolicyRuntime.cs` | 策略求值协议与运行时（Slice A/B；求值机制面保持 internal） |
| `ConsultationTypes.cs` | 咨询缝 typed 词汇 |

## 变更规则

驱动循环、L2 组装顺序、决策/咨询缝词汇进本目录；新增公开类型必须
同 change 修订白名单测试；决策语义的所有权仍在 UniAgent（L1），本目录
只持有缝的 Kernel 侧形状。

## 指向

- 基线 §7/§24；ADR-0019（自驱）、ADR-0025（driving surface 公开+白名单）、
  ADR-0026（buyer-driven 泛化）；
- 被组装的六域：`../Evidence/` `../World/` `../Run/` `../Control/`
  `../Assurance/` `../Effects/` 各 README。
