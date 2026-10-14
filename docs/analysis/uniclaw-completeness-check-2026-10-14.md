# UniClaw 字段有效性与完备性检查（2026-10-14）

> Status: REVIEWED / BUYER-SCOPED
> Authority: NONE

## 检查方法

这次不从“状态数量”开始，而从每条生命周期边的字段闭包开始。一个协议字段集合只有同时满足以下条件，才算完备：

```text
字段存在
∧ 值域可判定
∧ Owner/Authority 明确
∧ 缺失/未知/拒绝可表达
∧ identity/revision/scope/correlation/idempotency 语义可追踪
∧ 迟到/重复/恢复时仍可判定
∧ 下游确实消费该字段
```

字段“存在”不等于字段“有效”；字段“有效”也不等于字段链“完备”。

## 一、字段闭包的统一判据

每条边都用八个问题检查：

| 检查项 | 要回答的问题 |
|---|---|
| Presence | 必要字段是否存在，是否被错误地留空或静默缺省？ |
| Domain | 字段类型、枚举和值域是否封闭、可验证？ |
| Authority | 谁产生、谁校验、谁能拒绝或改写？ |
| Correlation | 能否关联 Task、Session、Run、Attempt、Intent、Binding、Revision？ |
| Validity | 能否判断 currentness、freshness、scope、permission 和 terminal validity？ |
| Absence | 缺失、未知、拒绝、未适用是否有不同表达？ |
| Retry | 重复、并发、迟到、乱序是否能由字段决定幂等或拒绝？ |
| Recovery | 重启后仅凭持久字段能否找回同一对象和下一步？ |

字段链的完备性定义为：

```text
Complete(edge) = Presence ∧ Domain ∧ Authority ∧ Correlation
                 ∧ Validity ∧ Absence ∧ Retry ∧ Recovery
```

任一项缺失，该边最多是“语义成立”或“条件成立”，不能是字段完备。

## 二、主生命周期字段链

### F1：Task → Instance → Session → Contract → Admission

目标字段链：

```text
taskRef + launchKey
→ taskInstanceId
→ productSessionId
→ contractRef + contractVersion
→ acceptedContractViewRef
```

检查结果：

| 字段组 | 有效性 | 完备性判断 |
|---|---|---|
| `taskRef` / `launchKey` | 类型可读，缺失可拒绝 | 基本成立 |
| `taskInstanceId` | Runtime Store 可生成、可持久化 | 显式 binding 消费已验证 |
| `productSessionId` | Runtime Store 可生成、可持久化 | 真实 Run 绑定已验证 |
| `contractRef` / `contractVersion` | Contract 类型有字段 | UniAgent authoring 尚未接线 |
| `acceptedContractViewRef` | `/runs` 与 activation record 显式消费 | **当前最小闭包成立** |

当前实现结果（LIFECYCLE-CLOSURE-001）：`/runs` 显式消费已存在且同属一个
binding 的 `taskInstanceId + productSessionId + acceptedContractViewRef +
contractGeneration`；ProductSession 绑定真实 RunId，`run-pending-*` 不进入
canonical Store；缺失、找不到或错配时 fail-closed，零新身份/Run/Effect 并保留
rejected reason。

**结论：F1 当前最小 buyer 闭包成立；跨 Host 完整现场恢复仍 deferred。**

### F2：P1 Contract Admission

P1 至少需要：

```text
version
objective
scope
allowedEffects / forbiddenEffects
proofCriteria / obligations
taskInstanceId
productSessionId
contractRef
budget + budgetValueOrigin
```

检查结果：

- Contract 结构、预算解析、`valueOrigin` 和 identity 三件套已进入类型；
- `null → default` 的预算语义可以被 `BudgetValueOrigin` 区分；
- 缺 identity 会 fail-closed；
- 但当前 producer 仍是 HostRunner 代造，不是 UniAgent；
- `MaxConsultations` / `MaxTotalSteps` 的默认值虽然可留痕，但终止测度来源没有成为 P1 的强制字段；
- 不同内容但相同幂等身份的冲突规则只在部分对象上存在。

**结论：P1 字段语义基本成立；跨组件来源、终止测度和完整冲突闭包仍不完备。**

