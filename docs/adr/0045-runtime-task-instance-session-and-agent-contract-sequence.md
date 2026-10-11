# ADR-0045：Runtime 创建 Task Instance/Session，UniAgent 细化契约

状态：accepted（2026-10-11，ARCH-ALIGN-001）

本决策修正 ADR-0043/0044 中关于 Task Launch 铸造 Task Instance、以及 Product
Session 只能随 Run 创建的时序假设。用户任务需要先进入产品会话，UniAgent 才能
根据任务和会话信息形成可执行的细化契约，Kernel 随后才创建并激活 Primary Run。

## Decision

Task Definition → UniClaw Runtime 创建 Task Instance → UniClaw Runtime 分配 Product Session → UniAgent 形成细化 Execution Contract → Run Model/Kernel 接受 Contract View → Legal Activation → Primary Run 自驱执行

- Task Definition 由 Task Catalog/Host 提供；它不是一次执行。
- UniClaw Runtime 是 Task Instance 与 Product Session 的产品身份铸造者。Task
  Launch 只提交带 Task 引用的请求并读取 Runtime 返回的 binding，不自行生成这两个
  Product identity。创建路径不得让调用方传入或覆盖 taskInstanceId/productSessionId；
  只有恢复/查询已有 binding 时，Runtime 才能按启动钥匙查回这两个值。
- Product Session 在 Primary Run 之前建立，是 Goal、Contract、Run、Outcome 和
  Goal Evaluation 的 correlation root；建立 Session 不等于开始执行。
- UniAgent 根据 Task 和 Session context 形成更细的 Execution Contract/协议，并
  将其交给 Kernel；UniAgent 不直接创建 Run State 或 Effect。
- Kernel/Run Model 只在 Contract View 被接受并通过 Legal Activation 后创建/激活
  Primary Run。
- 同一次提交的重试复用已有 Task Instance/Session binding；任务结束后再次执行，
  创建新的 Task Instance、Product Session 和 Primary Run。
- Host 只创建自己的 hostSessionRef，并通过显式 binding 关联 Product Session；
  Host session 不是 Product identity。

## Consequences

- “Session → Goal → Contract → Run” 的语义顺序与 Runtime 身份实现一致；Session
  可以承载 UniAgent 的前置理解和契约形成过程。
- P24 activation key 仍只负责同一 accepted Contract View 的启动幂等，不负责创建
  Task Instance 或 Product Session。
- Runtime HTTP 当前先生成 Run/Session 再由 HostRunner 构造 Contract 的路径仍属于
  known gap；Task/Session admission、Agent contract 和 Run activation 尚未拆成
  三个可观察阶段。
- Runtime→Workbench 的首条 identity seam 尚未实现；Workbench 当前仍有本地
  instanceId 与 Runtime taskInstanceId 两套编号，属于 known deviation。旧
  `/tasks/instantiate` 的 `psi-*` 是兼容路径，不是目标 Product Launch identity。

## Superseded wording

本 ADR supersedes ADR-0043/0044 中“Task Launch 铸造 taskInstanceId”和“Runtime
仅在 Run 创建/恢复时铸造 productSessionId”的表述；其余 identity separation、
host-neutral fields、protocol status 和 activation-key 规则继续有效。
