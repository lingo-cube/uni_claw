# UniClaw.Kernel 根索引 — Uni Kernel 装配本体与维度导览

`UniKernel` 是 **aggregate execution boundary**（基线 §7）：组合六个 L2
责任域维持现实执行闭环，从 canonical Outcome State 发出 RuntimeOutcome。
它不是共享 mutable context，也不是所有状态的总 Owner。

本目录的维度划分**按基线 L0-L3 架构粒度**，不按物理目录平铺：

| 维度 | 目录 | 基线归属 | README |
|---|---|---|---|
| Evidence Ledger（L2） | `Evidence/` | §11 | [Evidence/README.md](Evidence/README.md) |
| World Model（L2） | `World/` | §12 | [World/README.md](World/README.md) |
| Run Model（L2） | `Run/` + `Outcome/` | §13 | [Run/README.md](Run/README.md) |
| Control Loop（L2） | `Control/` | §14 | [Control/README.md](Control/README.md) |
| Assurance（L2） | `Assurance/` | §15 | [Assurance/README.md](Assurance/README.md) |
| Effect Boundary（L2） | `Effects/` | §16 | [Effects/README.md](Effects/README.md) |
| Run Trace（非权威投影） | `Trace/` | ADR-0013 | [Trace/README.md](Trace/README.md) |
| 运行期诊断（非权威度量） | `Diagnostics/` | LAT-001 词表 | [Diagnostics/README.md](Diagnostics/README.md) |
| Uni Kernel 驱动面 | `Runtime/` | §7/§24 | [Runtime/README.md](Runtime/README.md) |
| Capability Plane | `Capability/` + `Perception/` | L1 · §8 | [Capability/README.md](Capability/README.md) |

## Core/ — Kernel→UniClaw.Core 投影缝

`Core/CoreSemanticProjection.cs`（CORE-002/007）：把 Kernel realization
侧事实（admitted evidence、container segment、DeriveSlice、WorldState
claim、accepted contract、CanonicalBinding、EffectReceipt）投影为
UniClaw.Core 候选语义记录。UI 专用字段保留在 Kernel；本缝只投影当前
实际需要的最小语义。跨 L2 只传 typed immutable values 的纪律（§7）同样
约束本缝。

## 变更规则

- 新增文件先进上表对应维度目录，再在该维度 README 归属表中登记；
- 出现新的架构维度（非既有维度的文件）= 基线级变更，先走 ADR/基线
  修订，不先建目录；
- 根 README 只维护导览与 Core 缝，不承载任何维度的具体归属。

## 指向

- 架构基线（L0-L3，CLOSED）：`docs/architecture/product-architecture-baseline-l0-l3.md`
- 组件间协议基线：`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`
- 公开面白名单执法：`tests/UniClaw.Kernel.Tests/Runtime/KernelRuntimeSurfaceWhitelistTests.cs`
- 词汇面：根 `CONTEXT.md`