### F3：P24 Legal Activation

P24 需要：

```text
acceptedContractViewRef
contractGeneration
taskInstanceId
productSessionId
activationCorrelation / idempotency key
terminal replay reference
```

当前字段链由 activation record 持久承载：activation key 为
`acceptedContractViewRef + contractGeneration`，先 `reserved` 后 finalize，支持
`failed`/`blocked`、终态回放、同 key 幂等和显式 resume；`launchId` 仅作外部查询/
传输幂等键。跨 Host 完整现场恢复不属于当前最小闭包。

**结论：P24 当前最小持久字段闭包成立；跨 Host 完整恢复 deferred。**

### F4：P25 Decision Consultation

P25 需要：

```text
runId
productSessionId
contractViewRef
decisionBoundaryId
consultationCorrelationId
budgetRemaining
renewalWindow / renewalProposal（若适用）
```

当前 journal 键为 `runId + contractViewRef + decisionBoundaryId +
consultationCorrelationId`；同 correlation transport retry 回放原结果且不重复
消耗 HardCap，新的 semantic consultation 才计数；timeout/no-response 持久化并
fail-closed，预算来源按维度保留 `configured/default`。

**结论：P25 当前最小字段与 retry/no-response 闭包成立；完整跨 Host journal 恢复 deferred。**

### F5：Observation → Evidence → WorldBelief

P2/P3 需要：

```text
claim
kind
context
origin
producer provenance
capture time
scope / lineage
attempt correlation（AttemptReport）
EvidenceId
revision/currentness
```

当前 `Origin` 已进入 Observation/Evidence 类型；账本从 Context 机械推导 canonical origin，解决了“生产者自报即权威”的问题。仍需区分：CaptureTime 只是 provenance，freshness 由消费侧判断；producer anti-spoof 仍是条件前提。

**结论：P2/P3 的核心字段闭包基本成立，来源可信度是 conditional soundness。**

### F6：Intent → Binding → Assurance → Dispatch

必须能关联：

```text
IntentId
BindingId
RevisionId
EffectClass
Target identity
Freshness judgment
ExpectedEffect
DeliveryTarget
AttemptId
```

当前 ExpectedEffect 已有类型实现，P13 具备 Intent/Binding/Revision 三元组和 Freshness；P10/P14 的执行 locator 分层已经有明确方向。但要证明一个 Effect，仍必须确保 ExpectedEffect、post-action Evidence、Origin 和 Attempt correlation 在同一字段链内闭合。

**结论：主动作字段结构成立，ExpectedEffect/Origin/Attempt 的跨边消费需要持续执法。**

### F7：Proof → Terminal Outcome → Runtime Outcome → Goal Evaluation

终局字段至少需要：

```text
OutcomeProofId
classification
obligation statuses
basis/effect/situation EvidenceIds
unresolved uncertainty
runId
productSessionId
exactly-once emission identity
GoalEvaluation correlation
```

当前四类 terminal classification、Outcome Proof、single-winner 和 delivery closure 语义成立；`RuntimeOutcome` 的 `productSessionId`、P19 实际 consumer/delivery 和重试字段仍不完整。

**结论：终局判断字段成立，跨 Session 交付字段不完备。**

## 三、字段有效性与场景反推

150 场景的字段压力不是“多写场景”，而是验证字段在不同轴上的判别能力：

| 压力轴 | 字段必须能判定 |
|---|---|
| 环境漂移 | revision、scope、freshness、currentness |
| 感知错误/迟到 | origin、provenance、capture time、EvidenceId、lineage |
| 多候选/失效 | Candidate identity、Canonical Binding、ambiguity reason |
| 权限/撤销 | effect class、grant/permission、judgment validity、terminal closure |
| 重复/并发 | idempotency key、generation、boundary correlation、single-winner token |
| 重启/乱序 | AttemptId、commit status、sequence、recovery reference |
| 历史更正 | revision、supersedes/invalidation、immutable record |

因此，SR-064、SR-068、SR-069、SR-087、SR-108、SR-144、SR-150 不是单纯流程故事，而是字段判别能力的反例：字段缺失时，系统无法区分 Receipt/Effect、Unknown/Failure、同一 Run/新 Run、当前/历史和可评价/不可评价。

