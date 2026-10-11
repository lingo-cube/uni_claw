# ADR-0046：P1 Contract、P24 Activation、P25 Consultation 的边界

状态：accepted（2026-10-11，ARCH-ALIGN-001）

前几轮已经确定 Task → Task Instance/Session → UniAgent Execution Contract →
Kernel Run 的产品顺序。本决策把这条顺序落实到三条最容易混淆的协议边，并冻结
重复启动和跨边界 identity 规则。

## Decision

### 1. P1 只负责 Contract

UniAgent 根据 Task 和 Product Session context 形成 Execution Contract，经 P1
提交给 Run Model。Run Model 负责 admission，并在接受后形成 immutable Contract
View。P1 不创建 Run、不启动 Kernel、不产生 Effect；P1 的输入至少要能关联
taskInstanceId、productSessionId、contract reference/version。

### 2. P24 只负责启动 Run

P24 只在 accepted Contract View 存在后请求 Legal Activation。它使用
acceptedContractViewRef + contractGeneration 作为启动钥匙。同一钥匙最多对应
一个 Product Session/Primary Run binding：

- 同一次提交的重试返回已有 binding；
- 并发请求的后到者读取同一结果；
- 同一钥匙但契约内容不一致时拒绝；
- Run 已终止时只返回终态，不再产生动作或新 Run。

P24 不接收 cycle 指令、Effect 载荷、WorldBelief 注入或新的契约内容。

### 3. P25 只负责运行中的边界咨询

P25 由 Kernel internal driver 在 Run 已启动后的 decision boundary 发起，向
UniAgent 请求一次具体的 advisory decision。每个 decision boundary 最多一次：
同一边界的重试沿用同一 consultation correlation id 并返回同一结果；进入不同
边界可以再次咨询。P25 不创建、修改或替换 Execution Contract/Contract View，
不直接写 Run State、WorldBelief、Assurance、Binding 或 Effect。P25 请求至少带
runId、productSessionId、Contract View reference 和本次 consultation correlation
id；响应沿用同一 correlation id。

### 4. 产品协议只使用中性 identity

P1/P24/P25 可以携带 taskInstanceId、productSessionId、runId、
acceptedContractViewRef、contractGeneration 和 consultationCorrelationId。
不得携带 dshSessionId、Host transcript id 或其他 realization 私有 identity。

## Known gaps

当前协议文档和实现尚未完全具备上述字段与跨进程幂等：P1 现有最小载荷没有完整
Task/Session identity，P24 identity/correlation/并发/恢复仍是 Deferred ⑰，P25
的 per-boundary correlation/idempotency 与 productSessionId/Contract View reference
字段仍是 known gap，完整 Contract scope/capability/risk/budget 校验仍 deferred。
上述差异保持真实状态，不在本 ADR 中伪造 verified。

## Rejected alternatives

- **P25 先生成 Contract**：会把一次运行内咨询变成第二条 Contract authority path。
- **P24 携带新的 Contract 内容**：把 admission、activation 和 execution 混成一条
  不可审计的命令。
- **只传一个通用 correlationId**：无法区分 Task、Session、Contract、Run 和一次咨询。
- **把 DSH session id 放入产品协议**：把 Host realization 细节变成产品 identity。
