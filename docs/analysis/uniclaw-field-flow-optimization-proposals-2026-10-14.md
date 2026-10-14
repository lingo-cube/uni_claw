# UniClaw 字段与流程优化建议（进入 grill 前的候选方案）

> Status: REVIEWED / GRILL-ACCEPTED
> Authority: NONE

## 目的

本文件把字段完备性检查和生命周期流程检查合并成一组可裁决建议。它只定义问题、候选方案和验收方向，不把任何候选方案当作已接受的架构或实现。

当前判断：核心安全和权威分工成立；字段链、身份连续性、P24/P25、恢复和用户中途控制未完全闭合。下一轮 grill 应先裁决字段语义，再裁决流程入口和恢复行为。

## 一、建议采用的统一字段闭包

所有跨组件字段组都按以下顺序审查：

```text
Presence → Domain → Authority → Correlation → Validity
→ Absence → Retry/Late → Recovery → Consumer
```

每条协议边建立一张字段卡，至少记录：

| 字段卡栏目 | 内容 |
|---|---|
| semantic name | 业务语义名，不直接等同 C# 属性名 |
| producer / consumer | 谁产生、谁接收 |
| canonical owner | 谁拥有字段含义和拒绝权 |
| requiredness | required / optional / derived / not-applicable |
| domain | 类型、枚举、范围、非空规则 |
| derivation | 是否从其他字段机械推导 |
| identity/correlation | 关联哪一种对象或一次尝试 |
| validity | revision、scope、freshness、permission、terminal 条件 |
| absence | absent / unknown / rejected / not-applicable 的区别 |
| retry/late | 同值重试、内容冲突、迟到事件如何处理 |
| recovery | 重启后如何查回、续行或安全停止 |
| consumer proof | 哪个下游判断真正消费该字段 |

### 建议锁定的四类字段

1. **身份关联字段**：`taskInstanceId`、`productSessionId`、`runId`、`contractRef`、`contractViewRef`、`contractGeneration`、`decisionBoundaryId`、`consultationCorrelationId`。
2. **事实来源字段**：`EvidenceId`、`Origin`、`Producer`、`CaptureTime`、`Provenance`、`Lineage`、`AttemptId`。
3. **当前性字段**：`RevisionId`、`RevisionNumber`、`Scope`、`FreshnessJudgment`、`Validity`、`Sequence`。
4. **证明与终局字段**：`ExpectedEffect`、`OutcomeProofId`、`EvidenceRefs`、`ObligationStatus`、`UnresolvedUncertainty`、`EmissionId`。

## 二、P0：先修字段的事实一致性

### P0.1 修正身份链的单一来源

候选流程：

```text
POST /tasks → taskInstanceId
POST /sessions(taskInstanceId) → productSessionId
POST /runs(taskInstanceId, productSessionId, contractRef) → Run
```

`/runs` 只允许消费已存在的 binding；不存在、跨 Task、已绑定其他 Run 或内容冲突时显式拒绝。

必须移除 `run-pending-{launchId}` 作为 ProductSession 的持久 RunId。真实 Run 创建成功后，才写入真实 `runId`；Run 创建失败只能留下明确的 pending/failed 状态，不能留下伪 RunId。

### P0.2 让身份必填迁移成为完整字段变更

`RunModel` 已经拒收缺少三件套的 Contract，但 Kernel、Simulation 和 Host 测试组合根仍有旧字段调用。建议：

- 先建立测试专用但语义合法的 neutral identity fixture；
- 所有直接构造 Contract 的入口显式带三件套；
- 禁止通过恢复 optional-first 语义来绕过产品路径；
- Host 新增测试补齐测试框架引用后再评价行为。

验收不是“编译通过”，而是：旧调用面全部能给出合法 identity，缺 identity 的产品入口仍 fail-closed。

### P0.3 建立幂等冲突字段规则