## 四、字段完备性总账

| 字段域 | 存在性 | 值域/Owner | 跨边关联 | 迟到/恢复 | 总判定 |
|---|---|---|---|---|---|
| Task/Session/Run identity | 有 | Runtime Owner 明确 | `/runs` 消费显式 binding | 真实 RunId、activation record | **当前最小闭包成立** |
| Contract identity/budget | 有 | budget origin 有 | HostRunner authoring 为 known deviation | P24 key 已有 | **当前最小闭包成立（authoring 偏差另开 Change）** |
| Observation/Evidence origin | 有 | Ledger 推导 | P2/P3 基本贯通 | anti-spoof 条件 | **条件完备** |
| Revision/freshness | 有 | Owner 分离 | P10/P13 基本贯通 | 跨 Run recovery 缺 | **部分完备** |
| ExpectedEffect | 有 | Assurance 消费 | post-action 链需持续执法 | 迟到 effect 需重判 | **部分完备** |
| Attempt/receipt/outcome | 有 | 三态 outcome | Attempt correlation 已有 | Unknown recovery 部分 | **条件完备** |
| Terminal proof | 有 | Assurance/Run Model 分权 | RuntimeOutcome Session 字段缺 | P19 delivery 缺 | **部分完备** |

## 五、最终结论

从字段角度，UniClaw 的**结构字段**已经足够表达核心安全闭环，但**元字段**仍有四类欠账：

```text
1. identity association：Task / Session / Run / Contract / Boundary
2. provenance semantics：Origin / producer trust / capture validity
3. effect proof payload：ExpectedEffect / Attempt correlation
4. lifecycle recovery：generation / idempotency / sequence / resume reference
```

所以当前结论是：**当前最小 buyer 闭包的身份、P24、P25 字段链与异常分支已闭合；跨 Host 完整现场恢复、无 buyer 扩展和部分 Producer/Consumer 真实性仍按条件或 deferred 管理。**

当前实现状态为 `VERIFY PASS`：RUNTIME-IDENTITY-001 的显式 identity binding、
Session→真实 Run 绑定、P24 activation record 与 P25 journal 已通过 Change 验收；
跨 Host 完整现场恢复和无 buyer 扩展继续按 deferred 管理。

参考：`docs/design/protocol-completeness-formal-audit-v0.1.md` 附录 A/B、`docs/design/protocol-workflow-steps.generated.js`、`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`、`docs/adr/0045-runtime-task-instance-session-and-agent-contract-sequence.md`、`docs/adr/0046-p1-p24-p25-contract-activation-consultation-boundary.md`。

## 六、按最小购买者闭包重新验证（2026-10-14）

### 6.1 判定口径

本轮不再把所有预留协议字段都当成当前完备性的必要条件。字段只有在存在当前
consumer、已登记场景或已接受 Change 的明确使用者时，才进入当前最小闭包。
Owner 的语义裁决会冻结字段含义，但**单独的裁决不等于已经出现实现 buyer**。

允许保持 `deferred / placeholder` 的条件是：

1. 当前没有 consumer 或场景依赖该字段；
2. 该字段不影响当前 canonical identity、authority、Effect safety 或 terminal closure；
3. 缺失状态有明确的 `NO_REAL_BUYER` / `deferred` 标记；
4. 未来 buyer 出现时有恢复入口和验收方向。

违反上述任一条件，仍计入当前字段闭包缺口。

### 6.2 最小 buyer 集合复核

