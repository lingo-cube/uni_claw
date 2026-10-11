# ADR-0043：Core、执行身份、Capability Owner 与协议审阅边界

状态：accepted（2026-10-11，ARCH-ALIGN-001）

ADR-0042 固定了 Product T0–T3 与独立语义生命周期，但仍有四个边界会决定
后续协议是否出现第二套 authority：Core 是否成为产品组件、Task/Session/Run/Host
是否被压成一个 identity、Capability 四层由谁拥有，以及外围协议是否被遗漏。

## Decision

### 1. Core 是 Product T3 的正交共享面

Core 承载跨域 typed immutable records 和 projections，保留 owner 引用与来源，
但不拥有 Product canonical state、Runtime authority、独立生命周期或新的 Product
T2。需要领域运行时闭环的实现仍由自身 Runtime/Kernel 持有 authority；不需要运行时
机制的 realization 可以经明确缝直接投影到 Core。Core 不进入 Product T1，也不成为
共享 mutable hub 或第二 Runtime。

### 2. 四个执行身份保持分离

```text
Task Instance   ──Task Launch Binding──► Product Session
Product Session ──Run-Session Binding─► Primary Run
Product Session ──Host Binding────────► Host Session
```

四者各有自己的 identity 和 owner。当前范围可以是一对一，但一对一是 cardinality
约束，不是 identity 合并。目录名、artifact 文件名、transcript 或 Host session
都不能推导另一种 identity；关联必须由带来源和 authority 的显式 binding 记录。

### 3. Capability 四层不产生新的 Product T2

| 层 | Owner / 语义 | 边界 |
|---|---|---|
| Capability Definition | Product T1 Capability Plane 管理面 | 定义输入、输出、scope、失败语义；不等于实现/实例 |
| Capability Implementation | Provider、Host 或内部算法 realization | 可替换；不取得 Product canonical authority |
| Capability Binding | Composition Root / Host / Task scope | 决定注入、激活和暴露；不改变 Definition |
| Capability Instance | 当前 Host/Task scope | 绑定期运行实例；不回写全局 registry |

Capability Management Role 只负责注册、分类、协议、scope、健康和生命周期装配，
不升格为 Product T2 或 canonical state owner。

### 4. 协议审阅采用同一 Change 的两段式范围

第一段先锁 Product Runtime 主链：Product Semantic Lifecycle、P1–P26、UIWorld
作为 World Model realization 的边界，以及每条边的 producer、consumer、meaning、
minimal payload、identity、revision、authority、validity、failure、absence、
duplicate、late、retry、evidence 和 lifecycle。

第二段紧接审阅 Host/Workspace/Task/Capability/Provider/Profile/Launch/Harness
外围边，确认它们只能通过显式 projection、binding 或 adapter 接入，不能倒灌 Product
authority。两段共用 ARCH-ALIGN-001 的 owner/authority 矩阵和状态词，不新建第二套
协议 Change。

## Consequences

- Core 的实现可继续演化，但任何新增 record/projection 必须声明来源 owner 和
  projection 方向；代码存在不等于取得 authority。
- Task Launch、Run-Session Binding、Host Binding 将成为后续身份协议的重点；
  Workspace 只能读取已确认的 binding，不能从观察产物补造关联。
- Capability 的 Definition/Implementation/Binding/Instance 可以独立替换，
  但协议审阅必须分别记录其 producer、consumer 和生命周期。
- P1–P26 的现有编号和载荷在本 ADR 中不变；本 Change 的下一阶段才处理协议
  payload、状态和实现偏差。

## Rejected alternatives

- **Core 升为 Product T1**：把共享记录形状误当运行时组件，并制造第二 authority。
- **Task Instance 与 Product Session 合并 identity**：当前一对一不保证未来仍一对一，
  会让 Task Catalog、Runtime 和 Workspace 无法分别表达 owner。
- **一个 Session identity 覆盖 Run/Host**：把 Host realization 状态和 Product Run
  truth 混为一体，无法安全处理重连、辅助执行或多次运行。
- **只审 P1–P26，不审外围边**：会留下 Task Launch、Capability Binding、Workspace
  projection 和 Harness authority 倒灌的未审缝。