同一 `idempotencyKey` 的 payload 若 TaskRef、scope 或 contract signature 不同，必须返回 conflict；不能静默复用旧记录。

建议定义：

```text
same key + same canonical payload → reuse
same key + different canonical payload → conflict
```

## 三、P1：补齐 P24/P25 字段链

### P1.1 P24 ActivationKey

候选字段：

```text
acceptedContractViewRef
contractGeneration
taskInstanceId
productSessionId
activationIdempotencyKey
```

验收：同一 key 重试和并发只得到同一 binding；Contract View 内容变化时拒绝；terminal 后只回放终态；重启后能查回同一 binding。

### P1.2 P25 ConsultationKey

候选字段：

```text
runId
productSessionId
contractViewRef
decisionBoundaryId
consultationCorrelationId
budgetRemaining
```

验收：同一 boundary 重试复用同一结果；不同 boundary 才能新咨询；P25 不得创建或改写 Contract/View；恢复时能识别已完成 boundary。

### P1.3 ExpectedEffect 与 Attempt 绑定

验证输入必须同时带：

```text
AttemptId + ExpectedEffect + PostAction EvidenceRefs + Origin + RevisionId
```

状态型和变化型期望统一走 `ExpectedEffect`；不得继续以滚动专用布尔字段代替通用字段。

## 四、P1：把流程入口恢复成可观察的语义阶段

建议把当前 HostRunner 代造 Contract 的路径标成过渡适配，不作为最终 Product authoring 路径：

```text
Task Catalog
→ Runtime creates TaskInstance
→ Runtime creates ProductSession
→ UniAgent authors Contract
→ Run Model admits Contract
→ P24 activates Run
→ Kernel self-drives
```

每一步都要产生可查询的 binding/ref，但只有对应 Owner 能写入 canonical 字段。Host 只附加 `hostSessionRef`，不能写 Product identity。

## 五、P2：补齐迟到、恢复和用户控制字段

### P2.1 迟到与恢复

为 Attempt、Consultation、Activation、Runtime Outcome 固定 `sequence/generation/status/recoveryDisposition`。恢复结果至少区分：

```text
RecoveredBinding
RecoveredSnapshot
ResumableRun
SafeStopRequired
CorruptOrUnavailable
```

“查到旧 snapshot”不能直接称为“恢复执行”。

### P2.2 WaitingForInput

明确 `waitId`、`waitingSince`、`resumeCondition`、`cancelCondition` 和 `watchdogDisposition`。没有 canonical clock 或宿主再入保证时，必须能进入明确的 BLOCKED/SafeStop，而不是无限合法等待。

### P2.3 User 中途控制

是否允许 Cancel、Clarification、Escalation、Contract Revision 是产品裁决，不应由实现猜测。若允许，至少需要：

```text
userCommandId
productSessionId
runId
commandKind
requestedAt / ordering
effect on current Contract/Run
acknowledgement / rejection reason
```

## 六、建议的字段级验收矩阵

每条边至少新增四类测试：

1. **Presence/Domain**：缺字段、空字段、非法枚举、范围越界；
2. **Cross-field**：identity、revision、scope、attempt、expected effect 不匹配；
3. **Retry/Late**：同 key 重试、同 key 内容冲突、迟到事件、terminal 后事件；
4. **Recovery**：写入中断、重启读取、恢复后续行、恢复失败安全停止。

场景库应从字段反例生成，而不是只从状态名生成。最小反例集合应覆盖：

```text
缺 Task/Session/Contract identity
同 key 异 payload
placeholder RunId 与真实 RunId 不一致
旧 Revision + 新 Freshness
Origin 自报与 Context 推导不一致
ExpectedEffect 缺失或与 Attempt 不匹配
同 boundary 重复咨询
terminal 后迟到 emission/effect
恢复 snapshot 但缺 resume authority
```

## 七、进入 grill 前的裁决 frontier

以下问题不能由实现便利代替产品决定：

