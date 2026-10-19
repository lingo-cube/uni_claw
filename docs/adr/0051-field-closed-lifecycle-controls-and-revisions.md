# ADR-0051：字段闭包的生命周期控制、ContractRevision 与交付语义

状态：accepted（2026-10-14，grill-with-docs）

本 ADR 收口字段与流程完备性建议中的 Owner 裁决。它修订 P25 的重复咨询语义，补充有限 ContractRevision、Cancel command、GoalEvaluation delivery 的边界；Memory 仍保持 deferred。它仅替代 ADR-0046 关于“同一 decision boundary 最多一次”的重复次数规则，不改变 ADR-0046 的其他边界定义。

> 2026-10-18：§3及相关consequences对有限累计HardCap的必需依赖，由accepted ADR-0052部分替代；P25独立correlation、transport幂等、语义咨询计费及其他生命周期决定继续有效。

## Decision

### 1. 启动必须消费显式身份 binding

生命周期入口固定为：

```text
POST /tasks → taskInstanceId
POST /sessions(taskInstanceId) → productSessionId
POST /runs(taskInstanceId, productSessionId, contractRef) → Primary Run
```

`/runs` 不得因为缺少 binding 而静默重新 mint Task/Session。当前范围继续保持 `1 Task Instance / 1 Product Session / 1 Primary Run`。ProductSession 必须绑定真实 RunId；`run-pending-*` 只能是非 canonical 的过程状态，不能持久化成 Run identity。

### 2. P24 启动钥匙

P24 activation key 固定为：

```text
acceptedContractViewRef + contractGeneration
```

`launchId` 只用于外部提交查询和 transport 幂等，不单独代表要启动哪一份 Contract View。相同 activation key 的重试/并发返回同一 binding；终态只回放终态，不复活 Run。

### 3. P25 同一 boundary 可重复咨询

同一 decision boundary 可以重复咨询，但每次咨询必须有独立 `consultationCorrelationId`。Kernel 仍拥有 boundary 记录、调用时机和生命周期推进权；UniAgent 只回答，不发起生命周期推进。咨询总次数受 Contract/Policy HardCap 限制。

P25 最小字段为：

```text
runId + productSessionId + contractViewRef + decisionBoundaryId
+ consultationCorrelationId + budgetRemaining
```

### 4. 有限 ContractRevision

当前 Run 允许有限修订，修订白名单只有：

```text
budget / adaptation policy / consultation policy
```

以下字段在当前 Run 内不可修改：

```text
objective / scope / effect permissions / proof criteria
```

每次修订生成新的 immutable `ContractRevision` 和 `ContractViewRef`；旧 revision 保留为历史依据。Run Model 接受修订后更新 current ContractViewRef；不重新执行 P24 activation，P25 使用最新 View。

### 5. Cancel command

Cancel 可以从 Host 接口进入，但 Host 只负责鉴权和传输适配。Kernel/Runtime 必须校验 `productSessionId + runId`，再铸造 Product Cancel command 并写入 canonical lifecycle。

Cancel 后立即禁止新 Effect；在途 Attempt 必须对账。对账成功形成 SafeStop；无法对账进入 BLOCKED。Cancel 不修改 Contract。

### 6. GoalEvaluation delivery

GoalEvaluation 是 UniAgent 的只读监督输出，不回写 Run、WorldBelief、Contract 或 Runtime Outcome。交付至少保留：

```text
EvaluationId + delivery status + retry correlation
```

交付失败不丢弃 Evaluation；后续重试必须幂等，最终 disposition 为 `NeedsFollowUp`。

### 7. Memory

Memory P20/P21 保持 deferred。未来实现前仍必须保持：Memory Recall 只能作为 prior/context/history，不能直接建立 current claim 或修改 WorldBelief。

### 8. 最小 buyer 闭包与 P24/P25 记录形状

当前最小 buyer 必须同时具备 consumer、登记场景/验收和可回指 Change；Owner 单独裁决只冻结语义，不自动构成实现 buyer。无 buyer 的扩展保持 `deferred / placeholder`，但不能掩盖当前 identity、authority、Effect safety 或 terminal closure 依赖。

`/runs` 的 canonical 启动请求显式携带：

```text
taskInstanceId + productSessionId + acceptedContractViewRef + contractGeneration
```

`contractRef` 从已接受 View/binding 解析；`launchId` 和 `idempotencyKey` 只作查询/传输幂等键。P24 先按 activation key 预留记录，再绑定真实 RunId；成功 finalize，失败保留 failed/terminal record，重试只回放原记录。

P25 journal key 固定为：

```text
runId + contractViewRef + decisionBoundaryId + consultationCorrelationId
```

同 correlation 的 transport retry 返回原结果且不重复消耗 HardCap；新的 correlation 才是新的 semantic consultation。

### 9. 异常分支

binding 缺失、找不到或错配时必须 fail-closed，零新身份、Run 和 Effect，并保留可审计 rejected reason。P24 预留中断后保留 `reserved`；重启只能按原 activation key 查询/恢复，无法证明原 owner 时转 BLOCKED/failed，禁止重新 mint，恢复必须显式 resume。P25 timeout/no-response 计为一次 semantic consultation，持久化 `no-response/timeout` 与 fail-closed disposition；同 correlation retry 回放，新 correlation 才重新计数。

## Consequences

- P24 的 activation identity 与外部 launch identity 分离；
- P25 不再采用“同 boundary 只能一次”，但仍有 correlation 和 HardCap，避免无限咨询；
- Contract 不再是“永远不可变”的单一版本，而是同一 Run 内受白名单约束的 immutable revision chain；
- Cancel 的 transport owner 与 Product command owner 分离，Host 不取得 Run authority；
- GoalEvaluation 交付成为可恢复的只读投影，不成为第二个 Runtime outcome authority；
- P20/P21 不因本 ADR 获得实现授权。

## Implementation status

- LIFECYCLE-CLOSURE-001 已落地当前最小 buyer 闭包：`/runs` 消费显式
  Task/Session binding，P24 使用 JSONL activation record，P25 使用
  in-memory/File journal；缺失 binding、reserved 中断、终态回放、同 correlation
  retry 和 no-response disposition 均有 deterministic/scenario 证据；
- 当前实现状态由 `changes/LIFECYCLE-CLOSURE-001/state.md` 维护，跨 Host 完整
 现场恢复仍不属于本切片；
- P1 的 HostRunner Contract authoring 是已记录的 known deviation；在没有 UniAgent authoring buyer 的当前最小闭包中不阻塞身份/P24/P25 P0，但出现该 buyer 后必须另开 Change；
- 每个预算维度已分别保留 `configured/default` 来源；聚合 `BudgetValueOrigin.Mixed`
  仅作兼容投影，不再承担事实表达；
- P24 当前最小持久范围是 activation record、显式 binding、同 key 幂等和终态回放；跨 Host resume 与完整现场恢复继续 deferred；
- P25 的 transport retry 不重复消耗 HardCap；新的 semantic consultation 才消耗一次并生成新 correlation；
- ContractRevision、Host Cancel transport、WaitingForInput watchdog、GoalEvaluation delivery retry 在没有独立 consumer/场景前继续 deferred，只保留语义和重开条件；
- Memory buyer 和 retention/supersession 规则继续 deferred。
