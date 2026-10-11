# ADR-0044：Runtime Session 身份铸造与协议状态纪律

状态：accepted（2026-10-11，ARCH-ALIGN-001；Task/Session 时序由 ADR-0045 修正）

ADR-0043 已把 Task Instance、Product Session、Primary Run、Host Session 分开，
但现有实现仍同时出现 `sessionId`、`dshSessionId`、`hostSessionRef`，且 P24/P25
等边混有 verified、partial、deferred 和 placeholder。此 ADR 固化身份铸造权和
协议状态表达，避免 adapter alias 变成 Product truth，也避免文档把未完成协议说成已完成。

## Decision

### 1. Runtime 铸造 Product Session

```text
Task Launch  ──creates──► taskInstanceId
UniClaw Runtime ──creates/recover──► productSessionId + runId
Host adapter ──creates──► hostSessionRef
UniAgent ──consumes/supervises──► Goal / Contract / Runtime Outcome
```

UniClaw Runtime 在合法 Run 创建或恢复路径铸造 `productSessionId`，并负责它与
`runId` 的 canonical 关联。Task Launch 不铸造 Product Session 或 Run，只铸造
`taskInstanceId`、提交 launch request 并持久化显式 binding。Host 只铸造自己的
`hostSessionRef`。UniAgent 不铸造 Runtime identity，只消费和监督。

### 2. 跨边界使用中性身份字段

产品/Harness 跨边界只允许 `productSessionId`、`taskInstanceId`、`runId`、
`hostSessionRef` 等 host-neutral 字段。`dshSessionId`、`sessionId` 等是 DSH
adapter 内部 alias；映射必须披露 `source`、`authority`、`valueOrigin`，不得让
alias 成为 Product identity 或 Runtime correlation。迁移兼容可以在 adapter 内
保留 alias，但公共 payload 不新增 DSH 专有字段。

### 3. 协议状态保持事实分类

所有协议边统一使用：

```text
verified | known gap | known deviation | placeholder | deferred | not applicable
```

`verified` 必须有可复现的协议/行为证据；`known gap` 表示边界已明确但实现或证据
不足；`known deviation` 表示有意偏离且已记录；`placeholder` 只有语义占位；`deferred`
等待真实 buyer 或后续裁决；`not applicable` 仅对当前范围不适用。代码存在、文档
描述、测试通过或字段能序列化都不能单独升级状态。

ARCH-ALIGN-001 只闭合 Owner、Boundary、审阅矩阵和状态分类，不凭文档补造 P24/P25
activation buyer、P20/P21 Memory buyer、P23 continuity payload 或 P26 async writer。

### 4. 已接受契约版本是启动钥匙

Owner 在第四轮接受 `acceptedContractViewRef + contractGeneration` 作为 P24 的
activation key。它标识一份具体的已接受契约版本；单独的版本数字（如 `7`）不是
全局唯一 identity。不同契约都可以有第 7 版，必须连同契约视图引用一起识别。

同一 key 至多绑定一个 Product Session / Primary Run。重复启动请求返回已有关联，
不创建第二次运行、不重放动作；launch request identity 只关联提交请求，不能替代
这条语义去重规则。该裁决记录产品约束，P24 完整载荷、持久化、并发和恢复实现的
验证状态继续按现有证据标记，不升级为 `verified`。

## Consequences

- 后续 P1/P24/P25 correlation 审阅以 Runtime 的 `productSessionId`/`runId` 为主线，
  Task Launch、Host 和 UniAgent 只能提交或消费显式 projection。
- 现有 `dshSessionId` 可以安全兼容，但必须在 adapter 侧转换；跨边界字段清理需要
  单独实现 Change，不在本 ADR 中伪造完成。
- 协议审阅表可以同时容纳已验证边和真实未闭合边，避免“实现中”成为第四种隐含状态。

## Rejected alternatives

- **Task Launch 铸造 Product Session**：会让发起层越过 Runtime 语义 authority，
  也无法表达恢复和合法激活的真实生命周期。
- **UniAgent 铸造 Product Session**：把认知监督主体变成 Runtime identity owner，
  违反 UniAgent 不拥有 Run State/Effect 的边界。
- **保留 dshSessionId 为公共字段**：把 Host realization vocabulary 倒灌 Product
  protocol，阻断第二 Host realization。
- **把所有 partial/deferred 边标为 verified**：没有行为证据和真实 buyer，属于伪造
  协议完成度。