1. `/runs` 是否必须只接受前序 `/tasks`、`/sessions` 产生的 binding？
2. ProductSession 是否允许多个 Primary Run，还是当前继续冻结 1:1？
3. P24 的 activation key 是否由 `acceptedContractViewRef + contractGeneration` 固定？
4. P25 是否严格执行“每个 decision boundary 一次”，还是允许同一边界在 HardCap 内重复咨询？
5. Contract budget 缺失时，是允许带 `valueOrigin=default`，还是直接拒收？
6. WaitingForInput 超时后进入 BLOCKED、SafeStop 还是继续等待？
7. 是否支持 Run 中的 Cancel、Clarification、Escalation 和 Contract Revision？
8. Memory recall 是否永远只作 prior/context，不能产生 current claim？

## 当前建议的默认方向

若没有新的产品取舍，建议优先采用：

- `/runs` 只消费显式 Task/Session binding；
- 当前 1 Task Instance / 1 Product Session / 1 Primary Run 基数保持不变；
- P24 使用 `acceptedContractViewRef + contractGeneration`；
- P25 允许同一 decision boundary 重复咨询；每次使用独立 `consultationCorrelationId`，总次数受 Contract/Policy HardCap 限制；
- 预算缺失允许 default，但必须明确 `valueOrigin`；
- WaitingForInput 必须有有限等待或明确 BLOCKED/SafeStop；
- Memory 保持 non-evidentiary prior；
- 所有新增字段先通过字段级验收矩阵，再进入实现。

本节是裁决前的默认方向；最终语义以第八节的 Owner 裁决、ADR-0051 和已同步的 CONTEXT 为准。

## 八、Grill 已裁决结果（2026-10-14）

以下决定已由 Owner 在本轮 grill 中确认，后续可进入 ADR/实现 Change：

- `/runs` 必须消费前序 `/tasks`、`/sessions` 产生的显式 binding；
- 当前继续保持 `1 Task Instance / 1 Product Session / 1 Primary Run`；
- P24 启动钥匙为 `acceptedContractViewRef + contractGeneration`，`launchId` 只作外部提交查询键；
- P25 允许同一 decision boundary 重复咨询，但每次使用独立 `consultationCorrelationId`，总次数受 Contract/Policy HardCap 限制；
- 缺少预算允许使用默认值，但必须记录 `valueOrigin=default`；
- WaitingForInput 超时进入 BLOCKED/SafeStop，不自动产生 Effect，恢复必须显式 resume；
- Cancel 从 Host 接口进入，但 Host 只负责鉴权和传输适配；Kernel/Runtime 校验 Session/Run 后铸造 Product Cancel command；
- 当前 Run 允许有限 ContractRevision，范围只包括预算、适应策略、咨询策略；objective、scope、effect 权限、proof criteria 不可修订；
- 每次有限修订生成新的 immutable ContractRevision 和新的 ContractViewRef，旧 revision 不可变；同一 Primary Run 更新 current ContractViewRef，不重新 P24 激活，P25 使用最新 View；
- GoalEvaluation 只读交付给 user/session，不回写 Run/World/Contract；交付失败保留 EvaluationId 和待交付状态，后续重试幂等，最终为 NeedsFollowUp；
- Memory 维持 deferred，暂不裁决产品化语义。

## 九、修正后复审结论（2026-10-14）

### 已解决的论证空白

本轮裁决已经解决前一版论证中的**产品语义未决**：启动是否依赖显式 binding、生命周期基数、P24 启动钥匙、P25 同边界重复咨询规则、预算缺失语义、等待超时处置、Cancel 权威、有限 ContractRevision、GoalEvaluation 交付和 Memory 边界均已有明确答案。它们足以消除“因为需求未裁决而无法判定完备性”的问题。

### 仍未解决的字段与流程闭包

这些决定本身还没有让实现满足字段闭包公理；它们只是把验收目标固定下来：