| 字段/流程 | 当前 buyer 证据 | 复核结果 | 是否阻塞最小闭包 |
|---|---|---|---|
| `taskInstanceId → productSessionId → runId` | RUNTIME-IDENTITY-001、Task/Session HTTP 入口、运行场景、LIFECYCLE-CLOSURE-001 | `/runs` 消费显式 binding；真实 RunId 写回 ProductSession；错配 fail-closed 且零副作用 | 否（跨 Host 完整恢复 deferred） |
| P1 Contract identity 与 admission | Run Model、Contract admission 测试、当前运行链 | identity 三件套有类型和 fail-closed；canonical UniAgent authoring 仍由 HostRunner 代演，当前按 known deviation 记录 | 否（另开 Change） |
| P24 `acceptedContractViewRef + contractGeneration` | 生命周期启动语义、RUNTIME-IDENTITY-001、LIFECYCLE-CLOSURE-001 | 显式四字段请求、activation record、同 key 幂等、终态回放与显式 resume 已验证 | 否（跨 Host 完整恢复 deferred） |
| P25 boundary / consultation correlation / HardCap | AGT-001/003、AgentDecision seam、LIFECYCLE-CLOSURE-001 | 四元组 journal、semantic/transport retry、HardCap 与 timeout/no-response 已验证 | 否（完整跨 Host 恢复 deferred） |
| P2–P4、P6–P18 Evidence / Effect / Proof 主链 | 现有场景库、47 条不变量、Effect/Outcome 测试 | 核心字段链基本成立；跨 Provider transport 和全量恢复属于条件缺口 | 否（保留条件状态） |
| WaitingForInput 正常等待/显式 resume | RFS-001、S4/S5、Kernel/Simulation 测试 | 正常等待和 resume 有 buyer；超时 watchdog → SafeStop/Blocked 没有独立当前 consumer | 否（超时扩展 deferred） |
| Cancel 的 Kernel 语义 | RFS-001 / Kernel Cancel 输入路径 | in-process cancel 有语义 buyer；Host 接口、在途对账和 canonical command 尚无当前 consumer | 否（Host transport deferred） |
| GoalEvaluation 生产 | GEV-004、GoalEvaluationTests、real evaluation 场景 | immutable Evaluation 生产闭合；user/session delivery retry 仍是未来 consumer | 否（delivery deferred） |
| ContractRevision | 本轮 Owner 裁决，但没有对应场景/consumer | 语义已冻结，当前没有实现 buyer；不得把缺类型计为当前最小闭包失败 | 否（候选扩展） |
| P5 Run Snapshot | 基线明确 `deferred（no-current-buyer）` | 当前 Control 不消费 run-side data | 否 |
| P20/P21 Memory | 基线明确 placeholder / Memory buyer deferred | 没有 current claim 或 canonical consumer | 否 |
| 跨 Host authority token、完整跨进程 resume | 基线和多个 Change 标为 future buyer | 当前只要求显式 identity 与 fail-closed，不要求预建通用协议 | 否 |

### 6.3 重新判定

按上述口径，当前结果不是“所有协议都不完备”，而是：

```text
当前最小 buyer 闭包：成立
已闭合：身份连续性、P24 启动字段/activation record、P25 咨询 journal/HardCap/timeout
条件成立：P2–P18 主链、正常 WaitingForInput/resume、Kernel cancel 语义、GoalEvaluation 生产
允许 deferred：P5、P20/P21、GoalEvaluation delivery、Host lifecycle command transport、ContractRevision 实现、P1 canonical authoring seam、跨 Host/跨进程扩展
```

因此，前一版把已实现的身份、P24、P25 字段链继续标成阻塞项，结论已由
LIFECYCLE-CLOSURE-001 的验收证据修正；剩余 deferred 只覆盖没有当前 buyer 的
扩展和跨 Host 完整现场恢复。

### 6.4 异常分支的字段验收

| 分支 | 字段闭包要求 | 当前判定 |
|---|---|---|
| binding 缺失/错配 | identity 三件套、binding relation、rejected reason、request correlation；拒绝不得 mint 新身份或 Run | **已闭合** |
| P24 reserved 中断 | activation key、record status、nullable RunId、owner/recovery reference、显式 resume authority | **已闭合（跨 Host 完整恢复 deferred）** |
| P24 finalize/replay | 真实 RunId、final status、terminal replay reference；重复只读回放 | **已闭合** |
| P25 timeout/no-response | 四元组 journal key、status、fail-closed disposition、semantic count、retry correlation | **已闭合** |
| P25 同 boundary 新咨询 | 新 correlation、current ContractViewRef、HardCap remaining；超限拒绝 | **已闭合** |

这组异常字段属于当前最小 buyer 闭包，因为它们直接决定“是否会偷偷创建第二个
Run、重复消耗预算或在 no-response 后产生 Effect”。