| 论证问题 | 修正后的状态 | 仍需实现/验证的闭口 |
|---|---|---|
| Task → Session → Run 身份连续性 | 语义已裁决 | `/runs` 仍会重新 mint Task/Session，且仍写入 `run-pending-*`；必须消费前序 binding 并回写真实 RunId |
| P24 启动唯一性 | activation key 已裁决 | `acceptedContractViewRef`、`contractGeneration` 尚未成为请求、持久 binding、并发复用和恢复字段 |
| P25 重复咨询 | 重复规则和 HardCap 已裁决 | `consultationCorrelationId`、boundary journal、current ContractViewRef、跨进程重试/冲突判定仍未闭合 |
| 默认预算 | `valueOrigin=default` 已裁决 | 当前只有一个聚合 `BudgetValueOrigin`；部分预算显式、部分预算缺失时无法逐字段证明来源 |
| ContractRevision | 白名单与 immutable chain 已裁决 | 尚无 revision 类型、审计链、Run Model 接受路径和同 Run current View 更新实现 |
| Cancel | Host/Kernel 权威和 SafeStop/Blocked 结果已裁决 | 尚无完整 Host→Kernel canonical command、Session/Run 校验、在途 Attempt 对账与终局恢复证据 |
| WaitingForInput | 超时 SafeStop/Blocked、显式 resume 已裁决 | watchdog、wait/resume 字段和持久恢复路径仍需落地 |
| GoalEvaluation | 只读、EvaluationId 保留、重试幂等已裁决 | delivery status、retry correlation、待交付存储和 NeedsFollowUp 终局仍需实现 |

### 复审判定

因此，修正**能解决前面论证中的需求裁决空白和语义反例定义**，但**不能单独证明当前项目已经完备**。当前结论应保持为：

```text
产品语义：已足够进入字段级实现验收
字段闭包：仍有 P24/P25/Revision/Cancel/Delivery/恢复缺口
流程完备性：身份启动链仍被 RUNTIME-IDENTITY-001 当前实现阻断
数学性质：Safety 前提更清楚；Consistency/Liveness/Reachability/Recovery 仍为条件成立
```

下一步应把本节表格逐项转成实现 Change 的 acceptance matrix，再按场景库反推验证；在此之前不能把“裁决完成”写成“实现完备”。

## 十、按最小 buyer 集合调整优化优先级（2026-10-14）

### P0：只修当前 buyer 直接依赖的字段链

1. **身份链**：让 `/runs` 只接受已存在的 `taskInstanceId + productSessionId`，校验二者属于同一 binding；Run 创建成功后再写入真实 `runId`，删除 `run-pending-*` 作为 canonical 值的路径，并增加同 key 异 payload 冲突拒绝。
2. **P24**：引入明确的 activation record，至少持久化 `acceptedContractViewRef`、`contractGeneration`、Task/Session binding、launch/idempotency 查询键、结果状态和终态回放引用。`launchId` 只做查询键。
3. **P25**：区分 logical consultation 与 transport retry；同一次 retry 复用 correlation，新的同 boundary 咨询生成新 correlation；记录 HardCap 消耗、Contract View、boundary 和结果，形成跨重试可判定的 journal。
4. **预算来源**：把 `valueOrigin` 从聚合枚举拆成每个预算维度的来源，至少分别记录 `maxConsultations` 与 `maxTotalSteps` 的 `configured/default`，避免“一个显式、一个默认”时丢失字段事实。

### P1：有明确 buyer 后再实现的扩展

- ContractRevision：保留本轮白名单和 immutable revision 语义；出现真实 revision 场景后，再补 revision record、审计、Run Model 接受和 current View 更新。
- P1 authoring：继续把 HostRunner 代演标记为 known deviation；出现 UniAgent authoring buyer 后再补真实 producer seam。
- Cancel Host transport：保留 Host 适配、Kernel 铸造 command、Session/Run 校验和在途对账语义；出现 Host cancel consumer 后再补 endpoint、持久 command 和 SafeStop/Blocked recovery。
- WaitingForInput timeout：当前正常 resume 已有 buyer；出现 watchdog consumer 后再补 `waitId / deadline / resumeCondition / watchdogDisposition`。
- GoalEvaluation delivery：出现 user/session delivery consumer 后再补 `EvaluationId / deliveryStatus / retryCorrelation / pending store / NeedsFollowUp`。

### P2：继续保持 deferred，不预造字段

P5 Run Snapshot、P20/P21 Memory、跨 Host authority token、通用跨进程 resume、没有
consumer 的额外 transport metadata 继续保留 `deferred / placeholder`。这些字段不
进入当前完备性分母，也不应通过空 DTO、空协议或 speculative schema 提前实现。

### 优化后的验收顺序

```text
先证明 Task/Session/Run 连续性
→ 再证明 P24 activation key 的持久与幂等
→ 再证明 P25 logical consultation / retry / HardCap
→ 再复验 P1/P2–P18 场景库
→ buyer 出现时单独开启 Revision / Cancel / Delivery / Recovery Change
```

这会把验证从“全量协议是否已经齐全”收窄为“当前 buyer 所需字段是否闭合”，同时保留
未来扩展的语义边界，避免用没有购买者的需求制造当前实现负担。

## 十一、第二轮 grill 输入（裁决前）

本轮只保留会改变最小闭包分母、P0 验收范围或字段形状的问题：

1. **Buyer 认定门槛**：建议只有“当前 consumer + 已登记场景/验收 + 可回指 Change”
   才算当前 buyer；Owner 单独裁决只冻结语义，不自动把实现列入当前闭包。
2. **P1 authoring 偏差**：建议暂时记录为 known deviation，不阻塞身份/P24/P25
   的 P0 修复；出现 UniAgent authoring 的真实场景后另开 Change。
3. **预算来源形状**：建议使用固定的逐维度字段（例如
   `maxConsultationsOrigin`、`maxTotalStepsOrigin`），不要继续使用一个聚合
   `BudgetValueOrigin`。
4. **P24 最小持久范围**：建议当前 buyer 只要求 activation record、显式 binding、
   同 key 幂等和终态回放；跨 Host resume、完整 Kernel/World现场恢复另列 deferred。
5. **P25 计数规则**：建议 transport retry 复用同一 correlation 且不重复消耗；新的
   semantic consultation 使用新 correlation 并消耗一次 HardCap；同 boundary 仍可在
   HardCap 内重复咨询。
6. **候选扩展**：建议 ContractRevision、Host Cancel transport、WaitingForInput
   watchdog、GoalEvaluation delivery 在没有独立 consumer/场景前保持 deferred，只保留
   已裁决语义和重开条件。

## 十二、第二轮 grill 已裁决结果（2026-10-14）

- 当前 buyer 必须同时具备 consumer、登记场景/验收和可回指 Change；Owner 单独裁决只冻结语义，不自动进入当前闭包。
- P1 HostRunner 代演 Contract authoring 暂记 `known deviation`，不阻塞身份/P24/P25 的 P0；出现 UniAgent authoring buyer 后另开 Change。
- `BudgetValueOrigin` 拆为每个预算维度的固定来源字段，分别记录 `configured/default`。
- P24 当前只做 activation record、显式 binding、同 key 幂等和终态回放；跨 Host resume 与完整现场恢复继续 deferred。
- P25 transport retry 复用同一 correlation 且不消耗 HardCap；新的 semantic consultation 使用新 correlation 并消耗一次；同 boundary 仍可重复。
- ContractRevision、Host Cancel transport、WaitingForInput watchdog、GoalEvaluation delivery 在没有独立 consumer/场景前继续 deferred，只保留语义和重开条件。

## 十三、第三轮 grill 输入（实现边界）

1. **P24/P25 之前的启动请求**：建议 `/runs` 显式携带
   `taskInstanceId + productSessionId + acceptedContractViewRef + contractGeneration`；
   `contractRef` 从已接受 View/binding 解析，`launchId` 和 `idempotencyKey` 只作查询/传输幂等键。
2. **P24 activation record 时序**：建议先以 activation key 预留记录，再绑定真实 RunId；
   成功后 finalize，失败则保留 failed/terminal record，重试只回放同一记录，不重新 mint。
3. **P25 journal 键**：建议使用
   `(runId, contractViewRef, decisionBoundaryId, consultationCorrelationId)`；同 correlation
   的 retry 返回原结果且不再次计数；同 boundary 的新 correlation 才是新的 semantic consultation，
   受 HardCap 限制。

## 十四、第三轮 grill 已裁决结果（2026-10-14）

- `/runs` 显式携带 `taskInstanceId`、`productSessionId`、`acceptedContractViewRef`、`contractGeneration`；`contractRef` 从已接受 View/binding 解析，`launchId` 和 `idempotencyKey` 只作查询/传输幂等键。
- P24 先按 activation key 预留记录，再绑定真实 RunId；成功 finalize，失败保留 failed/terminal record，重试只回放原记录。
- P25 journal 使用 `runId + contractViewRef + decisionBoundaryId + consultationCorrelationId` 四元组；同 correlation retry 返回原结果且不计数，新 correlation 才消耗一次 HardCap。

## 十五、第四轮 grill 输入（异常分支）

1. **binding 缺失或错配**：建议直接 fail-closed，返回可区分的 rejected reason，零新
   Task/Session/Run、零 Effect，并留下可审计拒绝记录。
2. **P24 预留后进程中断**：建议保留 `reserved` 记录；重启后只能按原 activation key
   查询/恢复，不能重新 mint；无法证明原 owner 时转为 BLOCKED/failed，等待显式 resume。
3. **P25 timeout/no-response**：建议作为一次 semantic consultation 计数，持久化
   `no-response/timeout` 结果和 fail-closed disposition；同 correlation retry 回放原结果，
   新 correlation 才重新消耗 HardCap。

## 十六、第四轮 grill 已裁决结果（2026-10-14）

- binding 缺失、找不到或错配时 fail-closed，零新身份、Run 和 Effect，并保留可审计 rejected reason。
- P24 预留中断后保留 `reserved`；重启只能按原 activation key 查询/恢复，无法证明原 owner 时转 BLOCKED/failed，禁止重新 mint，恢复必须显式 resume。
- P25 timeout/no-response 计为一次 semantic consultation，持久化 `no-response/timeout` 与 fail-closed disposition；同 correlation retry 回放，新 correlation 才重新计数。

## 十七、异常分支字段验收矩阵

| 分支 | 必须存在的字段 | 必须满足的不变量 | 失败后的唯一合法动作 |
|---|---|---|---|
| binding 缺失/错配 | `taskInstanceId`、`productSessionId`、binding relation、rejected reason、request correlation | 不重新 mint；不建立 Run；不产生 Effect；拒绝可审计 | fail-closed rejected |
| P24 reserved 中断 | activation key、record status、nullable `runId`、owner/recovery reference、resume authority | 同 key 只能查询/恢复；不得第二次 mint；owner 不可证明不得假装 running | 保留 reserved，或转 BLOCKED/failed，显式 resume |
| P24 finalize | activation key、真实 `runId`、final status、terminal replay reference | finalize 只发生一次；重复请求只回放 binding/终态 | replay existing result |
| P25 timeout/no-response | 四元组 journal key、status、disposition、consumed semantic count、retry correlation | timeout 计数一次；同 correlation 不重复计数；不得产生 Effect | replay fail-closed result 或新 correlation 重试 |
| P25 同 boundary 新咨询 | 新 `consultationCorrelationId`、current ContractViewRef、HardCap remaining | 新逻辑咨询独立计数；不复用旧结果身份；HardCap 封顶 | 超限拒绝/安全停止 |
